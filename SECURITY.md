# 🔐 Segurança — Documentação Completa

Documento consolidado de **todas** as proteções de segurança do projeto, incluindo:
- Que ataque cada proteção previne
- Exemplo real do ataque
- Explicação método a método de cada classe

---

## 📋 Índice

1. [Resumo de Proteções](#1-resumo-de-proteções)
2. [SecurityHeadersMiddleware](#2-securityheadersmiddleware)
3. [ValidateWhatsAppSignatureFilter](#3-validatewhatsappsignaturefilter)
4. [ValidateTeamsJwtFilter](#4-validateteamsjwtfilter)
5. [ExceptionHandlingMiddleware](#5-exceptionhandlingmiddleware)
6. [CorrelationIdMiddleware](#6-correlationidmiddleware)
7. [Rate Limiting (Program.cs)](#7-rate-limiting-programcs)
8. [Request Size Limit](#8-request-size-limit)
9. [Deduplicação de Mensagens](#9-deduplicação-de-mensagens)
10. [Validação de Configuração](#10-validação-de-configuração)
11. [User Secrets](#11-user-secrets)
12. [Logging Seguro (Produção)](#12-logging-seguro-produção)
13. [Pipeline de Middleware — Ordem](#13-pipeline-de-middleware--ordem)

---

## 1. Resumo de Proteções

| # | Proteção | Classe/Ficheiro | Ataque Prevenido | Exemplo |
|---|----------|-----------------|------------------|---------|
| 1 | X-Frame-Options: DENY | SecurityHeadersMiddleware | **Clickjacking** | Site malicioso embebe a tua app num iframe invisível — user clica em "Jogar" mas na verdade está a confirmar presença |
| 2 | X-Content-Type-Options: nosniff | SecurityHeadersMiddleware | **MIME Sniffing** | Atacante faz upload de `foto.txt` que o browser executa como JavaScript |
| 3 | X-XSS-Protection: 0 | SecurityHeadersMiddleware | **XSS Auditor Bugs** | Auditor antigo do browser pode ser abusado para *causar* XSS em vez de o prevenir |
| 4 | Referrer-Policy | SecurityHeadersMiddleware | **Referrer Leakage** | URL `site.com/user/123/token/abc` aparece como Referer quando clicas num link externo |
| 5 | Permissions-Policy | SecurityHeadersMiddleware | **API Abuse** | Script injetado liga a câmara/microfone sem o user saber |
| 6 | Content-Security-Policy | SecurityHeadersMiddleware | **Content Injection** | Atacante injeta `<script>` que rouba cookies ou redireciona o user |
| 7 | Strict-Transport-Security | SecurityHeadersMiddleware | **Downgrade/MITM** | Atacante interceta tráfego num Wi-Fi público forçando HTTP em vez de HTTPS |
| 8 | Remove Server/X-Powered-By | SecurityHeadersMiddleware | **Information Disclosure** | Atacante vê "Kestrel 8.0" → procura CVEs específicas para essa versão |
| 9 | HMAC-SHA256 | ValidateWhatsAppSignatureFilter | **Webhook Spoofing** | Alguém descobre o URL do webhook e envia payloads falsas fingindo ser a Meta |
| 10 | JWT + OpenID Connect | ValidateTeamsJwtFilter | **Token Forgery** | Alguém envia Activities falsas para o endpoint Teams com um token inventado |
| 11 | Error Handling (prod) | ExceptionHandlingMiddleware | **Stack Trace Leakage** | Erro mostra código-fonte, nomes de tabelas, connection strings ao atacante |
| 12 | Correlation ID | CorrelationIdMiddleware | **— (Auditoria)** | Permite rastrear um request suspeito em todos os logs com um único GUID |
| 13 | Rate Limiting (30/min) | Program.cs | **DDoS / Brute Force** | Bot envia 10.000 requests/segundo → bloqueado após 30, responde 429 |
| 14 | Request Size Limit (1MB) | WebhookController | **Payload Bomb / DoS** | Atacante envia body de 5 GB → rejeitado imediatamente pelo Kestrel |
| 15 | Deduplicação (5 min) | WebhookConcurrencyGuard + MessageProcessingService | **Replay Attack** | Atacante captura um webhook legítimo da Meta e reenvia 100× |
| 16 | Config Validation (fail-fast) | Program.cs + ConfigurationValidator | **Misconfiguration** | App arranca sem AppSecret → HMAC fica desativado → webhooks sem proteção |
| 17 | User Secrets | dotnet user-secrets | **Secret Leakage** | Secrets nunca vão para o git; se alguém clonar o repo, não tem os tokens |
| 18 | Logging restrito (prod) | appsettings.Production.json | **Log Info Disclosure** | Em prod não loga conteúdo de mensagens, só warnings/errors |
| 19 | Anti-Spam (guard + lock + grace) | WhatsAppConcurrencyGuardFilter + WebhookConcurrencyGuard + MessageProcessingService | **Spam / Flood** | Dedup 5 min + lock por remetente + filtro `SentAt` com grace de 1s + delayed unlock (2s) |
| 20 | Re-tap Protection | MessageProcessingService | **Confirmation Abuse** | Re-envio do mesmo comando durante confirmação pendente → ignorado (não queima tentativas) ⭐ NOVO v6.0 |

**Total: 20 proteções cobrindo 14+ tipos de ataque.**

---

## 2. SecurityHeadersMiddleware

**Ficheiro:** `Api/Middleware/SecurityHeadersMiddleware.cs`  
**Tipo:** Middleware ASP.NET Core (executa em TODOS os pedidos)  
**Referência:** [OWASP Secure Headers](https://owasp.org/www-project-secure-headers/)

### O que faz
Adiciona automaticamente 7 headers de segurança + remove 2 headers perigosos de **TODAS** as respostas HTTP, sem que os controllers precisem de saber.

### Métodos

#### `InvokeAsync(HttpContext context)`
- **O que faz:** Regista um callback `OnStarting` que modifica os headers da resposta ANTES de serem enviados ao cliente. Depois chama `_next(context)` para continuar o pipeline.
- **Porquê `OnStarting`?** Se adicionássemos os headers depois de `_next()`, a resposta já poderia ter sido enviada (especialmente com streaming). O `OnStarting` garante que executamos antes do primeiro byte ser enviado.
- **Lógica condicional:** O header `Strict-Transport-Security` (HSTS) só é adicionado em produção. Em desenvolvimento usamos HTTP local e o HSTS iria causar problemas.

### Headers — Detalhe

#### `X-Frame-Options: DENY`
- **Ataque: Clickjacking**
- **Como funciona o ataque:** O atacante cria uma página HTML com um `<iframe>` invisível que carrega a tua aplicação. Posiciona botões da tua app por baixo de elementos da página dele. O utilizador pensa que está a clicar em "Jogar" mas está a interagir com a tua app (ex: confirmar uma transferência).
- **Como o header protege:** O browser recusa renderizar a tua app dentro de qualquer `<iframe>`. Com `DENY`, nem o teu próprio domínio pode.
- **Exemplo real:**
  ```
  Atacante: <iframe src="https://tua-app.com/confirmar-presenca" style="opacity:0">
  User: Clica em "Ver Vídeo" → Na verdade clicou em "Confirmar" no teu site
  Com DENY: O iframe nem carrega → ataque falhado
  ```

#### `X-Content-Type-Options: nosniff`
- **Ataque: MIME Type Sniffing**
- **Como funciona o ataque:** O atacante faz upload de um ficheiro com extensão `.txt` mas que contém JavaScript. Sem este header, o browser pode "adivinhar" que é JavaScript e executá-lo.
- **Como o header protege:** O browser confia APENAS no `Content-Type` enviado pelo servidor, nunca "adivinha".
- **Exemplo real:**
  ```
  Upload: malware.txt (contém <script>alert(document.cookie)</script>)
  Sem nosniff: Browser vê o conteúdo → "isto parece HTML" → executa
  Com nosniff: Browser vê Content-Type: text/plain → trata como texto
  ```

#### `X-XSS-Protection: 0`
- **Ataque: XSS Auditor Abuse**
- **Porquê `0` e não `1`?** O XSS Auditor dos browsers era uma feature antiga que tentava detetar XSS. Mas foi descoberto que podia ser **abusado** para causar XSS (sim, a proteção podia ser usada como ataque). A OWASP recomenda desativá-lo.
- **Proteção moderna:** Usa-se `Content-Security-Policy` em vez do XSS Auditor.

#### `Referrer-Policy: strict-origin-when-cross-origin`
- **Ataque: Referrer Leakage**
- **Como funciona o ataque:** Quando fazes um request para outro site, o browser envia o URL de onde vieste no header `Referer`. Se o teu URL contiver dados sensíveis (tokens, IDs), esses dados vazam.
- **Como o header protege:** Para requests cross-origin, só envia a origem (`https://tua-app.com`), não o caminho completo.
- **Exemplo real:**
  ```
  URL: https://tua-app.com/admin/user/123?token=secret_abc
  Sem Referrer-Policy: Site externo recebe o URL COMPLETO incluindo o token
  Com strict-origin: Site externo recebe apenas "https://tua-app.com"
  ```

#### `Permissions-Policy: camera=(), microphone=(), geolocation=(), payment=()`
- **Ataque: Browser API Abuse**
- **Como funciona o ataque:** Se um atacante conseguir injetar código na tua página, pode aceder a APIs do browser como câmara, microfone, localização GPS ou APIs de pagamento.
- **Como o header protege:** Desativa estas APIs completamente. Mesmo que haja injeção de código, o `navigator.mediaDevices.getUserMedia()` falha.
- **Exemplo real:**
  ```
  Script injetado: navigator.mediaDevices.getUserMedia({video: true})
  Sem Policy: Câmara liga e atacante recebe o stream
  Com Policy: Browser bloqueia → "Permission denied by Permissions-Policy"
  ```

#### `Content-Security-Policy: default-src 'none'; frame-ancestors 'none'`
- **Ataque: Content Injection / XSS**
- **Como funciona:** CSP diz ao browser de onde pode carregar recursos (scripts, imagens, etc.). Como somos uma API pura (não servimos HTML), bloqueamos tudo com `'none'`.
- **`frame-ancestors 'none'`** é a versão moderna do `X-Frame-Options: DENY` (redundância intencional para browsers antigos).

#### `Strict-Transport-Security: max-age=31536000; includeSubDomains`
- **Ataque: Downgrade Attack / Man-in-the-Middle (MITM)**
- **Como funciona o ataque:** Num Wi-Fi público, o atacante interceta o tráfego e redireciona de HTTPS para HTTP. A partir daí, vê tudo em texto claro (tokens, mensagens, etc.).
- **Como o header protege:** Depois da primeira visita via HTTPS, o browser recusa-se a usar HTTP durante 365 dias.
- **Só em produção:** Em desenvolvimento usamos HTTP local (`http://localhost:5197`), e o HSTS bloquearia isso.

#### Remoção de `Server` e `X-Powered-By`
- **Ataque: Information Disclosure / Fingerprinting**
- **Como funciona:** Estes headers revelam a tecnologia do servidor (ex: "Kestrel", "ASP.NET"). Um atacante usa esta informação para procurar vulnerabilidades (CVEs) específicas dessa versão.
- **Exemplo:**
  ```
  Sem remoção: "Server: Kestrel" → Atacante pesquisa "Kestrel CVE 2025"
  Com remoção: Header não existe → Atacante não sabe que tecnologia usamos
  ```

---

## 3. ValidateWhatsAppSignatureFilter

**Ficheiro:** `Api/Middleware/ValidateWhatsAppSignatureFilter.cs`  
**Tipo:** ASP.NET Core Action Filter (`IAsyncActionFilter`)  
**Aplica-se a:** POST `/api/webhook/whatsapp` (via `[ServiceFilter]`)

### Ataque Prevenido: Webhook Spoofing / Forgery
- **O que é:** Alguém descobre o URL do teu webhook e envia payloads falsos, fingindo ser a Meta/WhatsApp.
- **Impacto sem proteção:** A tua app processa mensagens falsas como se fossem reais. Alguém pode simular que um utilizador enviou "sim" para confirmar uma ação que nunca pediu.
- **Exemplo real:**
  ```
  Atacante: curl -X POST https://tua-app.com/api/webhook/whatsapp \
    -d '{"entry":[{"changes":[{"value":{"messages":[{"from":"351999","text":{"body":"sim"}}]}}]}]}'
  
  Sem HMAC: App processa → marca presença falsa
  Com HMAC: App verifica assinatura → 401 Unauthorized
  ```

### Métodos

#### `OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)`
Método principal do filtro. Executa ANTES do controller.

**Fluxo:**
1. **Verifica se AppSecret está configurado.** Se não → emite warning e deixa passar (modo dev). Em produção NUNCA deve estar vazio.
2. **Verifica se é POST.** GET (verificação de webhook) não tem assinatura → ignora.
3. **Lê o header `X-Hub-Signature-256`.** Se não existe → rejeita com 401.
4. **Lê o body do request** usando `EnableBuffering()` (permite ler o body mais do que uma vez — o controller precisa de o ler depois).
5. **Calcula o HMAC-SHA256** do body com o AppSecret.
6. **Compara as assinaturas** com `CryptographicOperations.FixedTimeEquals()` — timing-safe para evitar timing attacks.
7. Se não corresponde → **401 Unauthorized**.

#### `ReadBodyBytesAsync(Stream body)`
- Lê todos os bytes do body HTTP para um `byte[]`.
- Usa `MemoryStream` para copiar eficientemente.
- Necessário porque o HMAC precisa dos raw bytes (não string).

#### `ComputeHmacSha256(byte[] payload, string secret)`
- Converte o `secret` para bytes UTF-8.
- Cria uma instância de `HMACSHA256` com a chave.
- Calcula o hash do payload.
- Devolve no formato `"sha256=abcdef..."` (mesmo formato que a Meta envia).

### Nota sobre `FixedTimeEquals`
A comparação normal de strings (`==`) pode terminar mais cedo se o primeiro carácter não corresponde. Um atacante pode medir o tempo de resposta para "adivinhar" a assinatura correta byte a byte (**timing attack**). `FixedTimeEquals` demora sempre o mesmo tempo, independentemente de onde está a diferença.

---

## 4. ValidateTeamsJwtFilter

**Ficheiro:** `Api/Middleware/ValidateTeamsJwtFilter.cs`  
**Tipo:** ASP.NET Core Action Filter (`IAsyncActionFilter`)  
**Aplica-se a:** POST `/api/webhook/teams` (via `[ServiceFilter]`)

### Ataque Prevenido: Token Forgery / Impersonation
- **O que é:** Alguém envia Activities falsas para o endpoint Teams, fingindo ser o Bot Framework.
- **Impacto sem proteção:** A app processa actividades falsas como se viessem do Teams real. Alguém pode simular mensagens de qualquer utilizador.
- **Exemplo real:**
  ```
  Atacante: POST /api/webhook/teams
    Authorization: Bearer <token_inventado>
    Body: {"type":"message","from":{"name":"CEO"},"text":"sim"}
  
  Sem JWT: App processa → executa ação em nome do "CEO"
  Com JWT: App valida token → inválido → 401 Unauthorized
  ```

### Métodos

#### `OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)`
Método principal do filtro. Executa ANTES do controller.

**Fluxo:**
1. **Verifica se estamos em Development.** Se sim → ignora validação (permite Bot Framework Emulator para testes locais).
2. **Verifica se BotId está configurado.** Se não → devolve 500 (sem BotId não podemos validar audience).
3. **Verifica se é POST.** Outros métodos → ignora.
4. **Lê o header `Authorization`.** Se não existe → 401.
5. **Verifica formato `Bearer <token>`.** Se não é Bearer → 401.
6. **Verifica se o token não está vazio.** Se está → 401.
7. **Obtém configuração OpenID Connect** do Bot Framework (`https://login.botframework.com/v1/.well-known/openidconfiguration`). Isto inclui as chaves públicas para verificar a assinatura do JWT.
8. **Valida o JWT** com `JwtSecurityTokenHandler.ValidateToken()`:
   - `ValidateIssuer` — o token foi emitido pelo Bot Framework?
   - `ValidateAudience` — o token é para o nosso bot (BotId)?
   - `ValidateLifetime` — o token ainda não expirou?
   - `ValidateIssuerSigningKey` — a assinatura é válida contra as chaves públicas?
   - `ClockSkew: 5 min` — tolerância de 5 minutos para relógios desincronizados.
9. Se inválido → **401 Unauthorized** com mensagem de erro.

### `OpenIdConfigManager` (campo estático)
- `ConfigurationManager<OpenIdConnectConfiguration>` que faz cache das chaves públicas do Bot Framework.
- Não faz request HTTP em cada pedido — guarda em cache e renova automaticamente.
- Partilhado entre todas as instâncias do filtro (performance).

---

## 5. ExceptionHandlingMiddleware

**Ficheiro:** `Api/Middleware/ExceptionHandlingMiddleware.cs`  
**Tipo:** Middleware ASP.NET Core (executa em TODOS os pedidos)

### Ataque Prevenido: Information Disclosure via Stack Traces
- **O que é:** Uma exceção não tratada devolve o stack trace completo ao cliente.
- **Impacto:** O atacante vê nomes de classes, métodos, caminhos de ficheiros, e potencialmente connection strings ou dados sensíveis.
- **Exemplo real:**
  ```
  Sem middleware (produção):
    { "error": "NullReferenceException at WebApplication1.Services.TeamsService.SendAsync() 
      in C:\\deploy\\WebApplication1\\Infrastructure\\Messaging\\TeamsService.cs:line 45
      Connection string: Server=sql01;Database=..." }
  
  Com middleware (produção):
    { "status": 500, "message": "Ocorreu um erro interno. Contacte o administrador." }
  
  Com middleware (desenvolvimento):
    { "status": 500, "message": "Object reference not set to an instance of an object" }
  ```

### Métodos

#### `InvokeAsync(HttpContext context)`
- Envolve `_next(context)` num `try/catch`.
- Se não há exceção → nada acontece, a resposta flui normalmente.
- Se há exceção → loga com `LogError` e chama `HandleExceptionAsync`.

#### `HandleExceptionAsync(HttpContext context, Exception exception)`
- Define `Content-Type: application/json` e `Status: 500`.
- **Em Development:** Devolve `exception.Message` (útil para debugging).
- **Em Production:** Devolve mensagem genérica `"Ocorreu um erro interno."` (sem informação sensível).
- Serializa a resposta como JSON com camelCase.

---

## 6. CorrelationIdMiddleware

**Ficheiro:** `Api/Middleware/CorrelationIdMiddleware.cs`  
**Tipo:** Middleware ASP.NET Core (executa em TODOS os pedidos)

### Para que serve: Auditoria e Rastreamento
- **Não previne um ataque específico**, mas é **essencial para investigar** ataques e problemas.
- Cada pedido HTTP recebe um GUID único. Esse GUID aparece em TODOS os logs relacionados com aquele pedido.
- Se receberes um alerta de segurança, podes procurar o Correlation ID nos logs e ver o fluxo completo.

### Exemplo prático
```
[CorrelationId: 7a3f8b2c-1234-5678-9abc-def012345678] POST /api/webhook/whatsapp
[CorrelationId: 7a3f8b2c-1234-5678-9abc-def012345678] Mensagem recebida de 351932947533: "presente"
[CorrelationId: 7a3f8b2c-1234-5678-9abc-def012345678] Comando "presença" acionado
[CorrelationId: 7a3f8b2c-1234-5678-9abc-def012345678] Resposta enviada com sucesso

→ Se houver erro, procuras por "7a3f8b2c" e vês tudo o que aconteceu nesse pedido.
```

### Métodos

#### `InvokeAsync(HttpContext context, ILogger<CorrelationIdMiddleware> logger)`
**Fluxo:**
1. **Verifica se o pedido já traz um `X-Correlation-ID`** (pode vir propagado de outro sistema). Se sim, reutiliza. Se não, gera um `Guid.NewGuid()`.
2. **Guarda no `HttpContext.Items["CorrelationId"]`** — acessível em qualquer parte do pipeline (controllers, serviços).
3. **Adiciona ao header da resposta** via `OnStarting` — o cliente recebe o ID de volta e pode usá-lo para referência em suporte.
4. **Adiciona ao scope do logger** com `logger.BeginScope()` — a partir daqui, TODOS os logs dentro deste pedido incluem automaticamente `CorrelationId: xxx`.

### Porque é importante para segurança
- **Investigação de incidentes:** "Às 14:32 houve um acesso suspeito" → filtra logs por timestamp → encontra Correlation ID → vê exatamente o que aconteceu.
- **Propagação:** Se a tua app chamar um serviço externo, pode enviar o Correlation ID no header. Assim os logs dos dois sistemas ficam ligados.
- **Compliance/Auditoria:** Muitos standards de segurança (SOC2, ISO 27001) exigem rastreamento de requests.

---

## 7. Rate Limiting (Program.cs)

**Ficheiro:** `Program.cs` (linhas 21–56)  
**Implementação:** `System.Threading.RateLimiting` com `FixedWindowRateLimiter`

### Ataque Prevenido: DDoS / Brute Force / Abuse
- **O que é:** Um atacante envia milhares de requests por segundo para sobrecarregar o servidor ou tentar operações por força bruta.
- **Exemplo real:**
  ```
  Atacante (sem rate limiting):
    for i in {1..10000}; do curl POST /api/webhook/whatsapp -d '...'; done
    → Servidor fica sobrecarregado, users reais não conseguem usar

  Com rate limiting:
    Primeiros 30 requests → 200 OK
    31.º request → 429 Too Many Requests
    Atacante bloqueado, servidor continua a funcionar para users reais
  ```

### Configuração

| Política | Limite | Janela | Queue | Endpoints |
|----------|--------|--------|-------|-----------|
| `webhook` | 30 requests | 1 minuto | 5 em fila | `/api/webhook/*` |
| `health` | 10 requests | 1 minuto | 2 em fila | `/health` |

### Como funciona
- **Partição por IP:** Cada IP tem o seu próprio contador. O IP A pode fazer 30 requests sem afetar o IP B.
- **Fixed Window:** A janela reinicia a cada minuto. Às 14:00:00 tens 30 tokens. Às 14:01:00 reinicia para 30.
- **Queue:** Se o limite for atingido, até 5 pedidos ficam em fila (esperam). Os restantes recebem 429 imediatamente.
- **429 Too Many Requests:** Código HTTP standard que diz "estás a fazer demasiados pedidos".

### Aplicação nos endpoints
Os controllers usam `[EnableRateLimiting("webhook")]` ou `[EnableRateLimiting("health")]` para associar cada endpoint à política correta.

---

## 8. Request Size Limit

**Ficheiro:** `Api/Controllers/WebhookController.cs`  
**Atributo:** `[RequestSizeLimit(1_048_576)]` (1 MB)

### Ataque Prevenido: Payload Bomb / Denial of Service
- **O que é:** O atacante envia um body HTTP enorme (gigabytes) para consumir memória e CPU do servidor.
- **Impacto:** O servidor tenta ler e parsear gigabytes de JSON → fica sem memória → crash.
- **Exemplo real:**
  ```
  Atacante: curl -X POST /api/webhook/whatsapp -d @ficheiro_5gb.json
  
  Sem limite: Servidor tenta ler 5 GB → OutOfMemoryException → app crash
  Com limite 1MB: Kestrel rejeita imediatamente → 413 Payload Too Large
  ```

### Porquê 1 MB?
- Uma mensagem normal do WhatsApp tem ~1-2 KB de JSON.
- Uma Activity do Teams tem ~2-5 KB.
- 1 MB dá margem enorme (1000× maior que o normal) mas impede payloads absurdos.

---

## 9. Deduplicação de Mensagens

**Ficheiros:** `Application/WebhookConcurrencyGuard.cs` + `Application/MessageProcessingService.cs`  
**Implementação:** deduplicação por `MessageId` com janela de 5 minutos

### Ataque Prevenido: Replay Attack
- **O que é:** O atacante captura um webhook legítimo da Meta (com assinatura HMAC válida) e reenvia-o múltiplas vezes.
- **Impacto:** A mesma mensagem é processada N vezes. O utilizador marca presença uma vez mas o sistema regista N vezes.
- **Exemplo real:**
  ```
  1. Meta envia webhook com messageId "wamid.abc123" e HMAC válido
  2. Atacante captura o request completo (headers + body)
  3. Atacante reenvia 100× (o HMAC continua válido porque o body é o mesmo)
  
  Sem deduplicação: 100 presenças registadas
  Com deduplicação: 1 presença registada, 99 ignoradas
  ```

### Como funciona
- Quando uma mensagem chega, o `MessageId` é registado para idempotência.
- Se o mesmo `MessageId` voltar a chegar dentro de 5 minutos → ignorado.
- Após 5 minutos, a entrada expira automaticamente.

---

## 10. Validação de Configuração

**Ficheiro:** `Program.cs` (linhas 137–148) + `Infrastructure/Configuration/ConfigurationValidator.cs`

### Ataque Prevenido: Security Misconfiguration
- **O que é:** A app arranca sem secrets de segurança configurados, desativando proteções.
- **Impacto:** Se `AppSecret` estiver vazio, o HMAC fica desativado e qualquer pessoa pode enviar webhooks falsos.
- **Exemplo real:**
  ```
  Deploy em produção:
    Programador esquece de configurar WhatsApp:AppSecret
    
    Sem validação: App arranca normalmente → HMAC desativado → webhooks sem proteção
    Com validação: App NÃO arranca → "❌ WhatsApp:AppSecret não configurado"
    
    Em Development: App arranca com warning (para não bloquear testes)
    Em Production: App RECUSA arrancar (fail-fast)
  ```

### Métodos

#### `ConfigurationValidator.ValidateWhatsAppSettings(WhatsAppSettings settings)`
- Verifica 5 campos: AccessToken, AppSecret, VerifyToken, PhoneNumberId, ApiVersion.
- Se algum está null ou vazio → lança `ConfigurationException` com mensagem clara e comando de correção.

#### `ConfigurationValidator.ValidateTeamsSettings(TeamsSettings settings)`
- Verifica 4 campos: BotId, ClientSecret, TenantId, LoginUrl.
- Mesma lógica do WhatsApp.

---

## 11. User Secrets

**Ferramenta:** `dotnet user-secrets`  
**Localização:** `%APPDATA%\Microsoft\UserSecrets\{guid}\secrets.json` (fora do projeto)

### Ataque Prevenido: Secret Leakage
- **O que é:** Credenciais ficam no código-fonte (appsettings.json) e são commitadas no git.
- **Impacto:** Qualquer pessoa com acesso ao repositório vê os tokens.
- **Exemplo real:**
  ```
  Sem User Secrets:
    appsettings.json: { "WhatsApp": { "AccessToken": "EAABx..." } }
    git push → Token visível em todo o histórico do git
    → Mesmo que removas depois, ficou no histórico para sempre
    
  Com User Secrets:
    appsettings.json: { "WhatsApp": { "AccessToken": "" } }
    Valor real está em %APPDATA%/UserSecrets/ (FORA do projeto)
    git push → Ficheiro appsettings.json não tem nada sensível
  ```

### Secrets configurados
```bash
dotnet user-secrets set "WhatsApp:AccessToken" "token_real"
dotnet user-secrets set "WhatsApp:AppSecret" "secret_real"
dotnet user-secrets set "Teams:ClientSecret" "secret_real"
```

---

## 12. Logging Seguro (Produção)

**Ficheiro:** `appsettings.Production.json`  
**Configuração:** Log level `Warning` (em vez de `Information`)

### Ataque Prevenido: Log Information Disclosure
- **O que é:** Logs em produção gravam informação detalhada demais (conteúdo de mensagens, tokens, IDs pessoais).
- **Impacto:** Se os logs forem comprometidos, o atacante tem acesso a dados sensíveis.
- **Diferença:**
  ```
  Development (Information):
    "Mensagem recebida de 351932947533: presente"
    "JWT Teams: eyJhbGci..."
    
  Production (Warning):
    Só aparece se algo correr mal:
    "🚫 Assinatura HMAC inválida"
    "❌ Erro ao processar webhook"
  ```

---

## 13. Pipeline de Middleware — Ordem

A ordem dos middlewares é **crítica**. Está configurada em `Program.cs`:

```
Request HTTP
    │
    ▼
1. ExceptionHandlingMiddleware     ← Apanha QUALQUER exceção (deve ser primeiro)
    │
    ▼
2. SecurityHeadersMiddleware       ← Adiciona headers OWASP a todas as respostas
    │
    ▼
3. CorrelationIdMiddleware         ← Gera GUID para rastreamento nos logs
    │
    ▼
4. RateLimiterMiddleware           ← Verifica limite por IP (429 se excedido)
    │
    ▼
5. HTTPS Redirection               ← Redireciona HTTP → HTTPS
    │
    ▼
6. Controller Routing               ← Encontra o endpoint correto
    │
    ▼
7. Action Filters                   ← ValidateWhatsAppSignatureFilter OU ValidateTeamsJwtFilter
    │
    ▼
8. Controller Method                ← WebhookController.ReceiveWhatsAppMessage() etc.
    │
    ▼
   Response (com headers de segurança + Correlation ID)
```

### Porquê esta ordem?
1. **ExceptionHandling primeiro** — Se o SecurityHeaders ou o CorrelationId falharem, o erro é apanhado.
2. **SecurityHeaders antes do CorrelationId** — Os headers de segurança devem estar em TODAS as respostas, incluindo erros 429 do rate limiter.
3. **CorrelationId antes do RateLimiter** — Mesmo que o request seja rejeitado por rate limit, fica com Correlation ID nos logs.
4. **RateLimiter antes dos controllers** — Requests excessivos são bloqueados antes de chegarem ao código de negócio.
5. **Action Filters** executam dentro do controller pipeline — são específicos por endpoint (HMAC para WhatsApp, JWT para Teams).

---

## 14. Anti-Spam (Filter + Guard + MessageProcessingService) ⭐ ATUALIZADO

**Ficheiros:** `Api/Middleware/WhatsAppConcurrencyGuardFilter.cs` + `Application/WebhookConcurrencyGuard.cs` + `Application/MessageProcessingService.cs`  
**Implementação:** lock por remetente + dedup por `MessageId` + `SentAt` com grace de 1s + delayed unlock

### Ataque Prevenido: Spam / Flood / Duplicate Submission
- **O que é:** O utilizador carrega repetidamente no botão "enviar" (WhatsApp, Teams), inundando o bot com mensagens iguais.
- **Impacto sem proteção:** O bot responde a cada mensagem, criando respostas duplicadas e confusão no chat.
- **Problema:** WhatsApp Web pode gerar rajadas muito rápidas e reentregas repetidas.
- **Exemplo real:**
  ```
  Sem anti-spam:
    User: "ajuda" ×10 (T=0 a T=3s)
    Bot responde 3-4 vezes (ondas de webhooks)

  Com guard + lock + grace:
    User: "presente" (T=0s) → Bot processa e responde
    User: "l" x20 (T=0.1s..2s) → Mensagens extra bloqueadas por lock do remetente
    User envia mensagem nova real → Processada normalmente
    = 1 resposta útil + sem backlog infinito
  ```

### Proteção 1: Filtro de Backlog + SentAt com Grace

Com confirmação pendente, só `sim`/`não` pode bypassar filtros.

1. **Backlog por `ReceivedAt`:**
  - `msg.ReceivedAt <= lastResponse` → mensagem pertence ao burst anterior → ignorada.

2. **Timestamp do telemóvel + grace (1s):**
  - `msg.SentAt <= lastResponse + 1s` → ignorada como spam.

3. **Confirmação pendente (strict):**
  - `sim`/`não` entram imediatamente;
  - qualquer outro texto continua sujeito ao anti-spam.

#### `RecordResponseTime(string userId)` / `RecordResponseTime(string userId, DateTime responseTime)`
- Regista o momento da última resposta ao utilizador.
- Chamado no **`finally`** do `ProcessMessageAsync`, ANTES do `DelayedUnlockAsync`.
- Overload com `DateTime` disponível para testes.

#### `GetLastResponseTime(string userId)`
- Obtém o timestamp da última resposta ao utilizador (ou `null` se nunca respondemos).

### Proteção 2: Lock por Remetente (WebhookConcurrencyGuard)

#### `TryAcquireSenderLock(string senderId)`
- Tenta adquirir lock para o remetente.
- Retorna `true` se lock foi adquirido.
- Retorna `false` se já existe processamento em curso para o mesmo remetente.

#### `ReleaseSenderLock(string senderId)`
- Liberta explicitamente o lock no `finally` após o processamento.

#### TTL de segurança
- `SenderLockTtlSeconds = 5` como fallback se houver falha antes do `finally`.

### Constantes de Configuração

| Constante | Valor | Descrição |
|-----------|-------|-----------|
| `POST_RESPONSE_GRACE_SECONDS` | 1 | Margem de `SentAt` para compensar arredondamento do WhatsApp |
| `DELAYED_UNLOCK_SECONDS` | 2 | Delay curto para absorver ondas imediatas após resposta |
| `SenderLockTtlSeconds` | 5 | TTL de segurança do lock por remetente (fallback) |
| `MessageIdTtlSeconds` | 300 | TTL da deduplicação por `MessageId` |

### Cobertura Temporal (simplificada)

```
T=0       Bot responde
T=0..2s   Delayed unlock pode absorver ondas imediatas
sempre    Backlog filter (`ReceivedAt <= lastResponse`) bloqueia mensagens antigas
sempre    `SentAt <= lastResponse + 1s` bloqueia spam do mesmo burst
```

### Re-tap Protection (Confirmação Pendente)
Durante uma confirmação pendente, se o utilizador re-envia o mesmo comando original:
- O re-envio é ignorado silenciosamente.
- Não conta como tentativa inválida de sim/não.
- O utilizador não "queima" as 3 tentativas disponíveis.

---

## 🧪 Testes de Segurança

Todas as proteções têm testes:

| Componente | Ficheiro de Teste | Nº Testes |
|------------|-------------------|-----------|
| Security Headers | SecurityHeadersIntegrationTests.cs | 10 |
| HMAC WhatsApp | ValidateWhatsAppSignatureFilterTests.cs | 6 |
| JWT Teams | ValidateTeamsJwtFilterTests.cs | 7 |
| Exception Handling | ExceptionHandlingMiddlewareTests.cs | 4 |
| Correlation ID | HealthIntegrationTests.cs (header check) | 1 |
| Rate Limiting | Testado via integração (429 responses) | — |
| Config Validation | ConfigurationValidatorTests.cs | 10+8 |
| Anti-Spam Triplo (Cooldown + Lock + Timestamp) | UserProcessingLockTests.cs | 42 |
| Confirmações Contextuais | ContextualConfirmationTests.cs | 8 |
| **Total** | | **94+ testes de segurança** |

---

**Versão:** 7.2 (Proteção Anti-Spam Tripla — Cooldown 10s + Delayed Lock 5s + Phone Timestamp)  
**Data:** 05/03/2026  
**Referências:** [OWASP Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/DotNet_Security_Cheat_Sheet.html) · [OWASP Secure Headers](https://owasp.org/www-project-secure-headers/) · [Microsoft Rate Limiting](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit)
