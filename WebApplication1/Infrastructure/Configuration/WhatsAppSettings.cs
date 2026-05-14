namespace WebApplication1.Infrastructure.Configuration
{
    /// <summary>
    /// Classe que mapeia a secção "WhatsApp" do appsettings.json + User Secrets.
    /// Tokens sensíveis (AccessToken, AppSecret) ficam nos User Secrets.
    /// Configuração não-sensível (VerifyToken, PhoneNumberId) fica no appsettings.json.
    /// </summary>
    public class WhatsAppSettings
    {
        public string VerifyToken { get; set; } = string.Empty;
        public string AccessToken { get; set; } = string.Empty;
        public string AppSecret { get; set; } = string.Empty;
        public string PhoneNumberId { get; set; } = string.Empty;
        public string ApiVersion { get; set; } = "v22.0";
    }
}
