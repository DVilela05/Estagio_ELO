using Microsoft.Extensions.Options;
using WebApplication1.Core.Localization;
using WebApplication1.Core.Models;
using WebApplication1.Infrastructure.Configuration;
using WebApplication1.Infrastructure.ExternalApis;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using WebApplication1.Core.Interfaces;

namespace WebApplication1.Core.Commands
{
    /// <summary>
    /// Comando de PRESENÇAS — valida mensagens de marcação e
    /// envia o registo ao servidor de negócio via IBusinessApiClient.
    /// 
    /// Fluxo:
    ///   1. CanHandle → verifica se a mensagem está na lista aceite
    ///   2. ExecuteAsync → chama o servidor de negócio (com retry Polly)
    ///      → Sucesso: mensagem de confirmação ao utilizador
    ///      → Erro:    mensagem amigável de falha ao utilizador
    /// 
    /// Usa Polly para retry com backoff exponencial em falhas transitórias
    /// (timeout, rede indisponível). Erros de negócio (INVALID_TOKEN, etc.)
    /// NÃO são retentados.
    /// </summary>
    public class PresencaCommandHandler : ICommandHandler
    {
        private readonly WebServiceSettings _wsSettings;
        private readonly ILogger<PresencaCommandHandler> _logger;
        private readonly IBotLocalizer _localizer;

        // ─── Política de retry Polly ─────────────────────────────────
        // Retry 3 vezes com backoff exponencial: 1s → 2s → 4s
        // Aplica-se apenas a erros transitórios (rede/timeout)
        private static readonly AsyncRetryPolicy<string> _retryPolicy =
            Policy<string>
                .Handle<HttpRequestException>()
                .Or<TaskCanceledException>()
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: tentativa => TimeSpan.FromSeconds(Math.Pow(2, tentativa - 1)),
                    onRetry: (outcome, delay, tentativa, _) =>
                    {
                        // Log será feito no handler via _logger, aqui só registamos no contexto
                        Console.WriteLine(
                            $"[Polly] Retry {tentativa} após {delay.TotalSeconds}s — Razão: {outcome.Exception?.Message ?? "resposta inesperada"}");
                    });

        // ─── Mapa de triggers → língua (construído a partir dos recursos) ──
        private readonly Dictionary<string, SupportedLanguage> _triggerMap;
        private readonly ITokenService _tokenService;

        public PresencaCommandHandler(
            IOptions<WebServiceSettings> wsOptions,
            ILogger<PresencaCommandHandler> logger,
            IBotLocalizer localizer,
            ITokenService tokenService)
        {
            _wsSettings = wsOptions.Value;
            _logger = logger;
            _localizer = localizer;
            _tokenService = tokenService;
            _triggerMap = localizer.GetAllTriggers("Presence");
        }

        public string CommandName => "presença";

        public string GetDescription(SupportedLanguage? language) => _localizer.Get("Presence_Description", language);

        public string[] Triggers => _triggerMap.Keys.ToArray();

        public bool CanHandle(IncomingMessage message)
        {
            string text = message.Body.Trim();
            return _triggerMap.ContainsKey(text);
        }

