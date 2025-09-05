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
        private static void Json(HttpListenerResponse res, int status, string jsonText)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(jsonText);
            res.StatusCode = status;
            res.ContentType = "application/json; charset=utf-8";
            res.ContentLength64 = bytes.Length;
            res.OutputStream.Write(bytes, 0, bytes.Length);
            res.OutputStream.Close();
        }

        private static void RegistrarTicket(HttpListenerContext context)
        {
            using var reader = new StreamReader(context.Request.InputStream);
            string body = reader.ReadToEnd();
            var datos = ParsearDatos(body);

            // Normalización
            string folio = datos.ContainsKey("Folio") ? (datos["Folio"] ?? "").Trim().ToUpperInvariant() : "";
            string descripcion = datos.ContainsKey("Descripcion") ? (datos["Descripcion"] ?? "").Trim() : "";
            string responsable = datos.ContainsKey("Responsable") ? (datos["Responsable"] ?? "").Trim() : "";
            string estado = datos.ContainsKey("Estado") ? (datos["Estado"] ?? "").Trim() : "";

            // Validaciones
            if (string.IsNullOrWhiteSpace(folio) || string.IsNullOrWhiteSpace(descripcion) ||
                string.IsNullOrWhiteSpace(responsable) || string.IsNullOrWhiteSpace(estado))
            { Json(context.Response, 400, "{\"ok\":false,\"message\":\"Faltan datos obligatorios\"}"); return; }

            if (!System.Text.RegularExpressions.Regex.IsMatch(folio, "^[A-Z0-9\\-]{6,20}$"))
            { Json(context.Response, 400, "{\"ok\":false,\"message\":\"El folio debe tener 6–20 caracteres alfanuméricos\"}"); return; }

            if (descripcion.Length < 10 || descripcion.Length > 500)
            { Json(context.Response, 400, "{\"ok\":false,\"message\":\"La descripción debe tener entre 10 y 500 caracteres\"}"); return; }

            if (responsable.Length == 0 || responsable.Length > 100)
            { Json(context.Response, 400, "{\"ok\":false,\"message\":\"El responsable debe tener hasta 100 caracteres\"}"); return; }

            var estadosPermitidos = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "Almacén","Capturado","Programado/Asignado","Abierto","En Proceso","Cerrado" };
            if (!estadosPermitidos.Contains(estado))
            { Json(context.Response, 400, "{\"ok\":false,\"message\":\"Estado no válido\"}"); return; }

            using var conexion = new MySqlConnection(ConexionBD.CadenaConexion);
            conexion.Open();

            // Verificar si ya existe el ticket
            string consultaExistencia = "SELECT Estado FROM tickets WHERE Folio = @Folio";
            using var cmdExiste = new MySqlCommand(consultaExistencia, conexion);
            cmdExiste.Parameters.AddWithValue("@Folio", datos["Folio"]);
            var estadoExistente = cmdExiste.ExecuteScalar();

            if (estadoExistente != null)
            {
                Json(context.Response, 200, "{\"ok\":true,\"message\":\"Ticket registrado previamente. En espera de actualización por Mesa de Control.\"}");
                return;
            }

            string query = @"INSERT INTO tickets 
        (Folio, Descripcion, Responsable, Estado, Comentario, FechaRegistro) 
        VALUES (@Folio, @Descripcion, @Responsable, @Estado, @Comentario, NOW())";

            using var cmd = new MySqlCommand(query, conexion);
            cmd.Parameters.AddWithValue("@Folio", folio);
            cmd.Parameters.AddWithValue("@Descripcion", descripcion);
            cmd.Parameters.AddWithValue("@Responsable", responsable);
            cmd.Parameters.AddWithValue("@Estado", estado);
            cmd.Parameters.AddWithValue("@Comentario", ""); // Comentario vacío por defecto

            int resultado = cmd.ExecuteNonQuery();
            if (resultado > 0)
                Json(context.Response, 200, "{\"ok\":true,\"message\":\"Ticket registrado correctamente\"}");
            else
                Json(context.Response, 400, "{\"ok\":false,\"message\":\"No se pudo registrar el ticket\"}");
        }


        private static void ActualizarTicket(HttpListenerContext context)
        {
            using var reader = new StreamReader(context.Request.InputStream);
            string body = reader.ReadToEnd();
            var datos = ParsearDatos(body);

            string folio = datos.ContainsKey("folio") ? (datos["folio"] ?? "").Trim().ToUpperInvariant() : "";
            string descripcion = datos.ContainsKey("descripcion") ? (datos["descripcion"] ?? "").Trim() : "";
            string responsable = datos.ContainsKey("responsable") ? (datos["responsable"] ?? "").Trim() : "";
            string estado = datos.ContainsKey("estado") ? (datos["estado"] ?? "").Trim() : "";

            if (string.IsNullOrWhiteSpace(folio) || string.IsNullOrWhiteSpace(descripcion) ||
                string.IsNullOrWhiteSpace(responsable) || string.IsNullOrWhiteSpace(estado))
            { Json(context.Response, 400, "{\"ok\":false,\"message\":\"Faltan datos obligatorios para actualizar el ticket\"}"); return; }

            if (!System.Text.RegularExpressions.Regex.IsMatch(folio, "^[A-Z0-9\\-]{6,20}$"))
            { Json(context.Response, 400, "{\"ok\":false,\"message\":\"El folio debe tener 6–20 caracteres alfanuméricos\"}"); return; }

            if (descripcion.Length < 10 || descripcion.Length > 500)
            { Json(context.Response, 400, "{\"ok\":false,\"message\":\"La descripción debe tener entre 10 y 500 caracteres\"}"); return; }

            if (responsable.Length == 0 || responsable.Length > 100)
            { Json(context.Response, 400, "{\"ok\":false,\"message\":\"El responsable debe tener hasta 100 caracteres\"}"); return; }

            using var conexion = new MySqlConnection(ConexionBD.CadenaConexion);
            conexion.Open();

            string query = @"UPDATE tickets SET 
                                Descripcion = @Descripcion, 
                                Responsable = @Responsable, 
                                Estado = @Estado 
                             WHERE Folio = @Folio";

            using var cmd = new MySqlCommand(query, conexion);
            cmd.Parameters.AddWithValue("@Descripcion", descripcion);
            cmd.Parameters.AddWithValue("@Responsable", responsable);
            cmd.Parameters.AddWithValue("@Estado", estado);
            cmd.Parameters.AddWithValue("@Folio", folio);

            int resultado = cmd.ExecuteNonQuery();
            if (resultado > 0)
                Json(context.Response, 200, "{\"ok\":true,\"message\":\"Ticket actualizado correctamente\"}");
            else
                Json(context.Response, 400, "{\"ok\":false,\"message\":\"No se pudo actualizar el ticket\"}");
        }

        private static Dictionary<string, string> ParsearDatos(string body)
        {
            var dict = new Dictionary<string, string>();
            body = (body ?? string.Empty).Trim('{', '}').Replace("\"", "");
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
