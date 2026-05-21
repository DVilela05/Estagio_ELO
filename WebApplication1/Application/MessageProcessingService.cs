using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using WebApplication1.Core.Commands;
using WebApplication1.Core.Interfaces;
using WebApplication1.Core.Localization;
using WebApplication1.Core.Models;
using WebApplication1.Infrastructure.Logging;

namespace WebApplication1.Application
{
    /// <summary>
    /// CORE da aplicação — processa mensagens de QUALQUER plataforma.
    /// 
    /// Este serviço contém TODA a lógica de negócio que é comum a todas
    /// as plataformas (WhatsApp, Teams, Telegram, etc.):
    ///   - Filtro de mensagens antigas (ignora msgs anteriores ao arranque do bot)
    ///   - Deduplicação de mensagens por MessageId (cache de 5 minutos)
    ///   - Proteção anti-spam TRIPLA:
    ///     a) Filtro SentAt + 1s grace: bloqueia msgs com SentAt antes da resposta
    ///        (1s compensa arredondamento de segundos do WhatsApp)
    ///     b) Lock por utilizador: só processa 1 mensagem de cada vez
    ///     c) Delayed unlock (2s) quando spam detetado — absorve ondas seguintes
    ///     d) Bypass: confirmação pendente (sim/não) ignora filtro E delayed lock
    ///   - Fluxo de confirmação (sim/não) com 3 tentativas inválidas
    ///   - Fluxo de confirmação (sim/não) com tentativas e expiração
    ///   - Confirmações contextuais (referencia o comando específico)
    ///   - Variações de mensagens (nunca repete consecutivamente)
    ///   - Normalização de texto (remoção de emojis, pontuação)
    ///   - Logging e console output
    /// 
    /// O controller só trata de HTTP (receber JSON, fazer parse) e
    /// delega TUDO a este serviço.
    /// 
    /// Para adicionar uma nova plataforma, NÃO precisas de tocar aqui.
    /// Basta criar um IMessagingService e um endpoint no controller.
    /// </summary>
    public class MessageProcessingService
    {
        // =====================================================================
        // Estado partilhado — deduplicação e confirmações
        // =====================================================================

        /// <summary>
        /// Momento em que a aplicação arrancou.
        /// Mensagens com SentAt anterior a este momento são consideradas "antigas"
        /// e são ignoradas — resolve o problema de reprocessar mensagens ao reiniciar.
        /// </summary>
        private static readonly DateTime _startupTime = DateTime.UtcNow;

        private static readonly ConcurrentDictionary<string, DateTime> _processedMessageIds = new();
        private static readonly TimeSpan _duplicateWindow = TimeSpan.FromMinutes(5);
        private static readonly ConcurrentDictionary<string, PendingConfirmation> _pendingConfirmations = new();

        // Proteção anti-spam TRIPLA:
        // 1. Filtro SentAt + 1s grace:
        //    Spam tem SentAt ANTES da resposta → sempre bloqueado.
        //    1s compensa o arredondamento de segundos do WhatsApp SentAt.
        //    Bypass: confirmação pendente → filtro ignorado
        // 2. Lock por utilizador: apenas 1 mensagem processada de cada vez
        // 3. Delayed unlock (2s) quando spam detetado:
        //    Mantém o lock por 2s após resposta, absorve ondas tardias do WhatsApp
        //    (server-time based — fiável independente do SentAt do telemóvel)
        //    Bypass: sim/não com confirmação pendente → cancela delayed lock
        // Resultado: spam de 45 msgs → responde a 1
        //           mensagens novas APÓS resposta → processadas em ~1s
        //           confirmações (sim/não) → resposta imediata sempre
        private static readonly ConcurrentDictionary<string, byte> _usersBeingProcessed = new();
        private static readonly ConcurrentDictionary<string, DateTime> _lastResponseTime = new();
        private static readonly HttpClient _reverseGeocodeClient = new()
        {
            Timeout = TimeSpan.FromSeconds(3)
        };

        // Contagem de mensagens bloqueadas por utilizador (spam detectado).
        // Usado para ativar delayed unlock e logging.
        private static readonly ConcurrentDictionary<string, int> _spamBlockedCount = new();

        // CancellationToken para delayed unlock — permite cancelar quando bypass de confirmação.
        private static readonly ConcurrentDictionary<string, CancellationTokenSource> _delayedUnlockCts = new();

        /// <summary>
        /// Grace period (em segundos) para o filtro SentAt.
        /// 1s compensa o arredondamento do WhatsApp (SentAt tem precisão de SEGUNDOS,
        /// o bot regista milissegundos — a diferença máxima é ~1s).
        /// Ninguém lê uma resposta e escreve outra em menos de 1 segundo.
        /// </summary>
        private const int POST_RESPONSE_GRACE_SECONDS = 1;

        /// <summary>
        /// Segundos que o lock fica ativo APÓS resposta quando spam foi detetado.
        /// 2s absorve ondas de spam que o WhatsApp entrega nos segundos após a resposta.
        /// Usa server-time (DateTime.UtcNow) — fiável, não depende do SentAt do telemóvel.
        /// Confirmações (sim/não) cancelam o delay e passam imediatamente.
        /// </summary>
        private const int DELAYED_UNLOCK_SECONDS = 2;

