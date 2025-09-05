using System;

namespace SWGROI_Server
{
    internal class Program
    {
        static void Main()
        {
            string url = "http://*:8888/";
            StaticServer.Iniciar(url);
        }
    }
}
