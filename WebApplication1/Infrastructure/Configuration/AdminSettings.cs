namespace WebApplication1.Infrastructure.Configuration
{
    /// <summary>
    /// Configuração do menu de administração por chat.
    ///
    /// Secção: Admin
    /// </summary>
    public class AdminSettings
    {
        /// <summary>
        /// Liga/desliga o comando admin.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Em Development, se não houver allowlist definida, permite acesso.
        /// </summary>
        public bool AllowInDevelopmentWithoutWhitelist { get; set; } = true;

        /// <summary>
        /// IDs de utilizador autorizados (ex.: WhatsApp from/userId, AAD object id).
        /// </summary>
        public string[] AllowedUserIds { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Telefones autorizados (normalizados, ex.: 932947533).
        /// </summary>
        public string[] AllowedPhones { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Emails autorizados.
        /// </summary>
        public string[] AllowedEmails { get; set; } = Array.Empty<string>();
    }
}
