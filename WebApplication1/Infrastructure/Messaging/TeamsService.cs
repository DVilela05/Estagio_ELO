using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using System.Net;
using WebApplication1.Core.Interfaces;
using WebApplication1.Core.Models;
using WebApplication1.Infrastructure.Configuration;
using WebApplication1.Infrastructure.Logging;

namespace WebApplication1.Infrastructure.Messaging
{
    /// <summary>
    /// Implementação do IMessagingService para o Microsoft Teams Bot Framework.
    /// 
    /// Toda a lógica ESPECÍFICA do Teams fica aqui:
    /// - Autenticação OAuth2 com o Azure AD
    /// - Envio de mensagens via Bot Framework REST API
    /// - Cache de tokens de acesso (expira a cada ~1h)
    /// 
    /// Funciona exatamente como o WhatsAppService, mas com a API do Teams.
    /// </summary>
    public class TeamsService : IMessagingService
    {
        private readonly HttpClient _httpClient;
        private readonly TeamsSettings _settings;
        private readonly ILogger<TeamsService> _logger;
        private string? _cachedAccessToken;
        private DateTime _tokenExpiration;
        private readonly SemaphoreSlim _tokenLock = new(1, 1);

        public TeamsService(
            HttpClient httpClient,
            IOptions<TeamsSettings> settings,
            ILogger<TeamsService> logger)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
            _logger = logger;
            _tokenExpiration = DateTime.MinValue;
        }

        /// <inheritdoc />
        public MessagePlatform Platform => MessagePlatform.Teams;

