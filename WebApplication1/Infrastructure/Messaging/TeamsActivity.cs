using System.Text.Json;
using System.Text.Json.Serialization;

namespace WebApplication1.Infrastructure.Messaging
{
    /// <summary>
    /// Representa uma Activity do Microsoft Bot Framework.
    /// Este é o formato JSON que o Teams envia para o webhook do bot.
    /// 
    /// Documentação: https://docs.microsoft.com/en-us/azure/bot-service/rest-api/bot-framework-rest-connector-api-reference
    /// </summary>
    public class TeamsActivity
    {
        /// <summary>
        /// Tipo de atividade (message, conversationUpdate, etc.).
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// ID único da atividade.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp quando a atividade foi criada.
        /// </summary>
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// URL do serviço que processa a atividade.
        /// Necessário para enviar respostas (ex: https://smba.trafficmanager.net/emea/).
        /// </summary>
        [JsonPropertyName("serviceUrl")]
        public string ServiceUrl { get; set; } = string.Empty;

        /// <summary>
        /// ID do canal (msteams, directline, etc.).
        /// </summary>
        [JsonPropertyName("channelId")]
        public string ChannelId { get; set; } = string.Empty;

        /// <summary>
        /// Informação sobre o remetente da mensagem.
        /// </summary>
        [JsonPropertyName("from")]
        public TeamsChannelAccount From { get; set; } = new();

        /// <summary>
        /// Informação sobre a conversa.
        /// </summary>
        [JsonPropertyName("conversation")]
        public TeamsConversationAccount Conversation { get; set; } = new();

        /// <summary>
        /// Informação sobre o destinatário (o bot).
        /// </summary>
        [JsonPropertyName("recipient")]
        public TeamsChannelAccount Recipient { get; set; } = new();

        /// <summary>
        /// Texto da mensagem.
        /// </summary>
        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// Attachments da Activity (ex: cartões, localização, etc.).
        /// Mantido como JsonElement para suportar variações de payload do Teams.
        /// </summary>
        [JsonPropertyName("attachments")]
        public JsonElement? Attachments { get; set; }

        /// <summary>
        /// Entities da Activity (ex: Place/GeoCoordinates).
        /// </summary>
        [JsonPropertyName("entities")]
        public JsonElement? Entities { get; set; }

        /// <summary>
        /// Nome do evento (ex: application/vnd.microsoft.readReceipt).
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        /// <summary>
        /// Payload de valor para eventos do Teams.
        /// </summary>
        [JsonPropertyName("value")]
        public JsonElement? Value { get; set; }

        /// <summary>
        /// Dados específicos do canal Teams.
        /// </summary>
        [JsonPropertyName("channelData")]
        public TeamsChannelData? ChannelData { get; set; }

        /// <summary>
        /// ID da atividade à qual esta é uma resposta (se aplicável).
        /// </summary>
        [JsonPropertyName("replyToId")]
        public string? ReplyToId { get; set; }
    }

    /// <summary>
    /// Representa uma conta de utilizador ou bot no Teams.
    /// </summary>
    public class TeamsChannelAccount
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("aadObjectId")]
        public string? AadObjectId { get; set; }

        [JsonPropertyName("userPrincipalName")]
        public string? UserPrincipalName { get; set; }
    }

    /// <summary>
    /// Representa uma conversa no Teams.
    /// </summary>
    public class TeamsConversationAccount
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("conversationType")]
        public string ConversationType { get; set; } = string.Empty;

        [JsonPropertyName("tenantId")]
        public string? TenantId { get; set; }
    }

    /// <summary>
    /// Dados específicos do canal Teams.
    /// </summary>
    public class TeamsChannelData
    {
        [JsonPropertyName("tenant")]
        public TeamsTenantInfo? Tenant { get; set; }

        [JsonPropertyName("eventType")]
        public string? EventType { get; set; }
    }

    /// <summary>
    /// Informação sobre o tenant do Teams.
    /// </summary>
    public class TeamsTenantInfo
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;
    }

    /// <summary>
    /// Estrutura para enviar mensagens de resposta via Bot Framework API.
    /// </summary>
    public class TeamsReplyPayload
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "message";

        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;
    }

    /// <summary>
    /// Payload do evento de read receipt no Teams.
    /// </summary>
    public class TeamsReadReceiptValue
    {
        [JsonPropertyName("lastReadMessageId")]
        public string? LastReadMessageId { get; set; }
    }
}
