# Multi-Platform Webhook Integration Service

Serviço ASP.NET Core 8.0 para integração com a **WhatsApp Business API** e **Microsoft Teams** (Bot Framework). Recebe mensagens, processa comandos e envia respostas automáticas com confirmação de ações.

## 🚀 Quick Start

### Pré-requisitos
- .NET 8.0 SDK ou superior
- **WhatsApp**: WhatsApp Business Account com App aprovado + Credenciais da Meta Graph API
- **Teams**: Bot registado no Azure Bot Service + Client Secret

> Este repositório inclui `global.json` com SDK `8.0.414` para garantir consistência de build/testes em máquinas com múltiplos SDKs.

### Instalação

1. Clone/abra o repositório
```bash
cd c:\Users\diogo\Desktop\Estagio_ELO
```

2. Configure os secrets locais
```bash
cd WebApplication1

# WhatsApp
dotnet user-secrets set "WhatsApp:AccessToken" "seu_access_token_aqui"
dotnet user-secrets set "WhatsApp:AppSecret" "seu_app_secret_aqui"

# Teams
dotnet user-secrets set "Teams:BotId" "seu_app_id_guid"
dotnet user-secrets set "Teams:ClientSecret" "seu_client_secret_aqui"
dotnet user-secrets set "Teams:TenantId" "seu_tenant_guid"

# Opcional: WCF Webservice
# O WCF envia as strings em Base64 diretamente a partir dos handlers.
```

Se quiseres usar o webservice real (e nao o modo stub), tambem precisas
configurar o webservice com os mesmos segredos:

```bash
cd D:\estagio\WebServerEstagioELO_\WebServerEstagioELO\WebServerEstagioElo
dotnet user-secrets set "AttendanceWebhookSecurity:ServiceToken" "seu_service_token"
dotnet user-secrets set "AttendanceWebhookSecurity:HmacSecret" "seu_hmac_secret"
```

3. Compile o projeto
```bash
dotnet build
```

4. Execute
```bash
dotnet run
```

O serviço estará disponível em `http://localhost:5197`

### Configuração Teams + Azure (passo a passo)

Para o Teams funcionar corretamente (auth, reply e eventos), os IDs têm de estar alinhados entre Azure, manifesto e `user-secrets`:

1. **Microsoft Entra ID (App Registration)**
  - Criar app registration para o bot.
  - Guardar:
    - `Application (client) ID` → `Teams:BotId`
    - `Directory (tenant) ID` → `Teams:TenantId`
  - Criar **Client Secret** → `Teams:ClientSecret`.

2. **Azure Bot Service**
  - Definir o mesmo `Microsoft App ID` do passo 1.
  - Ativar o canal **Microsoft Teams**.
  - Messaging endpoint do bot:
    - `https://<dominio-ou-tunnel>/api/webhook/teams`

3. **Manifesto da App Teams**
  - `bots[].botId` = mesmo GUID de `Teams:BotId`.
  - Incluir `personal` em `scopes`.
  - (Opcional para eventos de leitura) adicionar RSC `ChatMessageReadReceipt.Read.Chat`.

4. **App local (user-secrets)**
```bash
dotnet user-secrets set "Teams:BotId" "<APP_ID_GUID>"
dotnet user-secrets set "Teams:ClientSecret" "<CLIENT_SECRET>"
dotnet user-secrets set "Teams:TenantId" "<TENANT_GUID>"
dotnet user-secrets list
```

5. **Reinstalar/Atualizar app no Teams**
  - Sempre que o manifesto mudar, atualizar/reinstalar a app no cliente Teams.
  - Testar em **Teams Web** e **Desktop**.

> Dica: se `RecipientId` nos logs não contiver o `Teams:BotId`, há mismatch entre app instalada e bot configurado.

---

## 📋 Funcionalidades

### Comandos Suportados

#### 1. **Presença/Attendance** (Português + English)
Marca presença/attendance do utilizador.

