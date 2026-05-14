namespace WebApplication1.Infrastructure.Configuration
{
    /// <summary>
    /// Classe que mapeia a secção "Teams" do appsettings.json + User Secrets.
    /// Tokens sensíveis (ClientSecret) ficam nos User Secrets.
    /// Configuração não-sensível (BotId, TenantId) fica no appsettings.json.
    /// </summary>
    public class TeamsSettings
    {
        public string BotId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string TenantId { get; set; } = "botframework.com";
        public string LoginUrl { get; set; } = "https://login.microsoftonline.com";
        public string Scope { get; set; } = "https://api.botframework.com/.default";
    }
}
