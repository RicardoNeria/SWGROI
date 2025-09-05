using System;
using System.IO;
using System.Net;
using SWGROI_Server.DB;

namespace SWGROI_Server.Controllers
{
    public static class MenuController
    {
        public static void Procesar(HttpListenerContext context)
        {
            try
            {
                string rutaArchivo = "wwwroot/Styles/menu.html";

                if (context.Request.RawUrl.Contains("/menu/indicadores"))
                {
                    Console.WriteLine("↪ Solicitando KPIs de /menu/indicadores...");

                    int totalTickets = ConexionBD.ContarRegistros("tickets");
                    int totalAvisos = ConexionBD.ContarRegistros("avisos");

                    Console.WriteLine($"✔ Total Tickets: {totalTickets}");
                    Console.WriteLine($"✔ Total Avisos: {totalAvisos}");

                    string json = $"{{\"tickets\":{totalTickets},\"avisos\":{totalAvisos}}}";
                    byte[] buffer = System.Text.Encoding.UTF8.GetBytes(json);
                    context.Response.ContentType = "application/json";
                    context.Response.ContentLength64 = buffer.Length;
                    context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                }
                else if (File.Exists(rutaArchivo))
                {
                    string contenido = File.ReadAllText(rutaArchivo);
                    byte[] buffer = System.Text.Encoding.UTF8.GetBytes(contenido);
                    context.Response.ContentType = "text/html";
                    context.Response.ContentLength64 = buffer.Length;
                    context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                }
                else
                {
                    string mensaje = "<h1>Error 404 - Página no encontrada</h1>";
                    byte[] buffer = System.Text.Encoding.UTF8.GetBytes(mensaje);
                    context.Response.ContentType = "text/html";
                    context.Response.ContentLength64 = buffer.Length;
                    context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                }
            }
            catch (Exception ex)
            {
                string error = "<h1>Error interno del servidor</h1><p>" + ex.Message + "</p>";
                byte[] buffer = System.Text.Encoding.UTF8.GetBytes(error);
                context.Response.ContentType = "text/html";
                context.Response.ContentLength64 = buffer.Length;
                context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            }
            finally
            {
                context.Response.OutputStream.Close();
            }
        }
    }
}
