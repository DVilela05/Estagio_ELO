namespace WebApplication1.Api.Middleware
{
    /// <summary>
    /// Middleware que adiciona headers de segurança HTTP a TODAS as respostas.
    /// 
    /// Baseado nas recomendações OWASP (Open Web Application Security Project):
    /// https://cheatsheetseries.owasp.org/cheatsheets/DotNet_Security_Cheat_Sheet.html
    /// https://owasp.org/www-project-secure-headers/
    /// 
    /// Estes headers protegem contra:
    ///   - Clickjacking (X-Frame-Options)
    ///   - MIME type sniffing (X-Content-Type-Options)
    ///   - Information leakage (Server, X-Powered-By)
    ///   - Referrer leakage (Referrer-Policy)
    ///   - Funcionalidades do browser abusáveis (Permissions-Policy)
    /// 
    /// Em produção, adiciona também HSTS para forçar HTTPS.
    /// </summary>
    public class SecurityHeadersMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IHostEnvironment _env;

        public SecurityHeadersMiddleware(RequestDelegate next, IHostEnvironment env)
        {
            _next = next;
            _env = env;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Adicionar headers de segurança ANTES de enviar a resposta
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;

                // ─── Prevenir Clickjacking ──────────────────────────────────────────
                // Impede que a app seja embebida num <iframe> malicioso.
                headers["X-Frame-Options"] = "DENY";

                // ─── Prevenir MIME Type Sniffing ────────────────────────────────────
                // Impede o browser de "adivinhar" o tipo de conteúdo.
                // Sem isto, um atacante pode fazer upload de um ficheiro .txt
                // que o browser interpreta como HTML/JS.
                headers["X-Content-Type-Options"] = "nosniff";

                // ─── Desativar XSS Auditor (recomendação moderna) ───────────────────
                // O XSS Auditor do browser está obsoleto e pode causar mais
                // problemas do que resolve. A recomendação OWASP é desativá-lo.
                headers["X-XSS-Protection"] = "0";

                // ─── Controlar Referrer ─────────────────────────────────────────────
                // Não enviar o URL de origem em pedidos cross-origin.
                // Protege contra vazamento de dados sensíveis em URLs.
                headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

                // ─── Controlar funcionalidades do browser ───────────────────────────
                // Desativar APIs do browser que não usamos (câmara, microfone, etc.).
                headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";

                // ─── Content Security Policy (CSP) ──────────────────────────────────
                // Como somos uma API (não servimos HTML), bloqueamos tudo.
                headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";

                // ─── Remover headers que revelam informação do servidor ──────────────
                // Um atacante pode usar esta info para procurar vulnerabilidades
                // específicas da versão do software.
                headers.Remove("X-Powered-By");
                headers.Remove("Server");

                // ─── HSTS — forçar HTTPS em produção ────────────────────────────────
                // Diz ao browser: "Nos próximos 365 dias, usa SEMPRE HTTPS para este site."
                // Só em produção para não interferir com dev local (HTTP).
                if (!_env.IsDevelopment())
                {
                    // max-age=31536000 = 365 dias
                    headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
                }

                return Task.CompletedTask;
            });

            await _next(context);
        }
    }
}
