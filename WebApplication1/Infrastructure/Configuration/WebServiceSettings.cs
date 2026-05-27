namespace WebApplication1.Infrastructure.Configuration
{
    /// <summary>
    /// Configurações para o WebService WCF (nGMobileWS).
    /// </summary>
    public class WebServiceSettings
    {
        public string BaseUrl { get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;
        public int TimeoutSeconds { get; set; } = 10;
        public string SharedSecret { get; set; } = "EloGateway_SecKey_8f9C!2pL$vR4#mXqZ9*7nWt@5";
    }
}
