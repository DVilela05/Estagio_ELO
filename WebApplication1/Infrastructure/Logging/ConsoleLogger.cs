using WebApplication1.Core.Models;

namespace WebApplication1.Infrastructure.Logging
{
    /// <summary>
    /// Classe utilitária para escrever logs bonitos e coloridos na consola.
    /// Está separada porque não tem nada a ver com lógica de negócio —
    /// é apenas formatação visual.
    /// </summary>
    public static class ConsoleLogger
    {
        // Lock global para evitar output intercalado quando múltiplas tasks
        // async escrevem na consola em simultâneo (fire-and-forget webhooks).
        private static readonly object _consoleLock = new();

        public static void WriteColor(string text, ConsoleColor color)
        {
            lock (_consoleLock)
            {
                Console.ForegroundColor = color;
                Console.WriteLine(text);
                Console.ResetColor();
            }
        }

        public static void WriteColorInline(string text, ConsoleColor color)
        {
            lock (_consoleLock)
            {
                Console.ForegroundColor = color;
                Console.Write(text);
                Console.ResetColor();
            }
        }

        public static void Error(string message)
        {
            WriteColor($"   ⚠️  {message}", ConsoleColor.Red);
        }

        private static (string emoji, string name, ConsoleColor color) GetPlatformInfo(MessagePlatform platform)
        {
            return platform switch
            {
                MessagePlatform.WhatsApp => ("🟢", "WhatsApp", ConsoleColor.Green),
                MessagePlatform.Teams => ("🟣", "Teams", ConsoleColor.Blue),
                _ => ("⚪", "Desconhecido", ConsoleColor.Gray)
            };
        }

        /// <summary>
        /// Retorna um nome legível para o remetente.
        /// Teams: UserName (ou UserId se nome vazio). WhatsApp: número de telefone (From).
        /// </summary>
        public static string GetDisplayName(IncomingMessage msg)
        {
            // Se temos UserName, usar (Teams fornece sempre)
            if (!string.IsNullOrWhiteSpace(msg.UserName))
                return msg.UserName;

            // Se temos UserId diferente do From (ex: AAD ObjectId), usar
            if (!string.IsNullOrWhiteSpace(msg.UserId) && msg.UserId != msg.From)
                return msg.UserId;

            // Fallback: usar From (número WhatsApp ou outro)
            return msg.From;
        }