**Triggers:**
- 🇵🇹 `presente`, `presença`, `presenca`, `marcar presença`, `cá estou`, `estou cá`, `cheguei`
- 🇬🇧 `present`, `attendance`, `mark attendance`, `check in`, `i'm here`, `im here`, `here`, `arrived`

**Respostas (conforme modo):**
- ✅ Stub: `✅ Mensagem de *presença* recebida com sucesso.`
- ✅ Real: `✅ Presença registada com sucesso!`
- ⏱️ Timeout: `⏱️ O servidor demorou a responder...`
- 🔌 Indisponível: `🔌 O servidor de registo está temporariamente indisponível...`
- ⚠️ Erro genérico: `⚠️ Houve um problema ao registar a presença...`

#### 2. **Ajuda/Help**
Lista os comandos disponíveis.

**Triggers:**
- `ajuda`, `help`, `?`, `menu`, `command`, `commands`

**Resposta:**
```
📚 Comandos Disponíveis:
• presença - Marcar presença
• ajuda - Ver este menu
```

### Fluxo de Confirmação Contextual

Para ações críticas, o sistema solicita confirmação **referenciando o comando específico**:

1. **Utilizador envia comando**
   ```
   user: "presente"
   ```

2. **Sistema pede confirmação (contextual — menciona o comando)**
   ```
   service: "✅ Vamos executar *presença*. É mesmo isso que pretendes? (sim/não)"
   ```

3. **Utilizador confirma**
   ```
   user: "sim"
   ```

4. **Sistema executa**
   ```
   service: "✅ mensagem de *presença* recebida com sucesso!"
   ```

Máximo de 3 tentativas. Prompts randomizados para evitar monotonia.
As mensagens de ajuda e cancelamento também referenciam o comando: `"❌ Ok, *presença* cancelado."`
Na **3ª tentativa inválida** (resposta diferente de `sim/não`), o sistema cancela a confirmação, explica o esperado e sugere usar `ajuda`.

### Proteção Anti-Spam e Concorrência ⭐ ATUALIZADO

Mecanismo atual implementado no código:

| Camada | Mecanismo | Valor |
|---|---|---|
| `WhatsAppConcurrencyGuardFilter` + `WebhookConcurrencyGuard` | Deduplicação por `MessageId` | 5 minutos |
| `WhatsAppConcurrencyGuardFilter` + `WebhookConcurrencyGuard` | Lock por remetente (`from`) | 5 segundos (fallback) |
| `WebhookController` | Libertação explícita de lock no `finally` | após processamento |
| `MessageProcessingService` | Filtro `SentAt` com grace | +1s |
| `MessageProcessingService` | Delayed unlock quando há spam | 2s |

Comportamento esperado para burst do mesmo número:
1. A primeira mensagem é aceite.
2. Mensagens seguintes do mesmo número, durante processamento, são ignoradas.
3. Após resposta enviada, o lock é libertado e um novo ciclo começa.

---

## 🔧 Endpoints

### WhatsApp

#### GET `/api/webhook/whatsapp`
Verifica e valida o webhook junto da Meta.

**Query Parameters:**
- `hub.mode` - "subscribe"
- `hub.challenge` - Token de verificação
- `hub.verify_token` - Token de autenticação

**Exemplo:**
```
GET http://localhost:5197/api/webhook/whatsapp?hub.mode=subscribe&hub.challenge=1234567890&hub.verify_token=estagioelo2026
```

#### POST `/api/webhook/whatsapp`
Recebe mensagens de entrada do WhatsApp.

**Headers obrigatórios:**
- `X-Hub-Signature-256` - HMAC-SHA256 da payload (validação de segurança)

**Body exemplo:**
```json
{
  "object": "whatsapp_business_account",
  "entry": [
    {
      "changes": [
        {
          "value": {
            "messages": [
              {
                "from": "554199999999",
                "id": "wamid.123456789",
                "timestamp": "1708335600",
                "text": {
                  "body": "presente"
                },
                "type": "text"
              }
            ]
          }
        }
      ]
    }
  ]
}
```

