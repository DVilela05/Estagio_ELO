using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using WebApplication1.Infrastructure.Configuration;

namespace WebApplication1.Api.Middleware
{
    /// <summary>
    /// Filtro de segurança para validar JWTs recebidos no webhook do Teams.
    /// 
    /// Em produção, garante que a Activity foi enviada pelo Bot Framework
    /// e que o token foi emitido para o nosso bot (audience = BotId).
    /// 
    /// Em Development, a validação é ignorada para não bloquear o Emulator.
    /// </summary>
    public class ValidateTeamsJwtFilter : IAsyncActionFilter
    {
        private const string OpenIdMetadataUrl = "https://login.botframework.com/v1/.well-known/openidconfiguration";

        private static readonly ConfigurationManager<OpenIdConnectConfiguration> OpenIdConfigManager =
            new(OpenIdMetadataUrl, new OpenIdConnectConfigurationRetriever());

        private readonly TeamsSettings _settings;
        private readonly IHostEnvironment _environment;
        private readonly ILogger<ValidateTeamsJwtFilter> _logger;

        public ValidateTeamsJwtFilter(
            IOptions<TeamsSettings> settings,
            IHostEnvironment environment,
            ILogger<ValidateTeamsJwtFilter> logger)
        {
            _settings = settings.Value;
            _environment = environment;
            _logger = logger;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            // Em dev/test local não validamos JWT para permitir Bot Framework Emulator.
            if (_environment.IsDevelopment())
            {
                await next();
                return;
            }

            if (string.IsNullOrWhiteSpace(_settings.BotId))
            {
                _logger.LogError("Teams:BotId não configurado em produção — validação JWT não pode ser efetuada.");
                context.Result = new StatusCodeResult(StatusCodes.Status500InternalServerError);
                return;
            }

            var request = context.HttpContext.Request;

            // Só validamos POST do webhook.
            if (!HttpMethods.IsPost(request.Method))
            {
                await next();
                return;
            }

            if (!request.Headers.TryGetValue("Authorization", out var authHeaderValues))
            {
                _logger.LogWarning("🚫 Pedido Teams sem header Authorization — rejeitado.");
                context.Result = new UnauthorizedObjectResult("Missing Authorization header.");
                return;
            }

            string authHeader = authHeaderValues.ToString();
            if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("🚫 Header Authorization Teams inválido (não é Bearer) — rejeitado.");
                context.Result = new UnauthorizedObjectResult("Invalid Authorization header.");
                return;
            }

            string token = authHeader["Bearer ".Length..].Trim();
            if (string.IsNullOrWhiteSpace(token))
            {
                _logger.LogWarning("🚫 Token JWT Teams vazio — rejeitado.");
                context.Result = new UnauthorizedObjectResult("Empty bearer token.");
                return;
            }

            try
            {
                var openIdConfig = await OpenIdConfigManager.GetConfigurationAsync(context.HttpContext.RequestAborted);

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = openIdConfig.Issuer,
                    ValidateAudience = true,
                    ValidAudience = _settings.BotId,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(5),
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKeys = openIdConfig.SigningKeys
                };

                var tokenHandler = new JwtSecurityTokenHandler();
                tokenHandler.ValidateToken(token, validationParameters, out _);

                _logger.LogDebug("✅ JWT Teams válido.");
                await next();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "🚫 JWT Teams inválido — pedido rejeitado.");
                context.Result = new UnauthorizedObjectResult("Invalid Teams token.");
            }
        }
    }
}
