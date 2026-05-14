# 🚀 Multi-Platform Webhook Integration - Resumo Completo

## 📊 Estado do Projeto

**Versão:** 7.0 (Business API Integration + Polly Retry)
**Testes:** ✅ Todos a passar na última execução (`dotnet test`)
**Build:** 0 errors, 0 warnings  
**Plataformas:** WhatsApp + Microsoft Teams  
**Arquitetura:** Onion Architecture (Core → Application → Infrastructure → Api)  
**Documentação UML:** 11 diagramas PlantUML  
**Segurança:** HMAC-SHA256 + JWT + OWASP Headers + Rate Limiting + Anti-Spam  
**Business API:** Dual-mode (stub/real) com Polly retry  
**Status:** ✅ Production Ready

---

## 🎯 Características Principais (Diferenciais)

### 1. ✨ Sistema de Confirmação Inteligente com Variações
**O QUE É DIFERENTE:**
- **Confirmação obrigatória** antes de executar ações críticas (ex: marcar presença)
- **Mensagens NUNCA se repetem** consecutivamente (6 variações para cada tipo)
- **3 tentativas** com mensagens de ajuda progressivas
- **Detecção de "sim/não órfãos"** (quando não há confirmação pendente)

**FLUXO:**
```
User: "presente"
Bot:  "Iremos executar o pedido de presença. É isso que deseja? (sim/não)"
      [próxima vez: mensagem diferente]

User: "sim"
Bot:  "✅ Mensagem de *presença* recebida com sucesso."

User: "talvez"
Bot:  "Precisas mesmo de ajuda? Responde com SIM ou NÃO (s/n). Tentativas: 2"
      [próxima vez: mensagem diferente]

User: "não"
Bot:  "Ok, cancelado. Se precisares de ajuda, escreve ajuda."
```

**EDGE CASE:** Enviar "sim" após reinício
```
User: "sim" (mas não há confirmação pendente)
Bot:  "⚠️ Não tenho nenhum pedido pendente para confirmar.
       Se enviaste um comando antes, pode ter expirado ou o sistema foi reiniciado.
       Escreve *ajuda* para ver os comandos disponíveis."
      [próxima vez: uma de 6 variações diferentes]
```

---

### 2. 🔐 Segurança Multicamada

