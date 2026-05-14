# 🎓 How to Use - Quick Start Guide

Guia rápido de como usar cada uma das features implementadas.

---

## 🚀 Quick Start

### 1. Clone e Configure
```bash
cd c:\Users\diogo\Desktop\Estagio_ELO
cd WebApplication1

# WhatsApp secrets
dotnet user-secrets set "WhatsApp:AccessToken" "seu_token_aqui"
dotnet user-secrets set "WhatsApp:AppSecret" "seu_secret_aqui"

# Teams secrets
dotnet user-secrets set "Teams:BotId" "seu_app_id_guid"
dotnet user-secrets set "Teams:ClientSecret" "seu_client_secret_aqui"
dotnet user-secrets set "Teams:TenantId" "seu_tenant_guid"

# BusinessApi (modo real - opcional)
dotnet user-secrets set "BusinessApi:BaseUrl" "http://localhost:5008"
dotnet user-secrets set "BusinessApi:ServiceToken" "seu_service_token"
dotnet user-secrets set "BusinessApi:HmacSecret" "seu_hmac_secret"
```

### 2. Execute a App
```bash
dotnet run
```

**Output esperado:**
```
[16:16:15 INF] 🚀 Iniciando aplicação...
[16:16:16 INF] ✅ Configuração WhatsApp validada com sucesso
[16:16:16 INF] ✅ Configuração Teams validada com sucesso
[16:16:16 INF] 🔧 Configurando middleware...
[16:16:16 INF] 📚 Swagger disponível em http://localhost:5197/
[16:16:16 INF] ✅ Aplicação iniciada com sucesso
Now listening on: http://localhost:5197
```

### 3. Aceda ao Swagger UI
```
http://localhost:5197/
```

---

## ✅ Setup Checklist (completo)

### 1) WebApplication1 (bot)
- `appsettings.json`
  - `WhatsApp:VerifyToken`, `WhatsApp:PhoneNumberId`, `WhatsApp:ApiVersion`
  - `Teams:BotId`, `Teams:TenantId` (pode ser override), `Teams:LoginUrl`, `Teams:Scope`
  - `BusinessApi:BaseUrl`, `BusinessApi:AttendancePath`, `BusinessApi:AllowInsecureHttp`
- `appsettings.local.json` (ou user-secrets)
  - `WhatsApp:AccessToken`, `WhatsApp:AppSecret`
  - `Teams:ClientSecret`, `Teams:TenantId` (se override)
  - `BusinessApi:ServiceToken`, `BusinessApi:HmacSecret`

### 2) WebServerEstagioELO (webservice)
- `appsettings.json`
  - `AttendanceWebhookSecurity:AllowedClockSkewMinutes`
- user-secrets (ou config local equivalente)
  - `AttendanceWebhookSecurity:ServiceToken`
  - `AttendanceWebhookSecurity:HmacSecret`

### 3) Meta (WhatsApp)
- App → **WhatsApp > Configuration**
  - Callback URL: `https://<tunnel>/api/webhook/whatsapp`
  - Verify Token: igual a `WhatsApp:VerifyToken`
  - Phone Number ID: igual a `WhatsApp:PhoneNumberId`
- WhatsApp Manager → **Phone numbers**
  - Numero de teste ativo
  - Testers adicionados e aceites (modo dev)
- Access Token Tool
  - Permissoes: `whatsapp_business_management`, `whatsapp_business_messaging`

### 4) Teams (Azure + Teams Dev Portal)
- Azure Bot → **Configuration**
  - Messaging endpoint: `https://<tunnel>/api/webhook/teams`
- Entra ID → App Registration
  - `Application (client) ID` = `Teams:BotId`
  - `Directory (tenant) ID` = `Teams:TenantId`
  - `Client Secret` valido = `Teams:ClientSecret`
- Teams Developer Portal
  - Manifest `bots[].botId` = `Teams:BotId`
  - Reinstalar app no Teams apos mudar o manifesto

### 5) Infra local
- Devtunnel/Port forward da porta 5197 ativo (HTTPS)
- Bot a correr (`dotnet run` em WebApplication1)
- Webservice a correr (`dotnet run` em WebServerEstagioELO)

---

## 📌 Onde buscar cada parametro (e o que muda por PC)

