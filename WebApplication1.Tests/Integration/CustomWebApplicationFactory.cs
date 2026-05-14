using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using WebApplication1.Core.Interfaces;
using WebApplication1.Core.Models;
using WebApplication1.Infrastructure.Messaging;

namespace WebApplication1.Tests.Integration
{
    /// <summary>
    /// Factory personalizada para testes de integração.
    /// 
    /// Substitui os serviços reais (WhatsApp API, Teams API) por mocks,
    /// para que os testes não façam chamadas HTTP reais.
    /// 
    /// Usa WebApplicationFactory do ASP.NET Core que cria um servidor
    /// in-memory com toda a pipeline (middleware, DI, controllers).
    /// </summary>
    public class CustomWebApplicationFactory : WebApplicationFactory<Program>
    {
        /// <summary>
        /// Mock do serviço WhatsApp — permite verificar se SendTextMessage foi chamado.
        /// </summary>
        public Mock<IMessagingService> MockWhatsAppService { get; } = new();

        /// <summary>
        /// Mock do serviço Teams — permite verificar se SendTextMessage foi chamado.
        /// </summary>
        public Mock<IMessagingService> MockTeamsService { get; } = new();

        public CustomWebApplicationFactory()
        {
            // Configurar mocks com comportamento por defeito
            MockWhatsAppService.Setup(s => s.Platform).Returns(MessagePlatform.WhatsApp);
            MockWhatsAppService.Setup(s => s.SendTextMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
                .ReturnsAsync(true);
            MockWhatsAppService.Setup(s => s.MarkAsReadAsync(It.IsAny<string>()))
                .ReturnsAsync(true);

            MockTeamsService.Setup(s => s.Platform).Returns(MessagePlatform.Teams);
            MockTeamsService.Setup(s => s.SendTextMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
                .ReturnsAsync(true);
            MockTeamsService.Setup(s => s.MarkAsReadAsync(It.IsAny<string>()))
                .ReturnsAsync(true);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");

            // Sobrescrever config para garantir que filtros de segurança
            // não bloqueiam em testes (AppSecret vazio → HMAC skip, Dev → JWT skip)
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["WhatsApp:AppSecret"] = "",
                    ["WhatsApp:AccessToken"] = "test-token",
                    ["Teams:ClientSecret"] = "test-secret"
                });
            });

            builder.ConfigureServices(services =>
            {
                // Remover os serviços reais de messaging (WhatsApp HTTP, Teams HTTP)
                var descriptors = services
                    .Where(d => d.ServiceType == typeof(IMessagingService)
                             || d.ServiceType == typeof(MessagingServiceFactory))
                    .ToList();

                foreach (var descriptor in descriptors)
                    services.Remove(descriptor);

                // Registar mocks no lugar
                services.AddSingleton<IMessagingService>(MockWhatsAppService.Object);
                services.AddSingleton<IMessagingService>(MockTeamsService.Object);
                services.AddSingleton<MessagingServiceFactory>();
            });
        }
    }
}
