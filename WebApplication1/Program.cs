using System.Net.Http.Headers;
using System.Threading.RateLimiting;
using Azure.Identity;
using Microsoft.Extensions.Http;
using Polly;
using Polly.Extensions.Http;
using WebApplication1.Api.Middleware;
using WebApplication1.Application;
using WebApplication1.Core.Commands;
using WebApplication1.Core.Interfaces;
using WebApplication1.Infrastructure.Configuration;
using WebApplication1.Infrastructure.ExternalApis;
using WebApplication1.Infrastructure.Messaging;

var builder = WebApplication.CreateBuilder(args);

// Optional local overrides for secrets/config (gitignored).
builder.Configuration.AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);

// ─── Azure Key Vault ─────────────────────────────────────────────────────────
// Lê os segredos do Azure Key Vault quando o URL está configurado.
// Funciona em qualquer ambiente (dev, staging, produção).
var keyVaultUrl = builder.Configuration["KeyVault:Url"];
if (!string.IsNullOrEmpty(keyVaultUrl))
{
    builder.Configuration.AddAzureKeyVault(
        new Uri(keyVaultUrl),
        new DefaultAzureCredential());
}

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMemoryCache();

// ─── Rate Limiting (proteção contra abuso/DDoS) ──────────────────────────────
// Limita o número de pedidos por IP para proteger os endpoints.
// Baseado nas recomendações OWASP para APIs públicas.
builder.Services.AddRateLimiter(options =>
{
    // Política "webhook" — para endpoints que recebem callbacks de plataformas
    // Permite 30 pedidos por minuto por IP (Meta/Teams podem enviar rajadas)
    options.AddPolicy("webhook", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 5
            }));

    // Política "health" — para endpoints de monitorização
    // Mais restritiva: 10 pedidos por minuto (não precisa de mais)
    options.AddPolicy("health", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 2
            }));

    // Resposta quando o limite é atingido
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// ─── Configuração do WhatsApp ────────────────────────────────────────────────
// Mapeia "WhatsApp" do appsettings.json + User Secrets para WhatsAppSettings.
// Os tokens sensíveis (AccessToken, AppSecret) vêm dos User Secrets.
builder.Services.Configure<WhatsAppSettings>(
    builder.Configuration.GetSection("WhatsApp"));

// ─── Configuração do Teams ───────────────────────────────────────────────────
// Mapeia "Teams" do appsettings.json + User Secrets para TeamsSettings.
// O ClientSecret vem dos User Secrets.
builder.Services.Configure<TeamsSettings>(
    builder.Configuration.GetSection("Teams"));

// ─── Serviços de Mensagens Multi-Plataforma ──────────────────────────────────

// WhatsApp Service com HttpClient gerido
// Timeout de 10s evita que chamadas à API Meta bloqueiem 100s (default).
// Sem isto, se a API estiver lenta, o webhook não devolve 200 a tempo,
// o WhatsApp re-entrega, e o cooldown anti-spam de 30s expira → respostas duplicadas.
builder.Services.AddHttpClient<IMessagingService, WhatsAppService>((sp, client) =>
{
    var config = builder.Configuration.GetSection("WhatsApp").Get<WhatsAppSettings>()!;
    client.BaseAddress = new Uri($"https://graph.facebook.com/{config.ApiVersion}/");
    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", config.AccessToken);
    client.Timeout = TimeSpan.FromSeconds(10);
});

// Teams Service com HttpClient gerido
// Timeout mais folgado que WhatsApp porque o fluxo pode incluir:
// 1) obter token OAuth2
// 2) enviar reply ao Connector
// Em dev/túneis, 10s revelou-se curto e gerou TaskCanceledException.
builder.Services.AddHttpClient<IMessagingService, TeamsService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(25);
});

// ─── Fábrica multi-plataforma ────────────────────────────────────────────────
// Recolhe TODOS os IMessagingService registados e permite obter o correto
// por plataforma. Essencial para ter WhatsApp + Teams em simultâneo.
builder.Services.AddSingleton<MessagingServiceFactory>();

// ─── Configuração do servidor de negócio ─────────────────────────────────────
// Mapeia "BusinessApi" do appsettings.json para BusinessApiSettings.
// Se BaseUrl estiver vazio → modo stub (simulação para desenvolvimento).
// Se BaseUrl estiver preenchido → modo real (HTTP ao servidor de negócio).
builder.Services.Configure<BusinessApiSettings>(
    builder.Configuration.GetSection("BusinessApi"));

// ─── Configuração de administração por chat ────────────────────────────────
builder.Services.Configure<AdminSettings>(
    builder.Configuration.GetSection("Admin"));

// ─── Cliente para servidor de negócio (com Polly retry) ──────────────────────
// HttpClient gerido com retry automático para erros transitórios.
// Política: 3 tentativas com backoff exponencial (1s, 2s, 4s).
var businessApiSettings = builder.Configuration
    .GetSection("BusinessApi").Get<BusinessApiSettings>() ?? new BusinessApiSettings();

