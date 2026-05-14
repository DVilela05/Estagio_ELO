using Moq;
using WebApplication1.Application;
using Xunit;

namespace WebApplication1.Tests
{
    /// <summary>
    /// Testes para a proteção anti-spam tripla:
    /// 
    /// 1. Timestamp: msgs enviadas ANTES da última resposta do bot são ignoradas.
    ///    Protege contra spam muito atrasado (webhook delay >5s do WhatsApp).
    /// 
    /// 2. Lock por utilizador: cada userId só pode ter UMA mensagem em processamento.
    ///    Todas as seguintes são ignoradas até o lock ser libertado.
    /// 
    /// 3. Delayed Unlock: o lock fica ativo 5 segundos APÓS o bot enviar a resposta.
    ///    Absorve "ondas" de spam que o WhatsApp entrega ao longo de vários segundos.
    ///    (testado implicitamente — DelayedUnlockAsync é private, usa Task.Delay)
    /// 
    /// Cenários testados:
    ///   - TryLockUser: primeira chamada adquire o lock
    ///   - TryLockUser: chamadas seguintes (mesmo user) são bloqueadas
    ///   - TryLockUser: users diferentes não interferem entre si
    ///   - UnlockUser: liberta o lock para processar nova mensagem
    ///   - IsUserLocked: verifica estado do lock
    ///   - Ciclo completo: lock → unlock → re-lock
    ///   - IsLateMessage: msgs enviadas antes da última resposta → true
    ///   - IsLateMessage: msgs enviadas depois da última resposta → false
    ///   - RecordResponseTime / GetLastResponseTime: registo e consulta
    ///   - ResetUserState: limpa lock + timestamp
    ///   - Entradas null/vazias → tratadas sem exceções
    /// </summary>
    public class UserProcessingLockTests
    {
        // =====================================================================
        // TryLockUser — adquirir o lock
        // =====================================================================

        [Fact]
        public void TryLockUser_FirstCall_ShouldSucceed()
        {
            // Arrange — user ID único para este teste
            string userId = $"lock-first-{Guid.NewGuid()}";

            // Act
            bool result = MessageProcessingService.TryLockUser(userId);

            // Assert — primeira mensagem sempre adquire o lock
            Assert.True(result, "A primeira mensagem de um utilizador deve adquirir o lock");

            // Cleanup
            MessageProcessingService.UnlockUser(userId);
        }

        [Fact]
        public void TryLockUser_SecondCall_SameUser_ShouldFail()
        {
            // Arrange
            string userId = $"lock-second-{Guid.NewGuid()}";

            // Act — primeira adquire, segunda é bloqueada
            MessageProcessingService.TryLockUser(userId);
            bool secondCall = MessageProcessingService.TryLockUser(userId);

            // Assert
            Assert.False(secondCall, "Segunda mensagem do mesmo user deve ser bloqueada");

            MessageProcessingService.UnlockUser(userId);
        }

        [Fact]
        public void TryLockUser_ThirdCall_SameUser_ShouldStillFail()
        {
            // Arrange
            string userId = $"lock-third-{Guid.NewGuid()}";

            // Act — simula triplo clique rápido
            MessageProcessingService.TryLockUser(userId);
            MessageProcessingService.TryLockUser(userId);
            bool thirdCall = MessageProcessingService.TryLockUser(userId);

            // Assert
            Assert.False(thirdCall, "Terceira mensagem do mesmo user deve continuar bloqueada");

            MessageProcessingService.UnlockUser(userId);
        }

        [Fact]
        public void TryLockUser_RapidFireSameUser_OnlyFirstSucceeds()
        {
            // Arrange — simula 5 mensagens rápidas do mesmo user
            string userId = $"lock-rapid-{Guid.NewGuid()}";
            var results = new List<bool>();

            // Act
            for (int i = 0; i < 5; i++)
            {
                results.Add(MessageProcessingService.TryLockUser(userId));
            }

            // Assert — apenas a primeira deve ter sucesso
            Assert.True(results[0], "Primeira mensagem deve adquirir o lock");
            Assert.All(results.Skip(1), r =>
                Assert.False(r, "Mensagens seguintes devem ser bloqueadas"));

            MessageProcessingService.UnlockUser(userId);
        }

        // =====================================================================
        // Users DIFERENTES — locks independentes
        // =====================================================================

        [Fact]
        public void TryLockUser_DifferentUsers_ShouldBothSucceed()
        {
            // Arrange — dois users diferentes
            string user1 = $"lock-u1-{Guid.NewGuid()}";
            string user2 = $"lock-u2-{Guid.NewGuid()}";

            // Act
            bool r1 = MessageProcessingService.TryLockUser(user1);
            bool r2 = MessageProcessingService.TryLockUser(user2);

            // Assert — users diferentes não interferem entre si
            Assert.True(r1, "User1 deve adquirir o lock");
            Assert.True(r2, "User2 deve adquirir o lock (independente do User1)");

            MessageProcessingService.UnlockUser(user1);
            MessageProcessingService.UnlockUser(user2);
        }

