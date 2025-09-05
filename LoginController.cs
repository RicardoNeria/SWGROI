using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using MySql.Data.MySqlClient;
using SWGROI_Server.DB;
using SWGROI_Server.Models;
using SWGROI_Server.Security;
using SWGROI_Server.Utils;

namespace SWGROI_Server.Controllers
{
    public static class LoginController
    {
        // Endurece login: rate limit, PBKDF2 con migración, rotación de sesión y cookies seguras.
        public static void Procesar(HttpListenerContext context)
        {
            if (!string.Equals(context.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = 405;
                using var w0 = new StreamWriter(context.Response.OutputStream, Encoding.UTF8);
                w0.Write("{\"exito\":false}");
                return;
            }

            string ip = context.Request.RemoteEndPoint?.Address?.ToString() ?? "-";
            var rateKey = $"login:{ip}";
            if (RateLimiter.IsLimited(rateKey, maxHits: 10, window: TimeSpan.FromMinutes(5)))
            {
                context.Response.StatusCode = 429;
                using var wrl = new StreamWriter(context.Response.OutputStream, Encoding.UTF8);
                wrl.Write("{\"exito\":false}");
                return;
            }

            string body;
            using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
                body = reader.ReadToEnd();

            var campos = JsonSerializer.Deserialize<Dictionary<string, string>>(body ?? "{}") ?? new Dictionary<string, string>();
            string usuario = campos.ContainsKey("Usuario") ? (campos["Usuario"] ?? string.Empty).Trim() : string.Empty;
            string contrasena = campos.ContainsKey("Contrasena") ? (campos["Contrasena"] ?? string.Empty) : string.Empty;

            if (!Validate.NotNullOrEmpty(usuario) || !Validate.MaxLen(usuario, 100) || !Validate.MaxLen(contrasena, 200))
            {
                context.Response.StatusCode = 400;
                using var wbad = new StreamWriter(context.Response.OutputStream, Encoding.UTF8);
                wbad.Write("{\"exito\":false}");
                return;
            }

            using var conexion = new MySqlConnection(ConexionBD.CadenaConexion);
            conexion.Open();

            string sql = "SELECT Usuario, Contrasena, Rol, NombreCompleto FROM usuarios WHERE Usuario=@Usuario LIMIT 1";
            using var cmd = new MySqlCommand(sql, conexion);
            cmd.Parameters.AddWithValue("@Usuario", usuario);

            using var rd = cmd.ExecuteReader();
            using var writer = new StreamWriter(context.Response.OutputStream, Encoding.UTF8);

            if (rd.Read())
            {
                string stored = rd["Contrasena"]?.ToString() ?? string.Empty;
                string rol = rd["Rol"]?.ToString() ?? string.Empty;
                string nombreCompleto = rd["NombreCompleto"]?.ToString() ?? string.Empty;

                bool ok = PasswordHasher.Verify(contrasena, stored);
                if (!ok)
                {
                    writer.Write("{\"exito\":false}");
                    return;
                }

                rd.Close(); // actualizar si se requiere migración
                if (PasswordHasher.NeedsMigration(stored))
                {
                    try
                    {
                        string nuevo = PasswordHasher.Hash(contrasena);
                        using var up = new MySqlCommand("UPDATE usuarios SET Contrasena=@C WHERE Usuario=@U", conexion);
                        up.Parameters.AddWithValue("@C", nuevo);
                        up.Parameters.AddWithValue("@U", usuario);
                        up.ExecuteNonQuery();
                    }
                    catch { }
                }

                // Cookies clásicas para compatibilidad (leídas por el front)
                context.Response.Headers.Add("Set-Cookie", $"usuario={Uri.EscapeDataString(usuario)}; Path=/");
                context.Response.Headers.Add("Set-Cookie", $"rol={Uri.EscapeDataString(rol)}; Path=/");
                context.Response.Headers.Add("Set-Cookie", $"nombre={Uri.EscapeDataString(nombreCompleto)}; Path=/");

                // Rotar sesión segura (HttpOnly + SameSite=Strict y Secure si aplica)
                SessionManager.RotateOnLogin(context, usuario, rol);

                writer.Write("{\"exito\":true,\"rol\":\"" + rol.Replace("\"","\\\"") + "\",\"nombre\":\"" + nombreCompleto.Replace("\"","\\\"") + "\"}");
            }
            else
            {
                writer.Write("{\"exito\":false}");
            }
        }
    }
}

