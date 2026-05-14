using WebApplication1.Core.Models;

namespace WebApplication1.Core.Commands
{
    /// <summary>
    /// Interface que TODOS os comandos têm de implementar.
    /// 
    /// Cada comando é uma classe isolada que sabe:
    ///   - Qual é o seu nome e descrição (para o menu de ajuda)
    ///   - Se consegue processar uma determinada mensagem (CanHandle)
    ///   - O que fazer quando é acionado (ExecuteAsync)
    /// 
    /// Para criar um novo comando:
    ///   1. Cria uma classe que implementa ICommandHandler
    ///   2. Regista-a no Program.cs
    ///   3. O CommandRouter apanha-a automaticamente
    /// </summary>
    public interface ICommandHandler
    {
        /// <summary>
        /// Nome do comando (ex: "ajuda", "horário").
        /// Usado para identificação nos logs.
        /// </summary>
        string CommandName { get; }

        /// <summary>
        /// Descrição curta do que o comando faz.
        /// Aparece na lista de ajuda.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Palavras-chave que ativam este comando.
        /// Usadas pelo menu de ajuda para mostrar ao utilizador.
        /// Ex: ["ajuda", "help", "?"]
        /// </summary>
        string[] Triggers { get; }

        /// <summary>
        /// Verifica se este handler consegue processar a mensagem recebida.
        /// Normalmente compara o texto com palavras-chave.
        /// </summary>
        bool CanHandle(IncomingMessage message);

        /// <summary>
        /// Executa o comando e devolve o texto de resposta.
        /// </summary>
        Task<string> ExecuteAsync(IncomingMessage message);
    }
}
