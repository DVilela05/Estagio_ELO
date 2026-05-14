# 🐛 Registro de Problemas & Bugs Identificados

## 📋 Índice
- [Problema #1](#problema-1-processo-lock-durante-build)
- [Problema #2](#problema-2-null-literal-warnings-em-testes)
- [Problema #3](#problema-3-mensagem-sim-recebida-duas-vezes)
- [Nota #4](#nota-4-refactoring-para-multi-plataforma-v30)
- [Problema #5](#problema-5-moq-não-consegue-mockar-ihostenvironment)
- [Problema #6](#problema-6-tipo-de-resultado-unauthorizedobjectresult-vs-unauthorizedresult)
- [Problema #7](#problema-7-corrupção-de-ficheiro-durante-multi-edit)
- [Problema #8](#problema-8-exceptionhandlingmiddleware-retorna-500-não-400)
- [Problema #9](#problema-9-addfixedwindowlimiter-não-existe-no-aspnet-core-80)
- [Problema #10](#problema-10-validação-de-configuração-antes-do-build)
- [Problema #11](#problema-11-spam-de-mensagens-duplicadas-whatsapp) ⭐ NOVO
- [Problema #12](#problema-12-re-tap-queima-tentativas-de-confirmação) ⭐ NOVO
- [Problema #13](#problema-13-spam-em-confirmação-consome-tentativas-e-desalinha-respostas-whatsapp-web) ⭐ NOVO

---

## Problema #1: Processo Lock Durante Build

**Data:** 18/02/2026  
**Severidade:** 🔴 Alta  
**Status:** ✅ Resolvido

### Descrição
Ao tentar fazer build do projeto, recebia erro:
```
error MSB3021: Unable to copy file "WebApplication1.exe" 
because it is being used by another process.
```

### Causa
O processo `WebApplication1.exe` (PID 21584) estava em execução e bloqueava o ficheiro.

### Solução
```powershell
Stop-Process -ProcessName "WebApplication1" -Force
# Aguardar 1 segundo
Start-Sleep -Seconds 1
# Tentar build novamente
dotnet build
```

### Resultado
✅ Build bem-sucedido após parar o processo

---

## Problema #2: Null Literal Warnings em Testes

**Data:** 18/02/2026  
**Severidade:** 🟡 Média  
**Status:** ✅ Resolvido

### Descrição
Ao compilar `ConfigurationValidatorTests.cs`, recebiam-se warnings:
```
warning CS8625: Cannot convert null literal to non-nullable reference type.
```

### Causa
O projeto usa `nullable reference types` ativado. Tests usavam `null` diretamente para verificar valores nulos, o que é redundante com `string.Empty` para whitespace.

### Solução
Substituir `null` por `string.Empty` nos testes de whitespace:

**Antes:**
```csharp
[InlineData(null)]
[InlineData("")]
[InlineData("   ")]
```

**Depois:**
```csharp
[InlineData("")]
[InlineData("   ")]
```

E para testes de null explícitos, usar `null!` ou verificação direta.

### Resultado
✅ 0 warnings (2 warnings resolvidos)

---

## Problema #3: Mensagem "Sim" Recebida Duas Vezes

**Data:** 18/02/2026  
**Severidade:** 🔴 Alta (UX)  
**Status:** ✅ Resolvido

### Descrição
Quando o utilizador enviava "Sim" para confirmar um comando, recebia **duas respostas contraditórias**:
1. ✅ "Mensagem de presença recebida com sucesso"
2. ⚠️ "Comando não encontrado"

### Causa Raiz
O sistema armazena confirmações pendentes em **memória** (`ConcurrentDictionary<string, PendingConfirmation>`).

**Fluxo do problema:**
1. User envia "Presente" → Sistema guarda confirmação pendente em memória
2. Sistema pede: "Iremos executar o pedido de presença. É isso que deseja? (sim/não)"
3. **App é reiniciada** (ou tempo de inatividade)
4. Dicionário é **limpo** (memória perdida)
5. User envia "Sim" → Sistema **não encontra** confirmação pendente
6. Sistema trata "Sim" como **comando inválido** → Responde "Comando não encontrado"

### Evidência nos Logs
```
[16:18:17] Mensagem recebida de 351932947533: "Presente"
[16:18:19] ✅ Mensagem processada com sucesso (confirmação pedida)

[16:20:07] 🚀 Iniciando aplicação... ← APP REINICIADA
[16:20:08] ✅ Aplicação iniciada com sucesso

[16:20:24] Mensagem recebida de 351932947533: "Sim"
[16:20:25] Mensagem não corresponde a nenhum comando: "sim" ← ERRO
```

### Solução Implementada
Adicionar **detecção de "sim/não órfãos"** com mensagem explicativa:

```csharp
if (IsYes(msg.Body) || IsNo(msg.Body))
{
    reply = BuildNoPendingConfirmationMessage();
    _logger.LogWarning("Utilizador {From} enviou '{Body}' sem confirmação pendente", 
                       msg.From, msg.Body);
}
```

Mensagens variadas (6 variações para não parecer repetitivo):
```
⚠️ Não tenho nenhum pedido pendente para confirmar.
Se enviaste um comando antes, pode ter expirado ou o sistema foi reiniciado.
Escreve *ajuda* para ver os comandos disponíveis.

⚠️ Hmm, não encontro nenhuma confirmação pendente.
Talvez o pedido tenha expirado ou perdeu-se durante um reinício.
Tenta enviar o comando novamente ou escreve *ajuda*.

[... 4 mais variações ...]
```

### Testes Adicionados
- `ConfirmationEdgeCasesTests.cs` - 12 novos testes
- Cobertura de todos os tokens "sim/não"
- Edge cases de texto sem confirmação pendente

### Resultado
✅ UX melhorada - Mensagens claras e variadas
✅ 12 novos testes passando
✅ Logging de warning para debug

### Melhorias Futuras (Recomendadas)
1. **Persistir confirmações em Redis** - Em vez de memória
2. **Timeout automático** - Expirar após 5 minutos
3. **Notificar ao reiniciar** - Avisar users com confirmações pendentes

---

## Nota #4: Refactoring para Multi-Plataforma (v3.0)

**Data:** 18/02/2026  
**Severidade:** 🟢 Info  
**Status:** ✅ Concluído

### Descrição
Refactoring completo do `WebhookController` para suportar Microsoft Teams como segunda plataforma. Toda a lógica (endpoints WhatsApp + Teams e processamento partilhado) foi consolidada num único `WebhookController`.

### Alterações Realizadas

**Novos ficheiros:**
- `Services/TeamsService.cs` — Implementação IMessagingService para Teams (OAuth2 + Bot Framework)
- `Models/TeamsSettings.cs` — Configuração Teams
- `Models/TeamsActivity.cs` — DTOs para Bot Framework Activity

**Ficheiros modificados:**
- `Controllers/WebhookController.cs` — Expandido com endpoints Teams e lógica partilhada consolidada
- `Models/MessagePlatform.cs` — Teams enum ativado
- `Helpers/ConsoleLogger.cs` — Teams (🟣) adicionado
- `Helpers/ConfigurationValidator.cs` — `ValidateTeamsSettings()` adicionado
- `Program.cs` — DI para TeamsService + Configure<TeamsSettings>
- `appsettings.json` — Secção Teams adicionada

**Testes adicionados:**
- `TeamsSettingsTests.cs` — 3 testes
- `TeamsActivityTests.cs` — 8 testes
- `TeamsConfigurationValidatorTests.cs` — 8 testes
- `MessagePlatformTests.cs` — 2 testes novos para Teams

### Resultado
✅ Build: 0 errors, 0 warnings  
✅ Testes: 121/121 passing (antes: 95)  
✅ Nenhum teste existente quebrado pelo refactoring  
✅ Lógica consolidada num único WebhookController

---

## Problema #5: Moq Não Consegue Mockar IHostEnvironment

**Data:** 18/02/2026  
**Severidade:** 🔴 Alta  
**Status:** ✅ Resolvido

### Descrição
Ao criar testes para `ValidateTeamsJwtFilter` e `ExceptionHandlingMiddleware`, o Moq não conseguia mockar o método de extensão `IHostEnvironment.IsDevelopment()`:
```
System.NotSupportedException: Unsupported expression: 
e => e.IsDevelopment()
Extension methods (here: HostEnvironmentEnvExtensions.IsDevelopment) may not be used in setup / verification expressions.
```

### Causa
`IsDevelopment()` é um **método de extensão** (não faz parte da interface `IHostEnvironment`). O Moq apenas pode mockar membros de interfaces/classes virtuais, não métodos de extensão estáticos.

### Solução
Criar uma classe concreta `MockHostEnvironment` que implementa `IHostEnvironment`:

```csharp
private class MockHostEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = "Development";
    public string ApplicationName { get; set; } = "TestApp";
    public string ContentRootPath { get; set; } = "";
    public IFileProvider ContentRootFileProvider { get; set; } = null!;
}
```

Usar `mockEnv.EnvironmentName = "Development"` ou `"Production"` nos testes, e o método de extensão `IsDevelopment()` funciona corretamente pois verifica internamente `EnvironmentName == "Development"`.

### Resultado
✅ Padrão `MockHostEnvironment` adoptado em todos os testes que dependem de ambiente  
✅ Usado em: ValidateTeamsJwtFilterTests, ExceptionHandlingMiddlewareTests

---

## Problema #6: Tipo de Resultado UnauthorizedObjectResult vs UnauthorizedResult

**Data:** 18/02/2026  
**Severidade:** 🟡 Média  
**Status:** ✅ Resolvido

### Descrição
Testes de filtros de segurança falhavam ao verificar o tipo de resultado:
```
Assert.IsType<UnauthorizedResult>() failed.
Expected: UnauthorizedResult
Actual:   UnauthorizedObjectResult
```

### Causa
Os filtros retornam `new UnauthorizedObjectResult(new { error = "mensagem" })` (com body), não `new UnauthorizedResult()` (sem body). São tipos diferentes no ASP.NET Core:
- `UnauthorizedResult` → 401 sem body
- `UnauthorizedObjectResult` → 401 com body JSON

### Solução
Corrigir as assertions nos testes:

**Antes:**
```csharp
Assert.IsType<UnauthorizedResult>(context.Result);
```

**Depois:**
```csharp
Assert.IsType<UnauthorizedObjectResult>(context.Result);
```

### Resultado
✅ Todos os testes de filtros passam com o tipo correto

---

## Problema #7: Corrupção de Ficheiro Durante Multi-Edit

**Data:** 18/02/2026  
**Severidade:** 🔴 Alta  
**Status:** ✅ Resolvido

### Descrição
O ficheiro `ValidateTeamsJwtFilterTests.cs` ficou corrompido durante uma operação de multi-edit automática. O conteúdo ficou com secções duplicadas e código misturado.

### Causa
Múltiplas operações de replace_string_in_file executadas simultaneamente no mesmo ficheiro entraram em conflito, resultando em edições sobrepostas.

### Solução
1. Eliminar o ficheiro corrompido
2. Recriar o ficheiro completamente de raiz
3. Aplicar edições sequenciais (uma de cada vez) em vez de paralelas no mesmo ficheiro

### Resultado
✅ Ficheiro recriado e todos os 7 testes passam  
✅ Lição aprendida: evitar edições paralelas no mesmo ficheiro

---

## Problema #8: ExceptionHandlingMiddleware Retorna 500 (Não 400)

**Data:** 18/02/2026  
**Severidade:** 🟡 Média  
**Status:** ✅ Resolvido

### Descrição
Teste esperava que o middleware retornasse 400 (BadRequest) em modo Development, mas a implementação retorna sempre 500 (InternalServerError):
```
Assert.Equal(400, response.StatusCode) failed.
Expected: 400
Actual:   500
```

### Causa
O `ExceptionHandlingMiddleware` retorna sempre **500** para exceções não tratadas (comportamento correto para erros internos). A diferença entre dev e prod é apenas no **conteúdo da mensagem**:
- **Development:** Mensagem detalhada com stack trace
- **Production:** Mensagem genérica "Ocorreu um erro interno"

### Solução
Corrigir o teste para verificar **status code 500** e diferenciar pelo **conteúdo da resposta**:

```csharp
// Dev: status 500 com mensagem detalhada
Assert.Equal(500, context.Response.StatusCode);
Assert.Contains("Test exception", responseBody);

// Prod: status 500 com mensagem genérica
Assert.Equal(500, context.Response.StatusCode);
Assert.Contains("erro interno", responseBody);
```

### Resultado
✅ Testes corretos refletem o comportamento real do middleware  
✅ 4 testes do ExceptionHandlingMiddleware passam

---

## 📊 Resumo

| Problema | Tipo | Severidade | Status |
|----------|------|-----------|--------|
| Processo lock durante build | Ambiente | Alta | ✅ Resolvido |
| Null literal warnings | Compilação | Média | ✅ Resolvido |
| Mensagem "Sim" duas vezes | UX/Lógica | Alta | ✅ Resolvido |
| Refactoring Multi-Plataforma | Refactoring | Info | ✅ Concluído |
| Moq extension method (IHostEnvironment) | Testing | Alta | ✅ Resolvido |
| UnauthorizedObjectResult vs UnauthorizedResult | Testing | Média | ✅ Resolvido |
| Corrupção de ficheiro durante multi-edit | Tooling | Alta | ✅ Resolvido |
| ExceptionHandlingMiddleware 500 vs 400 | Testing | Média | ✅ Resolvido |
| AddFixedWindowLimiter não existe | Compilação | Alta | ✅ Resolvido |
| Validação config antes do Build | Compilação | Média | ✅ Resolvido |
| Spam de mensagens duplicadas (WhatsApp) | UX/Lógica | Alta | ✅ Resolvido |
| Re-tap queima tentativas de confirmação | UX/Lógica | Alta | ✅ Resolvido |
| Spam em confirmação consome tentativas / respostas desalinhadas | UX/Lógica | Alta | ✅ Resolvido |

---

**Total de Problemas Registados:** 13 (11 problemas + 1 nota + 1 fix)  
**Total Resolvidos:** 13 (100%)  
**Problemas Abertos:** 0

---

## Problema #9: AddFixedWindowLimiter Não Existe no ASP.NET Core 8.0

**Data:** 02/03/2026  
**Severidade:** 🔴 Alta  
**Status:** ✅ Resolvido

### Descrição
Ao implementar Rate Limiting, o código `options.AddFixedWindowLimiter("webhook", ...)` não compilava:
```
error CS1061: 'RateLimiterOptions' does not contain a definition for 'AddFixedWindowLimiter'
```

### Causa
O método `AddFixedWindowLimiter` não existe como extensão direta em `RateLimiterOptions` no ASP.NET Core 8.0. A API correta usa `AddPolicy` com `RateLimitPartition.GetFixedWindowLimiter`.

### Solução
Usar o padrão `AddPolicy` com `RateLimitPartition`:

**Antes (não compila):**
```csharp
options.AddFixedWindowLimiter("webhook", opt =>
{
    opt.PermitLimit = 30;
    opt.Window = TimeSpan.FromMinutes(1);
});
```

**Depois (correto):**
```csharp
options.AddPolicy("webhook", httpContext =>
    RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 30,
            Window = TimeSpan.FromMinutes(1)
        }));
```

### Resultado
✅ Rate Limiting funciona corretamente com `AddPolicy` + `RateLimitPartition`  
✅ Build: 0 errors, 0 warnings

---

## Problema #10: Validação de Configuração Antes do Build

**Data:** 02/03/2026  
**Severidade:** 🟡 Média  
**Status:** ✅ Resolvido

### Descrição
A validação de configuração no startup tentava aceder a `app.Environment` antes de `var app = builder.Build()`:
```
error CS0103: The name 'app' does not exist in the current context
```

### Causa
O código de validação foi adicionado antes da chamada `builder.Build()`, onde `app` ainda não existia. Também referenciava `builder.Configuration` em vez de `app.Configuration`.

### Solução
Mover a validação para depois de `var app = builder.Build()` e usar `app.Configuration` e `app.Environment`:

```csharp
var app = builder.Build();

// Validação de configuração (fail-fast)
try
{
    var config = app.Configuration;
    // ... validação usando config
}
catch (ConfigurationException ex)
{
    Log.Fatal(ex, "Configuração inválida");
    if (!app.Environment.IsDevelopment()) throw;
}
```

### Resultado
✅ Validação executa corretamente após o build  
✅ Em Development: aviso + continua (para facilitar testes)  
✅ Em Production: falha imediatamente (fail-fast)

---

## Problema #11: Spam de Mensagens Duplicadas (WhatsApp)

**Data:** 02/03/2026  
**Severidade:** 🔴 Alta (UX)  
**Status:** ✅ Resolvido

### Descrição
Quando o utilizador carregava múltiplas vezes no botão "enviar" no WhatsApp (ex: "ajuda" 5× rapidamente), o bot respondia a cada mensagem individual, criando respostas duplicadas no chat.

### Evidência
Screenshots do WhatsApp mostraram:
- Utilizador enviou "ajuda" 5 vezes em rápida sucessão
- Bot respondeu 2 vezes (o cooldown inicial de 5s era demasiado curto)
- Utilizador enviou "presença" 7 vezes → bot respondeu 1 vez (ok mas frágil)

### Causa Raiz
1. **Cooldown de 5s era insuficiente** — O utilizador carregava mais rápido do que 5s mas o processo assíncrono ainda estava a responder
2. **Sem reset ao responder** — O cooldown era fixo, não resetava quando o bot respondia

### Solução Implementada (Per-User Processing Lock)

**1. Lock exclusivo por utilizador (`TryLockUser`):**
```csharp
private static readonly ConcurrentDictionary<string, byte> _usersBeingProcessed = new();

public static bool TryLockUser(string userId)
{
    if (string.IsNullOrWhiteSpace(userId)) return false;
    return _usersBeingProcessed.TryAdd(userId, 0);
}
```

**2. Unlock automático no bloco finally (`UnlockUser`):**
```csharp
// Chamado no bloco finally de ProcessMessageAsync
public static void UnlockUser(string userId)
{
    if (!string.IsNullOrWhiteSpace(userId))
        _usersBeingProcessed.TryRemove(userId, out _);
}
```

### Testes Adicionados
- `UserProcessingLockTests.cs` — 23 testes (19 métodos)
- Cenários: lock, unlock, concorrência, multi-user, null safety

### Resultado
✅ WhatsApp: 5× "ajuda" → apenas 1 resposta  
✅ Teams: mesma proteção  
✅ 23 novos testes passando  
✅ Lock libertado quando bot termina (finally) → próxima mensagem aceite

---

## Problema #12: Re-tap Queima Tentativas de Confirmação

**Data:** 02/03/2026  
**Severidade:** 🔴 Alta (UX)  
**Status:** ✅ Resolvido

### Descrição
Quando o utilizador estava em modo de confirmação (sistema perguntou "Confirmas presença? sim/não") e voltava a carregar no mesmo comando ("ajuda" ou "presença"), essas mensagens eram tratadas como tentativas inválidas de resposta sim/não, "queimando" as 3 tentativas disponíveis.

### Evidência
Screenshots mostraram:
```
User: "ajuda"
Bot:  "Confirmas ajuda? (sim/não)"
User: "ajuda" (re-tap acidental)
Bot:  "❓ Responde SIM ou NÃO. Tentativas restantes: 2"  ← queimou tentativa!
User: "ajuda" (re-tap acidental)
Bot:  "❓ Responde SIM ou NÃO. Tentativas restantes: 1"  ← queimou outra!
```

### Causa Raiz
O sistema tratava QUALQUER mensagem que não fosse "sim" ou "não" como tentativa inválida, incluindo re-envios do mesmo comando original.

### Solução Implementada (Re-tap Protection)
O per-user processing lock resolve o problema:
- Enquanto o bot está a processar, o utilizador está locked (`TryLockUser` retorna false)
- TODAS as mensagens subsequentes do mesmo utilizador são ignoradas (não apenas a mesma mensagem)
- Quando o bot termina, `UnlockUser` é chamado no bloco `finally`
- O utilizador pode então enviar novas mensagens normalmente

### Testes Adicionados
- Testes incluídos em `UserProcessingLockTests.cs` (23 testes total)
- Verificam que lock/unlock funciona e que mensagens durante lock são ignoradas

### Resultado
✅ Re-taps durante processamento são ignorados silenciosamente  
✅ Tentativas de sim/não preservadas  
✅ UX limpa sem mensagens confusas

---

## Problema #13: Spam em Confirmação Consome Tentativas e Desalinha Respostas (WhatsApp Web)

**Data:** 16/03/2026  
**Severidade:** 🔴 Alta (UX)  
**Status:** ✅ Resolvido

### Descrição
Com confirmação pendente (ex.: após `presente`), bursts rápidos no WhatsApp Web (`l l l l ...`) eram processados como tentativas inválidas de sim/não, consumindo tentativas e gerando respostas fora de contexto.

### Causa Raiz
1. O bypass de confirmação estava demasiado permissivo para mensagens com confirmação pendente.  
2. Fila por utilizador em sequência ainda permitia backlog grande em burst infinito.  
3. Algumas mensagens antigas podiam chegar depois e competir com mensagens mais recentes.

### Solução Implementada
1. **Bypass estrito**: só `sim`/`não` pode bypassar lock/filtros quando há confirmação pendente.  
2. **Guard no webhook** (`WhatsAppConcurrencyGuardFilter` + `WebhookConcurrencyGuard`): deduplicação por `MessageId` (5 min) + lock por remetente (5s fallback).  
3. **Libertação explícita de lock** no `finally` após processamento da mensagem.  
4. **Filtro `SentAt` + grace (1s)** e **delayed unlock (2s)** no `MessageProcessingService`.

### Resultado
✅ Spam durante confirmação deixa de consumir tentativas  
✅ Redução de respostas desalinhadas em rajadas do WhatsApp Web  
✅ Só a primeira mensagem do burst é processada até resposta do bot

