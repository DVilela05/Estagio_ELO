using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace WebApplication1.Api.Controllers
{
    /// <summary>
    /// Health check endpoint — verifica se a aplicação está viva.
    /// 
    /// Usado por:
    ///   - Load balancers (para saber se podem enviar tráfego)
    ///   - Sistemas de monitorização (para alertar se a app caiu)
    ///   - Deploys automatizados (para confirmar que a app arrancou)
    /// 
    /// Rota: GET /health
    /// </summary>
    [Route("health")]
    [ApiController]
    public class HealthController : ControllerBase
    {
        /// <summary>
        /// Verifica se a aplicação está viva e pronta para receber pedidos.
        /// </summary>
        /// <returns>200 OK com informação básica do estado</returns>
        /// <response code="200">Aplicação está saudável</response>
        /// <response code="429">Demasiados pedidos de health check</response>
        [HttpGet]
        [EnableRateLimiting("health")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        public IActionResult GetHealth()
        {
            var healthInfo = new
            {
                status = "healthy",
                timestamp = DateTime.UtcNow,
                uptime = GetUptime(),
                version = "1.0.0"
            };

            return Ok(healthInfo);
        }

        private static string GetUptime()
        {
            var uptime = DateTime.UtcNow - System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime();
            return $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m";
        }
    }
}
