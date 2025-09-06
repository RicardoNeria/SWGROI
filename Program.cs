using System;

namespace SWGROI_Server
{
    internal class Program
    {
        static void Main()
        {
            // Mantener bootstrap simple; el routing aplicará headers y seguridad.
            string url = "http://*:8888/";
            StaticServer.Iniciar(url);
        }
    }
}
