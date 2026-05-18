using Microsoft.Extensions.Options;
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

        // Estado pendente por utilizador para reter a transição do fluxo
        private static readonly ConcurrentDictionary<string, PendingPeriodState> _pendingPeriods = new(StringComparer.OrdinalIgnoreCase);

        // Tempo limite de expiração do estado pendente (5 minutos)
        private static readonly TimeSpan _pendingStateExpiry = TimeSpan.FromMinutes(5);

        // ─── Triggers aceites para iniciar o comando ───────────────────────
        private static readonly HashSet<string> _acceptedTriggers = new(StringComparer.OrdinalIgnoreCase)
        {
            "ver marcações", "ver marcacoes", 
            "marcações", "marcacoes", 
            "listagem", "listagem de marcações", "listagem de marcacoes",
            "listar marcações", "listar marcacoes", 
            "minhas marcações", "minhas marcacoes",
            "ver assiduidade", "assiduidade",
            "listagem assiduidade", "listagem de assiduidade"
        };

        private enum ListagemState
        {
            AwaitingPeriod,
            AwaitingConfirmation
        }

        public ListagemMarcacoesCommandHandler(
            IOptions<WebServiceSettings> wsOptions,
            ILogger<ListagemMarcacoesCommandHandler> logger)
        {
            _wsSettings = wsOptions.Value;
            _logger = logger;
        }

        public string CommandName => "listagem_marcações";

        public string Description => "Ver as tuas marcações de assiduidade num determinado período (máximo 7 dias)";

        public string[] Triggers => _acceptedTriggers.ToArray();

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
            return _acceptedTriggers.Contains(text);
        }

        public async Task<string> ExecuteAsync(IncomingMessage message)
        {
            string userId = NormalizeKey(message.From);

            // Verificar se o utilizador já tem um estado pendente ativo
            if (_pendingPeriods.TryGetValue(userId, out var state))
            {
                // Limpeza se o estado tiver expirado
                if (DateTime.UtcNow > state.CreatedAt.Add(_pendingStateExpiry))
                {
                    _pendingPeriods.TryRemove(userId, out _);
                    if (!_acceptedTriggers.Contains(message.Body.Trim()))
                    {
                        return "⚠️ O pedido anterior expirou. Por favor, escreva *ver marcações* para recomeçar.";
                    }
                }
                else
                {
                    // ─── ETAPA 1: Aguardar Período ────────────────────────────
                    if (state.CurrentState == ListagemState.AwaitingPeriod)
                    {
                        if (TryExtractPeriod(message.OriginalBody, out DateTime startDate, out DateTime endDate, out string validationError))
                        {
                            // Transição de estado: datas válidas encontradas
                            state.SelectedStartDate = startDate;
                            state.SelectedEndDate = endDate;
                            state.CurrentState = ListagemState.AwaitingConfirmation;

                            string periodDesc = startDate == endDate
                                ? $"para a data de *{startDate:dd/MM/yyyy}*"
                                : $"para o período de *{startDate:dd/MM/yyyy} a {endDate:dd/MM/yyyy}*";

                            return $"Confirmas que pretendes ver a listagem de marcações {periodDesc}? (sim/não)";
                        }
                        else
                        {
                            // A extração ou validação falhou. Solicita novamente com exemplos
                            return $"{validationError}\n\nPor favor, indica uma data específica ou um período de análise (máximo 7 dias).\n" +
                                   "Exemplos:\n" +
                                   "• *10/10/2025*\n" +
                                   "• *01/10/2025 a 07/10/2025*\n" +
                                   "• *hoje*\n" +
                                   "• *ontem*\n\n" +
                                   "💡 _Dica: Podes escrever uma frase natural, ex: 'quero ver o dia 10/10/2025' ou 'mostra de ontem a hoje'._";
                        }
                    }

                    // ─── ETAPA 2: Aguardar Confirmação (Sim / Não) ────────────
                    if (state.CurrentState == ListagemState.AwaitingConfirmation)
                    {
                        string body = message.Body.Trim().ToLowerInvariant();

                        if (body == "sim" || body == "s")
                        {
                            // Sucesso! Remover o estado pendente e processar a listagem
                            _pendingPeriods.TryRemove(userId, out _);
                            return await ProcessListagemAsync(state.SelectedStartDate, state.SelectedEndDate, message);
                        }
                        else
                        {
                            // Qualquer resposta diferente de SIM cancela o fluxo
                            _pendingPeriods.TryRemove(userId, out _);
                            return "❌ Pedido de listagem cancelado. Se precisares de ajuda, escreve *ajuda*.";
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
                CurrentState = ListagemState.AwaitingPeriod
            };
            _pendingPeriods[userId] = newState;

            return "Qual é o período de análise? Podes indicar uma data (ex: *10/10/2025*, *hoje*, *ontem*) ou um período de análise até 7 dias (ex: *01/10/2025 a 07/10/2025*).\n\n" +
                   "💡 _Dica: Podes escrever uma frase natural, ex: 'mostra-me as marcações de ontem a hoje' ou 'quero ver o dia 12/10/2025'._";
        }

        /// <summary>
        /// Extrai e valida as datas da mensagem, independentemente do texto em redor.
        /// Suporta auto-swap de limites e atalhos "hoje" e "ontem".
        /// </summary>
        private bool TryExtractPeriod(string input, out DateTime startDate, out DateTime endDate, out string validationError)
        {
            startDate = default;
            endDate = default;
            validationError = string.Empty;

            if (string.IsNullOrWhiteSpace(input))
            {
                validationError = "⚠️ O texto enviado está vazio.";
                return false;
            }

            // Normalizar espaços à volta de barras, traços e pontos
            string cleanedInput = Regex.Replace(input, @"\s*([\/\-\.])\s*", "$1");

            // Procurar por padrões de data e pelas palavras-chave "hoje" e "ontem"
            string dateRegexPattern = @"\b\d{1,2}[\/\-\.]\d{1,2}[\/\-\.]\d{2,4}\b";
            var matches = Regex.Matches(cleanedInput, $"{dateRegexPattern}|\\bhoje\\b|\\bontem\\b", RegexOptions.IgnoreCase);

            var extractedDates = new List<DateTime>();

            foreach (Match match in matches)
            {
                string val = match.Value.ToLowerInvariant();
                if (val == "hoje")
                {
                    extractedDates.Add(DateTime.Today);
                }
                else if (val == "ontem")
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
                validationError = "⚠️ Não consegui encontrar nenhuma data ou palavra-chave de período válida.";
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
                    validationError = $"⚠️ O período de análise ({totalDays} dias) excede o limite máximo de 7 dias.";
                    return false;
                }

                return true;
            }
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

        private async Task<string> ProcessListagemAsync(DateTime startDate, DateTime endDate, IncomingMessage message)
        {
            _logger.LogInformation(
                "📅 A executar consulta de marcações para {UserId}: {StartDate:dd/MM/yyyy} a {EndDate:dd/MM/yyyy}", 
                message.From, startDate, endDate);

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

            return BuildMockMarkingsList(startDate, endDate, message);
        }

        /// <summary>
        /// Constrói a listagem formatada de marcações mock para exibição.
        /// </summary>
        private string BuildMockMarkingsList(DateTime startDate, DateTime endDate, IncomingMessage message)
        {
            string userName = string.IsNullOrWhiteSpace(message.UserName) ? "Colaborador" : message.UserName;
            string periodStr = startDate == endDate 
                ? startDate.ToString("dd/MM/yyyy") 
                : $"{startDate:dd/MM/yyyy} a {endDate:dd/MM/yyyy}";

            var lines = new List<string>
            {
                "📅 *Listagem de Marcações de Assiduidade*",
                $"👤 *Colaborador:* {userName}",
                $"📆 *Período:* {periodStr}",
                "──────────────────────────",
                ""
            };

            for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
            {
                string dayName = GetDayOfWeekPt(date.DayOfWeek);
                if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
                {
                    lines.Add($"*• {date:dd/MM/yyyy} ({dayName}):* 🛌 _Fim de semana_");
                }
                else
                {
                    lines.Add($"*• {date:dd/MM/yyyy} ({dayName}):*");
                    lines.Add("   📥 08:30 - Entrada (Escritório ELO)");
                    lines.Add("   📤 12:30 - Saída (Almoço)");
                    lines.Add("   📥 13:30 - Entrada (Almoço)");
                    lines.Add("   📤 17:30 - Saída (Fim do Turno)");
                }
                lines.Add("");
            }

            lines.Add("──────────────────────────");
            lines.Add("💡 _Nota: Estes dados são de demonstração enquanto a integração com o Web Service não é ativada._");

            return string.Join("\n", lines);
        }

        private string GetDayOfWeekPt(DayOfWeek day)
        {
            return day switch
            {
                DayOfWeek.Monday => "Segunda-feira",
                DayOfWeek.Tuesday => "Terça-feira",
                DayOfWeek.Wednesday => "Quarta-feira",
                DayOfWeek.Thursday => "Quinta-feira",
                DayOfWeek.Friday => "Sexta-feira",
                DayOfWeek.Saturday => "Sábado",
                DayOfWeek.Sunday => "Domingo",
                _ => day.ToString()
            };
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
        }
    }
}