        [Fact]
        public void ManyUsers_ShouldWorkIndependently()
        {
            // Arrange — 10 users diferentes
            var userIds = new List<string>();

            for (int i = 0; i < 10; i++)
            {
                string userId = $"lock-stress-{i}-{Guid.NewGuid()}";
                userIds.Add(userId);
            }

            // Act — todos adquirem lock
            var results = userIds.Select(id => MessageProcessingService.TryLockUser(id)).ToList();

            // Assert — todos devem ter sucesso (users diferentes)
            Assert.All(results, r => Assert.True(r));

            // Assert — todos devem estar locked
            Assert.All(userIds, id => Assert.True(MessageProcessingService.IsUserLocked(id)));

            // Cleanup — unlock todos
            foreach (var id in userIds)
                MessageProcessingService.UnlockUser(id);

            // Assert — todos devem estar unlocked
            Assert.All(userIds, id => Assert.False(MessageProcessingService.IsUserLocked(id)));
        }

        // =====================================================================
        // UnlockUser — libertar o lock
        // =====================================================================

        [Fact]
        public void UnlockUser_ThenTryLock_ShouldSucceed()
        {
            // Arrange
            string userId = $"lock-unlock-{Guid.NewGuid()}";

            // Act — lock, unlock, lock de novo
            MessageProcessingService.TryLockUser(userId);
            MessageProcessingService.UnlockUser(userId);
            bool result = MessageProcessingService.TryLockUser(userId);

            // Assert — após UnlockUser, deve poder adquirir o lock novamente
            Assert.True(result, "Após UnlockUser (bot respondeu), nova mensagem deve ser processada");

            MessageProcessingService.UnlockUser(userId);
        }

        [Fact]
        public void UnlockUser_OnlyAffectsTargetUser()
        {
            // Arrange — dois users
            string user1 = $"lock-target-u1-{Guid.NewGuid()}";
            string user2 = $"lock-target-u2-{Guid.NewGuid()}";

            // Act — ambos adquirem lock
            MessageProcessingService.TryLockUser(user1);
            MessageProcessingService.TryLockUser(user2);

            // Unlock APENAS o user1
            MessageProcessingService.UnlockUser(user1);

            bool r1 = MessageProcessingService.TryLockUser(user1);
            bool r2 = MessageProcessingService.TryLockUser(user2);

            // Assert — user1 desbloqueado, user2 ainda bloqueado
            Assert.True(r1, "User1 foi desbloqueado, deve adquirir lock novamente");
            Assert.False(r2, "User2 NÃO foi desbloqueado, deve continuar bloqueado");

            MessageProcessingService.UnlockUser(user1);
            MessageProcessingService.UnlockUser(user2);
        }

        [Fact]
        public void UnlockUser_DoubleFree_ShouldNotThrow()
        {
            // Arrange
            string userId = $"lock-dblunlock-{Guid.NewGuid()}";
            MessageProcessingService.TryLockUser(userId);
            MessageProcessingService.UnlockUser(userId);

            // Act & Assert — unlock duplo não deve dar erro
            var ex = Record.Exception(() => MessageProcessingService.UnlockUser(userId));
            Assert.Null(ex);
        }

        // =====================================================================
        // Ciclo completo — lock → processar → unlock → relock
        // =====================================================================

        [Fact]
        public void FullCycle_LockProcessUnlockRelock()
        {
            // Arrange — simula o ciclo completo de vida de uma mensagem
            string userId = $"lock-cycle-{Guid.NewGuid()}";

            // Step 1: utilizador envia mensagem → lock adquirido
            bool lock1 = MessageProcessingService.TryLockUser(userId);
            Assert.True(lock1, "Lock inicial deve ter sucesso");

            // Step 2: durante processamento, mensagem duplicada → bloqueada
            bool blocked = MessageProcessingService.TryLockUser(userId);
            Assert.False(blocked, "Mensagem durante processamento deve ser bloqueada");

            // Step 3: bot envia resposta → lock libertado
            MessageProcessingService.UnlockUser(userId);

            // Step 4: utilizador envia nova mensagem → lock adquirido de novo
            bool lock2 = MessageProcessingService.TryLockUser(userId);
            Assert.True(lock2, "Após resposta do bot, nova mensagem deve ser processada");

            // Step 5: mensagem duplicada → bloqueada
            bool blocked2 = MessageProcessingService.TryLockUser(userId);
            Assert.False(blocked2, "Nova mensagem durante processamento deve ser bloqueada");

            // Step 6: bot responde outra vez → lock libertado
            MessageProcessingService.UnlockUser(userId);

            // Step 7: mais uma mensagem → funciona
            bool lock3 = MessageProcessingService.TryLockUser(userId);
            Assert.True(lock3, "Após segunda resposta, mensagem deve funcionar");

            MessageProcessingService.UnlockUser(userId);
        }

