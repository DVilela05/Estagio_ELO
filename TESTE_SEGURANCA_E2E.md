# Guia de Testes — Segurança e Integração Webhook ↔ Web Service

## Objetivo
Validar que o bot e o web service comunicam de forma segura e que o fluxo de presença funciona de ponta a ponta.

---

## 1. Testes automáticos

### 1.1 Executar toda a suíte
Na raiz do repositório:

```powershell
dotnet test WebApplication1.Tests/WebApplication1.Tests.csproj
```

### 1.2 O que os testes cobrem
- configuração do Business API
- validação de segurança do Business API
- cliente HTTP assinado com token + HMAC
- validação do `WhatsAppSignatureFilter`
- validação do `TeamsJwtFilter`
- comando `presença`
- confirmações contextuais
- roteamento de comandos

### 1.3 Resultado esperado
- `Passed: 304`
- `Failed: 0`
- `Skipped: 0`

---

## 2. Configuração necessária para produção

### 2.1 WebApplication1
Definir:
- `BusinessApi:BaseUrl`
- `BusinessApi:AttendancePath`
- `BusinessApi:ServiceToken`
- `BusinessApi:HmacSecret`
- `BusinessApi:AllowInsecureHttp = false`

### 2.2 WebServerEstagioELO
Definir/verificar no endpoint `POST /api/attendance`:
- validação do token Bearer
- validação da assinatura HMAC
- validação do timestamp
- validação do nonce para evitar replay
- resposta JSON com `success`, `message` e `errorCode`

Configuracao de segredos no webservice (user-secrets):
```powershell
cd D:\estagio\WebServerEstagioELO_\WebServerEstagioELO\WebServerEstagioElo
dotnet user-secrets set "AttendanceWebhookSecurity:ServiceToken" "seu_service_token"
dotnet user-secrets set "AttendanceWebhookSecurity:HmacSecret" "seu_hmac_secret"
```

---

## 3. Teste manual do fluxo principal

### Passo a passo
1. Arrancar o web service (`WebServerEstagioELO`).
2. Arrancar o bot (`WebApplication1`).
3. No WhatsApp ou Teams, enviar `presente`.
4. Confirmar com `sim`.
5. Enviar a localização da própria app.

### Resultado esperado
- o bot pede confirmação da presença
- o bot pede a localização após o `sim`
- a presença só é enviada ao web service depois da localização válida
- o web service responde com sucesso
- os logs mostram `userId`, `userName`, `attendanceType`, `platform`, `timestamp` e, quando existir, `userPhone`/`userEmail`

---

## 4. Testes de segurança da comunicação

### 4.1 Sem token
- Remover `Authorization`.
- Esperado: rejeição com erro de autenticação.

### 4.2 Token inválido
- Usar token diferente do configurado.
- Esperado: rejeição.

### 4.3 HMAC inválido
- Alterar a assinatura ou o body depois de assinar.
- Esperado: rejeição.

### 4.4 Timestamp expirado
- Enviar um timestamp antigo.
- Esperado: rejeição.

### 4.5 Nonce repetido
- Repetir o mesmo nonce num segundo pedido.
- Esperado: rejeição por replay attack.

### 4.6 HTTP em produção
- Usar `http://` em ambiente de produção.
- Esperado: falha na validação de configuração.

---

## 5. Testes de anti-spam

### 5.1 Spam de presença
- Enviar várias mensagens `presente` muito rapidamente.
- Esperado:
  - responde à primeira mensagem
  - ignora as restantes
  - não consome tentativas extras indevidamente

### 5.2 Spam durante confirmação
- Enviar `presente` várias vezes antes de responder `sim`.
- Esperado:
  - o sistema não duplica o pedido de confirmação
  - mensagens repetidas contam apenas como ruído controlado

### 5.3 Spam após resposta
- Enviar spam logo após o bot responder.
- Esperado:
  - mensagens anteriores à resposta do servidor são bloqueadas
  - não há respostas duplicadas

---

## 6. Teste prático de integração HTTP

### Verificar serviço de negócio
- `GET /health` deve responder `200`
- `POST /api/attendance` deve responder `200` com payload válido

### Verificar bot
- O bot deve chamar o endpoint configurado em `BusinessApi:AttendancePath`
- Deve enviar `Authorization: Bearer ...`
- Deve enviar headers de assinatura HMAC e nonce

---

## 7. Ficheiros importantes

- [WebApplication1/Program.cs](WebApplication1/Program.cs)
- [WebApplication1/Infrastructure/ExternalApis/BusinessApiClient.cs](WebApplication1/Infrastructure/ExternalApis/BusinessApiClient.cs)
- [WebApplication1/Infrastructure/Configuration/BusinessApiSettings.cs](WebApplication1/Infrastructure/Configuration/BusinessApiSettings.cs)
- [WebApplication1/Infrastructure/Configuration/ConfigurationValidator.cs](WebApplication1/Infrastructure/Configuration/ConfigurationValidator.cs)
- [WebApplication1/Core/Commands/PresencaCommandHandler.cs](WebApplication1/Core/Commands/PresencaCommandHandler.cs)
- [WebApplication1/Application/MessageProcessingService.cs](WebApplication1/Application/MessageProcessingService.cs)

---

## 8. Checklist rápido antes de produção

- [ ] HTTPS ativo
- [ ] `ServiceToken` definido
- [ ] `HmacSecret` definido
- [ ] `AllowInsecureHttp = false`
- [ ] `POST /api/attendance` a responder corretamente
- [ ] testes automatizados a passar
- [ ] logs sem expor segredos
- [ ] webhook e web service com segredos diferentes por ambiente

---

## 9. Notas finais

Se qualquer um dos testes de segurança falhar, o pedido deve ser rejeitado e não deve marcar presença.