        /// <summary>
        /// Obtém um access token OAuth2 para autenticar pedidos ao Bot Framework.
        /// Usa cache interno — só pede um novo token quando o anterior expira.
        /// Thread-safe via SemaphoreSlim.
        /// 
        /// Se o BotId ou ClientSecret não estiverem configurados (modo dev/Emulator),
        /// retorna null e o pedido é enviado sem autenticação.
        /// </summary>
        internal async Task<string?> GetAccessTokenAsync()
        {
            // Sem credenciais → modo dev/Emulator (sem autenticação)
            if (string.IsNullOrEmpty(_settings.BotId) || string.IsNullOrEmpty(_settings.ClientSecret))
            {
                _logger.LogDebug("BotId ou ClientSecret não configurados — modo sem autenticação (Emulator/dev)");
                return null;
            }

            await _tokenLock.WaitAsync();
            try
            {
                // Se o token ainda é válido (com margem de 5 minutos), reutiliza
                if (!string.IsNullOrEmpty(_cachedAccessToken) && DateTime.UtcNow < _tokenExpiration.AddMinutes(-5))
                {
                    return _cachedAccessToken;
                }

                string connectorTenant = ResolveConnectorTenant(_settings.TenantId);
                var tokenUrl = $"{_settings.LoginUrl}/{connectorTenant}/oauth2/v2.0/token";
                var requestBody = new Dictionary<string, string>
                {
                    { "grant_type", "client_credentials" },
                    { "client_id", _settings.BotId },
                    { "client_secret", _settings.ClientSecret },
                    { "scope", _settings.Scope }
                };

                var content = new FormUrlEncodedContent(requestBody);
                var response = await _httpClient.PostAsync(tokenUrl, content);

                if (!response.IsSuccessStatusCode)
                {
                    string errorBody = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Falha ao obter token OAuth2 do Teams: {StatusCode} - {Error}",
                        response.StatusCode, errorBody);
                    throw new InvalidOperationException($"Falha ao obter token OAuth2: {response.StatusCode}");
                }

                using var tokenDoc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
                var root = tokenDoc.RootElement;

                _cachedAccessToken = root.GetProperty("access_token").GetString()
                    ?? throw new InvalidOperationException("Token de acesso vazio");

                int expiresIn = root.TryGetProperty("expires_in", out var expEl)
                    ? expEl.GetInt32()
                    : 3600;

                _tokenExpiration = DateTime.UtcNow.AddSeconds(expiresIn);

                _logger.LogDebug("Token OAuth2 Teams obtido com sucesso. Expira em {ExpiresIn}s", expiresIn);

                return _cachedAccessToken;
            }
            finally
            {
                _tokenLock.Release();
            }
        }

        /// <summary>
        /// Invalida o token em cache para forçar renovação no próximo envio.
        /// </summary>
        private void InvalidateCachedToken()
        {
            _cachedAccessToken = null;
            _tokenExpiration = DateTime.MinValue;
        }

        /// <summary>
        /// Obtém um token sem usar cache para um tenant específico.
        /// Usado como fallback quando a configuração está com tenant "common"
        /// e o Bot Framework devolve 401.
        /// </summary>
        private async Task<string?> GetAccessTokenForTenantAsync(string tenantId)
        {
            if (string.IsNullOrEmpty(_settings.BotId) || string.IsNullOrEmpty(_settings.ClientSecret))
                return null;

            var tokenUrl = $"{_settings.LoginUrl}/{tenantId}/oauth2/v2.0/token";
            var requestBody = new Dictionary<string, string>
            {
                { "grant_type", "client_credentials" },
                { "client_id", _settings.BotId },
                { "client_secret", _settings.ClientSecret },
                { "scope", _settings.Scope }
            };

            var content = new FormUrlEncodedContent(requestBody);
            var response = await _httpClient.PostAsync(tokenUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                string errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogError("Falha ao obter token OAuth2 (tenant={Tenant}): {StatusCode} - {Error}",
                    tenantId, response.StatusCode, errorBody);
                return null;
            }

            using var tokenDoc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            return tokenDoc.RootElement.GetProperty("access_token").GetString();
        }

        /// <summary>
        /// Resolve o tenant para obter token OAuth2.
        /// Regras:
        /// - Se vazio, usa "botframework.com" (compatibilidade).
        /// - Se vier GUID (tenant Entra da app), usa o próprio GUID.
        /// - Se vier alias válido (common/organizations/botframework.com), usa-o.
        /// </summary>
        private static string ResolveConnectorTenant(string? configuredTenant)
        {
            if (string.IsNullOrWhiteSpace(configuredTenant))
                return "botframework.com";

            string tenant = configuredTenant.Trim();

            if (tenant.Equals("botframework.com", StringComparison.OrdinalIgnoreCase) ||
                tenant.Equals("common", StringComparison.OrdinalIgnoreCase) ||
                tenant.Equals("organizations", StringComparison.OrdinalIgnoreCase))
            {
                return tenant;
            }

            if (Guid.TryParse(tenant, out _))
            {
                return tenant;
            }

            return tenant;
        }

        /// <summary>
        /// Envia uma mensagem para o endpoint do Bot Framework com bearer token opcional.
        /// </summary>
        private async Task<HttpResponseMessage> SendWithBearerAsync(string to, string jsonPayload, string? bearerToken)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, to)
            {
                Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
            };

            if (!string.IsNullOrWhiteSpace(bearerToken))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

            return await _httpClient.SendAsync(request);
        }

        /// <inheritdoc />
        /// <remarks>
        /// O Teams não tem conceito de "mark as read" como o WhatsApp.
        /// Mensagens são automaticamente marcadas quando o bot responde.
        /// Retornamos sempre true para manter compatibilidade com a interface.
        /// </remarks>
        public Task<bool> MarkAsReadAsync(string messageId)
        {
            _logger.LogDebug("MarkAsRead não aplicável no Teams (messageId={MessageId})", messageId);
            return Task.FromResult(true);
        }

        /// <inheritdoc />
        /// <remarks>
        /// O parâmetro "to" contém o endpoint completo para responder ao activity original:
        /// {serviceUrl}/v3/conversations/{conversationId}/activities/{activityId}
        /// Este endpoint é construído no WebhookController a partir da Activity recebida.
        /// </remarks>
        public async Task<bool> SendTextMessageAsync(string to, string text, string? replyToMessageId = null)
        {
            try
            {
                string? accessToken = await GetAccessTokenAsync();

                // Em modo autenticado (Teams cloud), deixamos o Connector preencher
                // os metadados de identidade do bot para evitar conflitos de formato
                // (ex: ids com prefixo "28:").
                // Em modo sem token (Emulator/dev), mantemos "from" para compatibilidade.
                object payload;
                if (!string.IsNullOrEmpty(accessToken))
                {
                    payload = new
                    {
                        type = "message",
                        textFormat = "markdown",
                        text = text,
                        replyToId = replyToMessageId
                    };
                }
                else
                {
                    payload = new
                    {
                        type = "message",
                        textFormat = "markdown",
                        text = text,
                        replyToId = replyToMessageId,
                        from = new
                        {
                            id = string.IsNullOrEmpty(_settings.BotId) ? "bot" : _settings.BotId,
                            name = "Bot"
                        }
                    };
                }

                var json = JsonSerializer.Serialize(payload);
                var response = await SendWithBearerAsync(to, json, accessToken);

                // Recuperação automática de autenticação:
                // Se vier 401, forçar refresh do token e tentar 1 vez adicional.
                if (response.StatusCode == HttpStatusCode.Unauthorized && !string.IsNullOrEmpty(accessToken))
                {
                    _logger.LogWarning("Teams API devolveu 401. A invalidar token e tentar renovar uma vez.");
                    InvalidateCachedToken();

                    string? retryToken = await GetAccessTokenAsync();

                    if (!string.IsNullOrEmpty(retryToken))
                    {
                        response = await SendWithBearerAsync(to, json, retryToken);
                    }

                    // Fallback multi-tenant: tenta tenants conhecidos quando o conector
                    // recusa o token (casos comuns: app single-tenant/organizations).
                    if (response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        string currentTenant = string.IsNullOrWhiteSpace(_settings.TenantId)
                            ? "botframework.com"
                            : _settings.TenantId;

                        var fallbackTenants = new[] { "botframework.com", "common", "organizations" }
                            .Where(t => !string.Equals(t, currentTenant, StringComparison.OrdinalIgnoreCase));

                        foreach (string tenant in fallbackTenants)
                        {
                            _logger.LogWarning(
                                "401 persistente. A tentar token no tenant fallback '{Tenant}'.", tenant);

                            string? fallbackToken = await GetAccessTokenForTenantAsync(tenant);
                            if (string.IsNullOrWhiteSpace(fallbackToken))
                                continue;

                            response = await SendWithBearerAsync(to, json, fallbackToken);
                            if (response.StatusCode != HttpStatusCode.Unauthorized)
                                break;
                        }
                    }
                }

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Mensagem Teams enviada com sucesso");
                    return true;
                }
                else
                {
                    string errorBody = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Erro da API Bot Framework: {StatusCode} - {Error}",
                        response.StatusCode, errorBody);
                    ConsoleLogger.Error($"Erro da API Bot Framework: {errorBody}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao enviar resposta Teams");
                ConsoleLogger.Error($"Erro ao enviar resposta Teams: {ex.Message}");
                return false;
            }
        }
        /// <summary>
        /// Faz uma chamada à API do Teams (GetMember) para recuperar os detalhes completos
        /// do membro (incluindo o Email/UserPrincipalName), tal como o TeamsInfo.GetMemberAsync faria.
        /// </summary>
        public async Task<string?> GetUserEmailAsync(string serviceUrl, string conversationId, string memberId)
        {
            try
            {
                string? accessToken = await GetAccessTokenAsync();
                if (string.IsNullOrEmpty(accessToken)) return null;

                string url = $"{serviceUrl.TrimEnd('/')}/v3/conversations/{conversationId}/members/{memberId}";
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("userPrincipalName", out var upnEl) && upnEl.ValueKind == JsonValueKind.String)
                        return upnEl.GetString();
                    if (doc.RootElement.TryGetProperty("email", out var emailEl) && emailEl.ValueKind == JsonValueKind.String)
                        return emailEl.GetString();
                }
                else
                {
                    _logger.LogWarning("Falha ao obter email da API do Teams: {StatusCode}", response.StatusCode);
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter email do Teams para membro {MemberId}", memberId);
                return null;
            }
        }
    }
}
