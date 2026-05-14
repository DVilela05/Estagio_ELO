using WebApplication1.Core.Models;

namespace WebApplication1.Core.Interfaces
{
    /// <summary>
    /// Interface GENÉRICA para qualquer serviço de mensagens.
    /// 
    /// Tanto o WhatsApp como o Teams (ou Slack, etc.) implementam
    /// esta mesma interface. O "core" da aplicação só conhece esta
    /// interface — não sabe os detalhes de cada plataforma.
    /// 
    /// Para adicionar uma nova plataforma:
    ///   1. Cria uma classe que implementa IMessagingService na camada Infrastructure
    ///   2. Regista-a no Program.cs
    ///   3. Adiciona um endpoint no WebhookController
    /// </summary>
    public interface IMessagingService
    {
        /// <summary>
        /// Identifica que plataforma este serviço representa.
        /// </summary>
        MessagePlatform Platform { get; }

        /// <summary>
        /// Marca uma mensagem como lida na plataforma (ex: vistos azuis no WhatsApp).
        /// Nem todas as plataformas suportam — retorna false se não suportar.
        /// </summary>
        Task<bool> MarkAsReadAsync(string messageId);

        /// <summary>
        /// Envia uma mensagem de texto para o destinatário indicado.
        /// O formato do "to" depende da plataforma (telefone, email, user ID, etc.).
        /// 
        /// Se replyToMessageId for fornecido, a mensagem é enviada como resposta
        /// vinculada (citação) à mensagem original — útil quando há spam e queremos
        /// que o utilizador veja claramente qual mensagem foi processada.
        /// </summary>
        Task<bool> SendTextMessageAsync(string to, string text, string? replyToMessageId = null);
    }
}
