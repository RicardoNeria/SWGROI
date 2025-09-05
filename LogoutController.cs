using System.IO;
using System.Net;
using SWGROI_Server.Security;

namespace SWGROI_Server.Controllers
{
    public static class LogoutController
    {
        public static void Procesar(HttpListenerContext context)
        {
            // Invalidar sesión y expirar cookies de compatibilidad sin romper el front.
            SessionManager.Destroy(context);
            context.Response.Headers.Add("Set-Cookie", "usuario=; Path=/; Max-Age=0");
            context.Response.Headers.Add("Set-Cookie", "rol=; Path=/; Max-Age=0");
            context.Response.Headers.Add("Set-Cookie", "nombre=; Path=/; Max-Age=0");
            context.Response.Redirect("/login.html");
            context.Response.Close();
        }
    }
}

