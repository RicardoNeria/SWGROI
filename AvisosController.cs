// Módulo de Avisos con serialización manual, validaciones y CRUD
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using MySql.Data.MySqlClient;
using SWGROI_Server.DB;
using SWGROI_Server.Models;

namespace SWGROI_Server.Controllers
{
    public static class AvisosController
    {
        public static void ManejarSolicitud(HttpListenerContext context)
        {
            try
            {
                switch (context.Request.HttpMethod?.ToUpperInvariant())
                {
                    case "GET":
                        ObtenerAvisos(context);
                        break;
                    case "POST":
                        if (!EsAdmin(context)) { EscribirJsonManual(context, "{\"error\":\"No autorizado\"}", 403); return; }
                        GuardarAviso(context);
                        break;
                    case "PUT":
                        if (!EsAdmin(context)) { EscribirJsonManual(context, "{\"error\":\"No autorizado\"}", 403); return; }
                        ActualizarAviso(context);
                        break;
                    case "DELETE":
                        if (!EsAdmin(context)) { EscribirJsonManual(context, "{\"error\":\"No autorizado\"}", 403); return; }
                        EliminarAviso(context);
                        break;
                    default:
                        EscribirJsonManual(context, "{\"error\":\"Método no permitido\"}", 405);
                        break;
                }
            }
            catch (Exception ex)
            {
                string payload = "{\"error\":\"Error interno\",\"detalle\":\"" + EscapeJson(ex.Message) + "\"}";
                EscribirJsonManual(context, payload, 500);
            }
        }

        private static void ObtenerAvisos(HttpListenerContext context)
        {
            var avisos = new List<AvisoEntidad>();

            using (var conexion = new MySqlConnection(ConexionBD.CadenaConexion))
            {
                conexion.Open();

                // Filtros: ?asunto= &fecha= (compat), ?desde=YYYY-MM-DD&hasta=YYYY-MM-DD
                string filtroFecha = context.Request.QueryString["fecha"] ?? string.Empty; // legado
                string filtroAsunto = context.Request.QueryString["asunto"] ?? string.Empty;
                string desde = context.Request.QueryString["desde"] ?? string.Empty;
                string hasta = context.Request.QueryString["hasta"] ?? string.Empty;

                // Paginación/sort
                int page = ParseInt(context.Request.QueryString["page"], 1);
                int pageSize = Clamp(ParseInt(context.Request.QueryString["pageSize"], 10), 5, 100);
                string sort = (context.Request.QueryString["sort"] ?? "Fecha").Trim();
                string dir = (context.Request.QueryString["dir"] ?? "DESC").Trim().ToUpperInvariant();
                bool exportCsv = string.Equals(context.Request.QueryString["export"], "csv", StringComparison.OrdinalIgnoreCase);

                var where = new List<string>();
                if (!string.IsNullOrWhiteSpace(filtroFecha))
                    where.Add("DATE(Fecha) = @Fecha");
                if (!string.IsNullOrWhiteSpace(filtroAsunto))
                    where.Add("Asunto LIKE @Asunto");
                if (!string.IsNullOrWhiteSpace(desde))
                    where.Add("DATE(Fecha) >= @Desde");
                if (!string.IsNullOrWhiteSpace(hasta))
                    where.Add("DATE(Fecha) <= @Hasta");

                string whereSql = where.Count > 0 ? (" WHERE " + string.Join(" AND ", where)) : string.Empty;
                string orderBy = MapSort(sort) + (dir == "ASC" ? " ASC" : " DESC");

                // Total para paginación
                long total = 0;
                string countSql = $"SELECT COUNT(*) FROM avisos{whereSql}";
                using (var cmdCount = new MySqlCommand(countSql, conexion))
                {
                    if (!string.IsNullOrWhiteSpace(filtroFecha)) cmdCount.Parameters.AddWithValue("@Fecha", filtroFecha);
                    if (!string.IsNullOrWhiteSpace(filtroAsunto)) cmdCount.Parameters.AddWithValue("@Asunto", "%" + filtroAsunto + "%");
                    if (!string.IsNullOrWhiteSpace(desde)) cmdCount.Parameters.AddWithValue("@Desde", desde);
                    if (!string.IsNullOrWhiteSpace(hasta)) cmdCount.Parameters.AddWithValue("@Hasta", hasta);
                    object cnt = cmdCount.ExecuteScalar();
                    total = Convert.ToInt64(cnt);
                }

                int offset = Math.Max(0, (page - 1) * pageSize);
                // Algunas versiones del conector MySql.Data pueden fallar con parámetros en LIMIT/OFFSET
                // Usamos valores enteros validados en la cadena para compatibilidad
                string query = $"SELECT Id, Fecha, Asunto, Mensaje FROM avisos{whereSql} ORDER BY {orderBy} LIMIT {pageSize} OFFSET {offset}";

                using (var cmd = new MySqlCommand(query, conexion))
                {
                    if (!string.IsNullOrWhiteSpace(filtroFecha))
                        cmd.Parameters.AddWithValue("@Fecha", filtroFecha);
                    if (!string.IsNullOrWhiteSpace(filtroAsunto))
                        cmd.Parameters.AddWithValue("@Asunto", "%" + filtroAsunto + "%");
                    if (!string.IsNullOrWhiteSpace(desde))
                        cmd.Parameters.AddWithValue("@Desde", desde);
                    if (!string.IsNullOrWhiteSpace(hasta))
                        cmd.Parameters.AddWithValue("@Hasta", hasta);

                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            avisos.Add(new AvisoEntidad
                            {
                                Id = Convert.ToInt32(rdr["Id"]),
                                Fecha = Convert.ToDateTime(rdr["Fecha"]).ToString("yyyy-MM-dd HH:mm"),
                                Asunto = rdr["Asunto"].ToString(),
                                Mensaje = rdr["Mensaje"].ToString()
                            });
                        }
                    }
                }
                if (exportCsv)
                {
                    // Exportación CSV simple
                    var sbCsv = new StringBuilder();
                    sbCsv.AppendLine("Id,Fecha,Asunto,Mensaje");
                    foreach (var a in avisos)
                    {
                        sbCsv.Append(a.Id).Append(',')
                             .Append('"').Append((a.Fecha ?? string.Empty).Replace("\"", "\"\"")).Append('"').Append(',')
                             .Append('"').Append((a.Asunto ?? string.Empty).Replace("\"", "\"\"")).Append('"').Append(',')
                             .Append('"').Append((a.Mensaje ?? string.Empty).Replace("\"", "\"\"")).Append('"').Append('\n');
                    }
                    string csv = sbCsv.ToString();
                    context.Response.StatusCode = 200;
                    context.Response.ContentType = "text/csv; charset=utf-8";
                    byte[] buffer = Encoding.UTF8.GetBytes(csv);
                    context.Response.ContentLength64 = buffer.Length;
                    context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                    context.Response.OutputStream.Close();
                    return;
                }
            }

