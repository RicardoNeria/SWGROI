using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using MySql.Data.MySqlClient;
using SWGROI_Server.DB;

namespace SWGROI_Server.Controllers
{
    public static class CotizacionesController
    {
        public static void ManejarSolicitud(HttpListenerContext ctx)
        {
            string ruta = (ctx.Request.Url.LocalPath ?? "").ToLowerInvariant();
            string metodo = ctx.Request.HttpMethod ?? "GET";

            if (ruta == "/cotizaciones/listar" && metodo == "GET")
            {
                var q = ctx.Request.Url.Query ?? "";
                string desde = GetQ(q, "desde");
                string hasta = GetQ(q, "hasta");
                string estado = GetQ(q, "estado");
                string texto = GetQ(q, "q");
                string ovsr3Csv = GetQ(q, "ovsr3");
                string foliosCsv = GetQ(q, "folios");

                var lista = Listar(desde, hasta, estado, texto, ovsr3Csv, foliosCsv);

                ctx.Response.ContentType = "application/json; charset=utf-8";
                using var w = new StreamWriter(ctx.Response.OutputStream, Encoding.UTF8);
                w.Write(JsonSerializer.Serialize(lista));
                return;
            }
            else if (ruta == "/cotizaciones/actualizar" && metodo == "POST")
            {
                string body;
                using (var r = new StreamReader(ctx.Request.InputStream))
                    body = r.ReadToEnd();

                var data = JsonSerializer.Deserialize<Dictionary<string, string>>(body);
                if (data == null) data = new Dictionary<string, string>();

                int id = data.ContainsKey("cotizacionId") ? SafeInt(data["cotizacionId"]) : 0;
                string estado = data.ContainsKey("estado") ? data["estado"] : null;
                string comentarios = data.ContainsKey("comentarios") ? data["comentarios"] : null;

                if (id <= 0)
                {
                    ctx.Response.StatusCode = 400;
                    using var w = new StreamWriter(ctx.Response.OutputStream);
                    w.Write("cotizacionId requerido");
                    return;
                }

                bool ok = Actualizar(id, estado, comentarios);
                ctx.Response.StatusCode = ok ? 200 : 404;
                return;
            }

            ctx.Response.StatusCode = 404;
            ctx.Response.Close();
        }

        private static string GetQ(string query, string key)
        {
            if (string.IsNullOrEmpty(query)) return null;
            foreach (var p in query.TrimStart('?').Split('&'))
            {
                int i = p.IndexOf('=');
                if (i <= 0) continue;
                var k = p.Substring(0, i);
                var v = Uri.UnescapeDataString(p.Substring(i + 1));
                if (k.Equals(key, StringComparison.OrdinalIgnoreCase)) return v;
            }
            return null;
        }

        private static int SafeInt(string s) { int v; return int.TryParse(s, out v) ? v : 0; }