        /// <summary>
        /// Janela (em segundos) para o utilizador enviar a localização após confirmar presença.
        /// </summary>
        private const int LOCATION_PIN_WINDOW_SECONDS = 30;

        /// <summary>
        /// Minutos até uma confirmação pendente expirar.
        /// Se o user não responder sim/não em 5 minutos, a confirmação é removida
        /// e a próxima mensagem é tratada como nova (não como tentativa inválida).
        /// </summary>
        private const int CONFIRMATION_EXPIRY_MINUTES = 5;

        /// <summary>
        /// [Retrocompatibilidade] Cooldown para IsLateMessage (usado pelos testes).
        /// NÃO é usado no fluxo principal.
        /// </summary>
        private const int SPAM_COOLDOWN_SECONDS = 10;

        static MessageProcessingService()
        {
            _reverseGeocodeClient.DefaultRequestHeaders.UserAgent.ParseAdd("EstagioELOBot/1.0");
        }

        // =====================================================================
        // Dependências
        // =====================================================================
        private readonly CommandRouter _commandRouter;
        private readonly ILogger<MessageProcessingService> _logger;
        private readonly MessagePrompts _messagePrompts;
        private readonly LanguageDetector _languageDetector;
        private readonly IBotLocalizer _localizer;

        public MessageProcessingService(
            CommandRouter commandRouter,
            ILogger<MessageProcessingService> logger,
            MessagePrompts messagePrompts,
            LanguageDetector languageDetector,
            IBotLocalizer localizer)
        {
            _commandRouter = commandRouter;
            _logger = logger;
            _messagePrompts = messagePrompts;
            _languageDetector = languageDetector;
            _localizer = localizer;
        }

