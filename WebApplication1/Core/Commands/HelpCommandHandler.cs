using WebApplication1.Core.Localization;
using WebApplication1.Core.Models;

namespace WebApplication1.Core.Commands
{
    /// <summary>
    /// Comando AJUDA — lista todos os comandos disponíveis.
    /// 
    /// Ativado por: "ajuda", "help", "?", "menu", "aide", "ayuda", "commands", "commandes", "comandos"
    /// Responde com a lista formatada de todos os comandos registados na língua detetada.
    /// </summary>
    public class HelpCommandHandler : ICommandHandler
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IBotLocalizer _localizer;
        private readonly Dictionary<string, SupportedLanguage> _triggerMap;

        /// <summary>
        /// Usa IServiceProvider para obter os handlers só quando necessário,
        /// evitando a dependência circular (HelpCommand → IEnumerable → HelpCommand).
        /// </summary>
        public HelpCommandHandler(IServiceProvider serviceProvider, IBotLocalizer localizer)
        {
            _serviceProvider = serviceProvider;
            _localizer = localizer;
            _triggerMap = localizer.GetAllTriggers("Help");
        }

        public string CommandName => "ajuda";

        public string GetDescription(SupportedLanguage? language) => _localizer.Get("Help_Description", language);

        public string[] Triggers => _triggerMap.Keys.ToArray();

        public bool CanHandle(IncomingMessage message)
        {
            string raw = (message.OriginalBody ?? string.Empty).Trim();
            if (raw == "?")
                return true;

            string text = message.Body.Trim().ToLowerInvariant();
            return _triggerMap.ContainsKey(text);
        }

        public Task<string> ExecuteAsync(IncomingMessage message)
        {
            // Detetar a língua pelo trigger usado
            string text = message.Body.Trim().ToLowerInvariant();
            if (_triggerMap.TryGetValue(text, out var triggerLang))
            {
                message.Language ??= triggerLang;
            }

            var lang = message.Language;

            // Resolve os handlers só agora (lazy) — evita dependência circular
            var allHandlers = _serviceProvider.GetServices<ICommandHandler>();

            var lines = new List<string>
            {
                _localizer.Get("Help_Title", lang),
                ""
            };

            foreach (var handler in allHandlers
                         .Where(h => !h.CommandName.StartsWith("admin", StringComparison.OrdinalIgnoreCase))
                         .OrderBy(h => h.CommandName))
            {
                // Obter nome e descrição na língua correta
                string commandName = handler.CommandName;
                string description = handler.GetDescription(lang);

                // Para cada handler, mostrar os triggers da língua detetada (ou todos se a língua não for detetada)
                string triggers = string.Join(", ", handler.Triggers.Take(5).Select(t => $"\"{t}\""));

                lines.Add(_localizer.Get("Help_CommandEntry", lang, commandName, description));
                lines.Add(_localizer.Get("Help_TriggerLabel", lang, triggers));
                lines.Add("");
            }

            lines.Add(_localizer.Get("Help_FooterSeparator", lang));
            lines.Add(_localizer.Get("Help_NotePresence", lang));
            lines.Add(_localizer.Get("Help_NoteMobile", lang));
            lines.Add("");
            lines.Add(_localizer.Get("Help_Hint", lang));

            return Task.FromResult(string.Join("\n", lines));
        }
    }
}