        [Fact]
        public void FullCycle_MultipleDifferentUsers_IndependentLocks()
        {
            // Arrange
            string user1 = $"lock-multi-1-{Guid.NewGuid()}";
            string user2 = $"lock-multi-2-{Guid.NewGuid()}";

            // Act & Assert — ambos adquirem lock
            Assert.True(MessageProcessingService.TryLockUser(user1));
            Assert.True(MessageProcessingService.TryLockUser(user2));

            // Ambos bloqueados
            Assert.False(MessageProcessingService.TryLockUser(user1));
            Assert.False(MessageProcessingService.TryLockUser(user2));

            // Unlock apenas user1
            MessageProcessingService.UnlockUser(user1);
            Assert.True(MessageProcessingService.TryLockUser(user1));  // user1 pode novamente
            Assert.False(MessageProcessingService.TryLockUser(user2)); // user2 ainda bloqueado

            MessageProcessingService.UnlockUser(user1);
            MessageProcessingService.UnlockUser(user2);
        }

        // =====================================================================
        // Mensagens DIFERENTES do mesmo user → TAMBÉM bloqueadas (key difference!)
        // =====================================================================

        [Fact]
        public void TryLockUser_DifferentMessages_SameUser_ShouldStillBlock()
        {
            // Arrange — o lock é por USER, não por mensagem
            // Mesmo que envie "ajuda" e depois "presente", está bloqueado
            string userId = $"lock-diffmsg-{Guid.NewGuid()}";

            // Act — primeiro lock (como se fosse "ajuda")
            MessageProcessingService.TryLockUser(userId);

            // Segundo lock (como se fosse "presente") — não interessa a mensagem
            bool result = MessageProcessingService.TryLockUser(userId);

            // Assert — lock é por user, não por mensagem
            Assert.False(result, "Mesmo com mensagem diferente, user bloqueado deve continuar bloqueado");

            MessageProcessingService.UnlockUser(userId);
        }

        // =====================================================================
        // IsUserLocked — verificar estado do lock
        // =====================================================================

        [Fact]
        public void IsUserLocked_WhenLocked_ShouldReturnTrue()
        {
            string userId = $"lock-check-{Guid.NewGuid()}";
            MessageProcessingService.TryLockUser(userId);

            Assert.True(MessageProcessingService.IsUserLocked(userId),
                "User com lock ativo deve retornar true");

            MessageProcessingService.UnlockUser(userId);
        }

        [Fact]
        public void IsUserLocked_WhenNotLocked_ShouldReturnFalse()
        {
            string userId = $"lock-check-no-{Guid.NewGuid()}";

            Assert.False(MessageProcessingService.IsUserLocked(userId),
                "User sem lock deve retornar false");
        }

        [Fact]
        public void IsUserLocked_AfterUnlock_ShouldReturnFalse()
        {
            string userId = $"lock-check-after-{Guid.NewGuid()}";
            MessageProcessingService.TryLockUser(userId);
            MessageProcessingService.UnlockUser(userId);

            Assert.False(MessageProcessingService.IsUserLocked(userId),
                "User após unlock deve retornar false");
        }

