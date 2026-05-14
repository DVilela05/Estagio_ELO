using System.Collections.Generic;

namespace WebApplication1.Application
{
    /// <summary>
    /// Centraliza as mensagens de comando (unknown/fallback) para evitar duplicação
    /// com lógica de rotação sem repetir consecutivamente (igual a MessagePrompts).
    /// </summary>
    internal static class CommandPrompts
    {
        private static readonly object _promptLock = new();
        private static readonly Queue<int> _lastUnknownIndexes = new();

        private static readonly string[] _unknownMessages =
        {
            "🤔 Hmm, não percebi o que querias dizer.\n\nEscreve *ajuda* para ver o que posso fazer.",
            "❓ Essa mensagem não corresponde a nenhum comando.\n\nExperimenta escrever *ajuda*.",
            "🙈 Não reconheço essa mensagem.\n\nEnvia *menu* para ver as opções disponíveis.",
            "⚠️ Comando não encontrado.\n\nEscreve *?* para ver a lista de comandos.",
            "🤷 Ainda não sei fazer isso!\n\nEscreve *ajuda* para ver o que está disponível.",
            "📭 Mensagem não suportada.\n\nDigita *help* para ver os comandos que aceito."
        };

        public static string GetNextUnknownMessage()
        {
            lock (_promptLock)
            {
                var excluded = _lastUnknownIndexes.ToHashSet();
                var candidates = new List<int>();

                for (int i = 0; i < _unknownMessages.Length; i++)
                {
                    if (!excluded.Contains(i))
                        candidates.Add(i);
                }

                if (candidates.Count == 0)
                {
                    for (int i = 0; i < _unknownMessages.Length; i++)
                        candidates.Add(i);
                }

                int index = candidates[Random.Shared.Next(candidates.Count)];

                _lastUnknownIndexes.Enqueue(index);
                while (_lastUnknownIndexes.Count > 3)
                    _lastUnknownIndexes.Dequeue();

                return _unknownMessages[index];
            }
        }
    }
}
