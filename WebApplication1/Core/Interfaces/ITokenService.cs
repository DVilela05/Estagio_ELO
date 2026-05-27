using System;

namespace WebApplication1.Core.Interfaces
{
    /// <summary>
    /// Serviço responsável por gerar um token seguro para comunicação com o ERP.
    /// O token garante a autenticidade e tem um prazo de validade (timestamp).
    /// </summary>
    public interface ITokenService
    {
        /// <summary>
        /// Gera um token seguro associado a um utilizador (email ou telefone).
        /// O token contém o identificador e expira após um certo tempo.
        /// </summary>
        /// <param name="userId">E-mail ou número de telefone do utilizador</param>
        /// <returns>Token criptográfico em Base64</returns>
        string GenerateToken(string userId);
    }
}
