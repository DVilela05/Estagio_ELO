using WebApplication1.Core.Exceptions;

namespace WebApplication1.Infrastructure.Configuration
{
    /// <summary>
    /// Valida se a configuração do WhatsApp e Teams foi carregada corretamente.
    /// Falha rápido se algo obrigatório está em falta.
    /// </summary>
    public static class ConfigurationValidator
    {
        public static void ValidateWhatsAppSettings(WhatsAppSettings settings)
        {
            if (settings == null)
                throw new ConfigurationException("WhatsAppSettings é nulo. Verifica a configuração em Program.cs");

            if (string.IsNullOrWhiteSpace(settings.VerifyToken))
                throw new ConfigurationException(
                    "WhatsApp:VerifyToken é obrigatório. " +
                    "Configura em appsettings.json ou user-secrets.");

            if (string.IsNullOrWhiteSpace(settings.AccessToken))
                throw new ConfigurationException(
                    "WhatsApp:AccessToken é obrigatório. " +
                    "Configura: dotnet user-secrets set \"WhatsApp:AccessToken\" \"seu_token\"");

            if (string.IsNullOrWhiteSpace(settings.AppSecret))
                throw new ConfigurationException(
                    "WhatsApp:AppSecret é obrigatório. " +
                    "Configura: dotnet user-secrets set \"WhatsApp:AppSecret\" \"seu_secret\"");

            if (string.IsNullOrWhiteSpace(settings.PhoneNumberId))
                throw new ConfigurationException(
                    "WhatsApp:PhoneNumberId é obrigatório. " +
                    "Configura em appsettings.json");

            if (string.IsNullOrWhiteSpace(settings.ApiVersion))
                throw new ConfigurationException(
                    "WhatsApp:ApiVersion é obrigatório. " +
                    "Configura em appsettings.json (ex: v22.0)");
        }

        public static void ValidateTeamsSettings(TeamsSettings settings)
        {
            if (settings == null)
                throw new ConfigurationException("TeamsSettings é nulo. Verifica a configuração em Program.cs");

            if (string.IsNullOrWhiteSpace(settings.BotId))
                throw new ConfigurationException(
                    "Teams:BotId é obrigatório. " +
                    "Configura em appsettings.json (obtém no Azure Bot Service).");

            if (string.IsNullOrWhiteSpace(settings.ClientSecret))
                throw new ConfigurationException(
                    "Teams:ClientSecret é obrigatório. " +
                    "Configura: dotnet user-secrets set \"Teams:ClientSecret\" \"seu_secret\"");

            if (string.IsNullOrWhiteSpace(settings.TenantId))
                throw new ConfigurationException(
                    "Teams:TenantId é obrigatório. " +
                    "Configura em appsettings.json (recomendado: 'botframework.com').");

            if (string.IsNullOrWhiteSpace(settings.LoginUrl))
                throw new ConfigurationException(
                    "Teams:LoginUrl é obrigatório. " +
                    "Configura em appsettings.json (ex: https://login.microsoftonline.com).");
        }


    }
}
