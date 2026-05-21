namespace WebApplication1.Core.Models
{
    /// <summary>
    /// Modelo universal que representa uma mensagem recebida de QUALQUER plataforma.
    /// 
    /// Seja WhatsApp, Teams, Slack, etc. — todas as mensagens são convertidas
    /// para este formato antes de serem processadas. Assim:
    ///   - O "core" da aplicação não sabe (nem precisa de saber) de onde veio a mensagem.
    ///   - Cada plataforma tem o seu Controller/parser que converte o JSON específico
    ///     da plataforma para este modelo genérico.
    ///   - Podes adicionar novas plataformas sem mudar a lógica de processamento.
    /// </summary>
    public class IncomingMessage
    {
        /// <summary>
        /// ID único da mensagem na plataforma de origem.
        /// Ex: "wamid.abc123" no WhatsApp, ou um GUID no Teams.
        /// Necessário para funcionalidades como "marcar como lida".
        /// </summary>
        public string MessageId { get; set; } = string.Empty;

        /// <summary>
        /// Identificador do remetente (número de telefone, email, user ID, etc.).
        /// Também é usado como chave anti-spam/estado por utilizador.
        /// </summary>
        public string From { get; set; } = string.Empty;

        /// <summary>
        /// Endereço técnico para enviar a resposta.
        /// WhatsApp: normalmente igual a From (número).
        /// Teams: endpoint do Bot Framework (conversation activities URL).
        /// </summary>
        public string ReplyEndpoint { get; set; } = string.Empty;

        /// <summary>
        /// ID único do utilizador na plataforma (Teams: AAD Object ID, WhatsApp: phone number).
        /// Usar para comparar com base de dados e identificar o utilizador.
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// Nome do utilizador (Teams: display name, WhatsApp: pode não estar disponível).
        /// </summary>
        public string UserName { get; set; } = string.Empty;

        /// <summary>
        /// Número de telefone associado ao utilizador (quando disponível, ex: WhatsApp).
        /// </summary>
        public string? UserPhone { get; set; }

        /// <summary>
        /// Email associado ao utilizador (quando disponível, ex: Teams).
        /// </summary>
        public string? UserEmail { get; set; }

        /// <summary>
        /// O texto da mensagem.
        /// </summary>
        public string Body { get; set; } = string.Empty;

        /// <summary>
        /// Texto original recebido (sem normalização).
        /// Útil para comandos como "?" que são removidos na normalização.
        /// </summary>
        public string OriginalBody { get; set; } = string.Empty;

        /// <summary>
        /// De que plataforma veio esta mensagem.
        /// </summary>
        public MessagePlatform Platform { get; set; }

        /// <summary>
        /// Quando a mensagem foi recebida pelo nosso servidor.
        /// </summary>
        public DateTime ReceivedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Quando a mensagem foi enviada pelo telefone do utilizador (timestamp da plataforma).
        /// WhatsApp: campo "timestamp" (Unix epoch seconds).
        /// Teams: campo "timestamp" da Activity.
        /// Usado para detetar mensagens atrasadas: se SentAt for anterior à última resposta do bot, ignorar.
        /// </summary>
        public DateTime SentAt { get; set; } = DateTime.MinValue;

        /// <summary>
        /// Indica se a mensagem contém uma localização (pin/GPS) enviada pelo utilizador.
        /// </summary>
        public bool HasLocation { get; set; }

        /// <summary>
        /// Latitude da localização enviada (quando HasLocation=true).
        /// </summary>
        public double? Latitude { get; set; }

        /// <summary>
        /// Longitude da localização enviada (quando HasLocation=true).
        /// </summary>
        public double? Longitude { get; set; }

        /// <summary>
        /// Nome/label da localização (se a plataforma fornecer).
        /// </summary>
        public string? LocationName { get; set; }

        /// <summary>
        /// Endereço textual da localização (se a plataforma fornecer).
        /// </summary>
        public string? LocationAddress { get; set; }

        /// <summary>
        /// Língua detetada para esta mensagem.
        /// Determinada pelo indicativo do telefone (WhatsApp) ou pelo conteúdo da mensagem.
        /// Valor null significa que a língua ainda não foi detetada.
        /// </summary>
        public SupportedLanguage? Language { get; set; }

        /// <summary>
        /// Texto exato que o utilizador escreveu para dar uma confirmação (ex: "s", "SIM", "yes").
        /// Preenchido pelo MessageProcessingService antes de encaminhar a mensagem final.
        /// </summary>
        public string? ConfirmationText { get; set; }

        /// <summary>
        /// Timestamp formatado para exibição nos logs.
        /// </summary>
        public string FormattedTime => ReceivedAt.ToString("dd/MM/yyyy HH:mm:ss");
    }
}
