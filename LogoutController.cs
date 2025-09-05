using System.Net;
using System.IO;

namespace SWGROI_Server.Controllers
{
    public static class LogoutController
    {
        public static void Procesar(HttpListenerContext context)
        {
            // Eliminar cookie
            context.Response.AppendCookie(new Cookie("usuario", "")
            {
                Expires = System.DateTime.Now.AddDays(-1)
            });

            context.Response.Redirect("/login.html");
            context.Response.Close();
        }
    }
}