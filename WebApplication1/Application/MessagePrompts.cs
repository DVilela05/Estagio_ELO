using System.Collections.Generic;

namespace WebApplication1.Application
{
    internal static class MessagePrompts
    {
        private static readonly object _promptLock = new();
        private static readonly Queue<int> _lastConfirmIndexes = new();
        private static readonly Queue<int> _lastHelpIndexes = new();
        private static readonly Queue<int> _lastNoPendingIndexes = new();
        private static readonly Queue<int> _lastInvalidFinalIndexes = new();
        private static readonly Queue<int> _lastLocationRequestIndexes = new();
        private static readonly Queue<int> _lastLocationHelpIndexes = new();
        private static readonly Queue<int> _lastLocationFinalIndexes = new();

        private static readonly string[] _confirmationPrompts =
        {
            "Recebi o pedido para realizar uma marcação de assiduidade. Posso avançar? (sim/não)"
        };

        private static readonly string[] _yesNoHelpPrompts =
        {
            "❓ Perguntei sobre *{0}* — responde com SIM ou NÃO (s/n, yes/no).",
            "📋 Ainda estou à espera: queres mesmo *{0}*? SIM ou NÃO (s/n, yes/no).",
            "🗣️ É sobre *{0}* — responde apenas SIM ou NÃO (s/n, yes/no).",
            "💬 Queres *{0}* ou não? Responde SIM ou NÃO (s/n, yes/no).",
            "✋ Para avançar com *{0}*, confirma com SIM ou NÃO (s/n, yes/no)."
        };

        private static readonly string[] _noPendingConfirmationPrompts =
        {
            "⚠️ Não tenho nenhum pedido pendente para confirmar.\n\n" +
            "Se enviaste um comando antes, pode ter expirado ou o sistema foi reiniciado.\n\n" +
            "Escreve *ajuda* para ver os comandos disponíveis.",

            "⚠️ Hmm, não encontro nenhuma confirmação pendente.\n\n" +
            "Talvez o pedido tenha expirado ou perdeu-se durante um reinício.\n\n" +
            "Tenta enviar o comando novamente ou escreve *ajuda*.",

            "⚠️ Não tenho nada a aguardar confirmação neste momento.\n\n" +
            "Se enviaste algo antes, pode ter sido há muito tempo ou o sistema foi reiniciado.\n\n" +
            "Para recomeçar, escreve *ajuda* e vê os comandos disponíveis.",

            "⚠️ Confirmar o quê? Não tenho nenhum pedido à espera.\n\n" +
            "Se tinhas enviado algo, pode ter expirado entretanto.\n\n" +
            "Escreve *ajuda* para veres o que podes fazer.",

            "⚠️ Não há nada pendente de confirmação.\n\n" +
            "Pode ter expirado ou perdeu-se quando o sistema foi reiniciado.\n\n" +
            "Usa *ajuda* para veres todos os comandos e recomeçar.",

            "⚠️ Ups, não tenho registo de nenhum pedido teu à espera.\n\n" +
            "Se enviaste antes, pode ter sido há muito tempo ou houve um reinício.\n\n" +
            "Escreve *ajuda* para saberes o que fazer."
        };

        private static readonly string[] _invalidFinalConfirmationPrompts =
        {
            "⚠️ A resposta esperada era *sim* ou *não* para *{0}*.\n\n" +
            "Vou cancelar este pedido por agora. Se precisares, escreve *ajuda*.",

            "⚠️ Não era a resposta esperada para *{0}* — precisava de *sim* ou *não*.\n\n" +
            "Pedido cancelado. Podes escrever *ajuda* para ver as opções.",

            "⚠️ Para *{0}* eu só esperava *sim* ou *não*.\n\n" +
            "Como já houve 3 tentativas inválidas, cancelei o pedido. Escreve *ajuda* para continuar.",

            "⚠️ Não consegui confirmar *{0}* porque a resposta não foi *sim*/*não*.\n\n" +
            "Cancelei esta confirmação. Se quiseres, escreve *ajuda*.",

            "⚠️ A confirmação de *{0}* foi cancelada: a resposta não era *sim* nem *não*.\n\n" +
            "Usa *ajuda* para retomar."
        };

        private static readonly string[] _locationRequestPrompts =
        {
            "📍 Perfeito — para concluir a *presença*, envia agora o PIN de localização da própria app.\nTens *{0} segundos* para enviar a localização atual.",
            "📍 Falta só a localização para fechar a *presença*. Envia o PIN da app.\nA localização tem de ser a *atual* e tens *{0} segundos*.",
            "📍 Confirmado. Para terminar a *presença*, partilha a tua localização atual pela app.\nJanela: *{0} segundos*.",
            "📍 Estamos quase: envia o PIN de localização da tua app para concluir a *presença*.\nTempo máximo: *{0} segundos*.",
            "📍 Último passo para registar a *presença*: envia o PIN da tua localização atual.\nApenas a localização *agora* é aceite.",
        };

