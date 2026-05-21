using WebApplication1.Core.Localization;
using WebApplication1.Core.Models;

namespace WebApplication1.Application
{
    /// <summary>
    /// Centraliza as mensagens de comando (unknown/fallback) para evitar duplicação
    /// com lógica de rotação sem repetir consecutivamente.
    /// Usa o IBotLocalizer para retornar as mensagens na língua correta.
    /// </summary>
    public class CommandPrompts
    {
        private readonly IBotLocalizer _localizer;

        public CommandPrompts(IBotLocalizer localizer)
        {
            _localizer = localizer;
        }

        public string GetNextUnknownMessage(SupportedLanguage? lang)
        {
            return _localizer.GetRandom("Unknown", lang);
        }

        /// <summary>
        /// Constrói a mensagem multi-língua para quando a língua não é detetada.
        /// Usada quando o comando é desconhecido E a língua é desconhecida.
        /// </summary>
        public string GetMultiLanguageFallback()
        {
            return _localizer.BuildMultiLanguageFallback("Unknown");
        }
    }
}