**HEADERS DE SEGURANÇA OWASP (SecurityHeadersMiddleware):** ⭐ NOVO v5.0
- Middleware que adiciona automaticamente headers de segurança a TODAS as respostas
- Baseado nas recomendações [OWASP Secure Headers](https://owasp.org/www-project-secure-headers/)

| Header | Valor | Proteção |
|--------|-------|----------|
| `X-Frame-Options` | `DENY` | Clickjacking |
| `X-Content-Type-Options` | `nosniff` | MIME type sniffing |
| `X-XSS-Protection` | `0` | XSS Auditor desativado (OWASP recomenda) |
| `Referrer-Policy` | `strict-origin-when-cross-origin` | Vazamento de URLs |
| `Permissions-Policy` | `camera=(), microphone=(), geolocation=(), payment=()` | APIs do browser |
| `Content-Security-Policy` | `default-src 'none'; frame-ancestors 'none'` | Injeção de conteúdo |
| `Strict-Transport-Security` | `max-age=31536000; includeSubDomains` | Forçar HTTPS (só prod) |
| `Server` | *(removido)* | Esconder stack tecnológico |
| `X-Powered-By` | *(removido)* | Esconder stack tecnológico |

**RATE LIMITING (Limitação de Taxa):** ⭐ NOVO v5.0
- Limita requests por IP para prevenir abuso/DDoS
- Política `webhook`: 30 requests/minuto por IP (endpoints `/api/webhook/*`)
- Política `health`: 10 requests/minuto por IP (endpoint `/health`)
- Retorna **429 Too Many Requests** quando excedido
- Implementação: `System.Threading.RateLimiting` com `FixedWindowRateLimiter`

**LIMITE DE TAMANHO DE REQUEST:** ⭐ NOVO v5.0
- Todos os endpoints POST limitados a **1 MB** (`[RequestSizeLimit(1_048_576)]`)
- Previne payloads maliciosas extra-grandes

**VALIDAÇÃO HMAC-SHA256 (WhatsApp):**
- Filtro `ValidateWhatsAppSignatureFilter` como Action Filter
- Header `X-Hub-Signature-256` validado em **TODOS** os POST
- Usa `AppSecret` para verificar autenticidade
- **Rejeita com 401** se assinatura inválida
- **Modo Dev:** warning se AppSecret não configurado (não bloqueia)

**VALIDAÇÃO JWT (Teams):**
- Filtro `ValidateTeamsJwtFilter` como Action Filter
- Valida tokens JWT contra OpenID Connect metadata do Bot Framework
- Verifica `issuer`, `audience` (BotId) e expiração
- **Modo Development:** bypass automático para Bot Framework Emulator
- **Produção:** rejeita tokens inválidos com **401 Unauthorized**

**IDENTIFICAÇÃO DE UTILIZADORES:**
- `IncomingMessage.UserId` — ID único (AAD ObjectId para Teams, telefone para WhatsApp)
- `IncomingMessage.UserName` — Nome de exibição do utilizador
- Campos usados para audit logging e rastreamento

**CORRELATION ID (CorrelationIdMiddleware):**
- Middleware que adiciona um GUID único (`X-Correlation-ID`) a CADA pedido HTTP
- Se o pedido já traz o header → reutiliza (propagação entre sistemas)
- Se não → gera `Guid.NewGuid()`
- O ID aparece em TODOS os logs do pedido via `logger.BeginScope()`
- Devolvido no header da resposta para referência do cliente
- Essencial para investigação de incidentes e auditoria (SOC2, ISO 27001)

**DEDUPLICAÇÃO:**
- `ConcurrentDictionary<messageId, timestamp>` em memória
- Janela de **5 minutos** para ignorar duplicatas
- Meta envia **múltiplos webhooks** (message + status + delivery) → só processa o primeiro

> 📖 Para documentação completa de segurança (tipos de ataque, exemplos, métodos), ver [SECURITY.md](SECURITY.md)

**USER SECRETS:**
- Credenciais **NUNCA** em appsettings.json
- Usa `dotnet user-secrets` (fora do git)
- Validação **pré-startup** com mensagens claras

---

### 2.5. 🛡️ Proteção Anti-Spam (Guard + Lock + Grace) ⭐ ATUALIZADO

**O QUE FAZ:**
Previne duplicação de mensagens quando o utilizador carrega repetidamente no botão "enviar" (comum em WhatsApp e Teams).

**MECANISMO:**
- **Deduplicação por `MessageId` (5 min)** no webhook
- **Lock por remetente (5s fallback)** no webhook
- **Libertação explícita de lock** no `finally` após processamento
- **SentAt + grace (1s)** — proteção adicional contra rajadas com timestamp arredondado
- **Delayed unlock (2s)** no processamento quando há spam detetado

**FLUXO:**
```
1. User envia comando válido → processa e responde
2. User envia burst rápido (`l l l l`) → mensagens extra bloqueadas por lock do remetente
3. Resposta enviada → lock libertado e ciclo reinicia
4. Próxima mensagem realmente nova → processada normalmente
```

**PONTOS-CHAVE:**
- Guard no webhook antes da action
- Idempotência por `MessageId` (5 min)
- Lock por remetente com fallback de 5s
- `MarkAsReadAsync` chamado cedo
- Confirmação pendente com bypass apenas para `sim`/`não`

**TESTES:** 23 testes (19 métodos) em `UserProcessingLockTests.cs`

**RESULTADO:**
- ✅ WhatsApp: utilizador carrega 5× "ajuda" → recebe 1 resposta
- ✅ Teams: mesma proteção
- ✅ Cooldown não afeta respostas legítimas (reset ao responder)

---

### 3. 🧪 Cobertura de Testes

**DISTRIBUIÇÃO:**
```
📁 Handlers & Routing (22 testes)
   ├─ CommandRouter: 4 testes
   ├─ HelpCommandHandler: 6 testes  
   └─ PresencaCommandHandler: 12 testes (15 inputs via Theory)

📁 Models (26 testes)
   ├─ IncomingMessage: 9 testes (inclui UserId/UserName)
   ├─ MessagePlatform: 5 testes (WhatsApp + Teams)
   ├─ WhatsAppSettings: 3 testes
   ├─ TeamsSettings: 3 testes
   └─ TeamsActivity: 8 testes (DTOs Bot Framework)

📁 Security Filters (13 testes)
   ├─ ValidateTeamsJwtFilter: 7 testes (JWT + OpenID Connect)
   └─ ValidateWhatsAppSignatureFilter: 6 testes (HMAC-SHA256)

📁 Middleware (4 testes)
   └─ ExceptionHandlingMiddleware: 4 testes (dev/prod)

📁 Services (2 testes)
   └─ MessagingServiceFactory: 2 testes

📁 Helpers (10 testes)
   ├─ TextNormalization: 10 testes (11 inputs via Theory)

📁 Per-User Processing Lock (23 testes, 19 métodos) ⭐ NOVO v6.0
   └─ UserProcessingLockTests: 23 testes (lock, unlock, multi-user, null safety, ciclos)

📁 Confirmações Contextuais (8 testes) ⭐ NOVO v6.0
   └─ ContextualConfirmationTests: 8 testes (presença mencionada, cancelamento, Teams)

📁 Exceptions (6 testes)
   └─ CustomExceptions: 6 testes (4 tipos)

📁 Configuration (18 testes)
   ├─ ConfigurationValidator (WhatsApp): 10 testes (Theory com whitespace)
   └─ TeamsConfigurationValidator: 8 testes (Theory com whitespace)

📁 Confirmation Edge Cases (12 testes)
   └─ ConfirmationEdgeCases: 12 testes
      ├─ Yes/No tokens: 6 inputs
      ├─ All variants: sim/s/yes/y, não/nao/n/no
      └─ Non-confirmation text: 4 inputs

📁 Integration (30 testes restantes)
```

**DESTAQUES:**
- ✅ **Theory attributes** para input variado (ex: 15 variações de "presente")
- ✅ **Edge cases:** emojis, pontuação, números, case-insensitivity, whitespace
- ✅ **Null safety:** validação de nulls e strings vazias
- ✅ **Security filters:** HMAC-SHA256 e JWT completamente testados
- ✅ **MockHostEnvironment pattern:** para testar código dependente de IHostEnvironment
- ✅ **Middleware testing:** ExceptionHandlingMiddleware com cenários dev/prod
- ✅ **User identification:** UserId e UserName testados para ambas plataformas
- ✅ **Integração HTTP end-to-end:** WebApplicationFactory com mocks (WhatsApp, Teams, Health) ⭐ NOVO v5.0
- ✅ **Security Headers OWASP:** 10 testes verificam presença de todos os headers ⭐ NOVO v5.0
- ✅ **Per-User Processing Lock:** 23 testes (19 métodos) de TryLockUser/UnlockUser/IsUserLocked ⭐ NOVO v6.0
- ✅ **Confirmações Contextuais:** 8 testes verificam menção do comando ⭐ NOVO v6.0
- ✅ **100% pass rate** na última execução

---

### 4. 📚 Swagger/OpenAPI Completo

**ACESSO:** http://localhost:5197/ (Development)

**CARACTERÍSTICAS:**
- ✅ Interface interativa para testar endpoints
- ✅ Metadata completo (title, version, contact)
- ✅ Suporte para XML comments (documentação inline)
- ✅ Modelos de request/response documentados
- ✅ "Try it out" direto no browser

**ENDPOINTS DOCUMENTADOS:**
```
GET  /api/webhook/whatsapp     - Verificação do webhook (Meta handshake)
POST /api/webhook/whatsapp     - Receber mensagens WhatsApp (com HMAC validation)
POST /api/webhook/teams        - Receber Activities do Teams (Bot Framework)
GET  /health                   - Health check
```

---

### 5. 📊 Serilog - Logging Estruturado

**DUAL OUTPUT:**
- **Console:** Logs coloridos em tempo real
- **File:** JSON estruturado com rolling diário

**LOCALIZAÇÃO:** `WebApplication1/logs/webhook-YYYY-MM-DD.txt`

**FORMATO:**
```
[2026-02-18 16:18:17] [INF] 🚀 Iniciando aplicação...
[2026-02-18 16:18:17] [INF] ✅ Configuração WhatsApp validada com sucesso
[2026-02-18 16:18:17] [INF] 🔧 Configurando middleware...
[2026-02-18 16:18:17] [INF] 📚 Swagger disponível em http://localhost:5197/
[2026-02-18 16:18:17] [INF] ✅ Aplicação iniciada com sucesso
[2026-02-18 16:18:17] [INF] Mensagem recebida de 351932947533: "Presente"
[2026-02-18 16:18:18] [INF] Comando "presença" acionado por 351932947533
[2026-02-18 16:18:18] [INF] ✅ Mensagem processada em 1250ms | Status=true → true
```

**NÍVEIS:**
- `Information` → Operações normais
- `Warning` → Validações falhadas, retries, "sim/não órfãos"
- `Error` → Erros recuperáveis
- `Fatal` → Erros críticos (ex: configuração inválida ao startup)

**ROLLING:** Ficheiro novo a cada dia, automático

---

### 6. ⚙️ Validação de Configuração Pré-Startup

**VALIDA CAMPOS OBRIGATÓRIOS:**

**WhatsApp (5 campos):**
```csharp
WhatsApp:AccessToken     → Token da Graph API
WhatsApp:AppSecret       → Secret para HMAC
WhatsApp:VerifyToken     → Token de verificação do webhook
WhatsApp:PhoneNumberId   → ID do número de telefone
WhatsApp:ApiVersion      → Versão da API (ex: v22.0)
```

**Teams (4 campos):**
```csharp
Teams:BotId              → ID do bot (App Registration)
Teams:ClientSecret       → Secret do bot (User Secrets)
Teams:TenantId           → Tenant ID (default: "botframework.com")
Teams:LoginUrl           → URL de autenticação OAuth2
```

**COMPORTAMENTO:**
- ❌ Se **qualquer campo** estiver null/vazio → **App não inicia**
- ✅ **Mensagem clara** indica qual secret falta
- ✅ **Comando de correção** é sugerido:

```
ConfigurationException: 
WhatsApp:AccessToken não configurado.

Configure com:
  dotnet user-secrets set "WhatsApp:AccessToken" "seu_token_aqui"

Ou em appsettings.json se for não-sensível.
```

---

### 7. 🚨 Exceções Personalizadas (4 tipos)

**TIPO 1: InvalidCommandException**
```csharp
throw new InvalidCommandException("Comando 'xyz' não reconhecido");
```
**Quando:** Comando não existe ou formato inválido

**TIPO 2: WebhookVerificationException**
```csharp
throw new WebhookVerificationException("Assinatura HMAC-SHA256 inválida");
```
**Quando:** Validação de webhook falha

**TIPO 3: ConfigurationException**
```csharp
throw new ConfigurationException("WhatsApp:AccessToken não configurado...");
```
**Quando:** Erro de configuração durante startup

**TIPO 4: MessageProcessingException**
```csharp
throw new MessageProcessingException("Falha ao enviar resposta", innerException);
```
**Quando:** Erro durante processamento (ex: Graph API falha)

**BENEFÍCIO:**
- ✅ Tratamento específico por tipo de erro
- ✅ InnerException preservada para debugging
- ✅ Mensagens amigáveis ao utilizador
- ✅ Logging categorizado

---

### 8. 🔄 Normalização de Texto Inteligente

**PIPELINE:**
```
Input:  "🎉 Presente!!! 😊"
  ↓ Remove emojis e pontuação
  ↓ Converte para minúsculas
  ↓ Normaliza espaços
Output: "presente"
```

**REGEX:**
```csharp
text = Regex.Replace(text, @"[^\p{L}\s]", " ");  // Remove tudo exceto letras e espaços (incl. números)
text = text.ToLowerInvariant();                        // Minúsculas
text = Regex.Replace(text, @"\s+", " ").Trim();        // Espaços
```

**RESULTADO:**
- ✅ "Presente!!!" = "presente"
- ✅ "🎉 PRESENTE 🎉" = "presente"
- ✅ "pReSenÇa" = "presença"
- ✅ "  cá   estou  " = "cá estou"
- ✅ "teste123" = "teste" (números removidos)

---

### 9. 🌍 Multi-Plataforma (WhatsApp + Teams)

**ABSTRAÇÃO:**
```csharp
interface IMessagingService
{
    MessagePlatform Platform { get; }
    Task<bool> SendTextMessageAsync(string to, string text);
    Task<bool> MarkAsReadAsync(string messageId);
}
```

**IMPLEMENTAÇÕES:**
- ✅ `WhatsAppService` → WhatsApp Business API (implementado)
- ✅ `TeamsService` → Microsoft Teams Bot Framework (implementado)
- 🔜 `TelegramService` → Telegram Bot API (futuro)

**FACTORY PATTERN:**
```csharp
MessagingServiceFactory.Create(MessagePlatform.WhatsApp)
MessagingServiceFactory.Create(MessagePlatform.Teams)
```

**CORE CONTROLLER (Unified):**
```csharp
class WebhookController
├── ProcessMessage()          // Lógica partilhada
├── IsDuplicateMessage()      // Deduplicação
├── NormalizeText()            // Normalização
├── IsYes() / IsNo()           // Confirmação
├── BuildConfirmationPrompt()  // Variações
├── GetNextPrompt()            // Anti-repetição
├── POST /api/webhook/whatsapp → Parse WhatsApp JSON + HMAC
└── POST /api/webhook/teams    → Parse Bot Framework Activity + RemoveBotMention
```

**BENEFÍCIO:** Um único controller com toda a lógica de processamento e endpoints para cada plataforma

---

### 10. 🎨 Console Logger Bonito

**CAIXAS COLORIDAS:**
```
┌────────────────────────────────────────┐
│ 📨 MENSAGEM RECEBIDA                   │
├────────────────────────────────────────┤
│ De:         351932947533               │
│ Plataforma: WhatsApp                   │
│ Hora:       2026-02-18 16:18:17        │
│ Mensagem:   Presente                   │
└────────────────────────────────────────┘

✅ Marcado como lido: ✓

┌────────────────────────────────────────┐
│ 📤 RESPOSTA ENVIADA                    │
├────────────────────────────────────────┤
│ Para:       351932947533               │
│ Status:     ✓ Sucesso                  │
└────────────────────────────────────────┘
```

**CORES:**
- 🟢 Verde → Sucesso
- 🔵 Azul → Info
- 🟡 Amarelo → Warning
- 🔴 Vermelho → Erro

---

## 🏗️ Arquitetura (Patterns Implementados)

### ✅ Command Pattern
```
CommandRouter
├─ HelpCommandHandler
└─ PresencaCommandHandler
   (fácil adicionar: AusenciaCommandHandler, HorarioCommandHandler, etc.)
```

### ✅ Factory Pattern
```
MessagingServiceFactory
├─ WhatsAppService (IMessagingService)
└─ TeamsService (IMessagingService)
```

### ✅ Strategy Pattern
```
ICommandHandler
├─ CanHandle(message)
└─ ExecuteAsync(message)
```

### ✅ Unified Controller Pattern
```
WebhookController (core)
├─ ProcessMessage()  → lógica partilhada
├─ POST /whatsapp    → Parse WhatsApp + HMAC
└─ POST /teams       → Parse Bot Framework + RemoveMention
```

### ✅ Repository Pattern (dual-mode ⭐ v7.0)
```
IBusinessApiClient
├─ RegisterAttendanceAsync() ← dual-mode (stub se BaseUrl vazio, HTTP real se configurado)
├─ GetUserInfoAsync()        ← consulta informação do utilizador
└─ IsAvailableAsync()        ← verifica disponibilidade do servidor

BusinessApiClient (Infrastructure)
├─ IOptions<BusinessApiSettings>  ← configuração (BaseUrl, Timeout, MaxRetries)
├─ HttpClient + Polly retry       ← exponential backoff (2s, 4s, 8s)
└─ BusinessApiResult              ← factory: Ok/Fail/Timeout/ServiceUnavailable
```

### ✅ Middleware Pattern
```
Pipeline:
  ExceptionHandlingMiddleware
  → SecurityHeadersMiddleware        ⭐ NOVO v5.0 (OWASP headers)
  → CorrelationIdMiddleware
  → RateLimiterMiddleware             ⭐ NOVO v5.0 (30/min webhook, 10/min health)
  → ValidateWhatsAppSignatureFilter (WhatsApp POST)
  → ValidateTeamsJwtFilter (Teams POST)
  → Controller
```

---

## 📁 Estrutura de Ficheiros (Onion Architecture)

```
WebApplication1/                              # Onion Architecture
├── Api/                                      # 🔵 Camada de Apresentação
│   ├── Controllers/
│   │   ├── WebhookController.cs              ← Core controller (WhatsApp + Teams endpoints)
│   │   └── HealthController.cs               ← Health check simples
│   └── Middleware/
│       ├── CorrelationIdMiddleware.cs         ← Tracking de requests
│       ├── ExceptionHandlingMiddleware.cs     ← Global error handling
│       ├── SecurityHeadersMiddleware.cs       ← Headers OWASP ⭐ v5.0
│       ├── ValidateTeamsJwtFilter.cs          ← JWT Bot Framework ⭐
│       └── ValidateWhatsAppSignatureFilter.cs ← HMAC-SHA256 ⭐
│
├── Application/                              # 🟢 Camada de Aplicação
│   └── MessageProcessingService.cs           ← Orquestração (anti-spam, confirmações contextuais, dedup)
│
├── Core/                                     # 🟡 Camada de Domínio (innermost)
│   ├── Commands/
│   │   ├── ICommandHandler.cs                ← Interface base
│   │   ├── CommandRouter.cs                  ← Roteamento + logging
│   │   ├── HelpCommandHandler.cs             ← Lista comandos
│   │   └── PresencaCommandHandler.cs         ← 15 triggers diferentes
│   ├── Interfaces/
│   │   ├── IMessagingService.cs              ← Abstração multi-plataforma
│   │   └── IBusinessApiClient.cs             ← Cliente servidor negócio (dual-mode: stub/real) ⭐
│   ├── Models/
│   │   ├── BusinessApiResult.cs              ← Resultado da API (Success/Fail/Timeout) ⭐ v7.0
│   │   ├── IncomingMessage.cs                ← Modelo universal (UserId + UserName) ⭐
│   │   └── MessagePlatform.cs                ← Enum (WhatsApp, Teams)
│   └── Exceptions/
│       └── ApplicationExceptions.cs          ← 4 exceções custom
│
├── Infrastructure/                           # 🔴 Camada de Infraestrutura
│   ├── Configuration/
│   │   ├── BusinessApiSettings.cs            ← Config Business API (BaseUrl, Timeout, MaxRetries) ⭐ v7.0
│   │   ├── ConfigurationValidator.cs         ← Validação pré-startup (WhatsApp + Teams) ⭐
│   │   ├── WhatsAppSettings.cs               ← Config WhatsApp
│   │   └── TeamsSettings.cs                  ← Config Teams
│   ├── Messaging/
│   │   ├── WhatsAppService.cs                ← Implementação WhatsApp
│   │   ├── TeamsService.cs                   ← Implementação Teams (Bot Framework)
│   │   ├── MessagingServiceFactory.cs        ← Factory multi-plataforma
│   │   └── TeamsActivity.cs                  ← DTOs Bot Framework Activity
│   ├── ExternalApis/
│   │   └── BusinessApiClient.cs              ← Cliente API de negócio (dual-mode + Polly retry) ⭐ v7.0
│   └── Logging/
│       └── ConsoleLogger.cs                  ← Logging formatado (WhatsApp 🟢 / Teams 🟣)
│
├── appsettings.json
├── appsettings.Development.json
├── appsettings.Production.json               ← Logging restrito ⭐
└── Program.cs                                ← DI + Serilog + Validação + Rate Limiting + Polly + BusinessApi ⭐

WebApplication1.Tests/                        # 26 ficheiros (total de testes dinâmico)
├── BusinessApiResultTests.cs                 ← 7 testes (BusinessApiResult factory) ⭐ v7.0
├── BusinessApiClientTests.cs                 ← 18 testes (dual-mode + Polly) ⭐ v7.0
├── CommandRouterTests.cs                     ← 4 testes
├── HelpCommandHandlerTests.cs                ← 6 testes
├── PresencaCommandHandlerTests.cs            ← 12 testes
├── TextNormalizationTests.cs                 ← 10 testes
├── IncomingMessageTests.cs                   ← 9 testes (incl. UserId/UserName) ⭐
├── MessagePlatformTests.cs                   ← 5 testes (WhatsApp + Teams)
├── MessagingServiceFactoryTests.cs           ← 2 testes
├── WhatsAppSettingsTests.cs                  ← 3 testes
├── TeamsSettingsTests.cs                     ← 3 testes
├── TeamsActivityTests.cs                     ← 8 testes
├── TeamsConfigurationValidatorTests.cs       ← 8 testes
├── CustomExceptionsTests.cs                  ← 6 testes
├── ConfigurationValidatorTests.cs            ← 10 testes
├── ConfirmationEdgeCasesTests.cs             ← 12 testes
├── UserProcessingLockTests.cs                ← 23 testes ⭐ v6.0
├── ContextualConfirmationTests.cs            ← 8 testes ⭐ v6.0
├── ValidateTeamsJwtFilterTests.cs            ← 7 testes ⭐
├── ValidateWhatsAppSignatureFilterTests.cs   ← 6 testes ⭐
├── ExceptionHandlingMiddlewareTests.cs       ← 4 testes ⭐
├── Integration/                              ← ⭐ Testes de integração HTTP v5.0
│   ├── CustomWebApplicationFactory.cs        ← Factory com mocks
│   ├── WhatsAppIntegrationTests.cs          ← 14 testes (GET/POST)
│   ├── TeamsIntegrationTests.cs             ← 6 testes (POST Teams)
│   ├── HealthIntegrationTests.cs            ← 4 testes (GET /health)
│   └── SecurityHeadersIntegrationTests.cs   ← 10 testes (OWASP headers) ⭐
└── WebApplication1.Tests.csproj

Architecture-UML/                             # 11 diagramas PlantUML
├── 01_C4_Level1_Context.puml                 ← Diagrama C4 nível 1 (contexto) ⭐ v7.0
├── logical-view.puml                         ← Vista lógica (Onion layers)
├── physical-view.puml                        ← Vista física (deployment)
├── use-cases.puml                            ← Casos de uso
├── sequence-whatsapp.puml                    ← Sequência WhatsApp
├── sequence-teams.puml                       ← Sequência Teams
├── sequence-security.puml                    ← Validação segurança
├── state-confirmation.puml                   ← Estados confirmação
├── activity-processing.puml                  ← Atividade processamento
├── classes-commands.puml                     ← Classes Command Pattern
└── components-middleware.puml                ← Componentes middleware
```

**⭐ = Adicionado na v4.0–7.0 (Onion + Security + Rate Limiting + OWASP + Business API + Polly)**

---

## 🎯 Comandos Suportados

### 1. PRESENÇA (15 variações)
**Português:**
- `presente`, `presença`, `presenca`
- `marcar presença`, `cá estou`, `estou cá`, `cheguei`

**English:**
- `present`, `attendance`, `mark attendance`
- `check in`, `i'm here`, `im here`, `here`, `arrived`

**Respostas (conforme modo):**
- ✅ Stub: `✅ Mensagem de *presença* recebida com sucesso.`
- ✅ Real: `✅ Presença registada com sucesso!`
- ⏱️ Timeout: `⏱️ O servidor demorou a responder...`
- 🔌 Indisponível: `🔌 O servidor de registo está temporariamente indisponível...`
- ⚠️ Erro: `⚠️ Houve um problema ao registar a presença...`

### 2. AJUDA (6 variações)
**Triggers:**
- `ajuda`, `help`, `?`, `menu`, `command`, `commands`

**Resposta:**
```
📚 Comandos Disponíveis:
───────────────────
▸ *presença* — Marcar presença (ex: "presente", "cá estou", "present")
   _Escreve:_ presente, presença, presenca, ...

▸ *ajuda* — Ver este menu
   _Escreve:_ ajuda, help, ?, menu

───────────────────
💡 _Escreve qualquer um dos comandos acima para começar._
```

### 3. CONFIRMAÇÃO (sim/não)
**Yes tokens:** `sim`, `s`, `yes`, `y`  
**No tokens:** `não`, `nao`, `n`, `no`

---

## 🔧 Tecnologias & Packages

```json
{
  "framework": ".NET 8.0",
  "language": "C# 12",
  "testing": "xUnit 2.4.2",
  "logging": {
    "Serilog": "4.3.1",
    "Serilog.AspNetCore": "10.0.0",
    "Serilog.Sinks.File": "7.0.0"
  },
  "api-docs": "Swashbuckle.AspNetCore 6.x",
  "http": "System.Net.Http + IHttpClientFactory",
  "di": "Microsoft.Extensions.DependencyInjection",
  "config": "Microsoft.Extensions.Configuration (User Secrets)"
}
```

---

## 📊 Métricas do Projeto

| Métrica | Valor |
|---------|-------|
| **Linhas de Código** | ~6000+ |
| **Ficheiros C#** | 48 (22 src + 26 tests) |
| **Testes** | Todos a passar na última execução (`dotnet test`) |
| **Cobertura Estimada** | ~95% |
| **Controllers** | 2 (WebhookController + HealthController) |
| **Handlers** | 2 |
| **Services** | 4 (WhatsApp + Teams + Factory + BusinessApi) |
| **Middlewares** | 5 (Correlation + Exception + SecurityHeaders + JWT + HMAC) |
| **Rate Limiting** | 2 políticas (webhook: 30/min, health: 10/min) |
| **Custom Exceptions** | 4 |
| **Endpoints** | 4 |
| **Plataformas** | 2 (WhatsApp + Teams) |
| **Diagramas UML** | 11 PlantUML |
| **Documentação .md** | 8 ficheiros (incl. SECURITY.md) |
| **Arquitetura** | Onion (4 camadas) |
| **Segurança** | HMAC + JWT + OWASP Headers + Rate Limiting + Anti-Spam guard/lock/grace |
| **Build Warnings** | 0 |
| **Build Errors** | 0 |

---

## 🚀 Comandos Úteis

```bash
# Build
dotnet build

# Testes
dotnet test
dotnet test --filter "PresencaCommandHandler"
dotnet test --verbosity normal

# Executar
dotnet run

# User Secrets
dotnet user-secrets set "WhatsApp:AccessToken" "seu_token"
dotnet user-secrets list
dotnet user-secrets remove "WhatsApp:AccessToken"

# Teams
dotnet user-secrets set "Teams:ClientSecret" "seu_secret"

# Publish
dotnet publish -c Release

# Logs
cat logs/webhook-2026-02-18.txt
tail -f logs/webhook-*.txt
```

---

## 🎯 Diferenciais Competitivos

### 1. ✅ NUNCA repete mensagens consecutivas
Sistema de rotação com Queue previne monotonia

### 2. ✅ Segurança production-ready
HMAC-SHA256 + JWT + OWASP Security Headers + Rate Limiting + Request Size Limits

### 3. ✅ Testabilidade completa
Suite completa com Theory, edge cases, null safety, security filters, HTTP end-to-end, Business API

### 4. ✅ Logging profissional
Dual output (console + file) com rolling diário

### 5. ✅ Fail-fast com mensagens claras
Validação pré-startup com instruções de correção

### 6. ✅ Arquitetura extensível
Fácil adicionar comandos, plataformas, handlers

### 7. ✅ UX refinada
Edge cases tratados (ex: "sim" sem confirmação pendente)

### 8. ✅ Documentação automática
Swagger UI interativo out-of-the-box

### 9. ✅ Zero warnings, zero errors
Código limpo e bem estruturado

### 10. ✅ Multi-idioma natural
Português + English para todos os comandos

### 11. ✅ Onion Architecture
Separação clara em 4 camadas (Core → Application → Infrastructure → Api)

### 12. ✅ Documentação UML completa
11 diagramas PlantUML cobrindo vistas lógica, física, sequência, estados, componentes e contexto C4

### 13. ✅ Identificação de utilizadores
UserId + UserName para audit logging e rastreamento cross-platform

### 14. ✅ OWASP Security Headers ⭐ NOVO v5.0
9 headers de segurança automáticos em todas as respostas HTTP

### 15. ✅ Rate Limiting ⭐ NOVO v5.0
Proteção contra abuso com limites por IP (30/min webhook, 10/min health)

### 16. ✅ Testes de Integração HTTP ⭐ NOVO v5.0
34 testes end-to-end com WebApplicationFactory (WhatsApp, Teams, Health, Security Headers)

### 17. ✅ Anti-Spam Inteligente ⭐ ATUALIZADO
Dedup 5 min + lock por remetente (5s fallback) + grace de `SentAt` (1s) + delayed unlock (2s)

### 18. ✅ Confirmações Contextuais ⭐ NOVO v6.0
Prompts de confirmação referenciam o comando específico (ex: "*presença*", "*ajuda*")

---

## 📝 Próximos Passos (Roadmap)

### Curto Prazo
- [ ] Persistir confirmações em Redis (em vez de memória)
- [ ] Timeout de 5 minutos para confirmações
- [ ] Comando "cancelar" para sair do modo confirmação

### Médio Prazo
- [ ] Handler para faltas/ausências
- [ ] Handler para horários
- [ ] Relatórios via comando (ex: "relatorio semanal")

### Longo Prazo
- [x] ~~Integração com Microsoft Teams~~ ✅ (v3.0)
- [ ] Integração com Telegram
- [ ] Dashboard web para gestão
- [ ] Notificações proativas

---

**Status Final:** ✅ **PRODUCTION READY**  
**Versão:** 8.x (WhatsApp Web anti-spam hardening)  
**Data:** 16/03/2026  
**Testes a passar** | **0 Errors no analisador** | **2 Plataformas (WhatsApp + Teams)** | **11 Diagramas UML** | **OWASP Headers** | **Rate Limiting** | **Anti-Spam guard/lock/grace** | **Business API**

