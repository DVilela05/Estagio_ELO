using WebApplication1.Core.Models;

namespace WebApplication1.Core.Interfaces
{
    /// <summary>
    /// Interface para chamadas ao servidor de negócio (REST API).
    /// 
    /// O servidor de negócio é responsável por:
    ///   - Registar presenças na base de dados
    ///   - Validar colaboradores (futuro)
    ///   - Consultar informação de utilizadores (futuro)
    /// 
    /// Quando o BaseUrl não está configurado, a implementação opera em
    /// modo STUB (simula sucesso). Quando configurado, faz HTTP real.
    /// 
    /// Todas as operações devolvem BusinessApiResult em vez de bool,
    /// para que o chamador saiba se foi sucesso, erro, ou stub.
    /// </summary>
    public interface IBusinessApiClient
    {
        /// <summary>
        /// Regista uma presença no servidor de negócio.
        /// </summary>
        /// <param name="userId">Identificador do utilizador (phone / AAD Object ID)</param>
        /// <param name="userName">Nome do utilizador (se disponível)</param>
        /// <param name="attendanceType">Tipo de marcação ("presente", "falta", etc.)</param>
        /// <param name="platform">Plataforma de origem ("WhatsApp", "Teams")</param>
        /// <param name="userPhone">Telefone do utilizador, quando disponível</param>
        /// <param name="userEmail">Email do utilizador, quando disponível</param>
        /// <returns>BusinessApiResult com sucesso/falha e mensagem descritiva</returns>
        Task<BusinessApiResult> RegisterAttendanceAsync(
            string userId,
            string? userName,
            string attendanceType,
            string platform,
            string? userPhone = null,
            string? userEmail = null);

        /// <summary>
        /// Obtém informação de um utilizador no servidor de negócio.
        /// Útil para validar se o user existe antes de registar presença.
        /// </summary>
        /// <param name="userId">Identificador do utilizador</param>
        /// <returns>BusinessApiResult com dados do utilizador ou erro</returns>
        Task<BusinessApiResult> GetUserInfoAsync(string userId);

        /// <summary>
        /// Verifica se o servidor de negócio está acessível.
        /// Útil para health checks e diagnóstico.
        /// </summary>
        /// <returns>True se o servidor respondeu com sucesso</returns>
        Task<bool> IsAvailableAsync();
    }
}
