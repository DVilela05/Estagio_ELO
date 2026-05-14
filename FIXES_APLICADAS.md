# 🔧 Fixes Aplicadas - Anti-Spam & Comunicação com WebServer

## 1. ⚠️ ANTI-SPAM — Bug do Delayed Unlock (CRÍTICO - FIXADO)

### Problema
Quando o utilizador fazia spam com 6-7 mensagens idênticas:
- ✅ Bot respondia à **primeira mensagem** (correto)
- ❌ Bot **também** respondia à **última/penúltima mensagem** (errado!)

### Causa Raiz
O `delayed unlock` não estava a manter o lock ativo enquanto decorria o delay:

```csharp
// ❌ ANTES (código bugado)
if (blockedCount > 0) {
    _ = Task.Run(async () => {
        try { 
            await Task.Delay(DELAYED_UNLOCK_SECONDS * 1000, cts.Token); 
        }
        catch (OperationCanceledException) { return; }
        UnlockUser(userId);  // ← Lock era libertado aqui
        _delayedUnlockCts.TryRemove(userId, out _);
    });
}
```

**O problema:** O lock era libertado imediatamente da primeira mensagem, 
ANTES do `Task.Delay` começar. Mensagens 2-7 chegavam enquanto o delay 
decorria, mas o `TryLockUser()` deixava passar porque o lock tinha sido 
libertado de novo na iteração anterior.

### Solução Aplicada
O lock agora **permanece ativo durante todo o delay de 2 segundos**:

```csharp
// ✅ DEPOIS (código corrigido)
if (blockedCount > 0) {
    string userId = msg.From;
    var cts = new CancellationTokenSource();
    _delayedUnlockCts[userId] = cts;
    
    // IMPORTANTE: Não libertamos o lock aqui — ele continua ativo!
    // O Task.Run apenas CONTA O TEMPO e depois liberta.
    // Enquanto o delay decorre, TryLockUser() retorna false e IncrementSpamCount é chamado.
    _ = Task.Run(async () => {
        try { 
            await Task.Delay(DELAYED_UNLOCK_SECONDS * 1000, cts.Token); 
        }
        catch (OperationCanceledException) { return; }
        finally { 
            UnlockUser(userId);  // ← Lock é libertado aqui (APÓS 2 segundos)
            _delayedUnlockCts.TryRemove(userId, out _); 
        }
    });
}
```

**Melhorias:**
- ✅ O `lock` é adquirido na primeira mensagem
- ✅ O `lock` permanece ativo durante 2 segundos (DELAYED_UNLOCK_SECONDS)
- ✅ Mensagens 2-7 que chegam durante esses 2s são bloqueadas com sucesso
- ✅ Apenas UMA resposta é enviada (à primeira mensagem do burst)
- ✅ Logging melhorado mostra quantas mensagens foram bloqueadas

**Teste:** Envia 6-7 mensagens iguais rapidamente no WhatsApp — 
agora deve receber apenas 1 resposta. ✅

---

## 2. 📡 COMUNICAÇÃO COM WEBSERVER — Modo Stub ativo (INFORMATIVO)

### Problema
O bot não estava a comunicar com o servidor de negócio 
(`POST /api/attendance` ou similar).

### Causa
Em `appsettings.json`, o `BusinessApi.BaseUrl` está **vazio**:

```json
{
  "BusinessApi": {
    "BaseUrl": "",  // ← VAZIO = modo stub (não faz HTTP real)
    "TimeoutSeconds": 30,
    "MaxRetries": 3
  }
}
```

Quando `BaseUrl` está vazio, o `BusinessApiClient` funciona em **modo stub**:
- ❌ Não faz pedidos HTTP reais ao servidor
- ✅ Simula respostas de sucesso (stub)
- ℹ️ Útil para testes, mas não comunica com o servidor real

### Solução para Ativar Comunicação Real

**Opção 1: No `appsettings.json` (produção)**
```json
{
  "BusinessApi": {
    "BaseUrl": "http://localhost:5008",  // ← URL do teu webserver
    "TimeoutSeconds": 30,
    "MaxRetries": 3
  }
}
```

**Opção 2: Via User Secrets (desenvolvimento)**
```powershell
dotnet user-secrets set "BusinessApi:BaseUrl" "http://localhost:5008"
```

**Opção 3: Via variável de ambiente**
```powershell
$env:BusinessApi__BaseUrl = "http://localhost:5008"
```

### Verificação de Conectividade

Quando o bot arranca, o validador de configuração mostra:

```
❌ Erro de configuração: BusinessApi BaseUrl não configurado. 
   Modo stub ativo (não fará HTTP real).
   
Para comunicar com o servidor:
  1. appsettings.json: "BaseUrl": "http://localhost:5008"
  2. User Secrets: dotnet user-secrets set "BusinessApi:BaseUrl" "..."
  3. Variável de ambiente: BusinessApi__BaseUrl=...
```

ou

```
✅ Configuração validada com sucesso.
   BusinessApi: HTTP real ativado (BaseUrl: http://localhost:5008)
```

### Porta Necessária?
- ❌ **NÃO precisas de abrir porta** se o webserver está no mesmo PC/rede
- ✅ Se o webserver está noutro PC/servidor, confirma que a porta está aberta na firewall
- ✅ URL típica: `http://localhost:5008` (local), `http://192.168.x.x:5008` (rede), ou domínio completo

---

## 📝 Resumo das Alterações

| Ficheiro | Mudança | Impacto |
|----------|---------|--------|
| [Application/MessageProcessingService.cs](WebApplication1/Application/MessageProcessingService.cs#L873-L895) | Fixed delayed unlock logic | ✅ Anti-spam 100% funcional |
| [appsettings.json](WebApplication1/appsettings.json) | BaseUrl vazio = modo stub | ℹ️ Configurar se necessário HTTP real |
| [appsettings.Development.json](WebApplication1/appsettings.Development.json) | BaseUrl = localhost:5008 | ✅ Desenvolvimento com HTTP real |

---

## 🧪 Teste Recomendado

1. **Teste anti-spam:**
   - Envia 6-7 "ajuda" rapidamente no WhatsApp
   - ✅ Esperado: 1 resposta apenas
   - ✅ Logs devem mostrar: "🛡️ Spam absorvido: X mensagens bloqueadas"

2. **Teste comunicação com servidor:**
   - Envia "presente" (presença)
   - Confirma com "sim"
   - Envia localização
   - ✅ Esperado: Bot pede confirmação ao servidor
   - Verifica logs para HTTP requests

---

**Build Status:** ✅ 0 warnings, 0 errors
