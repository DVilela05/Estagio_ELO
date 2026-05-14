using WebApplication1.Core.Interfaces;
using WebApplication1.Core.Models;
using WebApplication1.Infrastructure.ExternalApis;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

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
        private readonly IBusinessApiClient _businessApiClient;
        private readonly ILogger<PresencaCommandHandler> _logger;

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

        // ─── Mensagens aceites ───────────────────────────────────────
        private static readonly HashSet<string> _acceptedMessages = new(StringComparer.OrdinalIgnoreCase)
        {
            "presente", "presença", "presenca",
            "marcar presença", "marcar presenca",
            "marcação", "marcaçao", "marcacao",
            "marcação presença", "marcação presenca",
            "marcacao presença", "marcacao presenca",
            "cá estou", "ca estou",
            "estou cá", "estou ca",
            "cheguei",
            "present", "attendance",
            "mark attendance", "check in",
            "i'm here", "im here",
            "here", "arrived",

            // ── Descomenta quando necessário ──────────────────────
            // "falta", "ausente", "marcar falta", "não vou", "nao vou",
        };

        // ─── Mensagens de resposta ───────────────────────────────────
        private const string RespostaSucesso =
            "✅ Presença registada com sucesso!";

        private const string RespostaSucessoStub =
            "✅ Mensagem de *presença* recebida com sucesso.";

        private const string RespostaErroTimeout =
            "⏱️ O servidor demorou a responder. A tua presença não foi registada — tenta novamente em alguns minutos.";

        private const string RespostaErroIndisponivel =
            "🔌 O servidor de registo está temporariamente indisponível. Tenta novamente mais tarde.";

        private const string RespostaErroGenerico =
            "⚠️ Houve um problema ao registar a presença. A equipa técnica foi notificada. Tenta novamente mais tarde.";

        public PresencaCommandHandler(IBusinessApiClient businessApiClient, ILogger<PresencaCommandHandler> logger)
        {
            _businessApiClient = businessApiClient;
            _logger = logger;
        }

        public string CommandName => "presença";

        public string Description => "Marcar presença (requer PIN de localização; se o Web/PC não permitir, faz no telemóvel)";

        public string[] Triggers => _acceptedMessages.ToArray();

        public bool CanHandle(IncomingMessage message)
        {
            string text = message.Body.Trim();
            return _acceptedMessages.Contains(text);
        }

        public async Task<string> ExecuteAsync(IncomingMessage message)
        {
            // TODO: Futuramente, voltar a ativar a validação de localização quando for exigido
            // if (!message.HasLocation || !message.Latitude.HasValue || !message.Longitude.HasValue)
            // {
            //     return "📍 Para registar presença é obrigatório enviar a localização da própria app primeiro.";
            // }

            try
            {
                // ─── Construir parâmetro para metaim1: TOKEN|NumTelefone|mensagem ───
                string token = "Diogo";
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

                _logger.LogInformation(
                    "📱 A chamar metaim1 com parâmetro: {Param}", parametro);

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

                    string result = await MobileServiceUtils.WCFRESTServiceCall(
                        "POST", "metaim1", bodyParaEnviar, _logger);

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
                string response = MobileServiceUtils.Base64Decode(rawResponse.Replace("\"", ""));
                _logger.LogInformation("📱 metaim1 decoded response: {Response}", response);

                // ─── Tratar respostas ────────────────────────────────────────
                string cleanResponse = response.Trim().ToUpperInvariant();

                if (cleanResponse == "OK|0")
                {
                    return RespostaSucesso;
                }
                else if (cleanResponse == "ERROR|UNKNOWNAPI")
                {
                    _logger.LogWarning("⚠️ API desconhecida enviada para o WCF ({Response})", response);
                    return "⚠️ Ocorreu um erro interno (API desconhecida). A equipa técnica foi notificada.";
                }
                else if (cleanResponse == "ERROR|EMAIL_MANDATORY")
                {
                    _logger.LogWarning("⚠️ E-mail em falta na chamada do Teams ({Response})", response);
                    return "⚠️ Não foi possível identificar o teu e-mail corporativo. Contacta o suporte para garantir que a tua conta do Teams está bem configurada.";
                }
                else if (cleanResponse == "ERROR|NUMBER_MESSAGE_MANDATORY")
                {
                    _logger.LogWarning("⚠️ Número de telefone em falta na chamada do WhatsApp ({Response})", response);
                    return "⚠️ Não foi possível ler o teu número de telemóvel para associar à tua ficha de colaborador.";
                }
                else if (cleanResponse == "ERROR|TOKEN_MANDATORY")
                {
                    _logger.LogWarning("⚠️ Token em falta na chamada WCF ({Response})", response);
                    return "⚠️ Erro de segurança (Token em falta). Tenta novamente mais tarde.";
                }
                else if (cleanResponse == "ERROR|INVALID_DATETIME")
                {
                    _logger.LogWarning("⚠️ Data inválida enviada ao WCF ({Response})", response);
                    return "⚠️ A data da mensagem não foi compreendida pelo servidor. Tenta enviar a presença novamente.";
                }
                else if (cleanResponse.StartsWith("ERROR|LOGINERROR_"))
                {
                    _logger.LogWarning("⚠️ Erro de validação de colaborador no ELO ({Response})", response);

                    string idType = message.Platform == MessagePlatform.Teams ? "e-mail" : "nº de telefone";
                    return $"⚠️ Ocorreu um erro ao validar os teus dados. Confirma com os Recursos Humanos se o teu {idType} está corretamente associado à tua ficha. Escreve *help* para mais informações.";
                }
                else if (cleanResponse == "ERROR|DECODEERROR")
                {
                    _logger.LogWarning("⚠️ Erro a descodificar Base64 no WCF ({Response})", response);
                    return "⚠️ Ocorreu um erro na transmissão dos dados. Tenta novamente.";
                }
                else if (cleanResponse.StartsWith("ERROR|"))
                {
                    _logger.LogWarning("⚠️ Erro de negócio inesperado do serviço metaim1: {Response}", response);
                    return "⚠️ Não foi possível registar a presença. Erro do servidor: " + response;
                }
                else
                {
                    _logger.LogWarning("⚠️ Resposta com formato totalmente inesperado do metaim1: {Response}", response);
                    return "⚠️ Resposta inesperada do servidor: " + response;
                }
            }
            catch (TaskCanceledException)
            {
                _logger.LogError("⏱️ Timeout ao chamar metaim1 (após 3 tentativas Polly)");
                return RespostaErroTimeout;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "🔌 Serviço metaim1 indisponível (após 3 tentativas Polly)");
                return RespostaErroIndisponivel;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro inesperado ao chamar metaim1");
                return RespostaErroGenerico;
            }
        }
    }
}