builder.Services.AddHttpClient<IBusinessApiClient, BusinessApiClient>(client =>
{
    if (!string.IsNullOrWhiteSpace(businessApiSettings.BaseUrl))
        client.BaseAddress = new Uri(businessApiSettings.BaseUrl);

    client.Timeout = TimeSpan.FromSeconds(businessApiSettings.TimeoutSeconds);
})
.AddPolicyHandler(HttpPolicyExtensions
    .HandleTransientHttpError()
    .WaitAndRetryAsync(
        retryCount: businessApiSettings.MaxRetries,
        sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)),
        onRetry: (outcome, timespan, retryAttempt, _) =>
        {
            Console.WriteLine(
                $"⟳ Retry #{retryAttempt} para servidor de negócio após {timespan.TotalSeconds}s — " +
                $"Motivo: {outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString()}");
        }));

// ─── Sistema de comandos ─────────────────────────────────────────────────────
// Cada comando é uma classe isolada. Para adicionar um novo comando:
//   1. Cria uma classe que implementa ICommandHandler
//   2. Regista-a aqui com AddScoped<ICommandHandler, NomeDoComando>()
//   3. O CommandRouter apanha-a automaticamente
builder.Services.AddScoped<ICommandHandler, HelpCommandHandler>();
builder.Services.AddScoped<ICommandHandler, PresencaCommandHandler>();
builder.Services.AddScoped<ICommandHandler, AdminCommandHandler>();
// ↑ Adiciona novos comandos aqui ↑
builder.Services.AddScoped<CommandRouter>();

// ─── Serviço de processamento de mensagens ───────────────────────────────────
// Contém a lógica CORE partilhada por todas as plataformas:
// deduplicação, confirmações, normalização, variações de prompts.
// O controller fica "magro" — apenas HTTP e parsers.
builder.Services.AddScoped<MessageProcessingService>();
builder.Services.AddSingleton<WebhookConcurrencyGuard>();

// ─── Filtros de segurança ────────────────────────────────────────────────────
// Registado como Scoped para poder ser injetado com [ServiceFilter].
builder.Services.AddScoped<ValidateWhatsAppSignatureFilter>();
builder.Services.AddScoped<ValidateTeamsJwtFilter>();
builder.Services.AddScoped<WhatsAppConcurrencyGuardFilter>();
// ─────────────────────────────────────────────────────────────────────────────

var app = builder.Build();

// ─── Configuração de porta para Azure (lê variável PORT do ambiente) ──────────
if (!app.Environment.IsDevelopment())
{
    var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
    app.Urls.Clear();
    app.Urls.Add($"http://0.0.0.0:{port}");
}

// ─── Validação de configuração no arranque (fail-fast) ───────────────────────
// Se alguma configuração obrigatória está em falta, a app NÃO arranca.
// Melhor falhar logo aqui do que descobrir 3 horas depois num erro obscuro.
try
{
    var whatsAppSettings = app.Configuration.GetSection("WhatsApp").Get<WhatsAppSettings>();
    if (whatsAppSettings != null)
        ConfigurationValidator.ValidateWhatsAppSettings(whatsAppSettings);

    var teamsSettings = app.Configuration.GetSection("Teams").Get<TeamsSettings>();
    if (teamsSettings != null)
        ConfigurationValidator.ValidateTeamsSettings(teamsSettings);

    var businessSettings = app.Configuration.GetSection("BusinessApi").Get<BusinessApiSettings>();
    if (businessSettings != null)
        ConfigurationValidator.ValidateBusinessApiSettings(businessSettings, app.Environment.IsDevelopment());

    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("✅ Configuração validada com sucesso.");
    Console.ResetColor();
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"❌ Erro de configuração: {ex.Message}");
    Console.ResetColor();
    // Não bloqueia em Development (permite arrancar com config parcial para testes)
    if (!app.Environment.IsDevelopment())
        throw;
}

// ─── Middleware global de erros (deve ser o PRIMEIRO) ────────────────────────
app.UseMiddleware<ExceptionHandlingMiddleware>();

// ─── Headers de segurança OWASP ─────────────────────────────────────────────
// X-Frame-Options, X-Content-Type-Options, CSP, HSTS, etc.
app.UseMiddleware<SecurityHeadersMiddleware>();

// ─── Correlation ID (rastreamento de pedidos) ────────────────────────────────
// Adiciona um ID único a cada pedido para facilitar debug nos logs.
app.UseMiddleware<CorrelationIdMiddleware>();

// ─── Rate Limiting (proteção contra abuso) ──────────────────────────────────
app.UseRateLimiter();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.MapControllers();

app.Run();

// ─── Necessário para testes de integração com WebApplicationFactory ──────────
// Permite que o projeto de testes aceda ao Program.cs como entry point.
public partial class Program { }