        // =====================================================================
        // Método principal — processar uma mensagem de QUALQUER plataforma
        // =====================================================================
        /// <summary>
        /// Processa uma mensagem genérica (IncomingMessage) de qualquer plataforma.
        /// Recebe o IMessagingService correto para enviar respostas de volta.
        /// 
        /// Fluxo:
        /// 1. Logging e normalização do texto
        /// 2. Marcar como lida
        /// 3. Filtro de arranque: msg.SentAt anterior ao arranque do bot → ignorar
        /// 4. Filtro de spam (SentAt + 1s grace):
        ///    msg.SentAt ≤ lastResponse + 1s → ignorar (spam do mesmo burst)
        ///    (bypass: se há confirmação pendente, grace period ignorado)
        /// 5. Lock por utilizador: aguarda lock curto se já está em processamento
        ///    (bypass: sim/não com confirmação pendente → força entrada)
        /// 6. Confirmação expirada (>5 min)? → remover, tratar como nova
        /// 7. Se há confirmação pendente E é sim/não → processar confirmação
        /// 8. Se há confirmação pendente E NÃO é sim/não → tentativa inválida (3 max)
        /// 9. Se é comando válido → pedir confirmação
        /// 10. Senão → rotear (ajuda / comando desconhecido)
        /// 11. finally: RecordResponseTime + delayed unlock (2s) ou unlock imediato
        /// </summary>
        public async Task ProcessMessageAsync(IncomingMessage msg, IMessagingService service)
        {
            var stopwatch = Stopwatch.StartNew();

            string displayName = ConsoleLogger.GetDisplayName(msg);
            _logger.LogInformation("Mensagem recebida de {DisplayName} via {Platform}: {Body}",
                displayName, msg.Platform, msg.Body);

            ConsoleLogger.PrintMessageBox(msg);

            if (string.IsNullOrEmpty(msg.From))
                return;

            string replyAddress = GetReplyAddress(msg);

            msg.Body = NormalizeText(msg.Body);

            // Marcar como lida o mais cedo possível para evitar mensagens
            // "presas" sem visto quando forem ignoradas por filtros anti-spam.
            bool readOk = await service.MarkAsReadAsync(msg.MessageId);
            ConsoleLogger.PrintReadReceipt(msg.Platform, readOk);

            // Proteção 0 (WhatsApp): Mensagens anteriores ao arranque do bot.
            // Quando o bot reinicia, o WhatsApp pode re-entregar mensagens antigas.
            // No Teams isto pode causar falso-positivo (timestamp ligeiramente anterior),
            // por isso o filtro de arranque é aplicado apenas no WhatsApp.
            if (msg.Platform == MessagePlatform.WhatsApp && msg.SentAt < _startupTime)
            {
                _logger.LogInformation(
                    "🕐 Mensagem antiga ignorada: {DisplayName} — SentAt={SentAt:HH:mm:ss} anterior ao arranque ({Startup:HH:mm:ss})",
                    displayName, msg.SentAt, _startupTime);
                return;
            }

            // Proteção 1: Filtro de backlog/timestamp + grace.
            // 1) Backlog server-side: se a mensagem JÁ tinha chegado ao webhook antes
            //    da última resposta enviada, pertence ao burst anterior e é spam.
            //    Isto corrige cenários de WhatsApp Web com envio muito rápido.
            // 2) SentAt + grace: mantém proteção por timestamp do telemóvel.
            //
            // Bypass APENAS para resposta de decisão (sim/não) com confirmação pendente.
            // Mensagens não-sim/não continuam sujeitas ao anti-spam para não consumir
            // tentativas por bursts de teclas (ex.: "l l l l") após um comando.
            // ─── Deteção de língua ──────────────────────────────────────
            msg.Language ??= _languageDetector.DetectLanguage(msg);
            var lang = msg.Language;

            bool hasPendingConfirmation = _pendingConfirmations.ContainsKey(msg.From);
            bool isYesNoReply = _messagePrompts.IsYes(msg.Body) || _messagePrompts.IsNo(msg.Body);
            bool isPendingLocationReply = hasPendingConfirmation &&
                _pendingConfirmations.TryGetValue(msg.From, out PendingConfirmation? pendingForBypass) &&
                pendingForBypass.AwaitingLocation;
                
            // Bypass com "1" é exclusivo do Teams (porque não suporta envio de PIN de forma nativa)
            bool isOneBypass = msg.OriginalBody.Trim() == "1" && msg.Platform == MessagePlatform.Teams;
            bool allowPendingBypass = isYesNoReply || (isPendingLocationReply && (msg.HasLocation || isOneBypass));

            if ((!hasPendingConfirmation || !allowPendingBypass) &&
                _lastResponseTime.TryGetValue(msg.From, out DateTime lastResponse) &&
                msg.SentAt != DateTime.MinValue)
            {
                if (msg.ReceivedAt <= lastResponse)
                {
                    IncrementSpamCount(msg.From);
                    _logger.LogInformation(
                        "⏰ Spam backlog ignorado: {DisplayName} — ReceivedAt={ReceivedAt:HH:mm:ss.fff} ≤ LastResp={LastResp:HH:mm:ss.fff}",
                        displayName, msg.ReceivedAt, lastResponse);
                    return;
                }

                if (msg.SentAt <= lastResponse.AddSeconds(POST_RESPONSE_GRACE_SECONDS))
                {
                    IncrementSpamCount(msg.From);
                    _logger.LogInformation(
                        "⏰ Spam ignorado: {DisplayName} — SentAt={SentAt:HH:mm:ss.fff} dentro do grace period ({LastResp:HH:mm:ss.fff} +{Grace}s)",
                        displayName, msg.SentAt, lastResponse, POST_RESPONSE_GRACE_SECONDS);
                    return;
                }
            }

            // Proteção 2: Lock por utilizador — first-message-wins.
            // Se já há mensagem em processamento, esta mensagem é ignorada imediatamente.
            // Bypass: se há confirmação pendente e a resposta é válida (sim/não ou localização),
            // permitimos entrada para não bloquear o fluxo de confirmação.
            if (!TryLockUser(msg.From))
            {
                // Bypass: confirmação pendente + resposta válida → permitir resposta
                if (hasPendingConfirmation && allowPendingBypass)
                {
                    // Cancelar delayed unlock pendente para evitar race condition
                    if (_delayedUnlockCts.TryRemove(msg.From, out var pendingCts))
                        pendingCts.Cancel();

                    // Forçar entrada: libertar lock do delayed unlock e re-adquirir
                    UnlockUser(msg.From);
                    TryLockUser(msg.From);
                    _logger.LogInformation(
                        "🔓 Lock bypass para confirmação: {DisplayName} respondeu {Body}",
                        displayName, msg.Body);
                }
                else
                {
                    _logger.LogInformation(
                        "🔒 Mensagem ignorada: {DisplayName} em processamento (first-message-wins)",
                        displayName);
                    return;
                }
            }

            // try/finally garante que o lock é sempre libertado, mesmo em caso de erro
            bool responseDelivered = false;
            try
            {
            // Todas as respostas são vinculadas (reply-to) à mensagem original
            // para que o utilizador veja sempre qual mensagem o bot está a responder.
            string? replyTo = msg.MessageId;

            // Confirmação pendente → verificar expiração, depois processar sim/não.
            // Se expirou (>5 min), remover silenciosamente e tratar como mensagem nova.
            if (_pendingConfirmations.TryGetValue(msg.From, out PendingConfirmation? pending))
            {
                // Expiração: se a confirmação foi criada há mais de 5 minutos, descartar
                if (DateTime.UtcNow > pending.CreatedAt.AddMinutes(CONFIRMATION_EXPIRY_MINUTES))
                {
                    _pendingConfirmations.TryRemove(msg.From, out _);
                    _logger.LogInformation(
                        "⏳ Confirmação de *{Command}* expirada ({Minutes} min) — {DisplayName}",
                        pending.CommandName, CONFIRMATION_EXPIRY_MINUTES, displayName);
                    pending = null; // Fall through → tratar como mensagem nova
                }
            }

            if (pending != null)
            {
                string pendingCommand = pending.CommandName;

                if (pending.AwaitingLocation)
                {
                    if (pending.LocationRequestedAtUtc.HasValue &&
                        msg.SentAt > pending.LocationRequestedAtUtc.Value.AddSeconds(LOCATION_PIN_WINDOW_SECONDS))
                    {
                        _pendingConfirmations.TryRemove(msg.From, out _);

                        string finalLocationReply = _messagePrompts.BuildFinalLocationMissingMessage(lang);
                        bool finalLocationOk = await SendReplyAsync(
                            service,
                            msg,
                            replyAddress,
                            finalLocationReply,
                            replyTo,
                            msg.OriginalBody);
                        responseDelivered = finalLocationOk;
                        ConsoleLogger.PrintReplySent(msg, finalLocationOk);

                        stopwatch.Stop();
                        _logger.LogInformation(
                            "⏱️ Pedido de localização expirou por tempo | {DisplayName}",
                            displayName);
                        return;
                    }

                    double resolvedLatitude = 0;
                    double resolvedLongitude = 0;

                    if (isOneBypass || TryResolveLocation(msg, out resolvedLatitude, out resolvedLongitude))
                    {
                        if (isOneBypass)
                        {
                            msg.HasLocation = true;
                            msg.Latitude = 0.0;
                            msg.Longitude = 0.0;
                        }
                        else
                        {
                            msg.HasLocation = true;
                            msg.Latitude = resolvedLatitude;
                            msg.Longitude = resolvedLongitude;
                        }

                        _pendingConfirmations.TryRemove(msg.From, out _);

                        var confirmedCommandMessage = new IncomingMessage
                        {
                            MessageId = pending.OriginalMessage.MessageId,
                            From = pending.OriginalMessage.From,
                            ReplyEndpoint = pending.OriginalMessage.ReplyEndpoint,
                            UserId = pending.OriginalMessage.UserId,
                            UserName = pending.OriginalMessage.UserName,
                            UserPhone = pending.OriginalMessage.UserPhone,
                            UserEmail = pending.OriginalMessage.UserEmail,
                            Body = pending.OriginalMessage.Body,
                            OriginalBody = pending.OriginalMessage.OriginalBody,
                            Platform = pending.OriginalMessage.Platform,
                            ReceivedAt = pending.OriginalMessage.ReceivedAt,
                            SentAt = pending.OriginalMessage.SentAt,
                            HasLocation = true,
                            Latitude = msg.Latitude,
                            Longitude = msg.Longitude,
                            LocationName = msg.LocationName,
                            LocationAddress = msg.LocationAddress
                        };

                        string commandReply = await _commandRouter.RouteAsync(confirmedCommandMessage);
                        string locationSummary = _localizer.Get("Location_Received", lang);
                        string reply = $"{locationSummary}\n\n{commandReply}";

                        bool replyOk = await SendReplyAsync(
                            service,
                            msg,
                            replyAddress,
                            reply,
                            replyTo,
                            msg.OriginalBody);
                        responseDelivered = replyOk;
                        ConsoleLogger.PrintReplySent(msg, replyOk);

                        string? locationAddressForLog = await ResolveLocationAddressForLogAsync(msg);
                        string locationNameForLog = string.IsNullOrWhiteSpace(msg.LocationName)
                            ? "(sem nome)"
                            : msg.LocationName!;

                        stopwatch.Stop();
                        _logger.LogInformation(
                            "✅ Presença concluída com localização em {ElapsedMs}ms | From={DisplayName} | Lat={Latitude} | Lon={Longitude} | Name={LocationName} | Address={LocationAddress}",
                            stopwatch.ElapsedMilliseconds,
                            displayName,
                            msg.Latitude,
                            msg.Longitude,
                            locationNameForLog,
                            locationAddressForLog ?? "(sem morada)");
                        return;
                    }

                    string locationHelp = _messagePrompts.BuildLocationHelp(LOCATION_PIN_WINDOW_SECONDS, lang);

                    bool helpOk = await SendReplyAsync(
                        service,
                        msg,
                        replyAddress,
                        locationHelp,
                        replyTo,
                        msg.OriginalBody);
                    responseDelivered = helpOk;
                    ConsoleLogger.PrintReplySent(msg, helpOk);

                    stopwatch.Stop();
                    _logger.LogInformation(
                        "⚠️ Pedido de localização ainda dentro da janela ou texto inválido | {DisplayName}",
                        displayName);
                    return;
                }

                if (_messagePrompts.IsYes(msg.Body))
                {
                    // TODO: Localização comentada temporariamente para que o "Sim" avance diretamente para o webservice
                    /*
                    if (IsPresenceCommand(pendingCommand))
                    {
                        pending.AwaitingLocation = true;
                        pending.LocationRequestedAtUtc = DateTime.UtcNow;
                        string locationPrompt = _messagePrompts.BuildLocationRequestPrompt(LOCATION_PIN_WINDOW_SECONDS, lang);

                        bool locationPromptOk = await SendReplyAsync(
                            service,
                            msg,
                            replyAddress,
                            locationPrompt,
                            replyTo,
                            msg.OriginalBody);
                        responseDelivered = locationPromptOk;
                        ConsoleLogger.PrintReplySent(msg, locationPromptOk);

                        stopwatch.Stop();
                        _logger.LogInformation(
                            "📍 Presença confirmada por {DisplayName}; aguardando localização",
                            displayName);
                        return;
                    }
                    */

                    _pendingConfirmations.TryRemove(msg.From, out _);
                    
                    // Guardar o texto exato da confirmação para o handler poder usar no log
                    pending.OriginalMessage.ConfirmationText = msg.OriginalBody;
                    
                    string reply = await _commandRouter.RouteAsync(pending.OriginalMessage);

                    bool replyOk = await SendReplyAsync(
                        service,
                        msg,
                        replyAddress,
                        reply,
                        replyTo,
                        msg.OriginalBody);
                    responseDelivered = replyOk;
                    ConsoleLogger.PrintReplySent(msg, replyOk);

                    stopwatch.Stop();
                    _logger.LogInformation(
                        "✅ Mensagem processada em {ElapsedMs}ms | From={DisplayName} | Status={ReadOk} → {ReplyOk}",
                        stopwatch.ElapsedMilliseconds, displayName, readOk, replyOk);
                    return;
                }
                else if (_messagePrompts.IsNo(msg.Body))
                {
                    _pendingConfirmations.TryRemove(msg.From, out _);
                    string reply = _localizer.Get("Confirmation_Cancelled", lang, pendingCommand);

                    bool replyOk = await SendReplyAsync(
                        service,
                        msg,
                        replyAddress,
                        reply,
                        replyTo,
                        msg.OriginalBody);
                    responseDelivered = replyOk;
                    ConsoleLogger.PrintReplySent(msg, replyOk);

                    stopwatch.Stop();
                    _logger.LogInformation(
                        "✅ Mensagem processada em {ElapsedMs}ms | From={DisplayName} | Status={ReadOk} → {ReplyOk}",
                        stopwatch.ElapsedMilliseconds, displayName, readOk, replyOk);
                    return;
                }
                else
                {
                    // Mensagem não é sim/não → tentativa inválida.
                    // O utilizador tem 3 tentativas para responder sim/não.
                    // Após 3 tentativas inválidas, a confirmação é cancelada.
                    pending.InvalidAttempts++;

                    if (pending.InvalidAttempts >= 3)
                    {
                        // 3 tentativas esgotadas → cancelar confirmação
                        _pendingConfirmations.TryRemove(msg.From, out _);
                        _logger.LogInformation(
                            "🔄 Confirmação de *{Command}* cancelada após 3 tentativas inválidas — {DisplayName}",
                            pendingCommand, displayName);

                        string finalInvalidReply = _messagePrompts.BuildFinalInvalidConfirmationMessage(pendingCommand, lang);
                        bool finalInvalidOk = await SendReplyAsync(
                            service,
                            msg,
                            replyAddress,
                            finalInvalidReply,
                            replyTo,
                            msg.OriginalBody);
                        responseDelivered = finalInvalidOk;
                        ConsoleLogger.PrintReplySent(msg, finalInvalidOk);

                        stopwatch.Stop();
                        _logger.LogInformation(
                            "⚠️ Tentativa inválida 3/3 para *{Command}* | {DisplayName} enviou '{Body}'",
                            pendingCommand, displayName, msg.Body);
                        return;
                    }
                    else
                    {
                        // Mostrar ajuda com tentativas restantes
                        string helpReply = _messagePrompts.BuildYesNoHelp(pending.InvalidAttempts, pendingCommand, lang);

                        bool helpOk = await SendReplyAsync(
                            service,
                            msg,
                            replyAddress,
                            helpReply,
                            replyTo,
                            msg.OriginalBody);
                        responseDelivered = helpOk;
                        ConsoleLogger.PrintReplySent(msg, helpOk);

                        stopwatch.Stop();
                        _logger.LogInformation(
                            "⚠️ Tentativa inválida {Attempt}/3 para *{Command}* | {DisplayName} enviou '{Body}'",
                            pending.InvalidAttempts, pendingCommand, displayName, msg.Body);
                        return;
                    }
                }
            }

            // Sem confirmação pendente (ou expirada) → verificar se é comando válido
            if (!_commandRouter.IsValidCommand(msg))
            {
                string reply;

                if (_messagePrompts.IsYes(msg.Body) || _messagePrompts.IsNo(msg.Body))
                {
                    reply = _messagePrompts.BuildNoPendingConfirmationMessage(lang);
                    _logger.LogWarning("Utilizador {From} enviou '{Body}' sem confirmação pendente", msg.From, msg.Body);
                }
                else
                {
                    reply = await _commandRouter.RouteAsync(msg);
                }

                bool replyOk = await SendReplyAsync(
                    service,
                    msg,
                    replyAddress,
                    reply,
                    replyTo,
                    msg.OriginalBody);
                responseDelivered = replyOk;
                ConsoleLogger.PrintReplySent(msg, replyOk);

                stopwatch.Stop();
                _logger.LogInformation(
                    "✅ Mensagem processada em {ElapsedMs}ms | From={DisplayName} | Status={ReadOk} → {ReplyOk}",
                    stopwatch.ElapsedMilliseconds, displayName, readOk, replyOk);
                return;
            }

            // Comando válido → pedir confirmação
            // Determina o nome do comando para contextualizar a confirmação
            string commandName = "o pedido";
            ICommandHandler? matchedHandler = null;
            if (_commandRouter.TryGetMatchedHandler(msg, out matchedHandler) && matchedHandler != null)
            {
                commandName = matchedHandler.CommandName;
            }

            // Comandos administrativos, de ajuda ou de listagem de marcações não pedem confirmação
            bool bypassConfirmation = commandName.StartsWith("admin", StringComparison.OrdinalIgnoreCase)
                || commandName.Equals("ajuda", StringComparison.OrdinalIgnoreCase)
                || commandName.Equals("listagem_marcações", StringComparison.OrdinalIgnoreCase);

            if (bypassConfirmation && matchedHandler != null)
            {
                string directReply = await matchedHandler.ExecuteAsync(msg);
                bool directReplyOk = await SendReplyAsync(
                    service,
                    msg,
                    replyAddress,
                    directReply,
                    replyTo,
                    msg.OriginalBody);
                responseDelivered = directReplyOk;
                ConsoleLogger.PrintReplySent(msg, directReplyOk);

                stopwatch.Stop();
                _logger.LogInformation(
                    "✅ Comando {CommandName} executado sem confirmação em {ElapsedMs}ms | From={DisplayName} | Status={ReadOk} → {ReplyOk}",
                    commandName, stopwatch.ElapsedMilliseconds, displayName, readOk, directReplyOk);
                return;
            }

            var confirmation = new PendingConfirmation
            {
                OriginalMessage = new IncomingMessage
                {
                    MessageId = msg.MessageId,
                    From = msg.From,
                    ReplyEndpoint = msg.ReplyEndpoint,
                    UserId = msg.UserId,
                    UserName = msg.UserName,
                    UserEmail = msg.UserEmail,
                    UserPhone = msg.UserPhone,
                    Body = msg.Body,
                    OriginalBody = msg.OriginalBody,
                    Platform = msg.Platform,
                    ReceivedAt = msg.ReceivedAt,
                    SentAt = msg.SentAt
                },
                CommandName = commandName,
                CreatedAt = DateTime.UtcNow
            };

            _pendingConfirmations[msg.From] = confirmation;

            string confirmPrompt = _messagePrompts.BuildConfirmationPrompt(commandName, lang);
            bool confirmOk = await SendReplyAsync(
                service,
                msg,
                replyAddress,
                confirmPrompt,
                replyTo,
                msg.OriginalBody);
            responseDelivered = confirmOk;
            ConsoleLogger.PrintReplySent(msg, confirmOk);

            stopwatch.Stop();
            _logger.LogInformation(
                "✅ Mensagem processada em {ElapsedMs}ms | From={DisplayName} | Status={ReadOk} → {ReplyOk}",
                stopwatch.ElapsedMilliseconds, displayName, readOk, confirmOk);

            } // try
            finally
            {
                // Log de spam se houve mensagens bloqueadas
                int blockedCount = GetSpamBlockedCount(msg.From);
                if (blockedCount > 0)
                {
                    _logger.LogInformation(
                        "🛡️ Spam absorvido: {BlockedCount} mensagem(ns) bloqueada(s) de {DisplayName}",
                        blockedCount, displayName);
                }

                // Só registar o tempo de resposta se a mensagem foi REALMENTE entregue.
                // Se houve rate limit ou erro da API, NÃO marcar como respondido,
                // caso contrário as mensagens seguintes seriam bloqueadas como spam
                // mas o utilizador nunca teria recebido resposta.
                if (responseDelivered)
                {
                    RecordResponseTime(msg.From);
                }
                else
                {
                    _logger.LogWarning(
                        "⚠️ Resposta NÃO entregue a {DisplayName} — NÃO registar como respondido (próxima mensagem será processada)",
                        displayName);
                }
                ResetSpamCount(msg.From);

                // Unlock: imediato se sem spam, delayed (2s) se spam detetado.
                // O delayed unlock MANTÉM O LOCK por 2 segundos (server-time),
                // absorvendo ondas de spam que o WhatsApp entrega logo após a resposta.
                // Combinado com o grace de 1s (Proteção 1), cobre ~3s de proteção total.
                // Confirmações (sim/não) cancelam o delay e passam imediatamente.
                if (blockedCount > 0)
                {
                    string userId = msg.From;
                    var cts = new CancellationTokenSource();
                    _delayedUnlockCts[userId] = cts;
                    
                    // IMPORTANTE: NÃO libertamos o lock aqui — ele continua ativo!
                    // O Task.Run apenas CONTA O TEMPO e depois liberta.
                    // Enquanto o delay decorre, TryLockUser() retorna false e IncrementSpamCount é chamado.
                    _ = Task.Run(async () =>
                    {
                        try { await Task.Delay(DELAYED_UNLOCK_SECONDS * 1000, cts.Token); }
                        catch (OperationCanceledException) { return; }
                        finally { UnlockUser(userId); _delayedUnlockCts.TryRemove(userId, out _); }
                    });
                    _logger.LogInformation(
                        "🔒 Delayed unlock ({Seconds}s) para {DisplayName} — spam detetado ({BlockedCount} msg bloqueadas)",
                        DELAYED_UNLOCK_SECONDS, displayName, blockedCount);
                }
                else
                {
                    // Sem spam → unlock imediato, pronto para a próxima mensagem.
                    UnlockUser(msg.From);
                }
            }
        }

