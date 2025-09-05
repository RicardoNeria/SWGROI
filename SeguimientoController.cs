using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using MySql.Data.MySqlClient;
using SWGROI_Server.DB;

namespace SWGROI_Server.Controllers
{
    public static class SeguimientoController
    {
        public static void Procesar(HttpListenerContext context)
        {
            if (context.Request.HttpMethod == "GET")
            {
                if (!string.IsNullOrEmpty(context.Request.QueryString["folio"]))
                {
                    ObtenerComentarioPorFolio(context, context.Request.QueryString["folio"]);
                }
                else
                {
                    ObtenerTicketsAsignados(context);
                }
            }
            else if (context.Request.HttpMethod == "POST")
            {
                ActualizarEstadoTicket(context);
            }
            else
            {
                context.Response.StatusCode = 405;
                using var writer = new StreamWriter(context.Response.OutputStream);
                writer.Write("Método no permitido.");
            }
        }

        private static void ObtenerTicketsAsignados(HttpListenerContext context)
        {
            var lista = new List<Dictionary<string, string>>();

            using var conexion = new MySqlConnection(ConexionBD.CadenaConexion);
            conexion.Open();

            string query = @"SELECT Folio, Descripcion, Estado, Responsable, Comentario 
                     FROM tickets 
                     ORDER BY FechaRegistro DESC";

            using var cmd = new MySqlCommand(query, conexion);
            using var rdr = cmd.ExecuteReader();

            while (rdr.Read())
            {
                var folio = rdr["Folio"].ToString();
                var descripcion = rdr["Descripcion"].ToString();
                var estado = rdr["Estado"].ToString();
                var responsable = rdr["Responsable"].ToString();
                var comentario = rdr["Comentario"] != DBNull.Value ? rdr["Comentario"].ToString() : "";

                var ticket = new Dictionary<string, string>
                {
                    ["Folio"] = folio,
                    ["Descripcion"] = descripcion,
                    ["Estado"] = estado,
                    ["Responsable"] = responsable,
                    ["Comentario"] = comentario
                };

                lista.Add(ticket);
            }

            string json = ConvertirListaAJson(lista);
            context.Response.ContentType = "application/json";
            using var writer = new StreamWriter(context.Response.OutputStream);
            writer.Write(json);
        }


        private static void ObtenerComentarioPorFolio(HttpListenerContext context, string folio)
        {
            using var conexion = new MySqlConnection(ConexionBD.CadenaConexion);
            conexion.Open();

            string query = "SELECT Descripcion, Comentario, Estado FROM tickets WHERE Folio = @Folio LIMIT 1";
            using var cmd = new MySqlCommand(query, conexion);
            cmd.Parameters.AddWithValue("@Folio", folio);

            using var rdr = cmd.ExecuteReader();
            string descripcion = "";
            string comentario = "";
            string estado = "";

            if (rdr.Read())
            {
                descripcion = rdr["Descripcion"]?.ToString() ?? "";
                comentario = rdr["Comentario"]?.ToString() ?? "";
                estado = rdr["Estado"]?.ToString() ?? "";
            }

            string json = $"{{\"Descripcion\":\"{Escapar(descripcion)}\",\"Comentario\":\"{Escapar(comentario)}\",\"Estado\":\"{Escapar(estado)}\"}}";

            context.Response.ContentType = "application/json";
            using var writer = new StreamWriter(context.Response.OutputStream);
            writer.Write(json);
        }

        private static void ActualizarEstadoTicket(HttpListenerContext context)
        {
            string body;
            using (var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
            {
                body = reader.ReadToEnd();
            }

            var datos = ParsearJson(body);

            if (!datos.ContainsKey("folio") || !datos.ContainsKey("nuevoEstado"))
            {
                context.Response.StatusCode = 400;
                using var w = new StreamWriter(context.Response.OutputStream);
                w.Write("Faltan datos obligatorios.");
                return;
            }

            using var conexion = new MySqlConnection(ConexionBD.CadenaConexion);
            conexion.Open();

            string query = @"UPDATE tickets 
                             SET Estado = @Estado, Comentario = @Comentario 
                             WHERE Folio = @Folio";

            using var cmd = new MySqlCommand(query, conexion);
            cmd.Parameters.AddWithValue("@Estado", datos["nuevoEstado"]);
            cmd.Parameters.AddWithValue("@Comentario", datos.ContainsKey("comentario") ? datos["comentario"] : "");
            cmd.Parameters.AddWithValue("@Folio", datos["folio"]);

            int resultado = cmd.ExecuteNonQuery();

            context.Response.StatusCode = resultado > 0 ? 200 : 400;
            using var writer = new StreamWriter(context.Response.OutputStream);
            writer.Write(resultado > 0
                ? "Estado actualizado correctamente."
                : "No se pudo actualizar el estado.");
        }

        private static string ConvertirListaAJson(List<Dictionary<string, string>> lista)
        {
            var sb = new StringBuilder();
            sb.Append("[");

            for (int i = 0; i < lista.Count; i++)
            {
                sb.Append("{");
                var item = lista[i];
                int j = 0;

                foreach (var kv in item)
                {
                    sb.Append($"\"{kv.Key}\":\"{Escapar(kv.Value)}\"");
                    if (++j < item.Count) sb.Append(",");
                }

                sb.Append("}");
                if (i < lista.Count - 1) sb.Append(",");
            }

            sb.Append("]");
            return sb.ToString();
        }

        private static string Escapar(string input)
        {
            return input.Replace("\\", "\\\\")
                        .Replace("\"", "\\\"")
                        .Replace("\n", " ")
                        .Replace("\r", " ");
        }

        private static Dictionary<string, string> ParsearJson(string body)
        {
            var dict = new Dictionary<string, string>();
            body = body.Trim('{', '}').Replace("\"", "");
            foreach (var par in body.Split(','))
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
