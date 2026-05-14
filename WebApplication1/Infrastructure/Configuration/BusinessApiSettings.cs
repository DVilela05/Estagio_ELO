namespace WebApplication1.Infrastructure.Configuration
{
    /// <summary>
    /// Configuração do servidor de negócio (REST API).
    /// 
    /// Mapeado a partir da secção "BusinessApi" do appsettings.json.
    /// 
    /// Quando BaseUrl está vazio ou não configurado, o BusinessApiClient
    /// opera em modo STUB (simula respostas sem chamar servidor real).
    /// 
    /// Para ativar a comunicação real com o servidor:
    ///   1. Configura o BaseUrl no appsettings.json ou User Secrets
    ///   2. O BusinessApiClient começa automaticamente a fazer HTTP real
    /// </summary>
    public class BusinessApiSettings
    {
        /// <summary>
        /// URL base do servidor de negócio.
        /// Ex: "https://api-negocio.empresa.pt" ou "http://localhost:5100"
        /// 
        /// Se vazio/null → modo stub (não faz HTTP real).
        /// </summary>
        public string? BaseUrl { get; set; }

        /// <summary>
        /// Timeout em segundos para chamadas ao servidor de negócio.
        /// Default: 30 segundos.
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Número de retries automáticos em caso de erro transitório.
        /// Default: 3 retries (com backoff exponencial).
        /// </summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// Path do endpoint para registo de presença no servidor de negócio.
        /// Default: /api/attendance
        /// </summary>
        public string AttendancePath { get; set; } = "/api/attendance";

        /// <summary>
        /// Token de serviço usado no header Authorization (Bearer) entre webhook e web service.
        /// Deve ser guardado em User Secrets/variável de ambiente (nunca hardcoded em produção).
        /// </summary>
        public string? ServiceToken { get; set; }

        /// <summary>
        /// Segredo HMAC partilhado para assinatura dos pedidos.
        /// Deve ser longo e aleatório.
        /// </summary>
        public string? HmacSecret { get; set; }

        /// <summary>
        /// Permite HTTP sem TLS (apenas para desenvolvimento local).
        /// Em produção deve estar false.
        /// </summary>
        public bool AllowInsecureHttp { get; set; }

        /// <summary>
        /// True se o modo stub está ativo (BaseUrl não configurado).
        /// </summary>
        public bool IsStubMode => string.IsNullOrWhiteSpace(BaseUrl);

        /// <summary>
        /// True quando as credenciais de segurança para chamadas reais estão configuradas.
        /// </summary>
        public bool HasServiceSecurity
            => !string.IsNullOrWhiteSpace(ServiceToken)
            && !string.IsNullOrWhiteSpace(HmacSecret);
    }
}
