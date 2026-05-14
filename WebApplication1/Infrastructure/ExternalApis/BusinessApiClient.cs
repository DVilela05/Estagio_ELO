using System.Text;
using System.Text.Json;
using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using WebApplication1.Core.Interfaces;
using WebApplication1.Core.Models;
using WebApplication1.Infrastructure.Configuration;
using WebApplication1.Infrastructure.Logging;

namespace WebApplication1.Infrastructure.ExternalApis
{
    /// <summary>
    /// Cliente HTTP para o servidor de negócio (REST API).
    /// 
    /// Dois modos de operação:
    ///   1. STUB (BaseUrl não configurado) — simula sucesso, para dev/testes
    ///   2. REAL (BaseUrl configurado) — faz HTTP POST/GET ao servidor
    /// 
    /// O modo é determinado automaticamente pela configuração:
    ///   - appsettings.json → BusinessApi:BaseUrl
    ///   - Se vazio → stub
    ///   - Se preenchido → HTTP real
    /// 
    /// Registado com HttpClient gerido + Polly retry para erros transitórios.
    /// </summary>
    public class BusinessApiClient : IBusinessApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly BusinessApiSettings _settings;
        private readonly ILogger<BusinessApiClient> _logger;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        public BusinessApiClient(
            HttpClient httpClient,
            IOptions<BusinessApiSettings> settings,
            ILogger<BusinessApiClient> logger)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<BusinessApiResult> RegisterAttendanceAsync(
            string userId,
            string? userName,
            string attendanceType,
            string platform,
            string? userPhone = null,
            string? userEmail = null)
        {
            // ─── Modo STUB (sem servidor configurado) ────────────────────
            if (_settings.IsStubMode)
            {
                _logger.LogInformation(
                    "📤 [STUB] Registo de presença: User={UserId}, Name={UserName}, Type={Type}, Platform={Platform}",
                    userId, userName, attendanceType, platform);

                await Task.CompletedTask;
                return BusinessApiResult.Ok(
                    "Presença registada com sucesso (modo stub).",
                    isStub: true);
            }

            // ─── Modo REAL (servidor configurado) ────────────────────────
            try
            {
                string attendancePath = NormalizePath(_settings.AttendancePath);

                string? normalizedWhatsAppPhone = null;
                if (string.Equals(platform, "WhatsApp", StringComparison.OrdinalIgnoreCase))
                {
                    string rawPhone = string.IsNullOrWhiteSpace(userPhone) ? userId : userPhone;
                    normalizedWhatsAppPhone = NormalizeWhatsAppPhone(rawPhone);
                }

                var payload = new
                {
                    userId,
                    userName,
                    attendanceType,
                    platform,
                    whatsappPhone = normalizedWhatsAppPhone,
                    teamsEmail = string.Equals(platform, "Teams", StringComparison.OrdinalIgnoreCase)
                        ? (string.IsNullOrWhiteSpace(userEmail) ? userId : userEmail)
                        : userEmail,
                    timestamp = DateTime.UtcNow
                };

                var json = JsonSerializer.Serialize(payload, _jsonOptions);

                _logger.LogInformation(
                    "📤 Enviando registo de presença para {BaseUrl}{AttendancePath}: User={UserId}, Type={Type}",
                    _settings.BaseUrl, attendancePath, userId, attendanceType);

                var response = await SendSignedAsync(HttpMethod.Post, attendancePath, json);

                if (response.IsSuccessStatusCode)
                {
                    string successBody = await response.Content.ReadAsStringAsync();
                    if (!WasProcessingConfirmed(successBody, out string? confirmationMessage))
                    {
                        _logger.LogWarning(
                            "⚠️ Servidor de negócio respondeu 2xx mas sem confirmação de processamento: {Body}",
                            successBody);
                        return BusinessApiResult.Fail(
                            confirmationMessage ?? "Servidor respondeu sem confirmação de processamento.",
                            errorCode: "NOT_PROCESSED");
                    }

                    _logger.LogInformation(
                        "✅ Presença registada no servidor de negócio: User={UserId}, Type={Type}",
                        userId, attendanceType);
                    return BusinessApiResult.Ok("Presença registada com sucesso no servidor.");
                }

                // Tentar ler mensagem de erro do servidor
                string errorBody = await response.Content.ReadAsStringAsync();

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogWarning(
                        "⚠️ Endpoint não encontrado no servidor de negócio: {BaseUrl}{AttendancePath}. Verifica a rota do web service ou BusinessApi:AttendancePath.",
                        _settings.BaseUrl,
                        attendancePath);
                }

                _logger.LogWarning(
                    "⚠️ Servidor de negócio retornou erro: {StatusCode} — {ErrorBody}",
                    response.StatusCode, errorBody);

                return BusinessApiResult.Fail(
                    $"Servidor retornou {(int)response.StatusCode}: {errorBody}",
                    errorCode: response.StatusCode.ToString());
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                _logger.LogError(ex,
                    "⏱️ Timeout ao comunicar com servidor de negócio: User={UserId}", userId);
                return BusinessApiResult.Timeout();
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "🔐 Configuração de segurança inválida para BusinessApi");
                return BusinessApiResult.Fail(ex.Message, errorCode: "SECURITY_CONFIG_INVALID");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex,
                    "🔌 Erro de conexão com servidor de negócio: {Message}", ex.Message);
                return BusinessApiResult.ServiceUnavailable();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "❌ Erro inesperado ao comunicar com servidor de negócio: {Message}", ex.Message);
                return BusinessApiResult.Fail(
                    $"Erro inesperado: {ex.Message}",
                    errorCode: "UNEXPECTED_ERROR");
            }
        }

        private static string NormalizePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return "/api/attendance";

            return path.StartsWith('/') ? path : "/" + path;
        }

        private static string? NormalizeWhatsAppPhone(string? rawPhone)
        {
            if (string.IsNullOrWhiteSpace(rawPhone))
                return null;

            var digits = new string(rawPhone.Where(char.IsDigit).ToArray());
            if (string.IsNullOrWhiteSpace(digits))
                return null;

            // Requisito do serviço de negócio: usar número sem prefixo internacional 351.
            if (digits.StartsWith("351", StringComparison.Ordinal) && digits.Length > 9)
                digits = digits[3..];

            return digits;
        }

        /// <inheritdoc />
        public async Task<BusinessApiResult> GetUserInfoAsync(string userId)
        {
            // ─── Modo STUB ───────────────────────────────────────────────
            if (_settings.IsStubMode)
            {
                _logger.LogInformation("📤 [STUB] Pedido de info do user: {UserId}", userId);
                await Task.CompletedTask;
                return BusinessApiResult.Ok("Utilizador encontrado (stub).", isStub: true);
            }

            // ─── Modo REAL ───────────────────────────────────────────────
            try
            {
                var response = await SendSignedAsync(HttpMethod.Get, $"/api/users/{userId}");

                if (response.IsSuccessStatusCode)
                {
                    string body = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation("✅ Info do utilizador obtida: {UserId}", userId);
                    return BusinessApiResult.Ok(body);
                }

                string errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogWarning(
                    "⚠️ Erro ao obter info do utilizador {UserId}: {StatusCode}",
                    userId, response.StatusCode);

                return BusinessApiResult.Fail(
                    $"Erro {(int)response.StatusCode}: {errorBody}",
                    errorCode: response.StatusCode.ToString());
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "🔐 Configuração de segurança inválida para BusinessApi");
                return BusinessApiResult.Fail(ex.Message, errorCode: "SECURITY_CONFIG_INVALID");
            }
            catch (TaskCanceledException)
            {
                return BusinessApiResult.Timeout();
            }
            catch (HttpRequestException)
            {
                return BusinessApiResult.ServiceUnavailable();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao obter info do utilizador: {UserId}", userId);
                return BusinessApiResult.Fail(ex.Message, "UNEXPECTED_ERROR");
            }
        }

        /// <inheritdoc />
        public async Task<bool> IsAvailableAsync()
        {
            if (_settings.IsStubMode)
                return true;

            try
            {
                var response = await SendSignedAsync(HttpMethod.Get, "/api/ping");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private async Task<HttpResponseMessage> SendSignedAsync(HttpMethod method, string path, string body = "")
        {
            EnsureSecurityConfigured();

            string normalizedPath = NormalizePath(path);
            string timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
            string nonce = Guid.NewGuid().ToString("N");
            string bodyHash = ComputeSha256Base64(body);

            string canonical = string.Join('\n',
                method.Method.ToUpperInvariant(),
                normalizedPath,
                timestamp,
                nonce,
                bodyHash);

            string signature = ComputeHmacBase64(canonical, _settings.HmacSecret!);

            using var request = new HttpRequestMessage(method, normalizedPath);

            if (method != HttpMethod.Get && method != HttpMethod.Head)
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ServiceToken);
            request.Headers.Add("X-Signature-Version", "v1");
            request.Headers.Add("X-Request-Timestamp", timestamp);
            request.Headers.Add("X-Request-Nonce", nonce);
            request.Headers.Add("X-Request-Signature", signature);

            return await _httpClient.SendAsync(request);
        }

        private void EnsureSecurityConfigured()
        {
            if (_settings.IsStubMode)
                return;

            if (!_settings.HasServiceSecurity)
                throw new InvalidOperationException(
                    "BusinessApi em modo real requer ServiceToken e HmacSecret configurados.");
        }

        private static string ComputeSha256Base64(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text ?? string.Empty);
            byte[] hash = SHA256.HashData(bytes);
            return Convert.ToBase64String(hash);
        }

        private static string ComputeHmacBase64(string text, string secret)
        {
            byte[] key = Encoding.UTF8.GetBytes(secret);
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            using var hmac = new HMACSHA256(key);
            byte[] sig = hmac.ComputeHash(bytes);
            return Convert.ToBase64String(sig);
        }

        private static bool WasProcessingConfirmed(string? responseBody, out string? message)
        {
            message = null;

            if (string.IsNullOrWhiteSpace(responseBody))
            {
                message = "Resposta vazia do servidor.";
                return false;
            }

            try
            {
                using var doc = JsonDocument.Parse(responseBody);
                var root = doc.RootElement;

                if (root.ValueKind == JsonValueKind.Object &&
                    root.TryGetProperty("success", out JsonElement successEl) &&
                    (successEl.ValueKind == JsonValueKind.True || successEl.ValueKind == JsonValueKind.False))
                {
                    bool ok = successEl.GetBoolean();
                    if (!ok && root.TryGetProperty("message", out JsonElement msgEl) && msgEl.ValueKind == JsonValueKind.String)
                        message = msgEl.GetString();

                    return ok;
                }

                // Se o contrato ainda não devolver campo success, aceitamos 2xx para retrocompatibilidade.
                return true;
            }
            catch
            {
                // Resposta não-JSON (legado). Mantemos compatibilidade por agora.
                return true;
            }
        }
    }
}