        private static readonly string[] _locationHelpPrompts =
        {
            "📍 Ainda preciso do PIN de localização atual para concluir a *presença*.\nEnvia a localização da própria app dentro de *{0} segundos*.",
            "🗺️ Para terminar a *presença*, envia a tua localização atual pela app.\nA janela é curta: *{0} segundos*.",
            "📌 Para validar a *presença* preciso do PIN da localização atual.\nNão serve localização antiga ou alterada.",
            "📍 Falta a localização atual para validar a *presença*.\nTens de enviar o pin da própria app rapidamente.",
            "🧭 Para fechar a *presença* preciso do PIN da tua localização *agora*.\nSe a janela expirar, terás de recomeçar o pedido."
        };

        private static readonly string[] _locationFinalPrompts =
        {
            "⚠️ Não recebi o PIN de localização a tempo. Cancelei este pedido de *presença*.\nPara tentar de novo: *presente* e depois envia o PIN via *📎 Localização*.",
            "⚠️ Sem PIN de localização dentro do tempo limite não consigo concluir a *presença*. Pedido cancelado.",
            "⚠️ A *presença* foi cancelada porque a localização não chegou a tempo.\nQuando quiseres, recomeça com *presente*.",
            "⚠️ A janela de envio do PIN expirou. Cancelei a *presença*.\nNo próximo pedido, envia a localização dentro do tempo limite.",
            "⚠️ Pedido de *presença* cancelado: faltou o PIN de localização dentro da janela permitida."
        };

        public static bool IsYes(string text) => YesTokens.Contains(text);
        public static bool IsNo(string text) => NoTokens.Contains(text);

        public static string BuildConfirmationPrompt(string commandName)
            => string.Format(GetNextPrompt(_confirmationPrompts, _lastConfirmIndexes), commandName);

        public static string BuildYesNoHelp(int invalidAttempts, string commandName)
        {
            int remaining = Math.Max(0, 3 - invalidAttempts);
            string prompt = string.Format(GetNextPrompt(_yesNoHelpPrompts, _lastHelpIndexes), commandName);
            return $"{prompt}\n\n⏳ Tentativas restantes: *{remaining}*";
        }

        public static string BuildNoPendingConfirmationMessage()
            => GetNextPrompt(_noPendingConfirmationPrompts, _lastNoPendingIndexes);

        public static string BuildFinalInvalidConfirmationMessage(string commandName)
            => string.Format(GetNextPrompt(_invalidFinalConfirmationPrompts, _lastInvalidFinalIndexes), commandName);

        public static string BuildLocationRequestPrompt(int windowSeconds)
            => FormatWindowPrompt(GetNextPrompt(_locationRequestPrompts, _lastLocationRequestIndexes), windowSeconds);

        public static string BuildLocationHelp(int windowSeconds)
        {
            string prompt = FormatWindowPrompt(GetNextPrompt(_locationHelpPrompts, _lastLocationHelpIndexes), windowSeconds);
            return prompt;
        }

        public static string BuildFinalLocationMissingMessage()
            => GetNextPrompt(_locationFinalPrompts, _lastLocationFinalIndexes);

        private static string GetNextPrompt(string[] prompts, Queue<int> lastIndexes)
        {
            lock (_promptLock)
            {
                var excluded = lastIndexes.ToHashSet();
                var candidates = new List<int>();

                for (int i = 0; i < prompts.Length; i++)
                {
                    if (!excluded.Contains(i))
                        candidates.Add(i);
                }

                if (candidates.Count == 0)
                {
                    for (int i = 0; i < prompts.Length; i++)
                        candidates.Add(i);
                }

                int index = candidates[Random.Shared.Next(candidates.Count)];

                lastIndexes.Enqueue(index);
                while (lastIndexes.Count > 3)
                    lastIndexes.Dequeue();

                return prompts[index];
            }
        }

        private static string FormatWindowPrompt(string prompt, int windowSeconds)
        {
            if (!prompt.Contains("{0}", StringComparison.Ordinal))
                return prompt;

            return string.Format(prompt, windowSeconds);
        }

        private static readonly HashSet<string> YesTokens = new(StringComparer.OrdinalIgnoreCase)
        {
            "sim", "s", "yes", "y"
        };

        private static readonly HashSet<string> NoTokens = new(StringComparer.OrdinalIgnoreCase)
        {
            "nao", "não", "n", "no"
        };
    }
}