        // =====================================================================
        // Deduplicação
        // =====================================================================

        /// <summary>
        /// Verifica se uma mensagem já foi processada (dentro da janela de 5 min).
        /// </summary>
        public static bool IsDuplicateMessage(string messageId)
        {
            CleanupOldProcessedEntries();
            return !_processedMessageIds.TryAdd(messageId, DateTime.UtcNow);
        }

        private static void CleanupOldProcessedEntries()
        {
            if (_processedMessageIds.Count < 500)
                return;

            DateTime cutoff = DateTime.UtcNow - _duplicateWindow;
            foreach (var kvp in _processedMessageIds)
            {
                if (kvp.Value < cutoff)
                    _processedMessageIds.TryRemove(kvp.Key, out _);
            }
        }

        // =====================================================================
        // Proteção anti-spam: SentAt Grace Period + Lock
        // =====================================================================

        /// <summary>
        /// Proteção principal anti-spam: verifica se o utilizador está em cooldown.
        /// Após o bot responder, TODAS as mensagens são bloqueadas por SPAM_COOLDOWN_SECONDS.
        /// Também bloqueia mensagens com SentAt anterior à última resposta (phone timestamp).
        /// </summary>
        public static bool IsLateMessage(string userId, DateTime sentAt)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return false;

            if (!_lastResponseTime.TryGetValue(userId, out DateTime lastResponse))
                return false;