            // Serialización manual de objeto con items y meta
            var sb = new StringBuilder();
            sb.Append('{');
            sb.Append("\"items\":[");
            for (int i = 0; i < avisos.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(SerializeAviso(avisos[i]));
            }
            sb.Append("],\"total\":");
            // Nota: total calculado dentro del using; re-calculamos si no disponible
            // Para simplicidad, si no hay pageSize en query, asumimos total = items.Count
            int requestedPageSize = Clamp(ParseInt(context.Request.QueryString["pageSize"], 0), 0, 100);
            long totalOut = avisos.Count; // fallback
            try
            {
                // Reintentar obtener total con los mismos filtros pero sin LIMIT/OFFSET
                string filtroFecha = context.Request.QueryString["fecha"] ?? string.Empty;
                string filtroAsunto = context.Request.QueryString["asunto"] ?? string.Empty;
                string desde = context.Request.QueryString["desde"] ?? string.Empty;
                string hasta = context.Request.QueryString["hasta"] ?? string.Empty;

                using (var conexion2 = new MySqlConnection(ConexionBD.CadenaConexion))
                {
                    conexion2.Open();
                    var clauses = new List<string>();
                    if (!string.IsNullOrWhiteSpace(filtroFecha)) clauses.Add("DATE(Fecha) = @Fecha");
                    if (!string.IsNullOrWhiteSpace(filtroAsunto)) clauses.Add("Asunto LIKE @Asunto");
                    if (!string.IsNullOrWhiteSpace(desde)) clauses.Add("DATE(Fecha) >= @Desde");
                    if (!string.IsNullOrWhiteSpace(hasta)) clauses.Add("DATE(Fecha) <= @Hasta");
                    string whereSql2 = clauses.Count > 0 ? (" WHERE " + string.Join(" AND ", clauses)) : string.Empty;
                    string sql2 = $"SELECT COUNT(*) FROM avisos{whereSql2}";
                    using (var cmd2 = new MySqlCommand(sql2, conexion2))
                    {
                        if (!string.IsNullOrWhiteSpace(filtroFecha)) cmd2.Parameters.AddWithValue("@Fecha", filtroFecha);
                        if (!string.IsNullOrWhiteSpace(filtroAsunto)) cmd2.Parameters.AddWithValue("@Asunto", "%" + filtroAsunto + "%");
                        if (!string.IsNullOrWhiteSpace(desde)) cmd2.Parameters.AddWithValue("@Desde", desde);
                        if (!string.IsNullOrWhiteSpace(hasta)) cmd2.Parameters.AddWithValue("@Hasta", hasta);
                        totalOut = Convert.ToInt64(cmd2.ExecuteScalar());
                    }
                }
            }
            catch { }
            sb.Append(totalOut);
            int pageOut = ParseInt(context.Request.QueryString["page"], 1);
            int pageSizeOut = Clamp(ParseInt(context.Request.QueryString["pageSize"], 10), 5, 100);
            if (requestedPageSize == 0) { pageOut = 1; pageSizeOut = avisos.Count; }
            sb.Append(",\"page\":" + pageOut + ",\"pageSize\":" + pageSizeOut + "}");
            EscribirJsonManual(context, sb.ToString(), 200);
        }

        private static void GuardarAviso(HttpListenerContext context)
        {
            string body = LeerBody(context);
            var campos = ParsearJsonPlano(body);

            string asunto = (Obtener(campos, "asunto") ?? string.Empty).Trim();
            string mensaje = (Obtener(campos, "mensaje") ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(asunto) || string.IsNullOrWhiteSpace(mensaje))
            {
                EscribirJsonManual(context, "{\"error\":\"Asunto y Mensaje son obligatorios\"}", 400);
                return;
            }

            if (asunto.Length > 100 || mensaje.Length > 2000)
            {
                EscribirJsonManual(context, "{\"error\":\"Límites superados: Asunto(100), Mensaje(2000)\"}", 400);
                return;
            }

            int nuevoId;
            using (var conexion = new MySqlConnection(ConexionBD.CadenaConexion))
            {
                conexion.Open();
                string query = "INSERT INTO avisos (Fecha, Asunto, Mensaje) VALUES (NOW(), @Asunto, @Mensaje); SELECT LAST_INSERT_ID();";
                using (var cmd = new MySqlCommand(query, conexion))
                {
                    cmd.Parameters.AddWithValue("@Asunto", asunto);
                    cmd.Parameters.AddWithValue("@Mensaje", mensaje);

                    object scalar = cmd.ExecuteScalar();
                    nuevoId = Convert.ToInt32(scalar);
                }
            }

            string payload = "{\"mensaje\":\"Aviso publicado correctamente\",\"id\":" + nuevoId + "}";
            EscribirJsonManual(context, payload, 201);
        }

        private static void ActualizarAviso(HttpListenerContext context)
        {
            string idParam = context.Request.QueryString["id"];
            if (string.IsNullOrWhiteSpace(idParam) || !int.TryParse(idParam, out int id) || id <= 0)
            {
                EscribirJsonManual(context, "{\"error\":\"Id inválido\"}", 400);
                return;
            }

            string body = LeerBody(context);
            var campos = ParsearJsonPlano(body);
            string asunto = (Obtener(campos, "asunto") ?? string.Empty).Trim();
            string mensaje = (Obtener(campos, "mensaje") ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(asunto) || string.IsNullOrWhiteSpace(mensaje))
            {
                EscribirJsonManual(context, "{\"error\":\"Asunto y Mensaje son obligatorios\"}", 400);
                return;
            }
            if (asunto.Length > 100 || mensaje.Length > 2000)
            {
                EscribirJsonManual(context, "{\"error\":\"Límites superados: Asunto(100), Mensaje(2000)\"}", 400);
                return;
            }

            int afectadas;
            using (var conexion = new MySqlConnection(ConexionBD.CadenaConexion))
            {
                conexion.Open();
                string sql = "UPDATE avisos SET Asunto=@Asunto, Mensaje=@Mensaje WHERE Id=@Id";
                using (var cmd = new MySqlCommand(sql, conexion))
                {
                    cmd.Parameters.AddWithValue("@Asunto", asunto);
                    cmd.Parameters.AddWithValue("@Mensaje", mensaje);
                    cmd.Parameters.AddWithValue("@Id", id);
                    afectadas = cmd.ExecuteNonQuery();
                }
            }

            if (afectadas > 0)
                EscribirJsonManual(context, "{\"mensaje\":\"Aviso actualizado\"}", 200);
            else
                EscribirJsonManual(context, "{\"error\":\"Aviso no encontrado\"}", 404);
        }

        private static void EliminarAviso(HttpListenerContext context)
        {
            string idParam = context.Request.QueryString["id"];
            if (string.IsNullOrWhiteSpace(idParam) || !int.TryParse(idParam, out int id) || id <= 0)
            {
                EscribirJsonManual(context, "{\"error\":\"Id inválido\"}", 400);
                return;
            }

            int afectadas;
            using (var conexion = new MySqlConnection(ConexionBD.CadenaConexion))
            {
                conexion.Open();
                string sql = "DELETE FROM avisos WHERE Id = @Id";
                using (var cmd = new MySqlCommand(sql, conexion))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    afectadas = cmd.ExecuteNonQuery();
                }
            }

            if (afectadas > 0)
                EscribirJsonManual(context, "{\"mensaje\":\"Aviso eliminado\"}", 200);
            else
                EscribirJsonManual(context, "{\"error\":\"Aviso no encontrado\"}", 404);
        }

        // Utilidades de serialización/parsing manual
        private static string LeerBody(HttpListenerContext context)
        {
            var enc = context.Request.ContentEncoding ?? Encoding.UTF8;
            using var reader = new StreamReader(context.Request.InputStream, enc);
            return reader.ReadToEnd();
        }

        private static bool EsAdmin(HttpListenerContext context)
        {
            try
            {
                string cookie = context.Request.Headers["Cookie"] ?? string.Empty;
                if (string.IsNullOrEmpty(cookie)) return false;
                // Parse clave=valor; clave2=valor2
                var parts = cookie.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts)
                {
                    var kv = part.Split(new[] { '=' }, 2);
                    if (kv.Length == 2)
                    {
                        string k = kv[0].Trim();
                        string v = kv[1].Trim();
                        if (k.Equals("rol", StringComparison.OrdinalIgnoreCase))
                            return v.Equals("Administrador", StringComparison.OrdinalIgnoreCase);
                    }
                }
            }
            catch { }
            return false;
        }

        private static Dictionary<string, string> ParsearJsonPlano(string json)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(json)) return dict;

            int i = 0;
            int n = json.Length;

            while (i < n)
            {
                // buscar inicio de clave
                while (i < n && json[i] != '"') i++;
                if (i >= n) break;
                i++; // saltar "

                string key = LeerJsonString(json, ref i);

                // saltar espacios y hasta ':'
                while (i < n && char.IsWhiteSpace(json[i])) i++;
                if (i < n && json[i] == ':') i++; else break;
                while (i < n && char.IsWhiteSpace(json[i])) i++;

                // valor debe iniciar con comillas
                if (i >= n || json[i] != '"') break;
                i++; // saltar "
                string val = LeerJsonString(json, ref i);

                if (!string.IsNullOrWhiteSpace(key))
                    dict[key] = val;

                // avanzar hasta coma o fin de objeto
                while (i < n && json[i] != ',')
                {
                    if (json[i] == '}') break;
                    i++;
                }
                if (i < n && json[i] == ',') i++;
            }

            return dict;
        }

        private static string LeerJsonString(string s, ref int i)
        {
            var sb = new StringBuilder();
            int n = s.Length;
            while (i < n)
            {
                char c = s[i++];
                if (c == '"') break;
                if (c == '\\' && i < n)
                {
                    char esc = s[i++];
                    switch (esc)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (i + 3 < n)
                            {
                                string hex = s.Substring(i, 4);
                                if (ushort.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var code))
                                {
                                    sb.Append((char)code);
                                    i += 4;
                                }
                            }
                            break;
                        default:
                            sb.Append(esc);
                            break;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        private static string Obtener(Dictionary<string, string> d, string key)
        {
            if (d == null) return null;
            return d.TryGetValue(key, out var v) ? v : null;
        }

        private static string SerializeAviso(AvisoEntidad a)
        {
            return "{" +
                "\"Id\":" + a.Id + "," +
                "\"Fecha\":\"" + EscapeJson(a.Fecha ?? string.Empty) + "\"," +
                "\"Asunto\":\"" + EscapeJson(a.Asunto ?? string.Empty) + "\"," +
                "\"Mensaje\":\"" + EscapeJson(a.Mensaje ?? string.Empty) + "\"" +
            "}";
        }

        private static int ParseInt(string s, int def)
        {
            return int.TryParse(s, out var v) ? v : def;
        }
        private static int Clamp(int v, int min, int max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }
        private static string MapSort(string sort)
        {
            switch ((sort ?? "").Trim().ToLowerInvariant())
            {
                case "id": return "Id";
                case "asunto": return "Asunto";
                case "fecha": default: return "Fecha";
            }
        }

        private static string EscapeJson(string s)
        {
            if (s == null) return string.Empty;
            var sb = new StringBuilder(s.Length + 10);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 32)
                            sb.AppendFormat("\\u{0:X4}", (int)c);
                        else
                            sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        private static string UnescapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s
                .Replace("\\n", "\n")
                .Replace("\\r", "\r")
                .Replace("\\t", "\t")
                .Replace("\\\"", "\"")
                .Replace("\\\\", "\\");
        }

        private static void EscribirJsonManual(HttpListenerContext context, string jsonPayload, int statusCode)
        {
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json; charset=utf-8";
            byte[] buffer = Encoding.UTF8.GetBytes(jsonPayload ?? "{}");
            context.Response.ContentLength64 = buffer.Length;
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            context.Response.OutputStream.Close();
        }
    }
}
