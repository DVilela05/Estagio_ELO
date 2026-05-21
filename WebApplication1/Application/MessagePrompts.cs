using WebApplication1.Core.Localization;
using WebApplication1.Core.Models;

namespace WebApplication1.Application
{
    /// <summary>
    /// Centraliza os prompts do fluxo de mensagens (confirmação, sim/não, localização).
    /// Usa o IBotLocalizer para retornar as mensagens na língua correta.
    /// Mantém a rotação aleatória de variantes (nunca repete consecutivamente).
    /// </summary>
    public class MessagePrompts
    {
        private readonly IBotLocalizer _localizer;

        public MessagePrompts(IBotLocalizer localizer)
        {
            _localizer = localizer;
        }

        public bool IsYes(string text) => _localizer.IsYes(text);
        public bool IsNo(string text) => _localizer.IsNo(text);

        public string BuildConfirmationPrompt(string commandName, SupportedLanguage? lang)
            => _localizer.GetRandom("Confirmation_Prompt", lang, commandName);

        public string BuildYesNoHelp(int invalidAttempts, string commandName, SupportedLanguage? lang)
        {
            int remaining = Math.Max(0, 3 - invalidAttempts);
            string prompt = _localizer.GetRandom("Confirmation_YesNoHelp", lang, commandName);
            string remainingText = _localizer.Get("Confirmation_RemainingAttempts", lang, remaining);
            return $"{prompt}{remainingText}";
        }

        public string BuildNoPendingConfirmationMessage(SupportedLanguage? lang)
            => _localizer.GetRandom("Confirmation_NoPending", lang);

        public string BuildFinalInvalidConfirmationMessage(string commandName, SupportedLanguage? lang)
            => _localizer.GetRandom("Confirmation_InvalidFinal", lang, commandName);

        public string BuildLocationRequestPrompt(int windowSeconds, SupportedLanguage? lang)
            => FormatWindowPrompt(_localizer.GetRandom("Location_Request", lang), windowSeconds);

        public string BuildLocationHelp(int windowSeconds, SupportedLanguage? lang)
            => FormatWindowPrompt(_localizer.GetRandom("Location_Help", lang), windowSeconds);

        public string BuildFinalLocationMissingMessage(SupportedLanguage? lang)
            => _localizer.GetRandom("Location_Final", lang);

        private static string FormatWindowPrompt(string prompt, int windowSeconds)
        {
            if (!prompt.Contains("{0}", StringComparison.Ordinal))
                return prompt;

            return string.Format(prompt, windowSeconds);
        }
    }
}
