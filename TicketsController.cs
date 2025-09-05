using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using MySql.Data.MySqlClient;
using SWGROI_Server.DB;

namespace SWGROI_Server.Controllers
{
    public class TicketsController
    {
        public static void Procesar(HttpListenerContext context)
        {
            string url = context.Request.RawUrl?.ToLower();

            if (context.Request.HttpMethod == "POST")
            {
                if (url.Contains("/actualizar"))
                {
                    ActualizarTicket(context);
                }
                else
                {
                    RegistrarTicket(context);
                }
            }
            else
            {
                context.Response.StatusCode = 405;
                using var writer = new StreamWriter(context.Response.OutputStream);
                writer.Write("Método no permitido.");
            }
        }
        private static void RegistrarTicket(HttpListenerContext context)
        {
            using var reader = new StreamReader(context.Request.InputStream);
            string body = reader.ReadToEnd();
            var datos = ParsearDatos(body);

            if (!datos.ContainsKey("Folio") || !datos.ContainsKey("Descripcion") ||
                !datos.ContainsKey("Responsable") || !datos.ContainsKey("Estado"))
            {
                context.Response.StatusCode = 400;
                using var w1 = new StreamWriter(context.Response.OutputStream);
                w1.Write("Faltan datos obligatorios para registrar el ticket.");
                return;
            }

            using var conexion = new MySqlConnection(ConexionBD.CadenaConexion);
            conexion.Open();

            // Verificar si ya existe el ticket
            string consultaExistencia = "SELECT Estado FROM tickets WHERE Folio = @Folio";
            using var cmdExiste = new MySqlCommand(consultaExistencia, conexion);
            cmdExiste.Parameters.AddWithValue("@Folio", datos["Folio"]);
            var estadoExistente = cmdExiste.ExecuteScalar();

            if (estadoExistente != null)
            {
                context.Response.StatusCode = 200;
                using var w2 = new StreamWriter(context.Response.OutputStream);
                w2.Write("Ticket registrado previamente. En espera de actualización por Mesa de Control.");
                return;
            }

            string query = @"INSERT INTO tickets 
        (Folio, Descripcion, Responsable, Estado, Comentario, FechaRegistro) 
        VALUES (@Folio, @Descripcion, @Responsable, @Estado, @Comentario, NOW())";

            using var cmd = new MySqlCommand(query, conexion);
            cmd.Parameters.AddWithValue("@Folio", datos["Folio"]);
            cmd.Parameters.AddWithValue("@Descripcion", datos["Descripcion"]);
            cmd.Parameters.AddWithValue("@Responsable", datos["Responsable"]);
            cmd.Parameters.AddWithValue("@Estado", datos["Estado"]);
            cmd.Parameters.AddWithValue("@Comentario", ""); // Comentario vacío por defecto

            int resultado = cmd.ExecuteNonQuery();
            context.Response.StatusCode = resultado > 0 ? 200 : 400;

            using var writer = new StreamWriter(context.Response.OutputStream);
            writer.Write(resultado > 0
                ? "Ticket registrado correctamente."
                : "No se pudo registrar el ticket.");
        }


        private static void ActualizarTicket(HttpListenerContext context)
        {
            using var reader = new StreamReader(context.Request.InputStream);
            string body = reader.ReadToEnd();
            var datos = ParsearDatos(body);

            if (!datos.ContainsKey("folio") || !datos.ContainsKey("descripcion") ||
                !datos.ContainsKey("responsable") || !datos.ContainsKey("estado"))
            {
                context.Response.StatusCode = 400;
                using var w1 = new StreamWriter(context.Response.OutputStream);
                w1.Write("Faltan datos obligatorios para actualizar el ticket.");
                return;
            }

            using var conexion = new MySqlConnection(ConexionBD.CadenaConexion);
            conexion.Open();

            string query = @"UPDATE tickets SET 
                                Descripcion = @Descripcion, 
                                Responsable = @Responsable, 
                                Estado = @Estado 
                             WHERE Folio = @Folio";

            using var cmd = new MySqlCommand(query, conexion);
            cmd.Parameters.AddWithValue("@Descripcion", datos["descripcion"]);
            cmd.Parameters.AddWithValue("@Responsable", datos["responsable"]);
            cmd.Parameters.AddWithValue("@Estado", datos["estado"]);
            cmd.Parameters.AddWithValue("@Folio", datos["folio"]);

            int resultado = cmd.ExecuteNonQuery();
            context.Response.StatusCode = resultado > 0 ? 200 : 400;

            using var writer = new StreamWriter(context.Response.OutputStream);
            writer.Write(resultado > 0
                ? "Ticket actualizado correctamente."
                : "No se pudo actualizar el ticket.");
        }

        private static Dictionary<string, string> ParsearDatos(string body)
        {
            var dict = new Dictionary<string, string>();
            body = body.Trim('{', '}').Replace("\"", "");
            foreach (var par in body.Split(','))
            {
                var kv = par.Split(':');
                if (kv.Length == 2)
                {
                    string clave = kv[0].Trim();
                    string valor = kv[1].Trim();

                    if (!dict.ContainsKey(clave))
                        dict[clave] = valor;
                }
            }
            return dict;
        }
    }
}
