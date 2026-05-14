using System.Net;
using System.Text.Json;

namespace WebApplication1.Api.Middleware
{
    /// <summary>
    /// Middleware global de tratamento de exceções.
    /// 
    /// Se alguma exceção não tratada escapar de um controller,
    /// este middleware apanha-a e devolve uma resposta JSON limpa
    /// em vez de um stack trace feio (que também é um risco de segurança).
    /// 
    /// Em desenvolvimento mostra a mensagem de erro completa.
    /// Em produção mostra apenas "Ocorreu um erro interno."
    /// </summary>
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;
        private readonly IHostEnvironment _env;

        public ExceptionHandlingMiddleware(
            RequestDelegate next,
            ILogger<ExceptionHandlingMiddleware> logger,
            IHostEnvironment env)
        {
            _next = next;
            _logger = logger;
            _env = env;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Exceção não tratada: {Message}", ex.Message);
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            var response = new
            {
                status = 500,
                message = _env.IsDevelopment()
                    ? exception.Message  // Em dev, mostra o erro real.
                    : "Ocorreu um erro interno. Contacte o administrador.",
                timestamp = DateTime.UtcNow
            };

            var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await context.Response.WriteAsync(json);
        }
    }
}
