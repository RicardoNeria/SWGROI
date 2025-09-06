using System;
using System.IO;
using System.Net;
using SWGROI_Server.Controllers;
using SWGROI_Server.Utils;
using SWGROI_Server.Security;

namespace SWGROI_Server
{
    public static class RequestRouter
    {
        public static void ProcesarArchivosEstaticos(HttpListenerContext context)
        {
            // Middleware: requestId, headers de seguridad, sesión y CSRF.
            string requestId = Logger.NewRequestId();
            context.Response.Headers["X-Request-Id"] = requestId;
            SecurityHeaders.Apply(context.Response);
            SessionManager.EnsureSession(context);

            string rutaRaw = (context.Request.Url.LocalPath ?? "").TrimStart('/');

            if (string.IsNullOrWhiteSpace(rutaRaw))
                rutaRaw = "login.html";

            string ruta = rutaRaw.ToLowerInvariant();

            if (ruta == "ventas" || ruta.StartsWith("ventas/"))
            {
                if (!SessionManager.ValidateCsrf(context)) { RechazoCsrf(context); return; }
                VentasController.ManejarSolicitud(context);
                return;
            }

            if (ruta == "cotizaciones" || ruta.StartsWith("cotizaciones/"))
            {
                if (!SessionManager.ValidateCsrf(context)) { RechazoCsrf(context); return; }
                CotizacionesController.ManejarSolicitud(context); return; }

            if (ruta.StartsWith("menu/indicadores"))
            { MenuController.Procesar(context); return; }

            switch (ruta)
            {
                case "login":
                    // Excepción: permitir login sin CSRF para no romper flujo existente del login.html
                    LoginController.Procesar(context); return;
                case "logout": LogoutController.Procesar(context); return;
                case "admin": AdminController.Procesar(context); return;
                case "reportes": ReportesController.ManejarSolicitud(context); return;
                case "asignaciones": AsignacionesController.ManejarSolicitud(context); return;
                case "metricas": MetricasController.ManejarSolicitud(context); return;
                case "avisos": AvisosController.ManejarSolicitud(context); return;
                case "recuperar": RecuperarController.Procesar(context); return;
                case "tickets":
                case "tickets/actualizar":
                    if (!SessionManager.ValidateCsrf(context)) { RechazoCsrf(context); return; }
                    TicketsController.Procesar(context); return;
                case "seguimiento": TecnicosController.Procesar(context); return;
            }

            ServirArchivoEstatico(context, rutaRaw);
        }

        private static void ServirArchivoEstatico(HttpListenerContext context, string rutaOriginal)
        {
            string extension = Path.GetExtension(rutaOriginal).ToLowerInvariant();
            context.Response.ContentType = extension switch
            {
                ".html" => "text/html",
                ".css" => "text/css",
                ".js" => "application/javascript",
                ".png" => "image/png",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".json" => "application/json",
                _ => "application/octet-stream"
            };

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string rutaArchivo = Path.Combine(baseDir, "wwwroot", rutaOriginal.Replace("/", Path.DirectorySeparatorChar.ToString()));

            if (!File.Exists(rutaArchivo))
            {
                // Fallbacks de compatibilidad por mayúsculas/minúsculas en carpetas estáticas.
                string rutaArchivoLower = Path.Combine(baseDir, "wwwroot", rutaOriginal.ToLowerInvariant().Replace("/", Path.DirectorySeparatorChar.ToString()));
                if (File.Exists(rutaArchivoLower))
                {
                    rutaArchivo = rutaArchivoLower;
                }
                else
                {
                    // Intentar con capitalización estándar de carpetas: Styles, Scripts, Imagenes
                    string[] topLevel = new[] { "styles", "scripts", "imagenes" };
                    string[] stdNames = new[] { "Styles", "Scripts", "Imagenes" };
                    for (int i = 0; i < topLevel.Length; i++)
                    {
                        string t = topLevel[i];
                        if (rutaOriginal.StartsWith(t + "/", StringComparison.OrdinalIgnoreCase))
                        {
                            string alt = stdNames[i] + rutaOriginal.Substring(t.Length);
                            string altPath = Path.Combine(baseDir, "wwwroot", alt.Replace("/", Path.DirectorySeparatorChar.ToString()));
                            if (File.Exists(altPath)) { rutaArchivo = altPath; break; }
                        }
                    }
                }
            }

            if (File.Exists(rutaArchivo))
            {
                // Inyectar de forma no intrusiva para HTML: nada; las cookies CSRF ya se envían por Set-Cookie.
                byte[] buffer = File.ReadAllBytes(rutaArchivo);
                context.Response.ContentLength64 = buffer.Length;
                context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            }
            else
            {
                context.Response.StatusCode = 404;
                using var writer = new StreamWriter(context.Response.OutputStream);
                writer.Write("<h1 style='color:red'><b>Archivo no encontrado</b></h1>");
            }

            context.Response.OutputStream.Close();
        }

        private static void RechazoCsrf(HttpListenerContext ctx)
        {
            ctx.Response.StatusCode = 403;
            ctx.Response.ContentType = "application/json";
            using var w = new StreamWriter(ctx.Response.OutputStream);
            // Mensaje genérico para no filtrar detalles internos.
            w.Write("{\"code\":\"csrf_invalid\",\"message\":\"CSRF token inválido o ausente\"}");
            ctx.Response.OutputStream.Close();
        }
    }
}
