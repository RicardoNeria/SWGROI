using System;
using System.Net;

namespace SWGROI_Server
{
    public static class StaticServer
    {
        private static HttpListener listener;

        public static void Iniciar(string urlPrefix)
        {
            listener = new HttpListener();
            string nombreMaquina = Environment.MachineName;

            if (nombreMaquina == "RicardoNeria") // <--- PC local
            {
                listener.Prefixes.Add("http://localhost:8888/");
                Console.WriteLine("🧪 Entorno de DESARROLLO (localhost)");
            }
            else
            {
                listener.Prefixes.Add("http://+:8888/");
                Console.WriteLine("🚀 Entorno de PRODUCCION (VPS)");
            }
            listener.Start();
            Console.WriteLine($"Servidor escuchando en {urlPrefix}");

            while (true)
            {
                try
                {
                    var context = listener.GetContext();
                    RequestRouter.ProcesarArchivosEstaticos(context);
                }
                catch (HttpListenerException ex)
                {
                    Console.WriteLine($"[Error HttpListener] {ex.Message}");
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Error general] {ex.Message}");
                }
            }
        }

        public static void Detener()
        {
            listener?.Stop();
            Console.WriteLine("Servidor detenido.");
        }
    }
}
