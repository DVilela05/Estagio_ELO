namespace WebApplication1.Core.Models
{
    /// <summary>
    /// Resultado de uma operação no servidor de negócio.
    /// 
    /// Encapsula sucesso/falha de forma limpa, evitando que o chamador
    /// precise de fazer try/catch para saber se a operação correu bem.
    /// 
    /// Uso:
    ///   var result = await client.RegisterAttendanceAsync(...);
    ///   if (result.Success) { /* ok */ } else { /* result.ErrorMessage */ }
    /// </summary>
    public class BusinessApiResult
    {
        /// <summary>
        /// True se a operação foi bem sucedida.
        /// </summary>
        public bool Success { get; private set; }

        /// <summary>
        /// Mensagem descritiva (sucesso ou erro).
        /// Em caso de erro, contém a descrição legível para logs.
        /// </summary>
        public string? Message { get; private set; }

        /// <summary>
        /// Código de erro da API (ex: "DUPLICATE", "USER_NOT_FOUND", "TIMEOUT").
        /// Null em caso de sucesso.
        /// </summary>
        public string? ErrorCode { get; private set; }

        /// <summary>
        /// True se a operação foi executada em modo stub (sem servidor real).
        /// Útil para distinguir nos logs se foi registo real ou simulado.
        /// </summary>
        public bool IsStub { get; private set; }

        // ─── Factory Methods (padrão imutável) ─────────────────────────

        /// <summary>
        /// Cria um resultado de sucesso.
        /// </summary>
        public static BusinessApiResult Ok(string? message = null, bool isStub = false)
            => new() { Success = true, Message = message, IsStub = isStub };

        /// <summary>
        /// Cria um resultado de falha.
        /// </summary>
        public static BusinessApiResult Fail(string message, string? errorCode = null)
            => new() { Success = false, Message = message, ErrorCode = errorCode };

        /// <summary>
        /// Cria um resultado de falha por timeout.
        /// </summary>
        public static BusinessApiResult Timeout()
            => new()
            {
                Success = false,
                Message = "O servidor de negócio não respondeu a tempo.",
                ErrorCode = "TIMEOUT"
            };

        /// <summary>
        /// Cria um resultado de falha por servidor indisponível.
        /// </summary>
        public static BusinessApiResult ServiceUnavailable()
            => new()
            {
                Success = false,
                Message = "O servidor de negócio está indisponível.",
                ErrorCode = "SERVICE_UNAVAILABLE"
            };
    }
}