### Teams

#### POST `/api/webhook/teams`
Recebe Activities do Microsoft Teams via Bot Framework.

**Body exemplo (Activity):**
```json
{
  "type": "message",
  "id": "activity-id-123",
  "timestamp": "2026-02-18T15:30:00Z",
  "serviceUrl": "https://smba.trafficmanager.net/emea/",
  "channelId": "msteams",
  "from": {
    "id": "29:user-id",
    "name": "Diogo"
  },
  "conversation": {
    "id": "a]concat:id-1234",
    "tenantId": "tenant-id-here"
  },
  "text": "<at>BotName</at> presente"
}
```

**Nota:** O serviço remove automaticamente a menção `<at>...</at>` do texto antes de processar o comando.

#### Eventos de leitura (read receipts)
- O endpoint também aceita eventos `application/vnd.microsoft.readReceipt`.
- Estes eventos são de **observação** (o bot recebe), não de “marcar lido” manual.

#### Limitações visuais no cliente Teams
- O estado visual de leitura (ex.: “olhinho/seen”) é controlado pelo **cliente/política Teams**, não pelo bot.
- O bot envia replies com `replyToId` e endpoint contextual, mas o cliente Teams pode renderizar diferente do WhatsApp.

### Health

#### GET `/health`
Verificação de saúde do serviço.

**Resposta:**
```json
{
  "status": "Healthy",
  "timestamp": "2026-02-18T15:30:00Z"
}
```

---

## 🛡️ Segurança

### Validação de Assinatura WhatsApp (HMAC-SHA256)
Todas as mensagens POST do WhatsApp são validadas com HMAC-SHA256 usando `AppSecret` através do filtro `ValidateWhatsAppSignatureFilter`. Mensagens inválidas são rejeitadas com **401 Unauthorized**.
- Header `X-Hub-Signature-256` obrigatório em todos os POST
- Em modo Development sem AppSecret configurado: warning (não bloqueia)

### Validação JWT Teams
Mensagens POST do Teams são validadas com JWT através do filtro `ValidateTeamsJwtFilter`:
- Valida tokens JWT contra OpenID Connect metadata do Bot Framework
- Verifica `issuer`, `audience` (BotId) e expiração do token
- **Modo Development:** bypass automático para permitir testes com Bot Framework Emulator
- Em produção: rejeita tokens inválidos com **401 Unauthorized**

### Identificação de Utilizadores
O modelo `IncomingMessage` inclui campos de identificação:
- `UserId` — ID único na plataforma (AAD ObjectId para Teams, número de telefone para WhatsApp)
- `UserName` — Nome de exibição (disponível no Teams, nem sempre no WhatsApp)