        public static void PrintMessageBox(IncomingMessage msg)
        {
            var (emoji, platformName, platformColor) = GetPlatformInfo(msg.Platform);
            string displayName = GetDisplayName(msg);

            const int boxWidth = 60;
            string border = new string('═', boxWidth);
            string thin = new string('─', boxWidth);

            string truncatedName = displayName.Length > (boxWidth - 15)
                ? displayName.Substring(0, boxWidth - 18) + "..."
                : displayName;
            string displayBody = msg.Body.Length > (boxWidth - 15)
                ? msg.Body.Substring(0, boxWidth - 18) + "..."
                : msg.Body;
            string shortId = msg.MessageId.Length > (boxWidth - 15)
                ? msg.MessageId.Substring(0, boxWidth - 18) + "..."
                : msg.MessageId;

            // Lock atómico: toda a caixa é impressa de uma vez para evitar
            // output intercalado quando múltiplas mensagens chegam em simultâneo.
            lock (_consoleLock)
            {
                Console.WriteLine();

                Console.ForegroundColor = platformColor;
                Console.WriteLine($"╔{border}╗");
                Console.WriteLine($"║  📩  NOVA MENSAGEM RECEBIDA  {emoji} {platformName,-19}        ║");
                Console.WriteLine($"╠{border}╣");

                Console.Write("║  ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write("👤 De:       ");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write(truncatedName.PadRight(boxWidth - 15));
                Console.ForegroundColor = platformColor;
                Console.WriteLine("║");

                Console.Write("║  ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write("🕐 Hora:     ");
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write(msg.FormattedTime.PadRight(boxWidth - 15));
                Console.ForegroundColor = platformColor;
                Console.WriteLine("║");

                Console.Write("║  ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write("📱 Canal:    ");
                Console.ForegroundColor = platformColor;
                Console.Write($"{emoji} {platformName}".PadRight(boxWidth - 15));
                Console.WriteLine("║");

                Console.WriteLine($"║  {thin.Substring(4)}  ║");

                Console.Write("║  ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write("💬 Texto:    ");
                Console.Write(displayBody.PadRight(boxWidth - 15));
                Console.ForegroundColor = platformColor;
                Console.WriteLine("║");

                Console.Write("║  ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write("🆔 Msg ID:   ");
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write(shortId.PadRight(boxWidth - 15));
                Console.ForegroundColor = platformColor;
                Console.WriteLine("║");

                Console.WriteLine($"╚{border}╝");
                Console.ResetColor();
            }
        }

        public static void PrintReadReceipt(bool success)
            => PrintReadReceipt(MessagePlatform.WhatsApp, success);

        public static void PrintReadReceipt(MessagePlatform platform, bool success)
        {
            lock (_consoleLock)
            {
                if (platform == MessagePlatform.Teams)
                {
                    Console.ForegroundColor = ConsoleColor.DarkCyan;
                    Console.WriteLine("   👁️ Teams: read receipt não é enviado manualmente pelo bot (evento de leitura pode chegar depois)");
                }
                else if (success)
                {
                    Console.ForegroundColor = ConsoleColor.Blue;
                    Console.WriteLine("   ✔✔ Mensagem marcada como LIDA (vistos azuis enviados)");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("   ✖✖ Falha ao enviar vistos azuis");
                }
                Console.ResetColor();
            }
        }

        public static void PrintReplySent(string to, bool success)
        {
            lock (_consoleLock)
            {
                if (success)
                {
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.WriteLine($"   📤 Resposta enviada para {to} com sucesso!");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"   ❌ ERRO ao enviar resposta para {to}");
                }
                Console.ResetColor();
                Console.WriteLine();
            }
        }

        /// <summary>
        /// Overload que aceita IncomingMessage e mostra o nome legível em vez do endpoint.
        /// </summary>
        public static void PrintReplySent(IncomingMessage msg, bool success)
        {
            string displayName = GetDisplayName(msg);
            PrintReplySent(displayName, success);
        }

        public static void PrintVerification(string? mode, bool tokenValid, bool success)
        {
            lock (_consoleLock)
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("┌──────────────────────────────────────────────────────────────┐");
                Console.WriteLine("│  🤝  PEDIDO DE VERIFICAÇÃO DO WEBHOOK (GET)                 │");
                Console.WriteLine("└──────────────────────────────────────────────────────────────┘");

                Console.ForegroundColor = ConsoleColor.White;
                Console.Write("   Mode:  ");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(mode ?? "(vazio)");

                Console.ForegroundColor = ConsoleColor.White;
                Console.Write("   Token: ");
                Console.ForegroundColor = tokenValid ? ConsoleColor.Green : ConsoleColor.Red;
                Console.WriteLine(tokenValid ? "✅ Válido" : "❌ Inválido");

                Console.ForegroundColor = success ? ConsoleColor.Green : ConsoleColor.Red;
                Console.WriteLine(success
                    ? "   ✅ Webhook verificado com SUCESSO!"
                    : "   🚫 Verificação REJEITADA — token ou modo incorreto.");

                Console.ResetColor();
                Console.WriteLine();
            }
        }

        public static void PrintErrorBox(string message)
        {
            lock (_consoleLock)
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("┌──────────────────────────────────────────────────────────────┐");
                Console.WriteLine("│  ⚠️   ERRO NO PROCESSAMENTO                                 │");
                Console.WriteLine("└──────────────────────────────────────────────────────────────┘");
                Console.WriteLine($"   {message}");
                Console.ResetColor();
                Console.WriteLine();
            }
        }
    }
}
