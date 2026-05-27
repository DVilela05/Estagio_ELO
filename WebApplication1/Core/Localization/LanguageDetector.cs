using WebApplication1.Core.Models;

namespace WebApplication1.Core.Localization
{
    /// <summary>
    /// Detetor de língua — determina a língua do utilizador com base em múltiplas fontes.
    /// 
    /// Prioridade (do mais forte para o mais fraco):
    ///   1. Língua da mensagem (detetada pelo trigger que fez match)
    ///   2. Indicativo do telefone (WhatsApp) — dá a "língua de casa"
    ///   3. Desconhecido (null) — o bot responde nas 4 línguas
    /// 
    /// Analogia: é como num hotel. O passaporte (indicativo) diz de onde o hóspede é,
    /// mas se ele falar em inglês, respondes em inglês.
    /// </summary>
    public class LanguageDetector
    {
        private readonly IBotLocalizer _localizer;

        /// <summary>
        /// Mapa de indicativos telefónicos → língua.
        /// Cobre os principais países para PT, EN, FR e ES.
        /// </summary>
        private static readonly Dictionary<string, SupportedLanguage> _phoneCodeToLanguage = new()
        {
            // Português
            ["351"] = SupportedLanguage.Portuguese,  // Portugal
            ["55"] = SupportedLanguage.Portuguese,    // Brasil
            ["244"] = SupportedLanguage.Portuguese,   // Angola
            ["258"] = SupportedLanguage.Portuguese,   // Moçambique
            ["238"] = SupportedLanguage.Portuguese,   // Cabo Verde
            ["245"] = SupportedLanguage.Portuguese,   // Guiné-Bissau
            ["239"] = SupportedLanguage.Portuguese,   // São Tomé e Príncipe
            ["670"] = SupportedLanguage.Portuguese,   // Timor-Leste

            // Inglês
            ["44"] = SupportedLanguage.English,   // Reino Unido
            ["1"] = SupportedLanguage.English,    // EUA / Canadá
            ["61"] = SupportedLanguage.English,   // Austrália
            ["353"] = SupportedLanguage.English,  // Irlanda
            ["27"] = SupportedLanguage.English,   // África do Sul
            ["91"] = SupportedLanguage.English,   // Índia
            ["64"] = SupportedLanguage.English,   // Nova Zelândia

            // Francês
            ["33"] = SupportedLanguage.French,    // França
            ["32"] = SupportedLanguage.French,    // Bélgica
            ["41"] = SupportedLanguage.French,    // Suíça
            ["352"] = SupportedLanguage.French,   // Luxemburgo
            ["225"] = SupportedLanguage.French,   // Costa do Marfim
            ["221"] = SupportedLanguage.French,   // Senegal
            ["212"] = SupportedLanguage.French,   // Marrocos
            ["216"] = SupportedLanguage.French,   // Tunísia

            // Espanhol
            ["34"] = SupportedLanguage.Spanish,   // Espanha
            ["52"] = SupportedLanguage.Spanish,   // México
            ["54"] = SupportedLanguage.Spanish,   // Argentina
            ["57"] = SupportedLanguage.Spanish,   // Colômbia
            ["56"] = SupportedLanguage.Spanish,   // Chile
            ["51"] = SupportedLanguage.Spanish,   // Peru
            ["58"] = SupportedLanguage.Spanish,   // Venezuela
            ["593"] = SupportedLanguage.Spanish,  // Equador
        };

        public LanguageDetector(IBotLocalizer localizer)
        {
            _localizer = localizer;
        }

        /// <summary>
        /// Deteta a língua para uma mensagem.
        /// Tenta primeiro pelo conteúdo da mensagem (trigger), depois pelo número de telefone.
        /// </summary>
        public SupportedLanguage? DetectLanguage(IncomingMessage message)
        {
            // 1. Tentar detetar pelo conteúdo da mensagem (trigger)
            var langFromTrigger = DetectFromTrigger(message.Body);
            if (langFromTrigger.HasValue)
                return langFromTrigger.Value;

            // 2. Tentar pelo número de telefone (WhatsApp)
            if (message.Platform == MessagePlatform.WhatsApp)
            {
                var langFromPhone = DetectFromPhoneNumber(message.From);
                if (langFromPhone.HasValue)
                    return langFromPhone.Value;
            }

            // 3. Não conseguimos detetar → null
            return null;
        }

        /// <summary>
        /// Deteta a língua pelo conteúdo da mensagem comparando com todos os triggers conhecidos.
        /// </summary>
        public SupportedLanguage? DetectFromTrigger(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            string normalizedText = text.Trim().ToLowerInvariant();

            // Verificar triggers de cada comando
            string[] commandKeys = { "Presence", "Listagem", "Ferias", "Help" };

            foreach (var cmdKey in commandKeys)
            {
                var allTriggers = _localizer.GetAllTriggers(cmdKey);
                if (allTriggers.TryGetValue(normalizedText, out var lang))
                    return lang;
            }

            // Verificar tokens sim/não
            var yesLang = _localizer.DetectYesLanguage(normalizedText);
            if (yesLang.HasValue) return yesLang.Value;

            var noLang = _localizer.DetectNoLanguage(normalizedText);
            if (noLang.HasValue) return noLang.Value;

            return null;
        }

        /// <summary>
        /// Deteta a língua pelo indicativo do número de telefone.
        /// O número do WhatsApp vem no formato "351912345678" (sem +).
        /// </summary>
        public static SupportedLanguage? DetectFromPhoneNumber(string? phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
                return null;

            // Extrair apenas dígitos
            string digits = new string(phoneNumber.Where(char.IsDigit).ToArray());

            if (string.IsNullOrEmpty(digits))
                return null;

            // Tentar indicativos de 3 dígitos primeiro, depois 2, depois 1
            // (mais específico primeiro para evitar colisões)
            for (int length = 3; length >= 1; length--)
            {
                if (digits.Length >= length)
                {
                    string prefix = digits[..length];
                    if (_phoneCodeToLanguage.TryGetValue(prefix, out var lang))
                        return lang;
                }
            }

            return null;
        }
    }
}