        public async Task<string> ExecuteAsync(IncomingMessage message)
        {
            // Detetar a língua pelo trigger usado
            if (_triggerMap.TryGetValue(message.Body.Trim(), out var triggerLang))
            {
                message.Language ??= triggerLang;
            }

            var lang = message.Language;

            // TODO: Futuramente, voltar a ativar a validação de localização quando for exigido
            // if (!message.HasLocation || !message.Latitude.HasValue || !message.Longitude.HasValue)
            // {
            //     return "📍 Para registar presença é obrigatório enviar a localização da própria app primeiro.";
            // }

            try
            {
                // ─── Construir parâmetro para metaim1: TOKEN|NumTelefone|mensagem ───
                string token = "Diogo";
                
                // TODO: Para usar o token seguro e dinâmico, descomentar a linha abaixo e remover o token "Diogo"
                // string token = _tokenService.GenerateToken(message.Platform == MessagePlatform.Teams ? message.UserEmail : message.From);
                // Determina a API e os dados do utilizador com base na plataforma
                int apiType = message.Platform == MessagePlatform.Teams ? 2 : 1;
                string numTelefone = message.Platform == MessagePlatform.WhatsApp ? message.From : "";
                string email = message.Platform == MessagePlatform.Teams ? (message.UserEmail ?? "") : "";
                
                string mensagem = message.Body;

                // O parâmetro bruto reflete a nova estrutura
                string dataFormatada = message.ReceivedAt.ToString("dd-MM-yyyy HH:mm:ss");
                string parametroBruto = $"{token}|{apiType}|{numTelefone}|{mensagem}|{dataFormatada}|{email}";
                
                // Converte a string para Base64 antes de enviar para o webservice
                byte[] textBytes = System.Text.Encoding.UTF8.GetBytes(parametroBruto);
                string parametro = System.Convert.ToBase64String(textBytes);

                // ─── Criar string de log das mensagens processadas com sucesso ───
                // Como o comando de presença requer confirmação, sabemos que o fluxo foi:
                // 1. Comando original (exato)
                // 2. Confirmação (texto exato que o user respondeu, ex: "s", "SIM")
                string textoConfirmacao = message.ConfirmationText ?? "sim";
                string mensagensProcessadasLog = $"{message.OriginalBody}|{textoConfirmacao}";

                _logger.LogInformation(
                    "📱 A chamar metaim1 com parâmetro: {Param}. Histórico: {Log}", parametro, mensagensProcessadasLog);

                // ─── Chamada com Polly retry (backoff exponencial) ────────
                string rawResponse = await _retryPolicy.ExecuteAsync(async () =>
                {
                    _logger.LogDebug("📡 A executar chamada ao metaim1...");

                    // Fazemos um POST porque estamos a inserir dados (marcação).
                    // O parâmetro vai no Body da mensagem e entre aspas para ser aceite como JSON String.
                    string bodyParaEnviar = $"\"{parametro}\"";

                    // === PRINT PARA O TERMINAL ===
                    Console.WriteLine("\n=================================================");
                    Console.WriteLine($"[DEBUG] A ENVIAR PARA O WEBSERVICE NO BODY: {bodyParaEnviar}");
                    Console.WriteLine("=================================================\n");

                    string result = await WebServiceUtils.WCFRESTServiceCall(
                        "POST", "metaim1", bodyParaEnviar, _logger, _wsSettings.BaseUrl);

                    // Se a resposta indica erro transitório do lado do WCF
                    // (ex: EXCEPTION|...), lançar para que o Polly retente
                    if (result.StartsWith("EXCEPTION|"))
                    {
                        throw new HttpRequestException(
                            $"Erro transitório do serviço: {result}");
                    }

                    return result;
                });

                _logger.LogInformation("📱 metaim1 raw response: {Response}", rawResponse);

                // Descodificar Base64 (se aplicável)
                string response = WebServiceUtils.Base64Decode(rawResponse.Replace("\"", ""));
                _logger.LogInformation("📱 metaim1 decoded response: {Response}", response);

                // ─── Tratar respostas ────────────────────────────────────────
                string cleanResponse = response.Trim().ToUpperInvariant();

                // 1. Verificar Erros de Segurança do Token (HMAC)
                if (cleanResponse.Contains("INVALID_FORMAT") || 
                    cleanResponse.Contains("INVALID_TIMESTAMP") || 
                    cleanResponse.Contains("EXPIRED") || 
                    cleanResponse.Contains("FUTURE_TOKEN") || 
                    cleanResponse.Contains("INVALID_SIGNATURE") ||
                    cleanResponse.Contains("UNEXPECTED_ERROR"))
                {
                    _logger.LogWarning("❌ Erro de segurança no Token do WCF: {Erro}", response);
                    return _localizer.Get("Token_SecurityError", lang) ?? "Acesso negado: Falha na validação de segurança (Token inválido ou expirado).";
                }

                if (cleanResponse == "OK|0")
                {
                    return _localizer.Get("Presence_Success", lang);
                }
                else if (cleanResponse == "ERROR|UNKNOWNAPI")
                {
                    _logger.LogWarning("⚠️ API desconhecida enviada para o WCF ({Response})", response);
                    return _localizer.Get("Presence_ErrorUnknownApi", lang);
                }
                else if (cleanResponse == "ERROR|EMAIL_MANDATORY")
                {
                    _logger.LogWarning("⚠️ E-mail em falta na chamada do Teams ({Response})", response);
                    return _localizer.Get("Presence_ErrorEmailMandatory", lang);
                }
                else if (cleanResponse == "ERROR|NUMBER_MESSAGE_MANDATORY")
                {
                    _logger.LogWarning("⚠️ Número de telefone em falta na chamada do WhatsApp ({Response})", response);
                    return _localizer.Get("Presence_ErrorNumberMandatory", lang);
                }
                else if (cleanResponse == "ERROR|TOKEN_MANDATORY")
                {
                    _logger.LogWarning("⚠️ Token em falta na chamada WCF ({Response})", response);
                    return _localizer.Get("Presence_ErrorTokenMandatory", lang);
                }
                else if (cleanResponse == "ERROR|INVALID_DATETIME")
                {
                    _logger.LogWarning("⚠️ Data inválida enviada ao WCF ({Response})", response);
                    return _localizer.Get("Presence_ErrorInvalidDatetime", lang);
                }
                else if (cleanResponse.StartsWith("ERROR|LOGINERROR_"))
                {
                    _logger.LogWarning("⚠️ Erro de validação de colaborador no ELO ({Response})", response);

                    string idType = message.Platform == MessagePlatform.Teams
                        ? _localizer.Get("Presence_IdType_Email", lang)
                        : _localizer.Get("Presence_IdType_Phone", lang);
                    return _localizer.Get("Presence_ErrorLoginError", lang, idType);
                }
                else if (cleanResponse == "ERROR|DECODEERROR")
                {
                    _logger.LogWarning("⚠️ Erro a descodificar Base64 no WCF ({Response})", response);
                    return _localizer.Get("Presence_ErrorDecodeError", lang);
                }
                else if (cleanResponse.StartsWith("ERROR|"))
                {
                    _logger.LogWarning("⚠️ Erro de negócio inesperado do serviço metaim1: {Response}", response);
                    return _localizer.Get("Presence_ErrorBusinessGeneric", lang, response);
                }
                else
                {
                    _logger.LogWarning("⚠️ Resposta com formato totalmente inesperado do metaim1: {Response}", response);
                    return _localizer.Get("Presence_ErrorUnexpectedResponse", lang, response);
                }
            }
            catch (TaskCanceledException)
            {
                _logger.LogError("⏱️ Timeout ao chamar metaim1 (após 3 tentativas Polly)");
                return _localizer.Get("Presence_ErrorTimeout", lang);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "🔌 Serviço metaim1 indisponível (após 3 tentativas Polly)");
                return _localizer.Get("Presence_ErrorUnavailable", lang);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro inesperado ao chamar metaim1");
                return _localizer.Get("Presence_ErrorGeneric", lang);
            }
        }
    }
}