            // Cooldown server-side
            if (DateTime.UtcNow < lastResponse.AddSeconds(SPAM_COOLDOWN_SECONDS))
            {
                IncrementSpamCount(userId);
                return true;
            }

            // Phone timestamp (retrocompatibilidade)
            if (sentAt != DateTime.MinValue && sentAt < lastResponse)
            {
                IncrementSpamCount(userId);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Tenta adquirir o lock de processamento para um utilizador.
        /// Retorna true se o lock foi adquirido (pode processar).
        /// Retorna false se o utilizador já tem uma mensagem em processamento.
        /// </summary>
        public static bool TryLockUser(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return true;

            bool acquired = _usersBeingProcessed.TryAdd(userId, 0);
            if (!acquired)
                IncrementSpamCount(userId);
            return acquired;
        }

        /// <summary>
        /// Verifica se um utilizador tem uma mensagem em processamento.
        /// </summary>
        public static bool IsUserLocked(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return false;

            return _usersBeingProcessed.ContainsKey(userId);
        }

        /// <summary>
        /// Liberta o lock de processamento para um utilizador.
        /// Chamado automaticamente no finally após o bot enviar a resposta.
        /// </summary>
        public static void UnlockUser(string userId)
        {
            if (!string.IsNullOrWhiteSpace(userId))
                _usersBeingProcessed.TryRemove(userId, out _);
        }

        // =====================================================================
        // Spam Detection — contagem de mensagens bloqueadas
        // =====================================================================

        /// <summary>
        /// Incrementa o contador de mensagens bloqueadas para um utilizador.
        /// Chamado por IsLateMessage e TryLockUser quando bloqueiam uma mensagem.
        /// </summary>
        private static void IncrementSpamCount(string userId)
        {
            _spamBlockedCount.AddOrUpdate(userId, 1, (_, count) => count + 1);
        }

        /// <summary>
        /// Verifica se houve spam detectado (mensagens bloqueadas) para um utilizador.
        /// Quando true, a resposta do bot deve ser vinculada (reply-to) à mensagem original
        /// para que o utilizador veja claramente qual mensagem foi processada.
        /// </summary>
        public static bool WasSpamDetected(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return false;
            return _spamBlockedCount.TryGetValue(userId, out int count) && count > 0;
        }

        /// <summary>
        /// Obtém o número de mensagens bloqueadas para um utilizador.
        /// Útil para logging e diagnóstico.
        /// </summary>
        public static int GetSpamBlockedCount(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return 0;
            return _spamBlockedCount.TryGetValue(userId, out int count) ? count : 0;
        }

        /// <summary>
        /// Limpa o contador de spam para um utilizador.
        /// Chamado no finally após enviar a resposta.
        /// </summary>
        public static void ResetSpamCount(string userId)
        {
            if (!string.IsNullOrWhiteSpace(userId))
                _spamBlockedCount.TryRemove(userId, out _);
        }

        /// <summary>
        /// Regista o momento em que respondemos a um utilizador.
        /// Chamado no finally, ANTES do UnlockUser.
        /// Mensagens com SentAt ≤ este momento + grace period serão ignoradas como spam.
        /// </summary>
        public static void RecordResponseTime(string userId)
        {
            if (!string.IsNullOrWhiteSpace(userId))
                _lastResponseTime[userId] = DateTime.UtcNow;
        }

        /// <summary>
        /// Overload para testes — permite definir um timestamp específico.
        /// Usado para testar o phone-timestamp path sem esperar pelo cooldown.
        /// </summary>
        public static void RecordResponseTime(string userId, DateTime responseTime)
        {
            if (!string.IsNullOrWhiteSpace(userId))
                _lastResponseTime[userId] = responseTime;
        }

        /// <summary>
        /// Obtém o timestamp da última resposta ao utilizador.
        /// Retorna null se nunca respondemos a este utilizador.
        /// </summary>
        public static DateTime? GetLastResponseTime(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return null;

            return _lastResponseTime.TryGetValue(userId, out DateTime time) ? time : null;
        }

        /// <summary>
        /// Obtém o momento em que a aplicação arrancou.
        /// Mensagens com SentAt anterior são ignoradas como "antigas".
        /// </summary>
        public static DateTime GetStartupTime() => _startupTime;

        /// <summary>
        /// Limpa todo o estado de um utilizador (lock + timestamp).
        /// Útil para testes.
        /// </summary>
        public static void ResetUserState(string userId)
        {
            if (!string.IsNullOrWhiteSpace(userId))
            {
                _usersBeingProcessed.TryRemove(userId, out _);
                _lastResponseTime.TryRemove(userId, out _);
                _spamBlockedCount.TryRemove(userId, out _);
                if (_delayedUnlockCts.TryRemove(userId, out var cts))
                    cts.Cancel();
            }
        }

        // =====================================================================
        // Normalização de texto
        // =====================================================================

        /// <summary>
        /// Normaliza texto: remove emojis, pontuação, números e espaços extras.
        /// Mantém apenas letras e espaços.
        /// </summary>
        internal static string NormalizeText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            // Remove tudo exceto letras e espaços (remove números, emojis, pontuação)
            text = Regex.Replace(text, @"[^\p{L}\s]", " ", RegexOptions.Compiled);
            text = text.ToLowerInvariant();
            text = Regex.Replace(text, @"\s+", " ", RegexOptions.Compiled).Trim();

            return text;
        }

