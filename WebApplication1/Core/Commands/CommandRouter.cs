using WebApplication1.Application;
using WebApplication1.Core.Localization;
using WebApplication1.Core.Models;

namespace WebApplication1.Core.Commands
{
    /// <summary>
    /// Router de comandos — recebe uma mensagem e encontra o handler correto.
    /// 
    /// Fluxo:
    ///   1. Percorre todos os ICommandHandler registados
    ///   2. O primeiro que responder CanHandle(msg) == true é executado
    ///   3. Se nenhum corresponder, devolve uma mensagem padrão
    ///      na língua detetada (ou multi-língua se desconhecida)
    /// 
    /// Não precisa de ser alterado quando se adicionam novos comandos —
    /// basta registar o novo ICommandHandler na DI e ele é apanhado
    /// automaticamente.
    /// </summary>
    public class CommandRouter
    {
        private readonly IEnumerable<ICommandHandler> _handlers;
        private readonly ILogger<CommandRouter> _logger;
        private readonly CommandPrompts _commandPrompts;

        public CommandRouter(
            IEnumerable<ICommandHandler> handlers,
            ILogger<CommandRouter> logger,
            CommandPrompts commandPrompts)
        {
            _handlers = handlers;
            _logger = logger;
            _commandPrompts = commandPrompts;
        }

        /// <summary>
        /// Verifica se existe algum handler que aceite a mensagem.
        /// </summary>
        public bool IsValidCommand(IncomingMessage message)
        {
            return _handlers.Any(handler => handler.CanHandle(message));
        }

        /// <summary>
        /// Tenta obter o handler que aceita a mensagem.
        /// </summary>
        public bool TryGetMatchedHandler(IncomingMessage message, out ICommandHandler? matched)
        {
            foreach (var handler in _handlers)
            {
                if (handler.CanHandle(message))
                {
                    matched = handler;
                    return true;
                }
            }

            matched = null;
            return false;
        }

        /// <summary>
        /// Processa uma mensagem e devolve o texto de resposta.
        /// </summary>
        public async Task<string> RouteAsync(IncomingMessage message)
        {
            // Percorre todos os handlers e executa o primeiro que corresponde
            foreach (var handler in _handlers)
            {
                if (handler.CanHandle(message))
                {
                    _logger.LogInformation(
                        "Comando \"{Command}\" acionado por {From}: \"{Body}\"",
                        handler.CommandName, message.From, message.Body);

                    return await handler.ExecuteAsync(message);
                }
            }

            // Nenhum comando reconhecido — resposta na língua detetada ou multi-língua
            _logger.LogInformation(
                "Mensagem de {From} não corresponde a nenhum comando: \"{Body}\"",
                message.From, message.Body);

            if (message.Language.HasValue)
            {
                return _commandPrompts.GetNextUnknownMessage(message.Language);
            }

            // Língua desconhecida → mensagem multi-língua curta
            return _commandPrompts.GetMultiLanguageFallback();
        }
    }
}
