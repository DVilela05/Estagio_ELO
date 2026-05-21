using WebApplication1.Core.Models;

namespace WebApplication1.Core.Localization
{
    /// <summary>
    /// Interface para o serviço de localização do bot.
    /// Fornece acesso às strings traduzidas por chave e língua.
    /// </summary>
    public interface IBotLocalizer
    {
        /// <summary>
        /// Obtém uma string traduzida pela chave e língua.
        /// Se a chave não existir na língua pedida, retorna o valor em Português (fallback).
        /// </summary>
        string Get(string key, SupportedLanguage? language);

        /// <summary>
        /// Obtém uma string traduzida com parâmetros formatados (string.Format).
        /// </summary>
        string Get(string key, SupportedLanguage? language, params object[] args);

        /// <summary>
        /// Obtém uma variante aleatória de uma mensagem (para rotação de prompts).
        /// Procura chaves no formato "BaseKey_1", "BaseKey_2", etc.
        /// Se não existirem variantes, retorna a chave base.
        /// </summary>
        string GetRandom(string baseKey, SupportedLanguage? language);

        /// <summary>
        /// Obtém uma variante aleatória formatada com parâmetros.
        /// </summary>
        string GetRandom(string baseKey, SupportedLanguage? language, params object[] args);

        /// <summary>
        /// Obtém todos os triggers para um comando numa determinada língua.
        /// A chave deve ser no formato "NomeComando_Triggers".
        /// Os valores são separados por "|" no ficheiro de recursos.
        /// </summary>
        string[] GetTriggers(string commandKey, SupportedLanguage language);

        /// <summary>
        /// Obtém todos os triggers para um comando em TODAS as línguas.
        /// Retorna um dicionário: trigger → língua.
        /// </summary>
        Dictionary<string, SupportedLanguage> GetAllTriggers(string commandKey);

        /// <summary>
        /// Constrói a mensagem multi-língua para quando a língua não é detetada.
        /// </summary>
        string BuildMultiLanguageFallback(string baseKey);

        /// <summary>
        /// Verifica se o texto é um token "sim" em qualquer língua.
        /// </summary>
        bool IsYes(string text);

        /// <summary>
        /// Verifica se o texto é um token "não" em qualquer língua.
        /// </summary>
        bool IsNo(string text);

        /// <summary>
        /// Verifica se o texto corresponde a um token "sim" e retorna a língua detetada.
        /// </summary>
        SupportedLanguage? DetectYesLanguage(string text);

        /// <summary>
        /// Verifica se o texto corresponde a um token "não" e retorna a língua detetada.
        /// </summary>
        SupportedLanguage? DetectNoLanguage(string text);
    }
}