### Security Headers (OWASP)
O middleware `SecurityHeadersMiddleware` adiciona automaticamente headers de segurança a **todas** as respostas HTTP, seguindo as recomendações [OWASP](https://owasp.org/www-project-secure-headers/):

| Header | Valor | Proteção |
|--------|-------|----------|
| `X-Frame-Options` | `DENY` | Clickjacking |
| `X-Content-Type-Options` | `nosniff` | MIME type sniffing |
| `X-XSS-Protection` | `0` | XSS Auditor desativado (recomendação moderna) |
| `Referrer-Policy` | `strict-origin-when-cross-origin` | Vazamento de URLs |
| `Permissions-Policy` | `camera=(), microphone=(), geolocation=(), payment=()` | APIs do browser |
| `Content-Security-Policy` | `default-src 'none'; frame-ancestors 'none'` | Injeção de conteúdo |
| `Strict-Transport-Security` | `max-age=31536000; includeSubDomains` | Forçar HTTPS (só produção) |

Adicionalmente, remove os headers `Server` e `X-Powered-By` para não revelar informação do stack tecnológico.

### Rate Limiting (Limitação de Taxa)
O sistema limita o número de requests por IP para prevenir abuso:

| Política | Limite | Janela | Endpoints |
|----------|--------|--------|-----------|
| `webhook` | 30 requests | 1 minuto | `/api/webhook/*` |
| `health` | 10 requests | 1 minuto | `/health` |

Quando o limite é excedido, retorna **429 Too Many Requests**.

### Limite de Tamanho de Request
Todos os endpoints POST têm um limite de **1 MB** (`[RequestSizeLimit(1_048_576)]`) para prevenir payloads maliciosas.

### Deduplicação
Mensagens duplicadas (mesmo `MessageId`) são ignoradas numa janela de 5 minutos.

### User Secrets
Credenciais sensíveis são armazenadas em User Secrets, **nunca** em appsettings.json:

```bash
# WhatsApp secrets
dotnet user-secrets set "WhatsApp:AccessToken" "seu_token"
dotnet user-secrets set "WhatsApp:AppSecret" "seu_secret"

# Teams secrets
dotnet user-secrets set "Teams:ClientSecret" "seu_client_secret"

# Listar secrets
dotnet user-secrets list

# Remover
dotnet user-secrets remove "WhatsApp:AccessToken"
```

### Correlation ID (Rastreamento de Pedidos)
O `CorrelationIdMiddleware` adiciona um GUID único (`X-Correlation-ID`) a cada pedido HTTP:
- Se o pedido já traz o header, reutiliza (propagação entre sistemas).
- Caso contrário, gera um novo `Guid.NewGuid()`.
- O GUID aparece **em todos os logs** desse pedido (via `logger.BeginScope`).
- É devolvido no header da resposta para o cliente poder referenciar.
- Essencial para investigação de incidentes de segurança e auditoria.

> Para documentação completa de segurança (ataques, exemplos, métodos), consulta [SECURITY.md](SECURITY.md).

---

## 🧪 Testes

### Executar Todos os Testes
```bash
cd WebApplication1.Tests
dotnet test
```

### Com Verbosidade
```bash
dotnet test --verbosity normal
```

### Teste Específico
```bash
dotnet test --filter "PresencaCommandHandlerTests"
```

**Cobertura:**
- **26 ficheiros de teste** (total dinâmico; validar com `dotnet test`)
- 100% cobertura de handlers, modelos, configuração, filtros de segurança e middleware
- Testes de integração HTTP end-to-end (WhatsApp, Teams, Health, Security Headers)
- Edge cases incluídos (emojis, pontuação, números, case-insensitivity)
- Testes para WhatsApp e Teams
- Filtros de segurança testados (HMAC-SHA256 + JWT)
- Headers OWASP verificados (10 testes de security headers)
- User Processing Lock: 23 testes (lock, unlock, concorrência, multi-utilizador) ⭐ NOVO
- Confirmações contextuais: 8 testes (presença mencionada, cancelamento, Teams) ⭐ NOVO

---

## 📁 Estrutura do Projeto (Onion Architecture)

```
WebApplication1/                          # Onion Architecture
├── Api/                                  # 🔵 Camada de Apresentação (outermost)
│   ├── Controllers/
│   │   ├── WebhookController.cs          # Core controller (WhatsApp + Teams endpoints)
│   │   └── HealthController.cs           # Health check
│   └── Middleware/
│       ├── CorrelationIdMiddleware.cs     # Tracking de requests
│       ├── ExceptionHandlingMiddleware.cs # Global error handling
│       ├── SecurityHeadersMiddleware.cs   # ⭐ Headers OWASP (v5.0)
│       ├── ValidateTeamsJwtFilter.cs      # JWT validation (Bot Framework)
│       └── ValidateWhatsAppSignatureFilter.cs # HMAC-SHA256 validation
│
├── Application/                          # 🟢 Camada de Aplicação
│   └── MessageProcessingService.cs       # Orquestração (anti-spam, confirmações, dedup)
│
├── Core/                                 # 🟡 Camada de Domínio (innermost)
│   ├── Commands/
│   │   ├── ICommandHandler.cs            # Interface base
│   │   ├── CommandRouter.cs              # Roteador de comandos
│   │   ├── HelpCommandHandler.cs         # Handler de ajuda
│   │   └── PresencaCommandHandler.cs     # Handler de presença
│   ├── Interfaces/
│   │   ├── IMessagingService.cs          # Interface de messaging
│   │   └── IBotLocalizer.cs              # Contrato para traduções e localização
│   ├── Models/
│   │   ├── SupportedLanguage.cs          # Enumeração de línguas suportadas (PT, EN, FR, ES)
│   │   ├── IncomingMessage.cs            # Modelo universal (UserId + UserName)
│   │   └── MessagePlatform.cs            # Enum (WhatsApp, Teams)
│   └── Exceptions/
│       └── ApplicationExceptions.cs      # 4 excepções personalizadas
│
├── Infrastructure/                       # 🔴 Camada de Infraestrutura
│   ├── Configuration/
│   │   ├── WebServiceSettings.cs         # Configuração WCF API
│   │   ├── ConfigurationValidator.cs     # Validação pré-startup
│   │   ├── WhatsAppSettings.cs           # Config WhatsApp
│   │   └── TeamsSettings.cs              # Config Teams
│   ├── Messaging/
│   │   ├── WhatsAppService.cs            # Implementação WhatsApp
│   │   ├── TeamsService.cs               # Implementação Teams (Bot Framework)
│   │   ├── MessagingServiceFactory.cs    # Factory multi-plataforma
│   │   └── TeamsActivity.cs              # DTOs Bot Framework Activity
│   ├── ExternalApis/
│   │   └── LanguageDetector.cs           # Detetor de língua (indicativo telefone + triggers)
│   └── Logging/
│       └── ConsoleLogger.cs              # Logging formatado
│
├── appsettings.json                      # Config pública (WhatsApp + Teams)
├── appsettings.Development.json          # Dev-specific
├── appsettings.Production.json           # Production (logging restrito)
└── Program.cs                            # DI + Serilog + Validação + WCF Config + i18n
└── WebApplication1.csproj

WebApplication1.Tests/                    # 26 ficheiros de teste (total dinâmico)
├── BusinessApiClientTests.cs              # ⭐ Testes cliente Business API (v7.0)
├── BusinessApiResultTests.cs              # ⭐ Testes resultado Business API (v7.0)
├── CommandRouterTests.cs
├── HelpCommandHandlerTests.cs
├── PresencaCommandHandlerTests.cs
├── TextNormalizationTests.cs
├── IncomingMessageTests.cs               # Inclui testes UserId/UserName
├── MessagePlatformTests.cs
├── MessagingServiceFactoryTests.cs
├── WhatsAppSettingsTests.cs
├── TeamsSettingsTests.cs
├── TeamsActivityTests.cs
├── TeamsConfigurationValidatorTests.cs
├── CustomExceptionsTests.cs
├── ConfigurationValidatorTests.cs
├── ConfirmationEdgeCasesTests.cs
├── UserProcessingLockTests.cs            # ⭐ Per-user processing lock (23 testes)
├── ContextualConfirmationTests.cs        # ⭐ Confirmações contextuais (8 testes)
├── ValidateTeamsJwtFilterTests.cs        # ⭐ Testes filtro JWT Teams
├── ValidateWhatsAppSignatureFilterTests.cs # ⭐ Testes filtro HMAC WhatsApp
├── ExceptionHandlingMiddlewareTests.cs   # ⭐ Testes middleware de erros
├── Integration/                          # ⭐ Testes de integração HTTP (v5.0)
│   ├── CustomWebApplicationFactory.cs    # Factory com mocks para testes HTTP
│   ├── WhatsAppIntegrationTests.cs       # 14 testes (GET/POST WhatsApp)
│   ├── TeamsIntegrationTests.cs          # 6 testes (POST Teams)
│   ├── HealthIntegrationTests.cs         # 4 testes (GET /health)
│   └── SecurityHeadersIntegrationTests.cs # ⭐ 10 testes (headers OWASP)
└── WebApplication1.Tests.csproj

Architecture-UML/                         # 11 diagramas PlantUML
├── 01_C4_Level1_Context.puml             # ⭐ Diagrama C4 nível 1 (v7.0)
├── logical-view.puml                     # Vista lógica (Onion layers)
├── physical-view.puml                    # Vista física (deployment)
├── use-cases.puml                        # Casos de uso
├── sequence-whatsapp.puml                # Sequência WhatsApp
├── sequence-teams.puml                   # Sequência Teams
├── sequence-security.puml                # Sequência validação segurança
├── state-confirmation.puml               # Estados confirmação
├── activity-processing.puml              # Atividade processamento
├── classes-commands.puml                 # Classes Command Pattern
└── components-middleware.puml            # Componentes middleware
```

---

## 🔌 Configuração das Plataformas

### WhatsApp Business

1. Aceda a [Meta App Dashboard](https://developers.facebook.com/apps/)
2. Vá para **WhatsApp > Configuration**
3. Em **Webhook URL**, configure:
   ```
   https://seu-dominio.com/api/webhook/whatsapp
   ```
4. **Verify Token**: `estagioelo2026` (configurável em appsettings.json)
5. Em **Webhook fields**, subscreva: `messages`, `message_status`, `message_template_status_update`

### Microsoft Teams

1. Aceda ao [Azure Bot Service](https://portal.azure.com/)
2. Crie um **Azure Bot** com o Bot ID configurado em appsettings.json
3. Em **Configuration > Messaging Endpoint**, configure:
   ```
   https://seu-dominio.com/api/webhook/teams
   ```
4. Adicione o canal **Microsoft Teams**
5. Configure o Client Secret via User Secrets:
   ```bash
   dotnet user-secrets set "Teams:ClientSecret" "seu_client_secret"
   ```
6. O bot autentica-se via **OAuth2 client_credentials** com o Bot Framework

### Business API (Servidor de Negócio)

O bot comunica com um servidor de negócio para registar presenças. Opera em **modo dual**:

- **Modo Stub** (default): Quando `BusinessApi:BaseUrl` está vazio, retorna sucesso simulado
- **Modo Real**: Quando `BusinessApi:BaseUrl` está configurado, faz chamadas HTTP reais com retry (Polly)

**Configuração em `appsettings.json`:**
```json
{
  "BusinessApi": {
    "BaseUrl": "",
    "TimeoutSeconds": 30,
    "MaxRetries": 3
  }
}
```

**Polly Retry:** Retry automático com exponential backoff (2s, 4s, 8s) para falhas transitórias (HTTP 5xx, timeout).

---

## 📊 Logging com Serilog

Sistema de logging estruturado com **Serilog** que escreve para console e ficheiros:

### Localizações de Logs
- **Console**: Output em tempo real com cores
- **Ficheiros**: `logs/webhook-{data}.txt` com rolling diário (JSON estruturado)

### Configuração
```csharp
// Program.cs - Serilog inicializado automaticamente
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File(
        "logs/webhook-.txt",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
    )
    .CreateLogger();
```

### Exemplo de Log Estruturado
```json
{
  "Timestamp": "2026-02-18T15:30:45.123+00:00",
  "Level": "Information",
  "MessageTemplate": "Message processed from {PhoneNumber}",
  "Properties": {
    "PhoneNumber": "554199999999",
    "CorrelationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "ElapsedMs": 45
  }
}
```

### Eventos Registados
- `Log.Information()` - Operações normais (startup, processamento)
- `Log.Warning()` - Validações falhadas, tentativas de retentativa
- `Log.Error()` - Erros recuperáveis
- `Log.Fatal()` - Erros críticos ao iniciar

---

## 🚨 Excepções Personalizadas

Sistema de excepções específicas para melhor tratamento de erros:

### InvalidCommandException
Lançada quando um comando não é reconhecido ou é inválido.

```csharp
throw new InvalidCommandException(
    "Comando 'xyz' não reconhecido",
    innerException: originalError
);
```

**Quando ocorre:**
- Comando não existe
- Comando tem formato inválido
- Falta de argumentos obrigatórios

### WebhookVerificationException
Lançada quando a verificação de webhook falha.

```csharp
throw new WebhookVerificationException(
    "Assinatura HMAC-SHA256 inválida"
);
```

**Quando ocorre:**
- Assinatura de mensagem inválida
- Token de verificação incorreto
- Payload corrompida

### ConfigurationException
Lançada quando há problemas na configuração durante o startup.

```csharp
throw new ConfigurationException(
    "WhatsApp:AccessToken não configurado. " +
    "Configure com: dotnet user-secrets set \"WhatsApp:AccessToken\" \"seu_token\""
);
```

**Quando ocorre:**
- Secrets não configurados
- Valores inválidos (null, vazios)
- Valores de ambiente inválidos

### MessageProcessingException
Lançada durante o processamento de mensagens.

```csharp
throw new MessageProcessingException(
    "Falha ao enviar resposta para WhatsApp",
    innerException: apiError
);
```

**Quando ocorre:**
- API Graph retorna erro
- Timeout na resposta
- Falha ao serializar/deserializar JSON

---

## ✔️ Validação de Configuração

Sistema de validação pré-startup que garante que todos os secrets obrigatórios estão configurados:

### Campos Obrigatórios Validados
```csharp
// Helper/ConfigurationValidator.cs
ConfigurationValidator.ValidateWhatsAppSettings(whatsappSettings);
ConfigurationValidator.ValidateTeamsSettings(teamsSettings);
```

**WhatsApp** — Valida:
- ✅ `WhatsApp:AccessToken` - Token de acesso da Graph API
- ✅ `WhatsApp:AppSecret` - Secret da app
- ✅ `WhatsApp:VerifyToken` - Token para verificação de webhook
- ✅ `WhatsApp:PhoneNumberId` - ID do número de telefone
- ✅ `WhatsApp:ApiVersion` - Versão da API (ex: "v18.0")

**Teams** — Valida:
- ✅ `Teams:BotId` - ID do bot (App Registration)
- ✅ `Teams:ClientSecret` - Secret do bot (User Secrets)
- ✅ `Teams:TenantId` - Tenant ID (default: "botframework.com")
- ✅ `Teams:LoginUrl` - URL de autenticação OAuth2

### Comportamento
Se algum campo está **null** ou **vazio**:

1. **Startup falha** com `ConfigurationException`
2. **Mensagem amigável** indica qual secret falta
3. **Comandos de correção** são sugeridos:

```
ConfigurationException: 
WhatsApp:AccessToken não configurado.

Configure com:
  dotnet user-secrets set "WhatsApp:AccessToken" "seu_token_aqui"

Ou em appsettings.json se for não-sensível.
```

### Exemplo de Validação em Código
```csharp
// Program.cs
try
{
    var whatsappSettings = configuration
        .GetSection("WhatsApp")
        .Get<WhatsAppSettings>()
        ?? throw new ConfigurationException("WhatsApp section not found");
    
    ConfigurationValidator.ValidateWhatsAppSettings(whatsappSettings);
    Log.Information("WhatsApp configuration validated successfully");
}
catch (ConfigurationException ex)
{
    Log.Fatal(ex, "Configuration validation failed");
    throw;
}
```

---

## 📚 Swagger/OpenAPI Documentation

API documentation automática com interface interativa.

### Aceder à Documentação
```
http://localhost:5197/
```

### Funcionalidades
- 📖 Especificação OpenAPI 3.0
- 🧪 Testar endpoints diretamente no browser
- 📋 Modelos de request/response documentados
- 🔑 Autenticação integrada
- 📱 Responsivo para mobile

### Metadados Configurados
```csharp
// Program.cs
services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "WhatsApp Webhook Integration Service",
        Version = "v1",
        Description = "API para integração com WhatsApp Business",
        Contact = new()
        {
            Name = "ELO Internship",
            Url = new Uri("https://exemplo.com")
        }
    });
    
    // XML comments do código aparecem na documentação
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath);
});
```

### Exemplo de Documentação em Código
```csharp
/// <summary>
/// Recebe mensagens de entrada do WhatsApp
/// </summary>
/// <param name="request">Payload do webhook com mensagens</param>
/// <returns>Status da operação</returns>
[HttpPost("whatsapp")]
public async Task<IActionResult> ReceiveMessage(
    [FromBody] WebhookRequest request)
{
    // ... implementação
}
```

---

## 📊 Logging (Original)

Logs coloridos no console com:
- ⏱️ Tempo de execução (ElapsedMs)
- 🆔 Correlation ID para rastreamento
- 📱 Plataforma de origem
- 👤 Número de telefone
- 💬 Conteúdo da mensagem

**Exemplo:**
```
[INFO] WebhookController - ReceiveMessage: Message from 554199999999 (presente) processed in 45ms [Correlation: 3fa85f64-5717-4562-b3fc-2c963f66afa6]
```

---

## � Documentação UML

O projeto inclui **11 diagramas PlantUML** na pasta `Architecture-UML/`:

### Diagramas Gerais
| Diagrama | Descrição |
|----------|-----------|
| `01_C4_Level1_Context.puml` | Diagrama C4 nível 1 — visão de contexto ⭐ |
| `logical-view.puml` | Vista lógica da Onion Architecture (4 camadas) |
| `physical-view.puml` | Vista física de deployment |
| `use-cases.puml` | Casos de uso do sistema (WhatsApp + Teams) |

### Diagramas Específicos
| Diagrama | Descrição |
|----------|-----------|
| `sequence-whatsapp.puml` | Fluxo completo de mensagem WhatsApp |
| `sequence-teams.puml` | Fluxo completo de mensagem Teams |
| `sequence-security.puml` | Validação de segurança (HMAC + JWT) |
| `state-confirmation.puml` | Máquina de estados da confirmação |
| `activity-processing.puml` | Atividade de processamento de mensagem |
| `classes-commands.puml` | Diagrama de classes (Command Pattern) |
| `components-middleware.puml` | Componentes do pipeline middleware |

Para visualizar, use a extensão **PlantUML** do VS Code ou o site [plantuml.com](https://www.plantuml.com/).

---

## 🚀 Deploy

### Azure App Service

Em produção, a app lê a variável `PORT` e faz bind em `http://0.0.0.0:{PORT}`.
Se `PORT` não existir, usa `8080`.

```bash
dotnet publish -c Release
# Upload pasta bin/Release/net8.0/publish/
```

### Docker
```bash
docker build -t whatsapp-webhook .
docker run -e "WhatsApp:AccessToken=seu_token" -p 80:8080 whatsapp-webhook
```

### Local IIS
```bash
dotnet publish -c Release
# Copiar para wwwroot e configurar Application Pool
```

---

## 🐛 Troubleshooting

### "Invalid signature"
- Verifica se o `AppSecret` está correcto
- HMAC-SHA256 é case-sensitive

### "Webhook verification failed"
- Confirma o `VerifyToken` em appsettings.json
- Meta envia requests de teste, verifica os logs

### "Message processing timeout"
- Aumenta o timeout em Program.cs se necessário
- Verifica a latência da Graph API

---

## 📝 Licença

Projeto educacional - 2026

---

## 👨‍💻 Autor

Desenvolvido como projeto de estágio ELO
