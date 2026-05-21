using System.Collections.ObjectModel;
using WebApplication1.Core.Models;
using WebApplication1.Resources;

namespace WebApplication1.Core.Localization
{
    /// <summary>
    /// Serviço de localização do bot.
    /// Fornece acesso às strings traduzidas organizadas por chave e língua.
    /// 
    /// Funcionalidades:
    ///   - Lookup por chave + língua (com fallback para PT)
    ///   - Suporte a string.Format com placeholders {0}, {1}
    ///   - Rotação aleatória de variantes (chaves _1, _2, _3...)
    ///   - Lookup de triggers por comando e língua
    ///   - Mensagem multi-língua para quando a língua não é detetada
    /// </summary>
    public class BotLocalizer : IBotLocalizer
    {
        private static readonly object _randomLock = new();
        private static readonly Queue<int> _lastRandomIndexes = new();

        /// <summary>
        /// Mapa de língua → dicionário de strings.
        /// A ordem no dicionário define a prioridade de fallback.
        /// </summary>
        private static readonly ReadOnlyDictionary<SupportedLanguage, ReadOnlyDictionary<string, string>> _allStrings =
            new(new Dictionary<SupportedLanguage, ReadOnlyDictionary<string, string>>
            {
                [SupportedLanguage.Portuguese] = Messages_PT.Strings,
                [SupportedLanguage.English] = Messages_EN.Strings,
                [SupportedLanguage.French] = Messages_FR.Strings,
                [SupportedLanguage.Spanish] = Messages_ES.Strings,
            });

        /// <summary>
        /// Cache de triggers: commandKey → (trigger → língua).
        /// Construído uma vez no arranque.
        /// </summary>
        private static readonly Dictionary<string, Dictionary<string, SupportedLanguage>> _triggerCache = BuildTriggerCache();

        // =====================================================================
        // Interface pública
        // =====================================================================

        public string Get(string key, SupportedLanguage? language)
        {
            var lang = language ?? SupportedLanguage.Portuguese;

            // Tentar na língua pedida
            if (_allStrings.TryGetValue(lang, out var strings) &&
                strings.TryGetValue(key, out var value))
            {
                return value;
            }

            // Fallback para PT
            if (_allStrings[SupportedLanguage.Portuguese].TryGetValue(key, out var fallback))
            {
                return fallback;
            }

            // Chave não existe — retornar a própria chave (para debugging)
            return $"[{key}]";
        }

        public string Get(string key, SupportedLanguage? language, params object[] args)
        {
            string template = Get(key, language);
            try
            {
                return string.Format(template, args);
            }
            catch (FormatException)
            {
                return template;
            }
        }

        public string GetRandom(string baseKey, SupportedLanguage? language)
        {
            var lang = language ?? SupportedLanguage.Portuguese;

            // Recolher todas as variantes: baseKey_1, baseKey_2, ..., baseKey_N
            var variants = new List<string>();
            if (_allStrings.TryGetValue(lang, out var strings))
            {
                for (int i = 1; i <= 20; i++)
                {
                    if (strings.TryGetValue($"{baseKey}_{i}", out var variant))
                        variants.Add(variant);
                    else
                        break;
                }
            }

            // Se não houver variantes na língua pedida, tentar em PT
            if (variants.Count == 0 && lang != SupportedLanguage.Portuguese)
            {
                var ptStrings = _allStrings[SupportedLanguage.Portuguese];
                for (int i = 1; i <= 20; i++)
                {
                    if (ptStrings.TryGetValue($"{baseKey}_{i}", out var variant))
                        variants.Add(variant);
                    else
                        break;
                }
            }

            // Se não houver variantes numeradas, tentar a chave base
            if (variants.Count == 0)
            {
                return Get(baseKey, language);
            }

            // Selecionar uma variante aleatória sem repetir as últimas 3
            return PickRandom(variants);
        }

        public string GetRandom(string baseKey, SupportedLanguage? language, params object[] args)
        {
            string template = GetRandom(baseKey, language);
            try
            {
                return string.Format(template, args);
            }
            catch (FormatException)
            {
                return template;
            }
        }

        public string[] GetTriggers(string commandKey, SupportedLanguage language)
        {
            string key = $"{commandKey}_Triggers";

            if (_allStrings.TryGetValue(language, out var strings) &&
                strings.TryGetValue(key, out var triggersStr))
            {
                return triggersStr
                    .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToArray();
            }

            return Array.Empty<string>();
        }

