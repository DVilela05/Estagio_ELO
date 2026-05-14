using WebApplication1.Core.Interfaces;
using WebApplication1.Core.Models;

namespace WebApplication1.Infrastructure.Messaging
{
    /// <summary>
    /// Fábrica que permite ter VÁRIAS plataformas a funcionar ao mesmo tempo.
    /// 
    /// Problema original: o .NET só permite registar UMA implementação por interface.
    /// Se registasses WhatsAppService e TeamsService como IMessagingService,
    /// a segunda sobrepunha a primeira.
    /// 
    /// Solução: esta fábrica guarda TODAS as implementações registadas
    /// e devolve a correta quando pedimos por plataforma.
    /// 
    /// Uso: 
    ///   var whatsapp = factory.GetService(MessagePlatform.WhatsApp);
    ///   var teams = factory.GetService(MessagePlatform.Teams);
    /// </summary>
    public class MessagingServiceFactory
    {
        private readonly Dictionary<MessagePlatform, IMessagingService> _services;

        public MessagingServiceFactory(IEnumerable<IMessagingService> services)
        {
            _services = services.ToDictionary(s => s.Platform, s => s);
        }

        public IMessagingService GetService(MessagePlatform platform)
        {
            if (_services.TryGetValue(platform, out var service))
                return service;

            throw new InvalidOperationException(
                $"Nenhum serviço de mensagens registado para a plataforma '{platform}'. " +
                $"Plataformas disponíveis: {string.Join(", ", _services.Keys)}");
        }

        public bool HasService(MessagePlatform platform)
            => _services.ContainsKey(platform);

        public IReadOnlyCollection<MessagePlatform> RegisteredPlatforms
            => _services.Keys.ToList().AsReadOnly();
    }
}
