namespace WebApplication1.Core.Exceptions
{
    /// <summary>
    /// Exceção para quando um comando inválido é recebido.
    /// </summary>
    public class InvalidCommandException : Exception
    {
        public InvalidCommandException(string message) : base(message) { }
        public InvalidCommandException(string message, Exception innerException) 
            : base(message, innerException) { }
    }

    /// <summary>
    /// Exceção para quando a assinatura do webhook é inválida (segurança).
    /// </summary>
    public class WebhookVerificationException : Exception
    {
        public WebhookVerificationException(string message) : base(message) { }
        public WebhookVerificationException(string message, Exception innerException) 
            : base(message, innerException) { }
    }

    /// <summary>
    /// Exceção para quando a configuração está incompleta ou inválida.
    /// </summary>
    public class ConfigurationException : Exception
    {
        public ConfigurationException(string message) : base(message) { }
        public ConfigurationException(string message, Exception innerException) 
            : base(message, innerException) { }
    }

    /// <summary>
    /// Exceção para quando o processamento de mensagem falha.
    /// </summary>
    public class MessageProcessingException : Exception
    {
        public MessageProcessingException(string message) : base(message) { }
        public MessageProcessingException(string message, Exception innerException) 
            : base(message, innerException) { }
    }
}