        public Dictionary<string, SupportedLanguage> GetAllTriggers(string commandKey)
        {
            string cacheKey = $"{commandKey}_Triggers";

            if (_triggerCache.TryGetValue(cacheKey, out var map))
                return new Dictionary<string, SupportedLanguage>(map, StringComparer.OrdinalIgnoreCase);

            return new Dictionary<string, SupportedLanguage>(StringComparer.OrdinalIgnoreCase);
        }

        public string BuildMultiLanguageFallback(string baseKey)
        {
            // Constrói uma mensagem curta com as 4 línguas
            var lines = new List<string>();
            var flags = new Dictionary<SupportedLanguage, string>
            {
                [SupportedLanguage.Portuguese] = "🇵🇹",
                [SupportedLanguage.English] = "🇬🇧",
                [SupportedLanguage.French] = "🇫🇷",
                [SupportedLanguage.Spanish] = "🇪🇸",
            };

            foreach (var (lang, flag) in flags)
            {
                string hint = Get("MultiLang_Hint", lang);
                lines.Add($"{flag} {hint}");
            }

            return string.Join("\n", lines);
        }

        // =====================================================================
        // Métodos auxiliares de acesso (usados pelo LanguageDetector e handlers)
        // =====================================================================

        /// <summary>
        /// Verifica se o texto corresponde a um token "sim" em qualquer língua.
        /// Retorna a língua detetada ou null.
        /// </summary>
        public SupportedLanguage? DetectYesLanguage(string text)
        {
            return DetectTokenLanguage(text, "YesTokens");
        }

        /// <summary>
        /// Verifica se o texto corresponde a um token "não" em qualquer língua.
        /// Retorna a língua detetada ou null.
        /// </summary>
        public SupportedLanguage? DetectNoLanguage(string text)
        {
            return DetectTokenLanguage(text, "NoTokens");
        }

        /// <summary>
        /// Verifica se o texto é um token "sim" em qualquer língua.
        /// </summary>
        public bool IsYes(string text)
        {
            return DetectYesLanguage(text) != null;
        }

        /// <summary>
        /// Verifica se o texto é um token "não" em qualquer língua.
        /// </summary>
        public bool IsNo(string text)
        {
            return DetectNoLanguage(text) != null;
        }

        // =====================================================================
        // Privados
        // =====================================================================

        private SupportedLanguage? DetectTokenLanguage(string text, string tokenKey)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            string normalizedText = text.Trim().ToLowerInvariant();

            foreach (var (lang, strings) in _allStrings)
            {
                if (!strings.TryGetValue(tokenKey, out var tokensStr))
                    continue;

                var tokens = tokensStr.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var token in tokens)
                {
                    if (normalizedText.Equals(token, StringComparison.OrdinalIgnoreCase))
                        return lang;
                }
            }

            return null;
        }

        private static Dictionary<string, Dictionary<string, SupportedLanguage>> BuildTriggerCache()
        {
            var cache = new Dictionary<string, Dictionary<string, SupportedLanguage>>(StringComparer.OrdinalIgnoreCase);

            // Para cada língua, extrair todas as chaves que terminam em "_Triggers"
            foreach (var (lang, strings) in _allStrings)
            {
                foreach (var (key, value) in strings)
                {
                    if (!key.EndsWith("_Triggers", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!cache.ContainsKey(key))
                        cache[key] = new Dictionary<string, SupportedLanguage>(StringComparer.OrdinalIgnoreCase);

                    var triggers = value.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    foreach (var trigger in triggers)
                    {
                        // Se o trigger já existe de outra língua, a primeira língua fica
                        // (ex: "presente" existe em PT e ES — fica com PT)
                        cache[key].TryAdd(trigger, lang);
                    }
                }
            }

            return cache;
        }

        private static string PickRandom(List<string> options)
        {
            if (options.Count == 0)
                return string.Empty;

            if (options.Count == 1)
                return options[0];

            lock (_randomLock)
            {
                var excluded = _lastRandomIndexes.ToHashSet();
                var candidates = new List<int>();

                for (int i = 0; i < options.Count; i++)
                {
                    if (!excluded.Contains(i))
                        candidates.Add(i);
                }

                if (candidates.Count == 0)
                {
                    for (int i = 0; i < options.Count; i++)
                        candidates.Add(i);
                }

                int index = candidates[Random.Shared.Next(candidates.Count)];

                _lastRandomIndexes.Enqueue(index);
                while (_lastRandomIndexes.Count > 3)
                    _lastRandomIndexes.Dequeue();

                return options[index];
            }
        }
    }
}
