using Microsoft.Extensions.Options;
using WebApplication1.Core.Localization;
using WebApplication1.Core.Models;
using WebApplication1.Infrastructure.Configuration;
using WebApplication1.Infrastructure.ExternalApis;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WebApplication1.Core.Interfaces;

namespace WebApplication1.Core.Commands
{
    /// <summary>
    /// Comando de LISTAGEM DE FÉRIAS — Interpreta o comando para ver as férias
    /// do colaborador, solicita o ano pretendido (se não vier na mensagem),
    /// pede confirmação simples (sim/não) e apresenta os dados.
    /// 
    /// Fluxo:
    ///   1. Trigger inicial (ex: "férias", "vacations", "vacances", "vacaciones")
    ///   2. Se o ano NÃO vier na mensagem → perguntar o ano (AwaitingYear)
    ///   3. Se o ano vier na mensagem → pedir confirmação (AwaitingConfirmation)
    ///   4. Sim → executar consulta ao Web Service (comentado por agora)
    ///   5. Não → cancelar
    ///   6. Máximo 3 tentativas inválidas → cancelamento automático
    ///   7. Expiração do estado pendente após 5 minutos
    /// </summary>
    public class ListagemFeriasCommandHandler : ICommandHandler
    {
        private readonly WebServiceSettings _wsSettings;
        private readonly ILogger<ListagemFeriasCommandHandler> _logger;
        private readonly IBotLocalizer _localizer;

        // Estado pendente por utilizador para reter a transição do fluxo
        private static readonly ConcurrentDictionary<string, PendingFeriasState> _pendingFerias = new(StringComparer.OrdinalIgnoreCase);

        // Tempo limite de expiração do estado pendente (5 minutos)
        private static readonly TimeSpan _pendingStateExpiry = TimeSpan.FromMinutes(5);

        // ─── Mapa de triggers → língua (construído a partir dos recursos) ──
        private readonly Dictionary<string, SupportedLanguage> _triggerMap;
        private readonly ITokenService _tokenService;

        private enum FeriasState
        {
            AwaitingYear,
            AwaitingConfirmation
        }

        public ListagemFeriasCommandHandler(
            IOptions<WebServiceSettings> wsOptions,
            ILogger<ListagemFeriasCommandHandler> logger,
            IBotLocalizer localizer,
            ITokenService tokenService)
        {
            _wsSettings = wsOptions.Value;
            _logger = logger;
            _localizer = localizer;
            _tokenService = tokenService;
            _triggerMap = localizer.GetAllTriggers("Ferias");
        }

        public string CommandName => "listagem_ferias";

        public string GetDescription(SupportedLanguage? language) => _localizer.Get("Ferias_Description", language);

        public string[] Triggers => _triggerMap.Keys.ToArray();

