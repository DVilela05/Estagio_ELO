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

namespace WebApplication1.Core.Commands
{
    /// <summary>
    /// Comando de LISTAGEM DE MARCAÇÕES — Interpreta o comando para ver as marcações
    /// de assiduidade, solicita o período de análise, extrai as datas de forma robusta,
    /// pede confirmação simples (sim/não) e apresenta os dados.
    /// </summary>
    public class ListagemMarcacoesCommandHandler : ICommandHandler
    {
        private readonly WebServiceSettings _wsSettings;
        private readonly ILogger<ListagemMarcacoesCommandHandler> _logger;
        private readonly IBotLocalizer _localizer;

        // Estado pendente por utilizador para reter a transição do fluxo
        private static readonly ConcurrentDictionary<string, PendingPeriodState> _pendingPeriods = new(StringComparer.OrdinalIgnoreCase);

        // Tempo limite de expiração do estado pendente (5 minutos)
        private static readonly TimeSpan _pendingStateExpiry = TimeSpan.FromMinutes(5);

        // ─── Mapa de triggers → língua (construído a partir dos recursos) ──
        private readonly Dictionary<string, SupportedLanguage> _triggerMap;

        private enum ListagemState
        {
            AwaitingPeriod,
            AwaitingConfirmation
        }

        public ListagemMarcacoesCommandHandler(
            IOptions<WebServiceSettings> wsOptions,
            ILogger<ListagemMarcacoesCommandHandler> logger,
            IBotLocalizer localizer)
        {
            _wsSettings = wsOptions.Value;
            _logger = logger;
            _localizer = localizer;
            _triggerMap = localizer.GetAllTriggers("Listagem");
        }

        public string CommandName => "listagem_marcações";

        public string Description => _localizer.Get("Listagem_Description", SupportedLanguage.Portuguese);

        public string[] Triggers => _triggerMap.Keys.ToArray();

