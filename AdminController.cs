using System;
using System.IO;
using System.Net;
using System.Text;
using System.Collections.Generic;
using MySql.Data.MySqlClient;

using SWGROI_Server.DB;
using SWGROI_Server.Models;

namespace SWGROI_Server.Controllers
{
    public static class AdminController
    {
        // ===== Utilidad JSON =====
        private static void Json(HttpListenerResponse res, int status, string jsonText)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(jsonText);
            res.StatusCode = status;
            res.ContentType = "application/json; charset=utf-8";
            res.ContentLength64 = bytes.Length;
            res.OutputStream.Write(bytes, 0, bytes.Length);
            res.OutputStream.Close();
        }

        public static void Procesar(HttpListenerContext context)
        {
            string metodo = context.Request.HttpMethod;
            switch (metodo)
            {
                case "GET": ObtenerUsuarios(context); break;
                case "POST": GuardarUsuario(context); break;
                case "PUT": ActualizarUsuario(context); break;
                case "DELETE": EliminarUsuario(context); break;
                default: Json(context.Response, 405, "{\"exito\":false,\"mensaje\":\"Método no permitido\"}"); break;
            }
        }

        // ===== GET /admin?id=XX  o listado =====
        private static void ObtenerUsuarios(HttpListenerContext context)
        {
            try
            {
                string query = context.Request.Url.Query ?? string.Empty;
                int id = ObtenerIdDesdeQuery(query);
                List<string> usuarios = new List<string>();

                using (var conexion = new MySqlConnection(ConexionBD.CadenaConexion))
                {
                    conexion.Open();
                    string sql = (id > 0)
                        ? "SELECT * FROM usuarios WHERE IdUsuario = @Id"
                        : "SELECT * FROM usuarios";

                    using (var cmd = new MySqlCommand(sql, conexion))
                    {
                        if (id > 0) cmd.Parameters.AddWithValue("@Id", id);

                        using (var rdr = cmd.ExecuteReader())
                        {
                            while (rdr.Read())
                            {
                                // Construcción de JSON sin interpolación
                                string nombre = rdr.GetString("NombreCompleto").Replace("\"", "\\\"");
                                string usuario = rdr.GetString("Usuario").Replace("\"", "\\\"");
                                string pass = rdr.GetString("Contrasena").Replace("\"", "\\\"");
                                string rol = rdr.GetString("Rol").Replace("\"", "\\\"");

                                string item =
                                    "{"
                                    + "\"IdUsuario\":" + rdr.GetInt32("IdUsuario")
                                    + ",\"NombreCompleto\":\"" + nombre + "\""
                                    + ",\"Usuario\":\"" + usuario + "\""
                                    + ",\"Contrasena\":\"" + pass + "\""
                                    + ",\"Rol\":\"" + rol + "\""
                                    + "}";

                                usuarios.Add(item);
                            }
                        }
                    }
                }

                string jsonText = "[" + string.Join(",", usuarios) + "]";
                Json(context.Response, 200, jsonText);
            }
            catch
            {
                Json(context.Response, 500, "{\"exito\":false,\"mensaje\":\"Error al obtener usuarios\"}");
            }
        }

        // ===== POST /admin  (crear) =====
        private static void GuardarUsuario(HttpListenerContext context)
        {
            string body;
            using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
                body = reader.ReadToEnd();

            Dictionary<string, string> datos = ParseJson(body);

            // Validaciones básicas espejo del front
            string nombre = datos.ContainsKey("NombreCompleto") ? (datos["NombreCompleto"] ?? "").Trim() : "";
            string usuario = datos.ContainsKey("Usuario") ? (datos["Usuario"] ?? "").Trim() : "";
            string contrasena = datos.ContainsKey("Contrasena") ? (datos["Contrasena"] ?? "").Trim() : "";
            string rol = datos.ContainsKey("Rol") ? (datos["Rol"] ?? "").Trim() : "";

            if (string.IsNullOrWhiteSpace(nombre) || string.IsNullOrWhiteSpace(usuario) || string.IsNullOrWhiteSpace(contrasena) || string.IsNullOrWhiteSpace(rol))
            { Json(context.Response, 400, "{\"exito\":false,\"mensaje\":\"Faltan datos obligatorios\"}"); return; }
            if (usuario.Length > 100) { Json(context.Response, 400, "{\"exito\":false,\"mensaje\":\"El usuario debe tener hasta 100 caracteres\"}"); return; }
            if (nombre.Length > 150) { Json(context.Response, 400, "{\"exito\":false,\"mensaje\":\"El nombre debe tener hasta 150 caracteres\"}"); return; }
            if (contrasena.Length < 6 || contrasena.Length > 100) { Json(context.Response, 400, "{\"exito\":false,\"mensaje\":\"La contraseña debe tener entre 6 y 100 caracteres\"}"); return; }

            try
            {
                using (var conexion = new MySqlConnection(ConexionBD.CadenaConexion))
                {
                    conexion.Open();
                    string sql = "INSERT INTO usuarios (NombreCompleto, Usuario, Contrasena, Rol) VALUES (@n, @u, @p, @r)";
                    using (var cmd = new MySqlCommand(sql, conexion))
                    {
                        cmd.Parameters.AddWithValue("@n", nombre);
                        cmd.Parameters.AddWithValue("@u", usuario);
                        cmd.Parameters.AddWithValue("@p", contrasena);
                        cmd.Parameters.AddWithValue("@r", rol);
                        cmd.ExecuteNonQuery();
                    }
                }

                Json(context.Response, 201, "{\"exito\":true,\"mensaje\":\"Usuario creado\"}");
            }
            catch (MySqlException ex) when (ex.Number == 1062) // UNIQUE(Usuario)
            {
                Json(context.Response, 409, "{\"exito\":false,\"mensaje\":\"El nombre de usuario ya existe\"}");
            }
            catch
            {
                Json(context.Response, 500, "{\"exito\":false,\"mensaje\":\"Error al guardar usuario\"}");
            }
        }

