namespace WebApplication1.Core.Models
{
    /// <summary>
    /// Representa a PLATAFORMA de onde veio a mensagem.
    /// Para adicionar uma nova plataforma, basta adicionar aqui.
    /// </summary>
    public enum MessagePlatform
    {
        WhatsApp,
        Teams,
        // Slack,       ← descomenta quando integrares o Slack
    }
}