### WhatsApp (Meta)
- `WhatsApp:AccessToken` → Meta Developer → Tools → Access Token Tool
- `WhatsApp:AppSecret` → Meta Developer → App → Settings → Basic → App Secret
- `WhatsApp:VerifyToken` → definido no projeto em [WebApplication1/appsettings.json](WebApplication1/appsettings.json)
- `WhatsApp:PhoneNumberId` → Meta Developer → App → WhatsApp → Configuration → Phone Numbers
- Callback URL (webhook) → Meta Developer → App → WhatsApp → Configuration → Webhook (usa o tunnel)

### Teams (Azure/Entra)
- `Teams:BotId` → Entra ID → App registrations → App do bot → Application (client) ID
- `Teams:TenantId` → Entra ID → App registrations → App do bot → Directory (tenant) ID
- `Teams:ClientSecret` → Entra ID → App registrations → App do bot → Certificates & secrets → New client secret (copiar o Value)
- Messaging endpoint → Azure Bot → Configuration → Messaging endpoint (usa o tunnel)

### BusinessApi (Bot)
- `BusinessApi:BaseUrl` → URL do webservice (ex.: http://localhost:5008)
- `BusinessApi:ServiceToken` → segredo definido no webservice (ver abaixo)
- `BusinessApi:HmacSecret` → segredo definido no webservice (ver abaixo)

### Webservice (WebServerEstagioELO)
- `AttendanceWebhookSecurity:ServiceToken` → gerado por ti (GUID/token aleatorio)
- `AttendanceWebhookSecurity:HmacSecret` → gerado por ti (string aleatoria longa, Base64)

### O que muda entre PCs (dev)
- O **tunnel** muda (Forwarded Address do VS Code) → atualiza Meta e Azure Bot
- `appsettings.local.json` nao vem do git → leva contigo se mudares de PC
- `user-secrets` sao locais do Windows → exporta/recria noutro PC

---

## 🟣 Configurar Teams + Azure (end-to-end)

### 1) Entra ID (App Registration)
- Criar App Registration.
- Copiar:
  - `Application (client) ID` → `Teams:BotId`
  - `Directory (tenant) ID` → `Teams:TenantId`
- Criar `Client Secret` → `Teams:ClientSecret`.

### 2) Azure Bot Service
- Definir o mesmo `Microsoft App ID`.
- Ativar canal **Microsoft Teams**.
- Definir Messaging endpoint:
  - `https://<dominio-ou-tunnel>/api/webhook/teams`

### 3) Manifest da App Teams
- `bots[].botId` = `Teams:BotId`.
- `scopes` deve incluir `personal`.
- Opcional (evento de leitura): RSC `ChatMessageReadReceipt.Read.Chat`.

### 4) Secrets locais
```bash
dotnet user-secrets set "Teams:BotId" "<APP_ID_GUID>"
dotnet user-secrets set "Teams:ClientSecret" "<CLIENT_SECRET>"
dotnet user-secrets set "Teams:TenantId" "<TENANT_GUID>"
dotnet user-secrets list
```

---

## 🟢 BusinessApi real (bot + webservice)

O bot so fala com o webservice real se `BusinessApi:BaseUrl` estiver definido.
Para funcionar com seguranca, os dois lados precisam do mesmo `ServiceToken`
e do mesmo `HmacSecret`.

### 1) Bot (WebApplication1)
```bash
dotnet user-secrets set "BusinessApi:BaseUrl" "http://localhost:5008"
dotnet user-secrets set "BusinessApi:ServiceToken" "seu_service_token"
dotnet user-secrets set "BusinessApi:HmacSecret" "seu_hmac_secret"
```

### 2) Webservice (WebServerEstagioELO)
```bash
cd D:\estagio\WebServerEstagioELO_\WebServerEstagioELO\WebServerEstagioElo
dotnet user-secrets set "AttendanceWebhookSecurity:ServiceToken" "seu_service_token"
dotnet user-secrets set "AttendanceWebhookSecurity:HmacSecret" "seu_hmac_secret"
```

### 3) Dica de geracao de segredos
- ServiceToken: um GUID ou token aleatorio longo
- HmacSecret: 32+ bytes aleatorios em Base64

### 5) Validar nos logs
No log de diagnóstico Teams, confirmar:
- `RecipientId` contém o `Teams:BotId`
- token OAuth retorna `200`
- envio para `smba.trafficmanager.net` retorna `201`

---

## 👁️ Read receipts e contexto de resposta no Teams (importante)

- O bot **não força** o “olhinho” no Teams como no WhatsApp.
- O bot pode **receber** evento de leitura (`application/vnd.microsoft.readReceipt`) se a app/tenant permitir.
- A resposta contextual usa `replyToId`, mas a UI do Teams pode renderizar diferente do WhatsApp.

Se não aparecer visualmente:
1. confirmar Read receipts ativos no Teams (utilizador/admin);
2. confirmar permissões/manifests atualizados e app reinstalada;
3. testar Teams Web + Desktop.

---

## 📚 Swagger/OpenAPI - Documentação de API

### Acesso
- **Development**: http://localhost:5197/
- **Production**: https://seu-dominio.com/swagger/

### Funcionalidades
1. **Ver endpoints** - Lista todos os endpoints disponíveis
2. **Testar API** - "Try it out" para fazer requests
3. **Ver modelos** - Documentação dos DTOs
4. **Autorização** - Configurar headers se necessário

### Exemplo de Teste
1. Abrir Swagger UI
2. Encontrar `POST /api/webhook/whatsapp` ou `POST /api/webhook/teams`
3. Clicar "Try it out"
4. Colar um JSON de exemplo:

**WhatsApp:**
```json
{
  "object": "whatsapp_business_account",
  "entry": [{"changes": [{"value": {"messages": [{"from": "554199999999", "text": {"body": "presente"}}]}}]}]
}
```

**Teams (Bot Framework Activity):**
```json
{
  "type": "message",
  "id": "test-activity-1",
  "serviceUrl": "https://smba.trafficmanager.net/emea/",
  "channelId": "msteams",
  "from": { "id": "29:user-id", "name": "Diogo" },
  "conversation": { "id": "conv-123", "tenantId": "tenant-id-here" },
  "text": "presente"
}
```
5. Clicar "Execute"

---

## 📊 Serilog - Ver Logs

### Ficheiros de Log
```
📁 WebApplication1/
   └── 📁 logs/
       ├── webhook-20260218.txt  (18 de Fevereiro)
       ├── webhook-20260219.txt  (19 de Fevereiro)
       └── webhook-20260220.txt  (20 de Fevereiro)
```

### Ver Logs em Tempo Real
```bash
# Terminal 1 - Executar app
dotnet run

# Terminal 2 - Ver logs
tail -f logs/webhook-*.txt
```

### Ver Logs com PowerShell
```powershell
# Ver ficheiro mais recente
Get-ChildItem logs/ | Sort-Object LastWriteTime -Descending | Select-Object -First 1 | Get-Content

# Ver últimas 10 linhas
Get-Content logs/webhook-20260218.txt -Tail 10

# Ver em tempo real (PowerShell 7+)
Get-Content logs/webhook-20260218.txt -Wait -Tail 10
```

### Formato dos Logs
```
[2026-02-18 16:16:15] [INF] 🚀 Iniciando aplicação...
[2026-02-18 16:16:16] [INF] ✅ Configuração WhatsApp validada com sucesso
[2026-02-18 16:16:16] [INF] 📚 Swagger disponível em http://localhost:5197/
```

**Estrutura:**
- `[YYYY-MM-DD HH:MM:SS]` - Timestamp
- `[INF/WRN/ERR]` - Log Level
- `Mensagem` - Conteúdo do log

---

## 🚨 Custom Exceptions - Como Usar

### Em Handlers/Services

```csharp
using WebApplication1.Exceptions;

public class MyCommandHandler
{
    public void Handle(string command)
    {
        // ❌ Comando não reconhecido
        if (command != "known")
            throw new InvalidCommandException(
                $"Comando '{command}' não é reconhecido. " +
                "Use 'ajuda' para ver comandos disponíveis."
            );
        
        // ✅ Processamento bem-sucedido
    }
}
```

### Em Validação de Webhook

```csharp
using WebApplication1.Exceptions;

public class WebhookValidator
{
    public void ValidateSignature(string payload, string signature)
    {
        // Verificar HMAC
        if (!IsValidHmac(payload, signature))
            throw new WebhookVerificationException(
                "Assinatura HMAC-SHA256 inválida. " +
                "O webhook pode ter sido adulterado."
            );
    }
}
```

### Tratamento de Exceções (ExceptionHandlingMiddleware)

```csharp
try
{
    // Processamento
}
catch (InvalidCommandException ex)
{
    _logger.LogWarning(ex, "Comando inválido");
    return BadRequest(new { error = ex.Message });
}
catch (WebhookVerificationException ex)
{
    _logger.LogError(ex, "Falha na verificação de webhook");
    return Forbid();
}
catch (ConfigurationException ex)
{
    _logger.LogCritical(ex, "Erro de configuração crítico");
    return StatusCode(500, "Configuration error");
}
catch (MessageProcessingException ex)
{
    _logger.LogError(ex, "Falha no processamento de mensagem");
    return StatusCode(502, "Processing error");
}
```

### Tipos de Exceção e Quando Usar

| Exceção | Quando Usar | Exemplo |
|---------|------------|---------|
| `InvalidCommandException` | Comando não existe | `"xyz" é inválido` |
| `WebhookVerificationException` | Validação falha | Assinatura HMAC inválida |
| `ConfigurationException` | Secrets faltam | AccessToken não configurado |
| `MessageProcessingException` | API falha | Graph API retorna erro |

---

## ✔️ Configuration Validation - Verificar Configuração

### Durante o Startup
A validação acontece **automaticamente** quando o app inicia:

```
Program.cs:
├── Ler configuração de appsettings.json + user-secrets
├── 🔍 Validar cada campo
└── ✅ Ou ❌ Falhar com mensagem clara
```

### Se Falhar
```
ConfigurationException: 
Missing required WhatsApp configuration:
WhatsApp:AccessToken - dotnet user-secrets set "WhatsApp:AccessToken" "seu_token"
WhatsApp:AppSecret - dotnet user-secrets set "WhatsApp:AppSecret" "seu_secret"

[FATAL] Configuration validation failed - application cannot start
```

Para **Teams**, a mensagem é semelhante:
```
ConfigurationException:
Teams:BotId não configurado.
Configure em appsettings.json na secção "Teams".

Teams:ClientSecret não configurado.
Configure com: dotnet user-secrets set "Teams:ClientSecret" "seu_secret"
```

### Testar Validação
```bash
# 1. Remover um secret
dotnet user-secrets remove "WhatsApp:AccessToken"

# 2. Tentar executar app
dotnet run
# → Deve falhar com mensagem clara

# 3. Reconfigurar
dotnet user-secrets set "WhatsApp:AccessToken" "seu_token"

# 4. Executar novamente
dotnet run
# → Deve funcionar
```

---

## 🧪 Testes

### Executar Todos
```bash
cd WebApplication1.Tests
dotnet test
```

**Output:**
```
Passed!  - Failed: 0, Passed: <total_atual>, Skipped: 0
```

### Testes Específicos

#### WhatsApp Tests
```bash
dotnet test --filter "ConfigurationValidator"
dotnet test --filter "WhatsAppSettings"
```

#### Teams Tests
```bash
dotnet test --filter "TeamsSettings"
dotnet test --filter "TeamsActivity"
dotnet test --filter "TeamsConfigurationValidator"
```

#### Custom Exceptions Tests
```bash
dotnet test --filter "InvalidCommandException"
dotnet test --filter "WebhookVerificationException"
dotnet test --filter "ConfigurationException"
dotnet test --filter "MessageProcessingException"
```

#### Configuration Validator Tests
```bash
dotnet test --filter "ConfigurationValidator"
```

#### User Processing Lock Tests ⭐ NOVO
```bash
dotnet test --filter "UserProcessingLock"
```

#### Confirmações Contextuais Tests ⭐ NOVO
```bash
dotnet test --filter "ContextualConfirmation"
```

### Verificar Cobertura de Testes
```bash
# Com OpenCover (se instalado)
OpenCover.Console.exe -register:user -target:dotnet.exe -targetargs:"test" -output:coverage.xml
```

---

## 📈 Monitoramento em Produção

### Logs em Ficheiro
```bash
# Ficheiros criados diariamente
logs/webhook-2026-02-18.txt
logs/webhook-2026-02-19.txt
logs/webhook-2026-02-20.txt
```

### Análise de Logs
```powershell
# Contar mensagens por nível
Select-String "\[INF\]|\[WRN\]|\[ERR\]" logs/webhook-*.txt | 
  Group-Object { $_.Line -replace '.*\[(.+?)\].*', '$1' } | 
  Sort-Object Count -Descending

# Procurar erros
Select-String "\[ERR\]|\[FAT\]" logs/webhook-*.txt

# Timeline de eventos
Get-Content logs/webhook-*.txt | 
  Select-String -Pattern "ConfiguraÃ§Ã£o|iniciada|erro" |
  Select-Object -First 20
```

---

## 🔧 Troubleshooting

### "Configuration validation failed"
```
Solução:
1. Verificar quais secrets faltam
2. Configurar com: dotnet user-secrets set "caminho:da:chave" "valor"
3. Verificar user-secrets: dotnet user-secrets list
```

### "Swagger not available"
```
Verificações:
1. App em Development mode? (appsettings.Development.json)
2. ASPNETCORE_ENVIRONMENT=Development
3. URL correta? http://localhost:5197/ (não :5000)
```

### "Logs not created"
```
Verificações:
1. Folder logs/ existe em WebApplication1/?
2. Permissões de write na pasta?
3. Caminho em Program.cs está correto?
   "logs/webhook-.txt" (com hífen antes de .txt)
```

### "Tests failing"
```
Soluções:
1. dotnet clean && dotnet build
2. dotnet test --verbosity normal
3. Verificar se todos os packages estão instalados
   dotnet package restore
```

---

## 📝 Checklist de Implementação

- ✅ Serilog instalado e configurado
- ✅ Custom exceptions criadas
- ✅ Configuration validator implementado (WhatsApp + Teams)
- ✅ Swagger UI configurado
- ✅ Microsoft Teams integrado (Bot Framework REST API)
- ✅ WebhookController (core controller com endpoints WhatsApp + Teams)
- ✅ ValidateWhatsAppSignatureFilter (HMAC-SHA256)
- ✅ ValidateTeamsJwtFilter (JWT + OpenID Connect)
- ✅ Identificação de utilizadores (UserId + UserName)
- ✅ Onion Architecture (Core → Application → Infrastructure → Api)
- ✅ Testes a passar (26 ficheiros; total atual confirmado com `dotnet test`)
- ✅ Anti-Spam por guard/filter + dedup 5 min + lock por remetente + grace de 1s ⭐ ATUALIZADO
- ✅ Confirmações contextuais (mencionam o comando específico) ⭐ NOVO v6.0
- ✅ 11 diagramas UML (PlantUML)
- ✅ appsettings.Production.json (logging restrito)
- ✅ Documentação completa
- ✅ Logs sendo criados em ficheiros
- ✅ App compila sem erros

---

## 🟣 Microsoft Teams - Configuração

### Pré-requisitos
1. **Azure Bot** registado no [Azure Portal](https://portal.azure.com/)
2. **Client Secret** gerado na App Registration
3. **Dev Tunnel** ou domínio público para o endpoint

### Configuração

#### 1. appsettings.json (já configurado)
```json
{
  "Teams": {
    "BotId": "0c4d4876-e7d0-4a03-aa96-572ad1abc4e1",
    "TenantId": "botframework.com",
    "LoginUrl": "https://login.microsoftonline.com",
    "Scope": "https://api.botframework.com/.default"
  }
}
```

#### 2. User Secrets (Client Secret)
```bash
cd WebApplication1
dotnet user-secrets set "Teams:ClientSecret" "seu_client_secret_aqui"
```

#### 3. Configurar Endpoint no Azure Bot
No Azure Portal → Bot Service → Configuration → Messaging Endpoint:
```
https://seu-dominio.com/api/webhook/teams
```

Exemplo com Dev Tunnels:
```
https://dpp9qssn-5197.uks1.devtunnels.ms/api/webhook/teams
```

### Como Funciona

1. **Utilizador envia mensagem no Teams** (menciona o bot ou envia DM)
2. **Bot Framework** envia Activity JSON para `/api/webhook/teams`
3. **WebhookController** recebe, remove menção `<at>...</at>` e extrai texto
4. **WebhookController.ProcessMessage()** processa (mesma lógica do WhatsApp)
5. **TeamsService** obtém token OAuth2 e responde via Bot Framework REST API

### Autenticação OAuth2
O TeamsService usa **client_credentials** flow:
```
POST https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token
  grant_type=client_credentials
  client_id={botId}
  client_secret={clientSecret}
  scope=https://api.botframework.com/.default
```

O token é **cacheado** e reutilizado até expirar (refresh automático).

### Console Output (Teams)
```
┌────────────────────────────────────────┐
│ 📨 MENSAGEM RECEBIDA                   │
├────────────────────────────────────────┤
│ De:         Diogo                       │
│ Plataforma: 🟣 Teams                   │
│ Hora:       2026-02-18 16:18:17         │
│ Mensagem:   Presente                    │
└────────────────────────────────────────┘
```

---

## 🛡️ Anti-Spam WhatsApp Web — Guard + Filtro + Lock ⭐ ATUALIZADO

### Como Funciona
Quando um utilizador escreve muito rápido, o sistema aceita apenas a primeira mensagem do burst e bloqueia as seguintes até ao fim do processamento.

```
User: "presente"                 → processa normalmente
User: "l" "l" "l" "l" (spam)   → bloqueadas durante lock do remetente
Bot responde ao comando válido    → lock é libertado
User envia mensagem nova após isso → processa normalmente
```

### Mecanismos
1. **Deduplicação por `MessageId` (5 min)** no `WebhookConcurrencyGuard`
2. **Lock por remetente (5s fallback)** no `WebhookConcurrencyGuard`
3. **Fast-return 200** no `WhatsAppConcurrencyGuardFilter` quando payload é spam/duplicado
4. **Filtro `SentAt` + grace (1s)** no `MessageProcessingService`
5. **Delayed unlock (2s)** no `MessageProcessingService` quando spam é detetado

### Testar User Processing Lock
```bash
# Executar testes de user processing lock
dotnet test --filter "UserProcessingLock"

# Output esperado: testes a passar (número pode variar com novas suites)
```

---

## 💬 Confirmações Contextuais — Referência ao Comando ⭐ NOVO v6.0

### Como Funciona
Todos os prompts de confirmação agora referenciam o comando específico:

```
User: "presente"
Bot:  "✅ Vamos executar *presença*. É mesmo isso que pretendes? (sim/não)"
                                    ^^^^^^^^ referência ao comando

User: "talvez"
Bot:  "❓ Perguntei sobre *presença* — responde com SIM ou NÃO. Tentativas restantes: 2."

Na **3ª tentativa inválida**:
- a confirmação é cancelada,
- o bot explica que o esperado era `sim` ou `não`,
- e sugere a palavra `ajuda` para continuar o fluxo.
                          ^^^^^^^^

User: "não"
Bot:  "❌ Ok, *presença* cancelado. Se precisares de ajuda, escreve ajuda."
              ^^^^^^^^
```

### Testar Confirmações Contextuais
```bash
# Executar testes de confirmações contextuais
dotnet test --filter "ContextualConfirmation"

# Output esperado: 8 testes passados
```

---

## �🔐 Security Filters - Validação de Segurança

### ValidateWhatsAppSignatureFilter (HMAC-SHA256)
Aplicado automaticamente a todos os POST `/api/webhook/whatsapp`:
- Valida header `X-Hub-Signature-256` contra HMAC-SHA256 do body
- Usa `WhatsApp:AppSecret` para cálculo
- **Se AppSecret não configurado (dev):** emite warning, não bloqueia
- **Se assinatura inválida:** retorna 401 Unauthorized

### ValidateTeamsJwtFilter (JWT + OpenID Connect)
Aplicado automaticamente a todos os POST `/api/webhook/teams`:
- Valida token JWT no header `Authorization: Bearer <token>`
- Busca chaves públicas do Bot Framework via OpenID Connect metadata
- Verifica `issuer`, `audience` (BotId) e expiração
- **Modo Development:** bypass automático (permite Bot Framework Emulator)
- **Produção:** rejeita tokens inválidos com 401 Unauthorized

### Testar Segurança
```bash
# Enviar request sem assinatura (deve ser rejeitado)
curl -X POST http://localhost:5197/api/webhook/whatsapp \
  -H "Content-Type: application/json" \
  -d '{"object": "whatsapp_business_account"}'
# → 401 Unauthorized

# Enviar para Teams sem JWT (deve ser rejeitado em produção)
curl -X POST http://localhost:5197/api/webhook/teams \
  -H "Content-Type: application/json" \
  -d '{"type": "message", "text": "teste"}'
# → 401 Unauthorized (produção) ou 200 (development)
```

### Security Headers OWASP (⭐ NOVO v5.0)
O `SecurityHeadersMiddleware` adiciona automaticamente headers de segurança a **todas** as respostas:

| Header | Proteção |
|--------|----------|
| `X-Frame-Options: DENY` | Clickjacking |
| `X-Content-Type-Options: nosniff` | MIME sniffing |
| `X-XSS-Protection: 0` | XSS Auditor (desativado, OWASP recomenda) |
| `Referrer-Policy` | Vazamento de URLs |
| `Permissions-Policy` | APIs do browser (câmara, microfone, etc.) |
| `Content-Security-Policy` | Injeção de conteúdo |
| `Strict-Transport-Security` | Forçar HTTPS (só produção) |

Também **remove** `Server` e `X-Powered-By` para não revelar info do stack.

**Verificar headers:**
```bash
# Ver headers de resposta
curl -I http://localhost:5197/health
# Deve mostrar X-Frame-Options, X-Content-Type-Options, etc.
```

### Rate Limiting (⭐ NOVO v5.0)
Proteção contra abuso com limites por IP:

| Política | Limite | Endpoints |
|----------|--------|-----------|
| `webhook` | 30 requests/minuto | `/api/webhook/*` |
| `health` | 10 requests/minuto | `/health` |

**Quando excedido:** retorna **429 Too Many Requests**

**Testar rate limiting:**
```bash
# Enviar muitos requests rapidamente
for i in $(seq 1 35); do curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5197/health; done
# Os últimos devem retornar 429
```

### Limite de Tamanho (⭐ NOVO v5.0)
Todos os POST têm limite de **1 MB** (`[RequestSizeLimit(1_048_576)]`), prevenindo payloads maliciosas.

### Correlation ID (Rastreamento)
Cada pedido HTTP recebe um GUID único no header `X-Correlation-ID`:
- Aparece em todos os logs desse pedido
- Devolvido na resposta para referência
- Útil para investigar incidentes: procura o GUID nos logs e vês todo o fluxo

```bash
# Ver o Correlation ID na resposta
curl -I http://localhost:5197/health
# Resposta inclui: X-Correlation-ID: 7a3f8b2c-1234-5678-9abc-def012345678
```

> 📖 Para documentação completa de segurança (ataques prevenidos, exemplos reais, explicação método a método), ver [SECURITY.md](SECURITY.md)

---

## 📐 Diagramas UML

O projeto inclui **11 diagramas PlantUML** em `Architecture-UML/`:

```
Architecture-UML/
├── 01_C4_Level1_Context.puml  # Diagrama C4 nível 1 (contexto)
├── logical-view.puml          # Vista lógica (Onion Architecture)
├── physical-view.puml         # Vista física (deployment Azure)
├── use-cases.puml             # Casos de uso
├── sequence-whatsapp.puml     # Sequência WhatsApp
├── sequence-teams.puml        # Sequência Teams
├── sequence-security.puml     # Validação de segurança
├── state-confirmation.puml    # Estados de confirmação
├── activity-processing.puml   # Processamento de mensagem
├── classes-commands.puml      # Command Pattern classes
└── components-middleware.puml  # Pipeline middleware
```

**Para visualizar:**
- Extensão **PlantUML** do VS Code
- Site [plantuml.com](https://www.plantuml.com/)

---

## 🚀 Deploy em Produção

### Azure App Service
```bash
# Publish
dotnet publish -c Release

# Em produção a app usa PORT e faz bind em http://0.0.0.0:{PORT}
# Se PORT não existir, usa 8080
```

### Configuração de Produção
- `appsettings.Production.json` — Log level Warning (não Information)
- Variável `PORT` — Azure define automaticamente (fallback 8080)
- User Secrets não estão disponíveis em produção — usar Azure Key Vault ou App Settings

---

## 📝 Checklist de Verificação

1. Executar `dotnet run`
2. Abrir http://localhost:5197/ no browser
3. Explorar os endpoints no Swagger UI
4. Verificar logs em `logs/webhook-YYYY-MM-DD.txt`
5. Executar `dotnet test` (todos os testes devem passar)
6. Verificar diagramas UML em `Architecture-UML/`
7. Verificar Security Headers com `curl -I http://localhost:5197/health`
8. Testar Rate Limiting enviando muitos requests seguidos

---

**Versão**: 7.0 (Business API Integration)
**Data**: 02/03/2026  
**Status**: ✅ Ready for Production