        private static string GetReplyAddress(IncomingMessage msg)
        {
            if (!string.IsNullOrWhiteSpace(msg.ReplyEndpoint))
                return msg.ReplyEndpoint;

            return msg.From;
        }

        private static Task<bool> SendReplyAsync(
            IMessagingService service,
            IncomingMessage msg,
            string replyAddress,
            string replyText,
            string? replyTo,
            string? quotedSourceText = null)
        {
            string finalText = BuildReplyTextForPlatform(msg.Platform, replyText, quotedSourceText, replyTo);
            return service.SendTextMessageAsync(replyAddress, finalText, replyTo);
        }

        private static string BuildReplyTextForPlatform(
            MessagePlatform platform,
            string replyText,
            string? quotedSourceText,
            string? replyTo)
        {
            if (platform != MessagePlatform.Teams)
                return replyText;

            if (string.IsNullOrWhiteSpace(replyTo) || string.IsNullOrWhiteSpace(quotedSourceText))
                return replyText;

            string compactSource = quotedSourceText
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Trim();

            if (string.IsNullOrWhiteSpace(compactSource))
                return replyText;

            if (compactSource.Length > 180)
                compactSource = compactSource[..177] + "...";

            return $"> {compactSource}\n\n{replyText}";
        }

        private static bool IsPresenceCommand(string commandName)
            => commandName.Contains("presen", StringComparison.OrdinalIgnoreCase)
            || commandName.Contains("presença", StringComparison.OrdinalIgnoreCase);