        // =====================================================================
        // Null e strings vazias — tratamento seguro
        // =====================================================================

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        public void TryLockUser_NullOrEmpty_ShouldReturnTrue(string? userId)
        {
            // Act — null/vazio → deixar passar (não bloqueia)
            bool result = MessageProcessingService.TryLockUser(userId!);

            // Assert
            Assert.True(result, "Entradas null/vazias devem retornar true (não bloquear)");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        public void IsUserLocked_NullOrEmpty_ShouldReturnFalse(string? userId)
        {
            Assert.False(MessageProcessingService.IsUserLocked(userId!),
                "Entradas null/vazias nunca devem estar locked");
        }

        [Fact]
        public void UnlockUser_NullOrEmpty_ShouldNotThrow()
        {
            // Act & Assert — não deve lançar exceção com entradas inválidas
            var ex1 = Record.Exception(() => MessageProcessingService.UnlockUser(null!));
            var ex2 = Record.Exception(() => MessageProcessingService.UnlockUser(""));
            var ex3 = Record.Exception(() => MessageProcessingService.UnlockUser("  "));

            Assert.Null(ex1);
            Assert.Null(ex2);
            Assert.Null(ex3);
        }

        [Fact]
        public void UnlockUser_NonExistentUser_ShouldNotThrow()
        {
            // Act & Assert — unlock de user que não existe não deve dar erro
            var ex = Record.Exception(() =>
                MessageProcessingService.UnlockUser($"non-existent-{Guid.NewGuid()}"));
            Assert.Null(ex);
        }

        // =====================================================================
        // IsLateMessage — filtro de mensagens atrasadas (timestamp)
        // =====================================================================

        [Fact]
        public void IsLateMessage_NoResponseRecorded_ShouldReturnFalse()
        {
            // Arrange — user nunca recebeu resposta
            string userId = $"late-no-resp-{Guid.NewGuid()}";
            DateTime sentAt = DateTime.UtcNow;

            // Act
            bool result = MessageProcessingService.IsLateMessage(userId, sentAt);

            // Assert — sem resposta anterior → não é atrasada
            Assert.False(result, "Sem resposta anterior, mensagem não deve ser considerada atrasada");
        }

        [Fact]
        public void IsLateMessage_SentBeforeLastResponse_ShouldReturnTrue()
        {
            // Arrange — resposta foi há 15s (cooldown expirado)
            // Testa especificamente o path do phone timestamp.
            string userId = $"late-before-{Guid.NewGuid()}";
            DateTime responseTime = DateTime.UtcNow.AddSeconds(-15);
            MessageProcessingService.RecordResponseTime(userId, responseTime);

            // Simula uma mensagem enviada 10 segundos antes da resposta
            DateTime sentAt = responseTime.AddSeconds(-10);

            // Act
            bool result = MessageProcessingService.IsLateMessage(userId, sentAt);

            // Assert — enviada antes da resposta → é atrasada (phone timestamp)
            Assert.True(result, "Mensagem enviada antes da última resposta deve ser detectada (phone timestamp)");

            // Cleanup
            MessageProcessingService.ResetUserState(userId);
        }

        [Fact]
        public void IsLateMessage_WithinCooldown_ShouldReturnTrue()
        {
            // Arrange — resposta acabou de ser enviada (cooldown ativo = 10s)
            string userId = $"late-cooldown-{Guid.NewGuid()}";
            MessageProcessingService.RecordResponseTime(userId);

            // Mesmo com SentAt no futuro, o cooldown server-side bloqueia
            DateTime sentAt = DateTime.UtcNow.AddSeconds(5);

            // Act
            bool result = MessageProcessingService.IsLateMessage(userId, sentAt);

            // Assert — cooldown ativo (resposta há <10s) → bloqueada
            Assert.True(result, "Mensagem dentro do cooldown de 10s deve ser bloqueada");

            MessageProcessingService.ResetUserState(userId);
        }

        [Fact]
        public void IsLateMessage_SentAfterLastResponse_AfterCooldown_ShouldReturnFalse()
        {
            // Arrange — resposta foi há 15s (cooldown expirado)
            string userId = $"late-after-{Guid.NewGuid()}";
            DateTime responseTime = DateTime.UtcNow.AddSeconds(-15);
            MessageProcessingService.RecordResponseTime(userId, responseTime);

            // Msg enviada DEPOIS da resposta (SentAt posterior + cooldown expirado)
            DateTime sentAt = responseTime.AddSeconds(5);

            // Act
            bool result = MessageProcessingService.IsLateMessage(userId, sentAt);

            // Assert — cooldown expirado + enviada após resposta → legítima
            Assert.False(result, "Mensagem enviada após resposta + cooldown expirado → processar");

            MessageProcessingService.ResetUserState(userId);
        }

        [Fact]
        public void IsLateMessage_SameSecondAsResponse_AfterCooldown_ShouldReturnFalse()
        {
            // Arrange — resposta há 15s (cooldown expirado)
            // SentAt no mesmo instante da resposta → não é estritamente anterior.
            string userId = $"late-same-{Guid.NewGuid()}";
            DateTime responseTime = DateTime.UtcNow.AddSeconds(-15);
            MessageProcessingService.RecordResponseTime(userId, responseTime);

            DateTime sentAt = responseTime; // mesmo instante

            // Act
            bool result = MessageProcessingService.IsLateMessage(userId, sentAt);

            // Assert — cooldown expirado + não é anterior → false
            Assert.False(result, "SentAt == responseTime não é 'anterior' → processar");

            MessageProcessingService.ResetUserState(userId);
        }

        [Fact]
        public void IsLateMessage_SentAtMinValue_AfterCooldown_ShouldReturnFalse()
        {
            // Arrange — resposta há 15s (cooldown expirado)
            // SentAt = MinValue (WhatsApp não enviou o campo)
            string userId = $"late-minval-{Guid.NewGuid()}";
            MessageProcessingService.RecordResponseTime(userId, DateTime.UtcNow.AddSeconds(-15));

            // Act — DateTime.MinValue = sem timestamp válido
            bool result = MessageProcessingService.IsLateMessage(userId, DateTime.MinValue);

            // Assert — cooldown expirado + sem timestamp → não filtrar
            Assert.False(result, "Sem timestamp válido + cooldown expirado → processar");

            MessageProcessingService.ResetUserState(userId);
        }

        [Fact]
        public void IsLateMessage_SentAtMinValue_DuringCooldown_ShouldReturnTrue()
        {
            // Arrange — resposta acabou de ser enviada (cooldown ativo)
            // Mesmo sem timestamp válido, o cooldown server-side bloqueia.
            string userId = $"late-minval-cd-{Guid.NewGuid()}";
            MessageProcessingService.RecordResponseTime(userId);

            // Act
            bool result = MessageProcessingService.IsLateMessage(userId, DateTime.MinValue);

            // Assert — cooldown ativo → bloqueada (independente do sentAt)
            Assert.True(result, "Cooldown ativo bloqueia mesmo sem timestamp válido");

            MessageProcessingService.ResetUserState(userId);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        public void IsLateMessage_NullOrEmptyUser_ShouldReturnFalse(string? userId)
        {
            // Act — userId inválido → não filtrar
            bool result = MessageProcessingService.IsLateMessage(userId!, DateTime.UtcNow);

            // Assert
            Assert.False(result, "Entradas null/vazias nunca devem ser consideradas atrasadas");
        }

        [Fact]
        public void IsLateMessage_SpamBurstScenario_AllLateShouldBeDetected()
        {
            // Arrange — cenário real: user envia 5x "ajuda" em 0.5s
            // Bot processa a 1ª e responde. As restantes 4 chegam com atraso.
            string userId = $"late-burst-{Guid.NewGuid()}";

            // Momento T=0: user começa a enviar spam
            DateTime spamStart = DateTime.UtcNow.AddSeconds(-5);

            // Msg 1 (T=0.0s) — processada pelo bot
            // Msg 2 (T=0.1s) — spam
            // Msg 3 (T=0.2s) — spam
            // Msg 4 (T=0.3s) — spam
            // Msg 5 (T=0.4s) — spam
            DateTime[] spamTimestamps = {
                spamStart.AddMilliseconds(100),
                spamStart.AddMilliseconds(200),
                spamStart.AddMilliseconds(300),
                spamStart.AddMilliseconds(400),
            };

            // Bot respondeu agora (cooldown ativo = 10s)
            MessageProcessingService.RecordResponseTime(userId);

            // Act & Assert — todas bloqueadas (cooldown ativo + SentAt anterior)
            foreach (var ts in spamTimestamps)
            {
                Assert.True(MessageProcessingService.IsLateMessage(userId, ts),
                    $"Mensagem enviada às {ts:HH:mm:ss.fff} deve ser bloqueada (cooldown + phone timestamp)");
            }

            // Simula cooldown expirado — nova mensagem legítima
            MessageProcessingService.RecordResponseTime(userId, DateTime.UtcNow.AddSeconds(-15));
            Assert.False(MessageProcessingService.IsLateMessage(userId, DateTime.UtcNow),
                "Mensagem legítima após cooldown expirado deve ser aceite");

            MessageProcessingService.ResetUserState(userId);
        }

        // =====================================================================
        // RecordResponseTime / GetLastResponseTime
        // =====================================================================

        [Fact]
        public void RecordResponseTime_ShouldStoreTimestamp()
        {
            // Arrange
            string userId = $"record-time-{Guid.NewGuid()}";

            // Act
            DateTime before = DateTime.UtcNow;
            MessageProcessingService.RecordResponseTime(userId);
            DateTime after = DateTime.UtcNow;

            // Assert
            DateTime? lastResponse = MessageProcessingService.GetLastResponseTime(userId);
            Assert.NotNull(lastResponse);
            Assert.InRange(lastResponse.Value, before, after);

            MessageProcessingService.ResetUserState(userId);
        }

        [Fact]
        public void GetLastResponseTime_NoRecord_ShouldReturnNull()
        {
            // Arrange
            string userId = $"no-record-{Guid.NewGuid()}";

            // Act & Assert
            Assert.Null(MessageProcessingService.GetLastResponseTime(userId));
        }

        [Fact]
        public void RecordResponseTime_MultipleRecords_ShouldKeepLatest()
        {
            // Arrange
            string userId = $"multi-record-{Guid.NewGuid()}";

            // Act — regista duas vezes
            MessageProcessingService.RecordResponseTime(userId);
            DateTime? first = MessageProcessingService.GetLastResponseTime(userId);

            Thread.Sleep(50); // garante diferença de tempo

            MessageProcessingService.RecordResponseTime(userId);
            DateTime? second = MessageProcessingService.GetLastResponseTime(userId);

            // Assert — segunda deve ser mais recente
            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.True(second > first, "Segundo registo deve ser mais recente");

            MessageProcessingService.ResetUserState(userId);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        public void RecordResponseTime_NullOrEmpty_ShouldNotThrow(string? userId)
        {
            var ex = Record.Exception(() => MessageProcessingService.RecordResponseTime(userId!));
            Assert.Null(ex);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        public void GetLastResponseTime_NullOrEmpty_ShouldReturnNull(string? userId)
        {
            Assert.Null(MessageProcessingService.GetLastResponseTime(userId!));
        }

        // =====================================================================
        // ResetUserState — limpa lock + timestamp
        // =====================================================================

        [Fact]
        public void ResetUserState_ShouldClearLockAndTimestamp()
        {
            // Arrange
            string userId = $"reset-{Guid.NewGuid()}";
            MessageProcessingService.TryLockUser(userId);
            MessageProcessingService.RecordResponseTime(userId);

            // Act
            MessageProcessingService.ResetUserState(userId);

            // Assert — tudo limpo
            Assert.False(MessageProcessingService.IsUserLocked(userId), "Lock deve estar limpo");
            Assert.Null(MessageProcessingService.GetLastResponseTime(userId));
        }

        [Fact]
        public void ResetUserState_NullOrEmpty_ShouldNotThrow()
        {
            var ex1 = Record.Exception(() => MessageProcessingService.ResetUserState(null!));
            var ex2 = Record.Exception(() => MessageProcessingService.ResetUserState(""));
            var ex3 = Record.Exception(() => MessageProcessingService.ResetUserState("  "));

            Assert.Null(ex1);
            Assert.Null(ex2);
            Assert.Null(ex3);
        }

        // =====================================================================
        // Ciclo completo com proteção tripla (timestamp + lock + delayed unlock)
        // =====================================================================

        [Fact]
        public void FullCycle_TripleProtection_TimestampLockAndDelayedUnlock()
        {
            // Arrange — simula o cenário real completo
            // Nota: DelayedUnlockAsync (5s) é private/async, testamos os métodos públicos
            string userId = $"triple-full-{Guid.NewGuid()}";

            // Step 1: user envia mensagem → lock adquirido
            Assert.True(MessageProcessingService.TryLockUser(userId));

            // Step 2: mensagens simultâneas → bloqueadas pelo lock
            Assert.False(MessageProcessingService.TryLockUser(userId));

            // Step 3: bot responde → RecordResponseTime + UnlockUser
            // Usamos tempo 15s no passado para que Steps 4-5 testem o path de phone timestamp
            // (em produção, o cooldown de 10s seria a proteção principal)
            DateTime responseTime = DateTime.UtcNow.AddSeconds(-15);
            MessageProcessingService.RecordResponseTime(userId, responseTime);
            MessageProcessingService.UnlockUser(userId);

            // Step 4: mensagem atrasada chega (SentAt anterior à resposta, cooldown expirado)
            DateTime lateSentAt = responseTime.AddSeconds(-10);
            Assert.True(MessageProcessingService.IsLateMessage(userId, lateSentAt),
                "Mensagem atrasada deve ser detectada (phone timestamp)");

            // Step 5: nova mensagem legítima (SentAt posterior à resposta, cooldown expirado)
            DateTime newSentAt = DateTime.UtcNow;
            Assert.False(MessageProcessingService.IsLateMessage(userId, newSentAt),
                "Nova mensagem legítima deve ser aceite (cooldown expirado + SentAt posterior)");

            // Step 6: nova mensagem adquire lock
            Assert.True(MessageProcessingService.TryLockUser(userId));

            // Cleanup
            MessageProcessingService.ResetUserState(userId);
        }

        // =====================================================================
        // Spam Detection — contagem de mensagens bloqueadas + reply-to
        // =====================================================================

        [Fact]
        public void WasSpamDetected_NoBlockedMessages_ShouldReturnFalse()
        {
            // Arrange — user sem mensagens bloqueadas
            string userId = $"spam-none-{Guid.NewGuid()}";

            // Act & Assert
            Assert.False(MessageProcessingService.WasSpamDetected(userId),
                "Sem mensagens bloqueadas → WasSpamDetected deve ser false");
            Assert.Equal(0, MessageProcessingService.GetSpamBlockedCount(userId));
        }

        [Fact]
        public void WasSpamDetected_AfterIsLateMessage_ShouldReturnTrue()
        {
            // Arrange — simula msg bloqueada pelo IsLateMessage (cooldown ativo)
            string userId = $"spam-late-{Guid.NewGuid()}";
            MessageProcessingService.RecordResponseTime(userId); // cooldown ativo

            // Act — IsLateMessage bloqueia e incrementa o contador
            bool blocked = MessageProcessingService.IsLateMessage(userId, DateTime.UtcNow);
            Assert.True(blocked, "Mensagem deve ser bloqueada (cooldown ativo)");

            // Assert — spam foi detectado
            Assert.True(MessageProcessingService.WasSpamDetected(userId),
                "Após bloqueio por IsLateMessage, WasSpamDetected deve ser true");
            Assert.Equal(1, MessageProcessingService.GetSpamBlockedCount(userId));

            MessageProcessingService.ResetUserState(userId);
        }

        [Fact]
        public void WasSpamDetected_AfterTryLockUser_ShouldReturnTrue()
        {
            // Arrange — lock adquirido
            string userId = $"spam-lock-{Guid.NewGuid()}";
            Assert.True(MessageProcessingService.TryLockUser(userId));

            // Act — segunda tentativa de lock → bloqueada
            Assert.False(MessageProcessingService.TryLockUser(userId));

            // Assert — spam detectado
            Assert.True(MessageProcessingService.WasSpamDetected(userId),
                "Após bloqueio por TryLockUser, WasSpamDetected deve ser true");
            Assert.Equal(1, MessageProcessingService.GetSpamBlockedCount(userId));

            MessageProcessingService.ResetUserState(userId);
        }

        [Fact]
        public void SpamBlockedCount_MultiplBlocks_ShouldAccumulate()
        {
            // Arrange — simula várias mensagens bloqueadas
            string userId = $"spam-multi-{Guid.NewGuid()}";
            Assert.True(MessageProcessingService.TryLockUser(userId));

            // Act — 3 mensagens tentam o lock
            Assert.False(MessageProcessingService.TryLockUser(userId));
            Assert.False(MessageProcessingService.TryLockUser(userId));
            Assert.False(MessageProcessingService.TryLockUser(userId));

            // Assert — 3 bloqueadas
            Assert.Equal(3, MessageProcessingService.GetSpamBlockedCount(userId));

            MessageProcessingService.ResetUserState(userId);
        }

        [Fact]
        public void ResetSpamCount_ShouldClearCounter()
        {
            // Arrange — simula spam
            string userId = $"spam-reset-{Guid.NewGuid()}";
            Assert.True(MessageProcessingService.TryLockUser(userId));
            Assert.False(MessageProcessingService.TryLockUser(userId));

            Assert.True(MessageProcessingService.WasSpamDetected(userId));

            // Act
            MessageProcessingService.ResetSpamCount(userId);

            // Assert — contador limpo
            Assert.False(MessageProcessingService.WasSpamDetected(userId));
            Assert.Equal(0, MessageProcessingService.GetSpamBlockedCount(userId));

            MessageProcessingService.ResetUserState(userId);
        }

        [Fact]
        public void ResetUserState_ShouldAlsoClearSpamCount()
        {
            // Arrange — simula spam + lock + timestamp
            string userId = $"spam-state-{Guid.NewGuid()}";
            Assert.True(MessageProcessingService.TryLockUser(userId));
            Assert.False(MessageProcessingService.TryLockUser(userId));
            MessageProcessingService.RecordResponseTime(userId);

            // Verificar que tudo está definido
            Assert.True(MessageProcessingService.WasSpamDetected(userId));
            Assert.True(MessageProcessingService.IsUserLocked(userId));
            Assert.NotNull(MessageProcessingService.GetLastResponseTime(userId));

            // Act — ResetUserState limpa TUDO
            MessageProcessingService.ResetUserState(userId);

            // Assert — tudo limpo
            Assert.False(MessageProcessingService.WasSpamDetected(userId));
            Assert.False(MessageProcessingService.IsUserLocked(userId));
            Assert.Null(MessageProcessingService.GetLastResponseTime(userId));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        public void WasSpamDetected_NullOrEmptyUser_ShouldReturnFalse(string? userId)
        {
            // Act & Assert — entradas inválidas nunca indicam spam
            Assert.False(MessageProcessingService.WasSpamDetected(userId!));
            Assert.Equal(0, MessageProcessingService.GetSpamBlockedCount(userId!));
        }

        // =====================================================================
        // Startup Time — mensagens antigas (anteriores ao arranque) são ignoradas
        // =====================================================================

        [Fact]
        public void GetStartupTime_ShouldReturnReasonableTime()
        {
            // Act
            DateTime startupTime = MessageProcessingService.GetStartupTime();

            // Assert — o startup time deve ser recente (últimos 5 minutos)
            // e nunca no futuro
            Assert.True(startupTime <= DateTime.UtcNow,
                "O startup time não pode estar no futuro");
            Assert.True(startupTime > DateTime.UtcNow.AddMinutes(-5),
                "O startup time deve ser recente (últimos 5 minutos)");
        }

        [Fact]
        public async Task ProcessMessage_SentBeforeStartup_ShouldBeIgnored()
        {
            // Arrange — mensagem com SentAt 1 hora antes do arranque
            var mockService = new Mock<WebApplication1.Core.Interfaces.IMessagingService>();
            mockService.Setup(s => s.MarkAsReadAsync(It.IsAny<string>())).ReturnsAsync(true);
            mockService.Setup(s => s.SendTextMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
                .ReturnsAsync(true);

            var loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<MessageProcessingService>>();
            var routerLoggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<WebApplication1.Core.Commands.CommandRouter>>();
            var handlers = Array.Empty<WebApplication1.Core.Commands.ICommandHandler>();
            var router = new WebApplication1.Core.Commands.CommandRouter(handlers, routerLoggerMock.Object);
            var service = new MessageProcessingService(router, loggerMock.Object);

            string userId = $"startup-old-{Guid.NewGuid()}";
            var msg = new WebApplication1.Core.Models.IncomingMessage
            {
                MessageId = $"startup-old-{Guid.NewGuid()}",
                From = userId,
                UserId = userId,
                Body = "presente",
                Platform = WebApplication1.Core.Models.MessagePlatform.WhatsApp,
                ReceivedAt = DateTime.UtcNow,
                SentAt = MessageProcessingService.GetStartupTime().AddHours(-1) // 1h antes do arranque
            };

            // Act
            await service.ProcessMessageAsync(msg, mockService.Object);

            // Assert — SendTextMessageAsync NÃO deve ter sido chamado (msg ignorada)
            mockService.Verify(
                s => s.SendTextMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()),
                Times.Never,
                "Mensagens com SentAt anterior ao arranque devem ser ignoradas");

            MessageProcessingService.ResetUserState(userId);
        }

        [Fact]
        public async Task ProcessMessage_SentAfterStartup_ShouldBeProcessed()
        {
            // Arrange — mensagem com SentAt DEPOIS do arranque (normal)
            var mockService = new Mock<WebApplication1.Core.Interfaces.IMessagingService>();
            mockService.Setup(s => s.MarkAsReadAsync(It.IsAny<string>())).ReturnsAsync(true);
            mockService.Setup(s => s.SendTextMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
                .ReturnsAsync(true);

            var loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<MessageProcessingService>>();
            var routerLoggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<WebApplication1.Core.Commands.CommandRouter>>();
            var handlers = Array.Empty<WebApplication1.Core.Commands.ICommandHandler>();
            var router = new WebApplication1.Core.Commands.CommandRouter(handlers, routerLoggerMock.Object);
            var service = new MessageProcessingService(router, loggerMock.Object);

            string userId = $"startup-new-{Guid.NewGuid()}";
            var msg = new WebApplication1.Core.Models.IncomingMessage
            {
                MessageId = $"startup-new-{Guid.NewGuid()}",
                From = userId,
                UserId = userId,
                Body = "ajuda",
                Platform = WebApplication1.Core.Models.MessagePlatform.WhatsApp,
                ReceivedAt = DateTime.UtcNow,
                SentAt = DateTime.UtcNow // enviada agora (depois do arranque)
            };

            // Act
            await service.ProcessMessageAsync(msg, mockService.Object);

            // Assert — SendTextMessageAsync DEVE ter sido chamado (msg processada)
            mockService.Verify(
                s => s.SendTextMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()),
                Times.Once,
                "Mensagens com SentAt posterior ao arranque devem ser processadas normalmente");

            MessageProcessingService.ResetUserState(userId);
        }

        [Fact]
        public async Task ProcessMessage_SentExactlyAtStartup_ShouldBeProcessed()
        {
            // Arrange — mensagem com SentAt exatamente no momento do arranque
            // (edge case: SentAt == _startupTime → NÃO é anterior, deve processar)
            var mockService = new Mock<WebApplication1.Core.Interfaces.IMessagingService>();
            mockService.Setup(s => s.MarkAsReadAsync(It.IsAny<string>())).ReturnsAsync(true);
            mockService.Setup(s => s.SendTextMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
                .ReturnsAsync(true);

            var loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<MessageProcessingService>>();
            var routerLoggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<WebApplication1.Core.Commands.CommandRouter>>();
            var handlers = Array.Empty<WebApplication1.Core.Commands.ICommandHandler>();
            var router = new WebApplication1.Core.Commands.CommandRouter(handlers, routerLoggerMock.Object);
            var service = new MessageProcessingService(router, loggerMock.Object);

            string userId = $"startup-exact-{Guid.NewGuid()}";
            var msg = new WebApplication1.Core.Models.IncomingMessage
            {
                MessageId = $"startup-exact-{Guid.NewGuid()}",
                From = userId,
                UserId = userId,
                Body = "ajuda",
                Platform = WebApplication1.Core.Models.MessagePlatform.WhatsApp,
                ReceivedAt = DateTime.UtcNow,
                SentAt = MessageProcessingService.GetStartupTime() // exatamente no arranque
            };

            // Act
            await service.ProcessMessageAsync(msg, mockService.Object);

            // Assert — SentAt == startup não é "anterior", deve processar
            mockService.Verify(
                s => s.SendTextMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()),
                Times.Once,
                "Mensagens com SentAt exatamente no arranque devem ser processadas");

            MessageProcessingService.ResetUserState(userId);
        }
    }
}
