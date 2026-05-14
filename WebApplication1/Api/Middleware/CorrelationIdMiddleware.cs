namespace WebApplication1.Api.Middleware
{
    /// <summary>
    /// Middleware que adiciona um Correlation ID único a cada pedido HTTP.
    /// 
    /// O Correlation ID é um GUID que:
    ///   - Aparece em TODOS os logs deste pedido (facilita rastrear erros)
    ///   - É devolvido no header X-Correlation-ID da resposta
    ///   - Pode ser propagado para chamadas externas (servidor de negócio, etc.)
    /// 
    /// Exemplo de uso nos logs:
    ///   [CorrelationId: abc-123] Mensagem recebida de 351...
    ///   [CorrelationId: abc-123] Comando "presença" acionado
    ///   [CorrelationId: abc-123] Resposta enviada
    /// 
    /// Se houver erro, procuras por "abc-123" nos logs e vês todo o fluxo.
    /// </summary>
    public class CorrelationIdMiddleware
    {
        private readonly RequestDelegate _next;
        private const string CorrelationIdHeaderName = "X-Correlation-ID";

        public CorrelationIdMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, ILogger<CorrelationIdMiddleware> logger)
        {
            // 1. Verificar se o pedido já vem com Correlation ID (propagado de outro sistema)
            string correlationId = context.Request.Headers[CorrelationIdHeaderName].FirstOrDefault()
                                ?? Guid.NewGuid().ToString();

            // 2. Guardar no HttpContext para ser acessível em toda a pipeline
            context.Items["CorrelationId"] = correlationId;

            // 3. Adicionar ao header da resposta (para o cliente saber o ID do pedido)
            context.Response.OnStarting(() =>
            {
                context.Response.Headers[CorrelationIdHeaderName] = correlationId;
                return Task.CompletedTask;
            });

            // 4. Adicionar aos logs deste pedido (usando LoggerMessage pattern)
            using (logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId
            }))
            {
                await _next(context);
            }
        }
    }
}