        // ===== PUT /admin  (actualizar) =====
        private static void ActualizarUsuario(HttpListenerContext context)
        {
            string body;
            using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
                body = reader.ReadToEnd();

            Dictionary<string, string> datos = ParseJson(body);

            try
            {
                int filas;
                using (var conexion = new MySqlConnection(ConexionBD.CadenaConexion))
                {
                    conexion.Open();
                    string sql =
                        "UPDATE usuarios SET NombreCompleto=@n, Usuario=@u, Contrasena=@p, Rol=@r WHERE IdUsuario=@id";
                    using (var cmd = new MySqlCommand(sql, conexion))
                    {
                        string nombre = datos.ContainsKey("NombreCompleto") ? (datos["NombreCompleto"] ?? "").Trim() : "";
                        string usuario = datos.ContainsKey("Usuario") ? (datos["Usuario"] ?? "").Trim() : "";
                        string contrasena = datos.ContainsKey("Contrasena") ? (datos["Contrasena"] ?? "").Trim() : "";
                        string rol = datos.ContainsKey("Rol") ? (datos["Rol"] ?? "").Trim() : "";
                        cmd.Parameters.AddWithValue("@n", nombre);
                        cmd.Parameters.AddWithValue("@u", usuario);
                        cmd.Parameters.AddWithValue("@p", contrasena);
                        cmd.Parameters.AddWithValue("@r", rol);
                        cmd.Parameters.AddWithValue("@id", datos["IdUsuario"]);
                        filas = cmd.ExecuteNonQuery();
                    }
                }

                if (filas > 0)
                    Json(context.Response, 200, "{\"exito\":true,\"mensaje\":\"Usuario actualizado\"}");
                else
                    Json(context.Response, 200, "{\"exito\":false,\"mensaje\":\"Sin cambios\"}");
            }
            catch (MySqlException ex) when (ex.Number == 1062)
            {
                Json(context.Response, 409, "{\"exito\":false,\"mensaje\":\"El nombre de usuario ya existe\"}");
            }
            catch
            {
                Json(context.Response, 500, "{\"exito\":false,\"mensaje\":\"Error al actualizar usuario\"}");
            }
        }

        // ===== DELETE /admin?id=XX =====
        private static void EliminarUsuario(HttpListenerContext context)
        {
            try
            {
                string query = context.Request.Url.Query ?? string.Empty;
                int id = ObtenerIdDesdeQuery(query);

                int filas;
                using (var conexion = new MySqlConnection(ConexionBD.CadenaConexion))
                {
                    conexion.Open();
                    using (var cmd = new MySqlCommand("DELETE FROM usuarios WHERE IdUsuario = @Id", conexion))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);
                        filas = cmd.ExecuteNonQuery();
                    }
                }

                if (filas > 0)
                    Json(context.Response, 200, "{\"exito\":true,\"mensaje\":\"Usuario eliminado\"}");
                else
                    Json(context.Response, 200, "{\"exito\":false,\"mensaje\":\"No se encontró el usuario\"}");
            }
            catch
            {
                Json(context.Response, 500, "{\"exito\":false,\"mensaje\":\"Error al eliminar usuario\"}");
            }
        }

        // ===== Utilidades =====
        private static Dictionary<string, string> ParseJson(string body)
        {
            // Parser simple para JSON plano: {"k":"v",...}
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(body)) return dict;

            string s = body.Trim();
            if (s.StartsWith("{")) s = s.Substring(1);
            if (s.EndsWith("}")) s = s.Substring(0, s.Length - 1);

            // quitar comillas dobles
            s = s.Replace("\\\"", "§§q§§"); // proteger \" si llegara
            s = s.Replace("\"", "");
            s = s.Replace("§§q§§", "\"");

            string[] pares = s.Split(',');
            foreach (string par in pares)
            {
                string[] kv = par.Split(':');
                if (kv.Length == 2)
                {
                    string k = kv[0].Trim();
                    string v = kv[1].Trim();
                    dict[k] = v;
                }
            }
            return dict;
        }

        private static int ObtenerIdDesdeQuery(string query)
        {
            if (string.IsNullOrEmpty(query)) return 0;
            if (query.StartsWith("?")) query = query.Substring(1);
            string[] partes = query.Split('=');
            if (partes.Length == 2 && partes[0] == "id")
            {
                int id;
                if (int.TryParse(partes[1], out id))
                    return id;
            }
            return 0;
        }
    }
}
