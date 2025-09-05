using System.Net;
using System.IO;
using MySql.Data.MySqlClient;
using SWGROI_Server.DB;
using System.Collections.Generic;

namespace SWGROI_Server.Controllers
{
    public static class RecuperarController
    {
        public static void Procesar(HttpListenerContext context)
        {
            if (context.Request.HttpMethod != "POST") return;

            using (var reader = new StreamReader(context.Request.InputStream))
            {
                var body = reader.ReadToEnd();
                var datos = ParsearJson(body);

                using (var conexion = new MySqlConnection(ConexionBD.CadenaConexion))
                {
                    conexion.Open();
                    string sql = "UPDATE usuarios SET Contrasena = @NuevaContrasena WHERE Usuario = @Usuario";

                    using (var cmd = new MySqlCommand(sql, conexion))
                    {
                        cmd.Parameters.AddWithValue("@NuevaContrasena", datos["NuevaContrasena"]);
                        cmd.Parameters.AddWithValue("@Usuario", datos["Usuario"]);
                        int filas = cmd.ExecuteNonQuery();

                        string respuesta = "{\"exito\":" + (filas > 0 ? "true" : "false") + "}";
                        using (var writer = new StreamWriter(context.Response.OutputStream))
                        {
                            writer.Write(respuesta);
                        }
                    }
                }
            }
        }

        private static Dictionary<string, string> ParsearJson(string body)
        {
            var dict = new Dictionary<string, string>();
            body = body.Trim('{', '}').Replace("\"", "");
            var pares = body.Split(',');

            foreach (var par in pares)
            {
                var kv = par.Split(':');
                if (kv.Length == 2)
                {
                    dict[kv[0].Trim()] = kv[1].Trim();
                }
            }

            return dict;
        }
    }
}
