using WebApplication1.Core.Models;

namespace WebApplication1.Core.Commands
{
    /// <summary>
    /// Comando AJUDA — lista todos os comandos disponíveis.
    /// 
    /// Ativado por: "ajuda", "help", "?", "menu" ou "commands"
    /// Responde com a lista formatada de todos os comandos registados.
    /// </summary>
    public class HelpCommandHandler : ICommandHandler
    {
        private readonly IServiceProvider _serviceProvider;

        /// <summary>
        /// Usa IServiceProvider para obter os handlers só quando necessário,
        /// evitando a dependência circular (HelpCommand → IEnumerable → HelpCommand).
        /// </summary>
        public HelpCommandHandler(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public string CommandName => "ajuda";

        public string Description => "Mostra a lista de comandos disponíveis";

        public string[] Triggers => new[] { "ajuda", "help"};

        public bool CanHandle(IncomingMessage message)
        {
            string raw = (message.OriginalBody ?? string.Empty).Trim();
            if (raw == "?")
                return true;

            string text = message.Body.Trim().ToLowerInvariant();
            return Triggers.Contains(text);
        }

        public Task<string> ExecuteAsync(IncomingMessage message)
        {
            // Resolve os handlers só agora (lazy) — evita dependência circular
            var allHandlers = _serviceProvider.GetServices<ICommandHandler>();

            var lines = new List<string>
            {
                "🤖 *Comandos disponíveis:*",
                ""
            };

            foreach (var handler in allHandlers
                         .Where(h => !h.CommandName.StartsWith("admin", StringComparison.OrdinalIgnoreCase))
                         .OrderBy(h => h.CommandName))
            {
                // Mostra as palavras-chave entre aspas
                string triggers = string.Join(", ", handler.Triggers.Select(t => $"\"{t}\""));
                lines.Add($"▸ *{handler.CommandName}* — {handler.Description}");
                lines.Add($"   _Escreve:_ {triggers}");
                lines.Add("");
            }

            lines.Add("───────────────────");
            lines.Add("📍 _Nota de presença: a marcação exige PIN de localização._");
            lines.Add("📱 _Se no Web/PC não conseguires enviar PIN, marca a presença no telemóvel._");
            lines.Add("");
            lines.Add("💡 _Escreve qualquer um dos comandos acima para começar._");

            return Task.FromResult(string.Join("\n", lines));
        }
    }
}
