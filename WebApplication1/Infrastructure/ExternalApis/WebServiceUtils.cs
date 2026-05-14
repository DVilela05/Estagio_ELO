using System.Text;
using Microsoft.Extensions.Logging;

namespace WebApplication1.Infrastructure.ExternalApis
{
    /// <summary>
    /// Utilitário para chamadas ao serviço móvel nGMobileWS.
    /// 
    /// Dois modos:
    ///   1. Chamada direta (GET/POST) ao endpoint REST
    ///   2. Base64 decode da resposta
    /// </summary>
    public static class WebServiceUtils
    {
        /// <summary>
        /// Faz uma chamada HTTP (GET/POST) ao serviço REST móvel.
        /// 
        /// Exemplo:
        ///   var response = await WCFRESTServiceCall("GET", "vc_MOB3MSS_PD", "", logger, "http://elosvtests/Dev/nGMobileWS/nGMobileWS.svc/rest/");
        /// </summary>
        public static async Task<string> WCFRESTServiceCall(
            string methodRequestType,
            string methodName,
            string bodyParam = "",
            ILogger? logger = null,
            string? baseUrl = null)
        {
            string returnString = string.Empty;

            // Se não foi passado baseUrl, o serviço deve falhar ou usar o que vier das configs
            string urlWebService = baseUrl ?? string.Empty;

            try
            {
                logger?.LogInformation(
                    "📱 Chamada ao serviço móvel: Method={Method}, Name={MethodName}, URL={ServiceURI}",
                    methodRequestType, methodName, $"{urlWebService}{methodName}");

                // Por enquanto, aceita certificados auto-assinados (para testes)
                bool WS_CONN_SSL_TRUST_CA = true;
                string ServiceURI = urlWebService + methodName;

                HttpClient httpClient;

                if (WS_CONN_SSL_TRUST_CA)
                {
                    var httpClientHandler = new HttpClientHandler();
                    httpClientHandler.ServerCertificateCustomValidationCallback =
                        (message, certificate, chain, sslPolicyErrors) => true;

                    httpClient = new HttpClient(httpClientHandler);
                }
                else
                {
                    httpClient = new HttpClient();
                }

                HttpRequestMessage request = new HttpRequestMessage(
                    methodRequestType == "GET" ? HttpMethod.Get : HttpMethod.Post,
                    ServiceURI);

                if (!string.IsNullOrEmpty(bodyParam))
                {
                    request.Content = new StringContent(bodyParam, Encoding.UTF8, "application/json");
                }

                HttpResponseMessage response = await httpClient.SendAsync(request);
                returnString = await response.Content.ReadAsStringAsync();

                logger?.LogInformation(
                    "✅ Resposta do serviço móvel: StatusCode={StatusCode}, Body={Body}",
                    response.StatusCode, returnString);
            }
            catch (Exception exRequest)
            {
                returnString = "EXCEPTION|" + exRequest.Message;
                logger?.LogError(exRequest,
                    "❌ Erro ao chamar serviço móvel: {MethodName}", methodName);
            }

            return returnString;
        }

        /// <summary>
        /// Descodifica uma string Base64.
        /// Remove barras invertidas e converte para UTF-8.
        /// </summary>
        public static string Base64Decode(string base64EncodedData)
        {
            try
            {
                // Carater inválido
                if (base64EncodedData.Contains("\\"))
                    base64EncodedData = base64EncodedData.Replace("\\", "");

                var base64EncodedBytes = System.Convert.FromBase64String(base64EncodedData);
                return System.Text.Encoding.UTF8.GetString(base64EncodedBytes, 0, base64EncodedBytes.Length);
            }
            catch
            {
                return base64EncodedData; // Se não for válido Base64, retorna original
            }
        }

        /// <summary>
        /// Testa conectividade com o serviço móvel (ping).
        /// </summary>
        public static async Task<bool> TestConnectivityAsync(
            ILogger? logger = null,
            string? baseUrl = null)
        {
            try
            {
                string response = await WCFRESTServiceCall("GET", "ping", "", logger, baseUrl);
                return !response.StartsWith("EXCEPTION");
            }
            catch
            {
                return false;
            }
        }
    }
}