        private string NormalizeKey(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

        public bool CanHandle(IncomingMessage message)
        {
            string userId = NormalizeKey(message.From);

            // Se o utilizador já tem um pedido pendente, este handler intercepta
            if (_pendingFerias.TryGetValue(userId, out var state))
            {
                // Limpeza silenciosa de estados expirados
                if (DateTime.UtcNow > state.CreatedAt.Add(_pendingStateExpiry))
                {
                    _pendingFerias.TryRemove(userId, out _);
                    return false;
                }
                return true;
            }

            string text = message.Body.Trim();
            return _triggerMap.ContainsKey(text);
        }

        public async Task<string> ExecuteAsync(IncomingMessage message)
        {
            string userId = NormalizeKey(message.From);

            // Detetar a língua pelo trigger usado (se for o trigger inicial)
            if (_triggerMap.TryGetValue(message.Body.Trim(), out var triggerLang))
            {
                message.Language ??= triggerLang;
            }

            var lang = message.Language;

            // Verificar se o utilizador já tem um estado pendente ativo
            if (_pendingFerias.TryGetValue(userId, out var state))
            {
                // Usar a língua guardada no estado pendente
                lang = state.DetectedLanguage ?? lang;
                message.Language = lang;

                // Limpeza se o estado tiver expirado
                if (DateTime.UtcNow > state.CreatedAt.Add(_pendingStateExpiry))
                {
                    _pendingFerias.TryRemove(userId, out _);
                    if (!_triggerMap.ContainsKey(message.Body.Trim()))
                    {
                        return _localizer.Get("Ferias_Expired", lang);
                    }
                }
                else
                {
                    // ─── ETAPA 1: Aguardar Ano ────────────────────────────────
                    if (state.CurrentState == FeriasState.AwaitingYear)
                    {
                        string bodyToLower = message.Body.Trim().ToLowerInvariant();
                        if (bodyToLower == "cancelar" || bodyToLower == "sair" || bodyToLower == "cancel" || bodyToLower == "exit")
                        {
                            _pendingFerias.TryRemove(userId, out _);
                            return _localizer.Get("Ferias_Cancelled", lang);
                        }

                        state.Attempts++;
                        if (state.Attempts >= 3)
                        {
                            _pendingFerias.TryRemove(userId, out _);
                            return _localizer.Get("Ferias_Cancelled", lang);
                        }

                        if (TryExtractYear(message.OriginalBody, out int extractedYear, out string yearError))
                        {
                            // Transição de estado: ano válido
                            state.SelectedYear = extractedYear;
                            state.YearInput = message.OriginalBody;
                            state.CurrentState = FeriasState.AwaitingConfirmation;

                            return _localizer.Get("Ferias_ConfirmYear", lang, extractedYear.ToString());
                        }
                        else
                        {
                            // A extração falhou. Solicitar novamente
                            return $"{yearError}{_localizer.Get("Ferias_YearRetryHelp", lang)}";
                        }
                    }

                    // ─── ETAPA 2: Aguardar Confirmação (Sim / Não) ────────────
                    if (state.CurrentState == FeriasState.AwaitingConfirmation)
                    {
                        string body = message.Body.Trim().ToLowerInvariant();

                        if (_localizer.IsYes(body))
                        {
                            // Sucesso! Remover o estado pendente e processar
                            _pendingFerias.TryRemove(userId, out _);
                            return await ProcessFeriasAsync(state, message, lang);
                        }
                        else
                        {
                            // Qualquer resposta diferente de SIM cancela o fluxo
                            _pendingFerias.TryRemove(userId, out _);
                            return _localizer.Get("Ferias_Cancelled", lang);
                        }
                    }
                }
            }

            // ─── Trigger inicial: verificar se o ano já vem na mensagem ──────
            // Ex: "férias 2026", "vacations 2025"
            string originalText = message.OriginalBody ?? message.Body;
            if (TryExtractYearFromTriggerMessage(originalText, out int yearFromTrigger))
            {
                // Ano já veio no trigger → ir diretamente para confirmação
                var newState = new PendingFeriasState
                {
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow,
                    OriginalMessage = message,
                    CurrentState = FeriasState.AwaitingConfirmation,
                    DetectedLanguage = lang,
                    SelectedYear = yearFromTrigger,
                    YearInput = originalText
                };
                _pendingFerias[userId] = newState;

                return _localizer.Get("Ferias_ConfirmYear", lang, yearFromTrigger.ToString());
            }

            // Início do comando sem ano: Guardar o estado pendente na Etapa 1
            var freshState = new PendingFeriasState
            {
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                OriginalMessage = message,
                CurrentState = FeriasState.AwaitingYear,
                DetectedLanguage = lang
            };
            _pendingFerias[userId] = freshState;

            return _localizer.Get("Ferias_AskYear", lang);
        }

        /// <summary>
        /// Extrai e valida um ano de 4 dígitos de uma mensagem.
        /// Aceita anos entre 2000 e 2099.
        /// </summary>
        private bool TryExtractYear(string input, out int year, out string validationError)
        {
            year = 0;
            validationError = string.Empty;

            if (string.IsNullOrWhiteSpace(input))
            {
                validationError = _localizer.Get("Ferias_EmptyInput", null);
                return false;
            }

            // Procurar um número de 4 dígitos que pareça um ano
            var match = Regex.Match(input.Trim(), @"\b(20\d{2})\b");
            if (match.Success && int.TryParse(match.Groups[1].Value, out int parsedYear))
            {
                year = parsedYear;
                return true;
            }

            validationError = _localizer.Get("Ferias_InvalidYear", null);
            return false;
        }

        /// <summary>
        /// Tenta extrair um ano a partir da mensagem original do trigger.
        /// Ex: "férias 2026" → extrai 2026
        /// </summary>
        private bool TryExtractYearFromTriggerMessage(string originalText, out int year)
        {
            year = 0;
            if (string.IsNullOrWhiteSpace(originalText))
                return false;

            var match = Regex.Match(originalText, @"\b(20\d{2})\b");
            if (match.Success && int.TryParse(match.Groups[1].Value, out int parsedYear))
            {
                year = parsedYear;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Processa a consulta de férias para o ano confirmado.
        /// A construção da string do WebService e o envio estão comentados.
        /// </summary>
        private async Task<string> ProcessFeriasAsync(PendingFeriasState state, IncomingMessage confirmationMessage, SupportedLanguage? lang)
        {
            int year = state.SelectedYear;

            _logger.LogInformation(
                "🏖️ A executar consulta de férias para {UserId}: Ano={Year}",
                state.OriginalMessage.From, year);

            // ─── Criar string de log das mensagens processadas ───
            string mensagensProcessadasLog = $"{state.OriginalMessage.OriginalBody}|{state.YearInput}|{confirmationMessage.OriginalBody}";
            _logger.LogInformation("Histórico processado com sucesso: {Log}", mensagensProcessadasLog);

            // ─── CHAMADA DO WEB SERVICE WCF ───
            // ╔══════════════════════════════════════════════════════════════╗
            // ║  QUANDO O ENDPOINT DE FÉRIAS ESTIVER PRONTO NO SERVIDOR:   ║
            // ║                                                            ║
            // ║  1. Descomentar o bloco /* ... */ abaixo (linhas do WCF)   ║
            // ║  2. Descomentar o método BuildFeriasResponse() mais abaixo ║
            // ║  3. APAGAR as 3 linhas marcadas com "REMOVER"              ║
            // ║  4. Ajustar o nome do método WCF ("metafr1") se necessário ║
            // ║  5. Ajustar o token se necessário                          ║
            // ╚══════════════════════════════════════════════════════════════╝
            try
            {
                /*  ←── DESCOMENTAR: remover esta linha
                string token = "Diogo";
                
                // TODO: Para usar o token seguro e dinâmico, descomentar a linha abaixo e remover o token "Diogo"
                // string token = _tokenService.GenerateToken(state.OriginalMessage.Platform == MessagePlatform.Teams ? state.OriginalMessage.UserEmail : state.OriginalMessage.From);
                
                IncomingMessage message = state.OriginalMessage;
                int apiType = message.Platform == MessagePlatform.Teams ? 2 : 1;
                string numTelefone = message.Platform == MessagePlatform.WhatsApp ? message.From : "";
                string email = message.Platform == MessagePlatform.Teams ? (message.UserEmail ?? "") : "";

                string commandType = "ferias";
                string dataFormatada = message.ReceivedAt.ToString("dd-MM-yyyy HH:mm:ss");

                // Parâmetro: token|apiType|telefone|commandType|dataFormatada|email|ano
                string parametroBruto = $"{token}|{apiType}|{numTelefone}|{commandType}|{dataFormatada}|{email}|{year}";

                byte[] textBytes = System.Text.Encoding.UTF8.GetBytes(parametroBruto);
                string parametro = System.Convert.ToBase64String(textBytes);

                _logger.LogInformation("📡 A chamar o Web Service de férias com o parâmetro: {Param}", parametro);

                string bodyParaEnviar = $"\"{parametro}\"";

                // Chamar o endpoint de férias (ajustar o nome do método quando disponível)
                string rawResponse = await WebServiceUtils.WCFRESTServiceCall(
                    "POST", "metafr1", bodyParaEnviar, _logger, _wsSettings.BaseUrl);

                if (rawResponse.StartsWith("EXCEPTION|"))
                {
                    throw new HttpRequestException($"Erro transitório do serviço: {rawResponse}");
                }

                // Descodificar resposta Base64
                string limpa = rawResponse.Replace("\"", "").Replace("\\", "");
                byte[] responseBytes = System.Convert.FromBase64String(limpa);
                string responseDecoded = System.Text.Encoding.UTF8.GetString(responseBytes);

                _logger.LogInformation("📡 Resposta decodificada do Web Service de férias: {Response}", responseDecoded);

                // TODO: Tratar os códigos de resposta do WCF para férias (igual ao PresencaCommandHandler)
                // TODO: Fazer parse da resposta e construir a mensagem de férias formatada

                return BuildFeriasResponse(responseDecoded, year, state.OriginalMessage, lang);
                */  // ←── DESCOMENTAR: remover esta linha

                // ╔══════════════════════════════════════════════════════════╗
                // ║  REMOVER: apagar estas 3 linhas quando ativar o WCF    ║
                // ╚══════════════════════════════════════════════════════════╝
                await Task.CompletedTask; // REMOVER
                return _localizer.Get("Ferias_Placeholder", lang, year.ToString()); // REMOVER
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao tentar chamar o Web Service de férias");
                return _localizer.Get("Ferias_ErrorGeneric", lang);
            }
        }

        // ╔══════════════════════════════════════════════════════════════╗
        // ║  DESCOMENTAR: remover o /* e o */ quando ativar o WCF       ║
        // ║  (corresponde ao passo 2 das instruções acima)              ║
        // ╚══════════════════════════════════════════════════════════════╝
        /*
        /// <summary>
        /// Faz o parse da string recebida do WebService e constrói a listagem de férias formatada.
        /// TODO: Implementar quando o formato de resposta do WCF for definido.
        /// </summary>
        private string BuildFeriasResponse(string decodedResponse, int year, IncomingMessage message, SupportedLanguage? lang)
        {
            string userName = string.IsNullOrWhiteSpace(message.UserName)
                ? _localizer.Get("Ferias_DefaultUser", lang)
                : message.UserName;

            var lines = new List<string>
            {
                _localizer.Get("Ferias_Title", lang),
                _localizer.Get("Ferias_Year", lang, year.ToString()),
                _localizer.Get("Ferias_Separator", lang),
                ""
            };

            // TODO: Parse da resposta do WCF e construção das linhas de férias
            // Formato esperado: a definir com a equipa do ERP

            lines.Add(_localizer.Get("Ferias_Separator", lang));
            return string.Join("\n", lines);
        }
        */

        // Classe auxiliar para manter o estado pendente
        private sealed class PendingFeriasState
        {
            public string UserId { get; init; } = string.Empty;
            public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
            public IncomingMessage OriginalMessage { get; init; } = new();
            public FeriasState CurrentState { get; set; } = FeriasState.AwaitingYear;
            public int SelectedYear { get; set; }
            public string YearInput { get; set; } = string.Empty;
            public SupportedLanguage? DetectedLanguage { get; set; }
            public int Attempts { get; set; } = 0;
        }
    }
}