        private static List<Dic> Listar(string desde, string hasta, string estado, string texto, string ovsr3Csv, string foliosCsv)
        {
            // último registro de ventasdetalle por CotizacionID para no duplicar
            var sql = new StringBuilder(@"
                SELECT 
                    c.CotizacionID,
                    t.Folio,
                    vd.OVSR3,
                    ec.Nombre AS Estado,
                    c.Monto,
                    c.FechaEnvio,
                    t.Cuenta,
                    t.RazonSocial,
                    t.Responsable AS Agente,
                    c.Comentarios
                FROM cotizaciones c
                LEFT JOIN tickets t            ON t.Id = c.TicketID
                LEFT JOIN estadoscotizacion ec ON ec.EstadoCotizacionID = c.EstadoCotizacionID
                LEFT JOIN (
                    SELECT v1.*
                    FROM ventasdetalle v1
                    JOIN (
                        SELECT CotizacionID, MAX(Fecha) AS MaxFecha
                        FROM ventasdetalle
                        GROUP BY CotizacionID
                    ) ult ON ult.CotizacionID = v1.CotizacionID AND ult.MaxFecha = v1.Fecha
                ) vd ON vd.CotizacionID = c.CotizacionID
                WHERE 1=1 ");

            var pars = new List<MySqlParameter>();

            DateTime d1, d2;
            if (!string.IsNullOrWhiteSpace(desde) && DateTime.TryParse(desde, out d1))
            {
                sql.Append(" AND c.FechaEnvio >= @d1 ");
                pars.Add(new MySqlParameter("@d1", d1.Date));
            }
            if (!string.IsNullOrWhiteSpace(hasta) && DateTime.TryParse(hasta, out d2))
            {
                sql.Append(" AND c.FechaEnvio <= @d2 ");
                pars.Add(new MySqlParameter("@d2", d2.Date));
            }
            if (!string.IsNullOrWhiteSpace(estado))
            {
                sql.Append(" AND UPPER(ec.Nombre) = UPPER(@est) ");
                pars.Add(new MySqlParameter("@est", estado));
            }
            if (!string.IsNullOrWhiteSpace(texto))
            {
                sql.Append(@" AND (
                        t.Folio LIKE @q OR
                        vd.OVSR3 LIKE @q OR
                        t.Cuenta LIKE @q OR
                        t.RazonSocial LIKE @q
                    ) ");
                pars.Add(new MySqlParameter("@q", "%" + texto + "%"));
            }

            // Filtros que llegan desde el REPORTE
            // ovsr3=OVS1,OVS2,...  folios=TCK1,TCK2,...
            var ovList = ParseCsv(ovsr3Csv);
            var foList = ParseCsv(foliosCsv);

            if (ovList.Count > 0)
            {
                var inParts = new List<string>();
                for (int i = 0; i < ovList.Count; i++)
                {
                    string p = "@ov" + i;
                    inParts.Add(p);
                    pars.Add(new MySqlParameter(p, ovList[i]));
                }
                sql.Append(" AND vd.OVSR3 IN (" + string.Join(",", inParts) + ") ");
            }

            if (foList.Count > 0)
            {
                var inParts = new List<string>();
                for (int i = 0; i < foList.Count; i++)
                {
                    string p = "@fo" + i;
                    inParts.Add(p);
                    pars.Add(new MySqlParameter(p, foList[i]));
                }
                sql.Append(" AND t.Folio IN (" + string.Join(",", inParts) + ") ");
            }

            sql.Append(" ORDER BY c.CotizacionID DESC ");

            var lista = new List<Dic>();
            using var cn = new MySqlConnection(ConexionBD.CadenaConexion);
            cn.Open();
            using var cmd = new MySqlCommand(sql.ToString(), cn);
            if (pars.Count > 0) cmd.Parameters.AddRange(pars.ToArray());

            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                string fechaEnvioStr = null;
                if (rd["FechaEnvio"] != DBNull.Value)
                {
                    try { fechaEnvioStr = Convert.ToDateTime(rd["FechaEnvio"]).ToString("yyyy-MM-dd"); }
                    catch { fechaEnvioStr = rd["FechaEnvio"].ToString(); }
                }

                lista.Add(new Dic
                {
                    ["CotizacionID"] = rd["CotizacionID"],
                    ["Folio"] = rd["Folio"]?.ToString(),
                    ["OVSR3"] = rd["OVSR3"]?.ToString(),
                    ["Estado"] = rd["Estado"]?.ToString(),
                    ["Monto"] = rd["Monto"] == DBNull.Value ? 0m : Convert.ToDecimal(rd["Monto"]),
                    ["FechaEnvio"] = fechaEnvioStr,
                    ["Cuenta"] = rd["Cuenta"]?.ToString(),
                    ["RazonSocial"] = rd["RazonSocial"]?.ToString(),
                    ["Agente"] = rd["Agente"]?.ToString(),
                    ["Comentarios"] = rd["Comentarios"]?.ToString()
                });
            }
            return lista;
        }

        private static List<string> ParseCsv(string csv)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(csv)) return list;
            var parts = csv.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                var s = parts[i].Trim();
                if (!string.IsNullOrEmpty(s)) list.Add(s);
            }
            return list;
        }

        private static bool Actualizar(int cotizacionId, string estadoNombre, string comentarios)
        {
            var set = new List<string>();
            var pars = new List<MySqlParameter>();

            if (!string.IsNullOrWhiteSpace(estadoNombre))
            {
                set.Add("EstadoCotizacionID = (SELECT EstadoCotizacionID FROM estadoscotizacion WHERE UPPER(Nombre)=UPPER(@e) LIMIT 1)");
                pars.Add(new MySqlParameter("@e", estadoNombre));
            }

            set.Add("Comentarios = @c");
            pars.Add(new MySqlParameter("@c", (object)comentarios ?? DBNull.Value));

            var sql = $"UPDATE cotizaciones SET {string.Join(", ", set)} WHERE CotizacionID = @id";
            pars.Add(new MySqlParameter("@id", cotizacionId));

            using var cn = new MySqlConnection(ConexionBD.CadenaConexion);
            cn.Open();
            using var cmd = new MySqlCommand(sql, cn);
            cmd.Parameters.AddRange(pars.ToArray());
            return cmd.ExecuteNonQuery() > 0;
        }

        private class Dic : Dictionary<string, object> { }
    }
}