        private string NormalizeKey(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

        public bool CanHandle(IncomingMessage message)
        {
            string userId = NormalizeKey(message.From);
            
            // Se o utilizador já tem um pedido pendente (a aguardar período ou confirmação), este handler intercepta
            if (_pendingPeriods.TryGetValue(userId, out var state))
            {
                // Limpeza silenciosa de estados expirados
                if (DateTime.UtcNow > state.CreatedAt.Add(_pendingStateExpiry))
                {
                    _pendingPeriods.TryRemove(userId, out _);
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
            if (_pendingPeriods.TryGetValue(userId, out var state))
            {
                // Usar a língua guardada no estado pendente
                lang = state.DetectedLanguage ?? lang;
                message.Language = lang;

                // Limpeza se o estado tiver expirado
                if (DateTime.UtcNow > state.CreatedAt.Add(_pendingStateExpiry))
                {
                    _pendingPeriods.TryRemove(userId, out _);
                    if (!_triggerMap.ContainsKey(message.Body.Trim()))
                    {
                        return _localizer.Get("Listagem_Expired", lang);
                    }
                }
                else
                {
                    // ─── ETAPA 1: Aguardar Período ────────────────────────────
                    if (state.CurrentState == ListagemState.AwaitingPeriod)
                    {
                        if (TryExtractPeriod(message.OriginalBody, lang, out DateTime startDate, out DateTime endDate, out string validationError))
                        {
                            // Transição de estado: datas válidas encontradas
                            state.SelectedStartDate = startDate;
                            state.SelectedEndDate = endDate;
                            state.PeriodInput = message.OriginalBody; // Guardar o texto exato (OriginalBody)
                            state.CurrentState = ListagemState.AwaitingConfirmation;

                            if (startDate == endDate)
                            {
                                return _localizer.Get("Listagem_ConfirmSingle", lang, startDate.ToString("dd/MM/yyyy"));
                            }
                            else
                            {
                                return _localizer.Get("Listagem_ConfirmRange", lang, startDate.ToString("dd/MM/yyyy"), endDate.ToString("dd/MM/yyyy"));
                            }
                        }
                        else
                        {
                            // A extração ou validação falhou. Solicita novamente com exemplos
                            return $"{validationError}{_localizer.Get("Listagem_DateRetryHelp", lang)}";
                        }
                    }

                    // ─── ETAPA 2: Aguardar Confirmação (Sim / Não) ────────────
                    if (state.CurrentState == ListagemState.AwaitingConfirmation)
                    {
                        string body = message.Body.Trim().ToLowerInvariant();

                        if (_localizer.IsYes(body))
                        {
                            // Sucesso! Remover o estado pendente e processar a listagem
                            _pendingPeriods.TryRemove(userId, out _);
                            return await ProcessListagemAsync(state, message, lang);
                        }
                        else
                        {
                            // Qualquer resposta diferente de SIM cancela o fluxo
                            _pendingPeriods.TryRemove(userId, out _);
                            return _localizer.Get("Listagem_Cancelled", lang);
                        }
                    }
                }
            }

            // Início do comando: Guardar o estado pendente na Etapa 1
            var newState = new PendingPeriodState
            {
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                OriginalMessage = message,
                CurrentState = ListagemState.AwaitingPeriod,
                DetectedLanguage = lang
            };
            _pendingPeriods[userId] = newState;

            return _localizer.Get("Listagem_AskPeriod", lang);
        }

        /// <summary>
        /// Extrai e valida as datas da mensagem, independentemente do texto em redor.
        /// Suporta auto-swap de limites e atalhos "hoje" e "ontem" em múltiplas línguas.
        /// </summary>
        private bool TryExtractPeriod(string input, SupportedLanguage? lang, out DateTime startDate, out DateTime endDate, out string validationError)
        {
            startDate = default;
            endDate = default;
            validationError = string.Empty;

            if (string.IsNullOrWhiteSpace(input))
            {
                validationError = _localizer.Get("Listagem_EmptyInput", lang);
                return false;
            }

            // Normalizar espaços à volta de barras, traços e pontos
            string cleanedInput = Regex.Replace(input, @"\s*([\/\-\.])\s*", "$1");

            // Recolher as palavras-chave de data em todas as línguas suportadas
            var todayKeywords = GetDateKeywordsAllLanguages("Listagem_DateToday");
            var yesterdayKeywords = GetDateKeywordsAllLanguages("Listagem_DateYesterday");

            // Procurar por padrões de data e pelas palavras-chave
            string dateRegexPattern = @"\b\d{1,2}[\/\-\.]\d{1,2}[\/\-\.]\d{2,4}\b";
            string keywordsPattern = string.Join("|", todayKeywords.Concat(yesterdayKeywords).Select(k => Regex.Escape(k)));
            string fullPattern = $"{dateRegexPattern}|\\b({keywordsPattern})\\b";
            
            var matches = Regex.Matches(cleanedInput, fullPattern, RegexOptions.IgnoreCase);

            var extractedDates = new List<DateTime>();

            foreach (Match match in matches)
            {
                string val = match.Value.ToLowerInvariant();
                if (todayKeywords.Contains(val))
                {
                    extractedDates.Add(DateTime.Today);
                }
                else if (yesterdayKeywords.Contains(val))
                {
                    extractedDates.Add(DateTime.Today.AddDays(-1));
                }
                else
                {
                    if (TryParseSingleDate(val, out DateTime parsedDate))
                    {
                        extractedDates.Add(parsedDate);
                    }
                }
            }

            // Remover datas duplicadas mantendo a ordem original
            extractedDates = extractedDates.Distinct().ToList();

            if (extractedDates.Count == 0)
            {
                validationError = _localizer.Get("Listagem_NoDateFound", lang);
                return false;
            }

            // Se for apenas uma data (ex: "hoje", "ontem" ou "10/10/2025")
            if (extractedDates.Count == 1)
            {
                startDate = extractedDates[0];
                endDate = extractedDates[0];
                return true;
            }
            else
            {
                // Se houver duas ou mais datas, pegamos nas duas primeiras encontradas
                DateTime d1 = extractedDates[0];
                DateTime d2 = extractedDates[1];

                // Auto-swap: organizar automaticamente a mais antiga como início e a mais recente como fim
                if (d1 <= d2)
                {
                    startDate = d1;
                    endDate = d2;
                }
                else
                {
                    startDate = d2;
                    endDate = d1;
                }

                // Validar que o período não excede 7 dias
                int totalDays = (endDate - startDate).Days + 1;
                if (totalDays > 7)
                {
                    validationError = _localizer.Get("Listagem_PeriodTooLong", lang, totalDays);
                    return false;
                }

                return true;
            }
        }

        /// <summary>
        /// Recolhe as palavras-chave de data (hoje/today/aujourd'hui/hoy) de todas as línguas.
        /// </summary>
        private HashSet<string> GetDateKeywordsAllLanguages(string key)
        {
            var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (SupportedLanguage lang in Enum.GetValues<SupportedLanguage>())
            {
                string keyword = _localizer.Get(key, lang);
                if (!string.IsNullOrWhiteSpace(keyword))
                    keywords.Add(keyword.ToLowerInvariant());
            }
            return keywords;
        }

        private bool TryParseSingleDate(string part, out DateTime parsedDate)
        {
            string[] formats = {
                "dd/MM/yyyy", "dd-MM-yyyy", "dd.MM.yyyy",
                "dd/MM/yy", "dd-MM-yy", "dd.MM.yy",
                "yyyy-MM-dd", "yyyy/MM/dd"
            };

            return DateTime.TryParseExact(
                part, 
                formats, 
                System.Globalization.CultureInfo.InvariantCulture, 
                System.Globalization.DateTimeStyles.None, 
                out parsedDate);
        }

        private async Task<string> ProcessListagemAsync(PendingPeriodState state, IncomingMessage confirmationMessage, SupportedLanguage? lang)
        {
            DateTime startDate = state.SelectedStartDate;
            DateTime endDate = state.SelectedEndDate;

            _logger.LogInformation(
                "📅 A executar consulta de marcações para {UserId}: {StartDate:dd/MM/yyyy} a {EndDate:dd/MM/yyyy}", 
                state.OriginalMessage.From, startDate, endDate);

            // ─── Criar string de log das mensagens processadas com sucesso ───
            // Usa sempre o texto exato (OriginalBody) em vez do texto limpo (Body)
            string mensagensProcessadasLog = $"{state.OriginalMessage.OriginalBody}|{state.PeriodInput}|{confirmationMessage.OriginalBody}";

            _logger.LogInformation("Histórico processado com sucesso: {Log}", mensagensProcessadasLog);

            // ─── CHAMADA DO WEB SERVICE WCF (A IMPLEMENTAR / DESCOBRIR) ───
            /*
            try
            {
                string token = "Diogo";
                int apiType = message.Platform == MessagePlatform.Teams ? 2 : 1;
                string numTelefone = message.Platform == MessagePlatform.WhatsApp ? message.From : "";
                string email = message.Platform == MessagePlatform.Teams ? (message.UserEmail ?? "") : "";

                string commandType = "listagem";
                string dataFormatada = message.ReceivedAt.ToString("dd-MM-yyyy HH:mm:ss");

                string dateStartStr = startDate.ToString("dd-MM-yyyy");
                string dateEndStr = startDate == endDate ? "" : endDate.ToString("dd-MM-yyyy");

                string parametroBruto = $"{token}|{apiType}|{numTelefone}|{commandType}|{dataFormatada}|{email}|{dateStartStr}|{dateEndStr}";
                
                byte[] textBytes = System.Text.Encoding.UTF8.GetBytes(parametroBruto);
                string parametro = System.Convert.ToBase64String(textBytes);

                _logger.LogInformation("📡 A chamar o Web Service de listagem de assiduidade com o parâmetro: {Param}", parametro);

                string bodyParaEnviar = $"\"{parametro}\"";

                // NOTA: Substituir "MÉTODO_A_DESCOBRIR" pelo nome real do método de listagem quando conhecido
                string rawResponse = await WebServiceUtils.WCFRESTServiceCall(
                    "POST", "MÉTODO_A_DESCOBRIR", bodyParaEnviar, _logger, _wsSettings.BaseUrl);

                if (rawResponse.StartsWith("EXCEPTION|"))
                {
                    throw new HttpRequestException($"Erro transitório do serviço: {rawResponse}");
                }

                string responseDecoded = WebServiceUtils.Base64Decode(rawResponse.Replace("\"", ""));
                _logger.LogInformation("📡 Resposta decodificada do Web Service: {Response}", responseDecoded);

                // Lógica para mapear a resposta descodificada para o texto de retorno...
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao tentar chamar o Web Service de listagem");
            }
            */

            return BuildMockMarkingsList(startDate, endDate, state.OriginalMessage, lang);
        }

        /// <summary>
        /// Constrói a listagem formatada de marcações mock para exibição.
        /// </summary>
        private string BuildMockMarkingsList(DateTime startDate, DateTime endDate, IncomingMessage message, SupportedLanguage? lang)
        {
            string userName = string.IsNullOrWhiteSpace(message.UserName) 
                ? _localizer.Get("Listagem_DefaultUser", lang) 
                : message.UserName;
            string periodStr = startDate == endDate 
                ? startDate.ToString("dd/MM/yyyy") 
                : $"{startDate:dd/MM/yyyy} a {endDate:dd/MM/yyyy}";

            var lines = new List<string>
            {
                _localizer.Get("Listagem_MockTitle", lang),
                _localizer.Get("Listagem_MockUser", lang, userName),
                _localizer.Get("Listagem_MockPeriod", lang, periodStr),
                _localizer.Get("Listagem_MockSeparator", lang),
                ""
            };

            for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
            {
                string dayName = GetDayOfWeek(date.DayOfWeek, lang);
                string dateStr = date.ToString("dd/MM/yyyy");

                if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
                {
                    lines.Add(_localizer.Get("Listagem_MockWeekend", lang, dateStr, dayName));
                }
                else
                {
                    lines.Add(_localizer.Get("Listagem_MockDayHeader", lang, dateStr, dayName));
                    lines.Add(_localizer.Get("Listagem_MockEntry", lang));
                    lines.Add(_localizer.Get("Listagem_MockLunchOut", lang));
                    lines.Add(_localizer.Get("Listagem_MockLunchIn", lang));
                    lines.Add(_localizer.Get("Listagem_MockExit", lang));
                }
                lines.Add("");
            }

            lines.Add(_localizer.Get("Listagem_MockSeparator", lang));
            lines.Add(_localizer.Get("Listagem_MockNote", lang));

            return string.Join("\n", lines);
        }

        private string GetDayOfWeek(DayOfWeek day, SupportedLanguage? lang)
        {
            string key = day switch
            {
                DayOfWeek.Monday => "Day_Monday",
                DayOfWeek.Tuesday => "Day_Tuesday",
                DayOfWeek.Wednesday => "Day_Wednesday",
                DayOfWeek.Thursday => "Day_Thursday",
                DayOfWeek.Friday => "Day_Friday",
                DayOfWeek.Saturday => "Day_Saturday",
                DayOfWeek.Sunday => "Day_Sunday",
                _ => "Day_Monday"
            };
            return _localizer.Get(key, lang);
        }

        // Classe auxiliar para manter o estado pendente
        private sealed class PendingPeriodState
        {
            public string UserId { get; init; } = string.Empty;
            public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
            public IncomingMessage OriginalMessage { get; init; } = new();
            public ListagemState CurrentState { get; set; } = ListagemState.AwaitingPeriod;
            public DateTime SelectedStartDate { get; set; }
            public DateTime SelectedEndDate { get; set; }
            public string PeriodInput { get; set; } = string.Empty;
            public SupportedLanguage? DetectedLanguage { get; set; }
        }
    }
}
