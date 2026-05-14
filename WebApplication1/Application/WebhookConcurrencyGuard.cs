using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace WebApplication1.Application
{
    /// <summary>
    /// Guarda de concorrência para webhooks com controlo de spam por resposta única.
    /// 
    /// Comportamento:
    ///   1. Primeira mensagem de um remetente: ACEITE, lock ativo
    ///   2. Mensagens seguintes enquanto lock ativo: REJEITADAS (spam)
    ///   3. Quando resposta webhook chega: lock libertado
    ///   4. Próximas mensagens: novo ciclo começa
    /// 
    /// Usa ConcurrentDictionary para operações atómicas (TryAdd/TryRemove)
    /// e IMemoryCache para expiração automática com fallback de segurança.
    /// </summary>
    public sealed class WebhookConcurrencyGuard
    {
        private const int SenderLockTtlSeconds = 5;
        private const int MessageIdTtlSeconds = 300;

        private readonly IMemoryCache _cache;

        private readonly ConcurrentDictionary<string, byte> _seenMessageIds = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, string> _sendersInProcessing = new(StringComparer.Ordinal);

        public WebhookConcurrencyGuard(IMemoryCache cache)
        {
            _cache = cache;
        }

        /// <summary>
        /// Verifica se um MessageId foi visto. Retorna false se novo, true se duplicado.
        /// TTL: 5 minutos.
        /// </summary>
        public bool TryRegisterMessageId(string messageId)
        {
            if (string.IsNullOrWhiteSpace(messageId))
                return false;

            if (!_seenMessageIds.TryAdd(messageId, 0))
                return false;

            string cacheKey = $"wa:dedup:{messageId}";

            _cache.Set(cacheKey, true, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(MessageIdTtlSeconds),
                Priority = CacheItemPriority.Low
            }.RegisterPostEvictionCallback((_, _, _, _) =>
            {
                _seenMessageIds.TryRemove(messageId, out _);
            }));

            return true;
        }

        /// <summary>
        /// Tenta adquirir lock de processamento para um remetente.
        /// Retorna true se lock foi adquirido, false se remetente já em processamento.
        /// </summary>
        public bool TryAcquireSenderLock(string senderId)
        {
            if (string.IsNullOrWhiteSpace(senderId))
                return false;

            string lockToken = Guid.NewGuid().ToString("N");
            if (!_sendersInProcessing.TryAdd(senderId, lockToken))
                return false;

            string cacheKey = $"wa:lock:{senderId}";

            _cache.Set(cacheKey, lockToken, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(SenderLockTtlSeconds),
                Priority = CacheItemPriority.High
            }.RegisterPostEvictionCallback((_, _, _, _) =>
            {
                if (_sendersInProcessing.TryGetValue(senderId, out string? current) &&
                    string.Equals(current, lockToken, StringComparison.Ordinal))
                {
                    _sendersInProcessing.TryRemove(senderId, out _);
                }
            }));

            return true;
        }

        /// <summary>
        /// Liberta o lock de um remetente após resposta webhook recebida.
        /// </summary>
        public void ReleaseSenderLock(string senderId)
        {
            if (string.IsNullOrWhiteSpace(senderId))
                return;

            _sendersInProcessing.TryRemove(senderId, out _);
            _cache.Remove($"wa:lock:{senderId}");
        }
    }
}
