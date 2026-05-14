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

        public static void ValidateBusinessApiSettings(BusinessApiSettings settings, bool isDevelopment)
        {
            if (settings == null)
                throw new ConfigurationException("BusinessApiSettings é nulo. Verifica a configuração em Program.cs");

            if (settings.IsStubMode)
                return;

            if (string.IsNullOrWhiteSpace(settings.AttendancePath))
                throw new ConfigurationException(
                    "BusinessApi:AttendancePath é obrigatório quando BaseUrl está configurado.");

            if (!settings.HasServiceSecurity)
                throw new ConfigurationException(
                    "BusinessApi exige segurança entre serviços: configura ServiceToken e HmacSecret " +
                    "(User Secrets ou variáveis de ambiente)."
                );

            if (!Uri.TryCreate(settings.BaseUrl, UriKind.Absolute, out var uri))
                throw new ConfigurationException("BusinessApi:BaseUrl inválido.");

            bool isHttps = string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
            if (!isHttps && !(isDevelopment && settings.AllowInsecureHttp))
                throw new ConfigurationException(
                    "BusinessApi:BaseUrl deve usar HTTPS em produção. " +
                    "Para dev local com HTTP, define AllowInsecureHttp=true apenas em appsettings.Development.json");
        }
    }
}
