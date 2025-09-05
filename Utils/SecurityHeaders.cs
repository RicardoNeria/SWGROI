using System.Net;

namespace SWGROI_Server.Utils
{
    // Aplica cabeceras de seguridad por defecto (CSP, no-sniff, etc.).
    public static class SecurityHeaders
    {
        public static void Apply(HttpListenerResponse res)
        {
            // CSP estricta pero compatible con CSS inline existente.
            res.Headers["Content-Security-Policy"] = "default-src 'self'; img-src 'self' data:; script-src 'self'; style-src 'self' 'unsafe-inline'";
            res.Headers["X-Content-Type-Options"] = "nosniff";
            res.Headers["X-Frame-Options"] = "DENY";
            res.Headers["Referrer-Policy"] = "no-referrer";
        }
    }
}