        private static async Task<string?> ResolveLocationAddressForLogAsync(IncomingMessage msg)
        {
            if (!string.IsNullOrWhiteSpace(msg.LocationAddress))
                return msg.LocationAddress;

            if (msg.Latitude.HasValue && msg.Longitude.HasValue)
            {
                return await TryReverseGeocodeAsync(msg.Latitude.Value, msg.Longitude.Value);
            }

            return null;
        }

        private static async Task<string?> TryReverseGeocodeAsync(double latitude, double longitude)
        {
            try
            {
                string lat = latitude.ToString("F6", System.Globalization.CultureInfo.InvariantCulture);
                string lon = longitude.ToString("F6", System.Globalization.CultureInfo.InvariantCulture);
                string url = $"https://nominatim.openstreetmap.org/reverse?format=jsonv2&lat={lat}&lon={lon}&zoom=18&addressdetails=1";

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Accept.ParseAdd("application/json");

                using HttpResponseMessage response = await _reverseGeocodeClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                    return null;

                await using var stream = await response.Content.ReadAsStreamAsync();
                using JsonDocument doc = await JsonDocument.ParseAsync(stream);

                if (doc.RootElement.TryGetProperty("display_name", out JsonElement displayNameEl) &&
                    displayNameEl.ValueKind == JsonValueKind.String)
                {
                    return displayNameEl.GetString();
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private static bool TryResolveLocation(IncomingMessage msg, out double latitude, out double longitude)
        {
            latitude = default;
            longitude = default;

            if (!msg.HasLocation || !msg.Latitude.HasValue || !msg.Longitude.HasValue)
            {
                return false;
            }

            latitude = msg.Latitude.Value;
            longitude = msg.Longitude.Value;

            if (latitude < -90 || latitude > 90 || longitude < -180 || longitude > 180)
            {
                return false;
            }

            // Null Island (coordenada típica de dados inválidos)
            if (Math.Abs(latitude) < 0.000001 && Math.Abs(longitude) < 0.000001)
            {
                return false;
            }

            return true;
        }

        // =====================================================================
        // Classe interna
        // =====================================================================
        private sealed class PendingConfirmation
        {
            public IncomingMessage OriginalMessage { get; set; } = new();
            public string CommandName { get; set; } = "o pedido";
            public int InvalidAttempts { get; set; }
            public bool AwaitingLocation { get; set; }
            public DateTime? LocationRequestedAtUtc { get; set; }
            public DateTime CreatedAt { get; set; }
        }
    }
}
