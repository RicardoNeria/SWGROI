using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Globalization;
using System.Linq;
using MySql.Data.MySqlClient;
using SWGROI_Server.DB;
using SWGROI_Server.Models;

namespace SWGROI_Server.Controllers
{
    public static class VentasController
    {
        private static void Json(HttpListenerResponse res, object obj, int status = 200)
        {
            res.StatusCode = status;
            res.ContentType = "application/json; charset=utf-8";
            var json = JsonSerializer.Serialize(obj);
            using var w = new StreamWriter(res.OutputStream, Encoding.UTF8);
            w.Write(json);
        }
        private static void Error(HttpListenerResponse res, int status, string code, string message)
        {
            Json(res, new { ok = false, code, message }, status);
        }
        private static string DictGet(Dictionary<string, string> d, string key)
        {
            if (d == null) return null;
            return d.TryGetValue(key, out var v) ? v : null;
        }

        public static void ManejarSolicitud(HttpListenerContext context)
        {
            string ruta = (context.Request.Url.LocalPath ?? "").ToLowerInvariant();
            string metodo = (context.Request.HttpMethod ?? "GET").ToUpperInvariant();

            try
            {
                // === CONSULTAR TICKET (datos base) ===
                if (ruta.StartsWith("/ventas/consultar-ticket") && metodo == "GET")
                {
                    string folio = GetQuery(context.Request.Url.Query, "folio");
                    if (string.IsNullOrWhiteSpace(folio)) { Error(context.Response, 400, "bad_request", "folio requerido"); return; }
                    var ticket = ObtenerDatosTicketPorFolio(folio);
                    if (ticket == null) { Error(context.Response, 404, "not_found", "folio no encontrado en tickets"); return; }
                    Json(context.Response, ticket);
                    return;
                }

                // === POR-TICKET (último registro de venta por folio u OVSR3) ===
                if (ruta == "/ventas/por-ticket" && metodo == "GET")
                {
                    string folio = GetQuery(context.Request.Url.Query, "folio");
                    string ov = GetQuery(context.Request.Url.Query, "ovsr3");

                    using var cn = new MySqlConnection(ConexionBD.CadenaConexion);
                    cn.Open();

                    if (!string.IsNullOrWhiteSpace(ov))
                    {
                        const string byOv = @"
SELECT v.OVSR3, t.Folio, v.Monto, ec.Nombre AS Estado,
       v.Cuenta, v.RazonSocial, v.Domicilio, v.FechaAtencion,
       v.AgenteResponsable, v.Descripcion, v.ComentariosCotizacion
FROM ventasdetalle v
JOIN cotizaciones c ON v.CotizacionID=c.CotizacionID
JOIN tickets t       ON c.TicketID=t.Id
LEFT JOIN estadoscotizacion ec ON c.EstadoCotizacionID=ec.EstadoCotizacionID
WHERE v.OVSR3=@ov
ORDER BY v.CotizacionID DESC, v.Fecha DESC
LIMIT 1";
                        using var cmd = new MySqlCommand(byOv, cn);
                        cmd.Parameters.AddWithValue("@ov", ov);
                        using var rd = cmd.ExecuteReader();
                        if (!rd.Read()) { Json(context.Response, new { }); return; }
                        Json(context.Response, new
                        {
                            ovsr3 = rd["OVSR3"]?.ToString() ?? "",
                            folioTicket = rd["Folio"]?.ToString() ?? "",
                            monto = rd["Monto"] == DBNull.Value ? 0m : Convert.ToDecimal(rd["Monto"]),
                            estado = rd["Estado"]?.ToString() ?? "",
                            cuenta = rd["Cuenta"]?.ToString() ?? "",
                            razonSocial = rd["RazonSocial"]?.ToString() ?? "",
                            domicilio = rd["Domicilio"]?.ToString() ?? "",
                            fechaAtencion = rd["FechaAtencion"] == DBNull.Value ? "" : Convert.ToDateTime(rd["FechaAtencion"]).ToString("yyyy-MM-dd"),
                            agenteResponsable = rd["AgenteResponsable"] == DBNull.Value ? "" : rd["AgenteResponsable"].ToString(),
                            descripcion = rd["Descripcion"] == DBNull.Value ? "" : rd["Descripcion"].ToString(),
                            comentariosCotizacion = rd["ComentariosCotizacion"] == DBNull.Value ? "" : rd["ComentariosCotizacion"].ToString()
                        });
                        return;
                    }

                    if (string.IsNullOrWhiteSpace(folio)) { Error(context.Response, 400, "bad_request", "folio u ovsr3 requerido"); return; }

                    const string sql = @"
SELECT v.OVSR3, v.Monto, ec.Nombre AS Estado,
       v.Cuenta, v.RazonSocial, v.Domicilio, v.FechaAtencion,
       v.AgenteResponsable, v.Descripcion, v.ComentariosCotizacion
FROM ventasdetalle v
JOIN cotizaciones c ON v.CotizacionID=c.CotizacionID
JOIN tickets t       ON c.TicketID=t.Id
LEFT JOIN estadoscotizacion ec ON c.EstadoCotizacionID=ec.EstadoCotizacionID
WHERE t.Folio=@f
ORDER BY v.CotizacionID DESC, v.Fecha DESC
LIMIT 1";

                    using var cmd2 = new MySqlCommand(sql, cn);
                    cmd2.Parameters.AddWithValue("@f", folio);
                    using var rd2 = cmd2.ExecuteReader();
                    if (!rd2.Read()) { Json(context.Response, new { }); return; }
                    Json(context.Response, new
                    {
                        ovsr3 = rd2["OVSR3"]?.ToString() ?? "",
                        folioTicket = folio,
                        monto = rd2["Monto"] == DBNull.Value ? 0m : Convert.ToDecimal(rd2["Monto"]),
                        estado = rd2["Estado"]?.ToString() ?? "",
                        cuenta = rd2["Cuenta"]?.ToString() ?? "",
                        razonSocial = rd2["RazonSocial"]?.ToString() ?? "",
                        domicilio = rd2["Domicilio"]?.ToString() ?? "",
                        fechaAtencion = rd2["FechaAtencion"] == DBNull.Value ? "" : Convert.ToDateTime(rd2["FechaAtencion"]).ToString("yyyy-MM-dd"),
                        agenteResponsable = rd2["AgenteResponsable"] == DBNull.Value ? "" : rd2["AgenteResponsable"].ToString(),
                        descripcion = rd2["Descripcion"] == DBNull.Value ? "" : rd2["Descripcion"].ToString(),
                        comentariosCotizacion = rd2["ComentariosCotizacion"] == DBNull.Value ? "" : rd2["ComentariosCotizacion"].ToString()
                    });
                    return;
                }

                // === GUARDAR (alta) con manejo de duplicado OVSR3 ===
                if (ruta == "/ventas/guardar" && metodo == "POST")
                {
                    string body;
                    using (var r = new StreamReader(context.Request.InputStream)) body = r.ReadToEnd();
                    var d = JsonSerializer.Deserialize<Dictionary<string, string>>(body) ?? new Dictionary<string, string>();

                    string folio = (DictGet(d, "folioTicket") ?? "").Trim();
                    string ovsr3 = (DictGet(d, "ovsr3") ?? "").Trim();
                    string estado = (DictGet(d, "estado") ?? "").Trim();
                    string sMonto = DictGet(d, "monto") ?? "0";
                    string sFechaAt = DictGet(d, "fechaAtencion") ?? "";
                    string cuenta = (DictGet(d, "cuenta") ?? "").Trim();
                    string razon = (DictGet(d, "razonSocial") ?? "").Trim();
                    string domicilio = (DictGet(d, "domicilio") ?? "").Trim();
                    string comentarios = DictGet(d, "comentariosCotizacion");
                    string statusPago = (DictGet(d, "statusPago") ?? "Pendiente").Trim();

                    if (string.IsNullOrWhiteSpace(folio) || string.IsNullOrWhiteSpace(ovsr3)) { Error(context.Response, 400, "bad_request", "Folio y OVSR3 son requeridos."); return; }
                    if (!ExisteFolioEnTickets(folio)) { Error(context.Response, 400, "invalid_folio", "El folio no existe en tickets."); return; }
                    if (!ValidOVSR3(ovsr3)) { Error(context.Response, 400, "invalid_ovsr3", "OVSR3 inválido."); return; }
                    if (string.IsNullOrWhiteSpace(estado)) { Error(context.Response, 400, "bad_request", "Estado requerido."); return; }
                    if (!decimal.TryParse(sMonto, NumberStyles.Any, CultureInfo.InvariantCulture, out var monto) || monto <= 0m) { Error(context.Response, 400, "invalid_amount", "Monto debe ser numérico y mayor a 0."); return; }
                    if (!DateTime.TryParse(sFechaAt, out var fechaAt)) fechaAt = DateTime.Now.Date;

                    var totalConIva = monto * 1.16m;
                    var comisionSobreIva = totalConIva * 1.03m;

                    using var cn = new MySqlConnection(ConexionBD.CadenaConexion);
                    cn.Open();
                    using var tx = cn.BeginTransaction();
                    try
                    {
                        int cotizacionID = InsertarCotizacion(cn, tx, folio, estado, monto);
                        InsertarOrdenVenta(cn, tx, cotizacionID, ovsr3, comisionSobreIva);
                        InsertarDetalleVenta(cn, tx, cotizacionID, ovsr3, folio, monto, estado,
                            cuenta, razon, domicilio, fechaAt, comentarios, statusPago);
                        tx.Commit();
                        Json(context.Response, new { ok = true });
                        return;
                    }
                    catch (MySqlException ex) when (ex.Number == 1062)
                    {
                        try { tx.Rollback(); } catch { }
                        Error(context.Response, 409, "duplicate", "No se puede registrar el ticket porque el OVSR3 ya existe.");
                        return;
                    }
                    catch (Exception ex)
                    {
                        try { tx.Rollback(); } catch { }
                        Error(context.Response, 500, "tx_error", "No se pudo registrar la venta: " + ex.Message);
                        return;
                    }
                }

                // === LISTAR (filtros) ===
                // === LISTAR (filtros: incluye ovsr3) ===
                // === LISTAR (filtros: incluye ovsr3) ===
                if (ruta == "/ventas/listar" && metodo == "GET")
                {
                    string folio = GetQuery(context.Request.Url.Query, "folio");
                    string estado = GetQuery(context.Request.Url.Query, "estado");
                    string ovsr3 = GetQuery(context.Request.Url.Query, "ovsr3");
                    string sMin = GetQuery(context.Request.Url.Query, "min");
                    string sMax = GetQuery(context.Request.Url.Query, "max");
                    string sPage = GetQuery(context.Request.Url.Query, "page");
                    string sSize = GetQuery(context.Request.Url.Query, "pageSize");

                    decimal? min = decimal.TryParse(sMin, out var tmin) ? (decimal?)tmin : (decimal?)null;
                    decimal? max = decimal.TryParse(sMax, out var tmax) ? (decimal?)tmax : (decimal?)null;
                    int page = int.TryParse(sPage, out var p) && p > 0 ? p : 1;
                    int size = int.TryParse(sSize, out var s) && s > 0 && s <= 200 ? s : 100;

                    int total;
                    var result = ObtenerVentas(folio, estado, ovsr3, min, max, page, size, out total);

                    // Totales globales del conjunto filtrado
                    decimal sumMonto = 0m, sumIva = 0m, sumIvaCom = 0m;
                    using (var cn = new MySqlConnection(ConexionBD.CadenaConexion))
                    {
                        cn.Open();
                        var where = new List<string>();
                        if (!string.IsNullOrWhiteSpace(folio)) where.Add("t.Folio LIKE @folio");
                        if (!string.IsNullOrWhiteSpace(estado)) where.Add("LOWER(ec.Nombre) = LOWER(@estado)");
                        if (!string.IsNullOrWhiteSpace(ovsr3)) where.Add("v.OVSR3 LIKE @ovsr3");
                        if (min.HasValue) where.Add("(v.Monto * 1.16 * 1.03) >= @min");
                        if (max.HasValue) where.Add("(v.Monto * 1.16 * 1.03) <= @max");
                        string whereSql = where.Count > 0 ? ("WHERE " + string.Join(" AND ", where)) : "";

                        string baseSelect = @"
FROM ventasdetalle v
JOIN cotizaciones c       ON v.CotizacionID = c.CotizacionID
LEFT JOIN tickets t       ON c.TicketID     = t.Id
LEFT JOIN estadoscotizacion ec ON c.EstadoCotizacionID = ec.EstadoCotizacionID";

                        string sqlSum = $@"
SELECT 
  COALESCE(SUM(v.Monto),0)                 AS SumMonto,
  COALESCE(SUM(v.Monto*1.16),0)            AS SumIva,
  COALESCE(SUM(v.Monto*1.16*1.03),0)       AS SumIvaCom
{baseSelect}
{whereSql}";
                        using var cmd = new MySqlCommand(sqlSum, cn);
                        if (!string.IsNullOrWhiteSpace(folio)) cmd.Parameters.AddWithValue("@folio", $"%{folio}%");
                        if (!string.IsNullOrWhiteSpace(estado)) cmd.Parameters.AddWithValue("@estado", estado);
                        if (!string.IsNullOrWhiteSpace(ovsr3)) cmd.Parameters.AddWithValue("@ovsr3", $"%{ovsr3}%");
                        if (min.HasValue) cmd.Parameters.AddWithValue("@min", min.Value);
                        if (max.HasValue) cmd.Parameters.AddWithValue("@max", max.Value);

                        using var rd = cmd.ExecuteReader();
                        if (rd.Read())
                        {
                            sumMonto = rd["SumMonto"] == DBNull.Value ? 0m : Convert.ToDecimal(rd["SumMonto"]);
                            sumIva = rd["SumIva"] == DBNull.Value ? 0m : Convert.ToDecimal(rd["SumIva"]);
                            sumIvaCom = rd["SumIvaCom"] == DBNull.Value ? 0m : Convert.ToDecimal(rd["SumIvaCom"]);
                        }
                    }

                    Json(context.Response, new { items = result, total = total, page = page, pageSize = size, sumMonto, sumIva, sumIvaCom });
                    return;
                }


                // === CANCELAR ===
                if (ruta == "/ventas/cancelar" && metodo == "POST")
                {
                    var d = LeerJson(context);
                    var ov = DictGet(d, "ovsr3");
                    var motivo = DictGet(d, "motivo") ?? "";
                    var usuario = DictGet(d, "usuario") ?? "";
                    if (string.IsNullOrWhiteSpace(ov)) { Error(context.Response, 400, "bad_request", "OVSR3 requerido"); return; }
                    if (string.IsNullOrWhiteSpace(motivo)) { Error(context.Response, 400, "bad_request", "Motivo requerido."); return; }

                    bool ok = CancelarVenta(ov, motivo, usuario);
                    if (!ok) { Error(context.Response, 404, "not_found", "OVSR3 no encontrado."); return; }
                    Json(context.Response, new { ok = true, status = "Cancelado" });
                    return;
                }

                // === ACTIVAR (valida colisión por folio) ===
                if (ruta == "/ventas/activar" && metodo == "POST")
                {
                    var d = LeerJson(context);
                    var ov = DictGet(d, "ovsr3");
                    if (string.IsNullOrWhiteSpace(ov)) { Error(context.Response, 400, "bad_request", "OVSR3 requerido"); return; }

                    int? ticketId = ObtenerTicketIdPorOVSR3(ov);
                    if (ticketId == null) { Error(context.Response, 404, "not_found", "OVSR3 no encontrado."); return; }
                    if (ExisteVentaActivaMismoTicketExcepto(ticketId.Value, ov))
                    {
                        Error(context.Response, 409, "conflict", "Elimina o cancela las otras ventas del mismo folio antes de activar.");
                        return;
                    }
                    bool ok = ReactivarVenta(ov);
                    if (!ok) { Error(context.Response, 404, "not_found", "No encontrado o ya activo."); return; }
                    Json(context.Response, new { ok = true, status = "Reactivado" });
                    return;
                }

                // === ACTUALIZAR STATUSPAGO ===
                if (ruta == "/ventas/actualizar-statuspago" && metodo == "POST")
                {
                    var d = LeerJson(context);
                    var ov = DictGet(d, "ovsr3");
                    var sp = (DictGet(d, "statusPago") ?? "").Trim();
                    if (string.IsNullOrWhiteSpace(ov)) { Error(context.Response, 400, "bad_request", "OVSR3 requerido"); return; }
                    if (string.IsNullOrWhiteSpace(sp)) { Error(context.Response, 400, "bad_request", "StatusPago requerido"); return; }
                    var permitidos = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Pendiente", "Promesa de pago", "Pagado" };
                    if (!permitidos.Contains(sp)) { Error(context.Response, 400, "invalid_status", "Valor no permitido."); return; }

                    const string sql = @"UPDATE ventasdetalle SET StatusPago=@s WHERE OVSR3=@ov AND (StatusPago IS NULL OR UPPER(StatusPago) <> 'CANCELADO')";
                    using var cn = new MySqlConnection(ConexionBD.CadenaConexion);
                    cn.Open();
                    using var cmd = new MySqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@s", sp);
                    cmd.Parameters.AddWithValue("@ov", ov);
                    int n = cmd.ExecuteNonQuery();
                    if (n <= 0) { Error(context.Response, 404, "not_found", "OVSR3 no encontrado o cancelado."); return; }
                    Json(context.Response, new { ok = true, statusPago = sp });
                    return;
                }

                // === REPORTE (HTML o texto) ===
                if (ruta == "/ventas/reporte" && metodo == "GET")
                {
                    string accept = context.Request.Headers["Accept"] ?? "";
                    string query = context.Request.Url.Query ?? "";
                    bool quiereHtml = (accept.IndexOf("text/html", StringComparison.OrdinalIgnoreCase) >= 0)
                                      || (query.ToLowerInvariant().IndexOf("format=html", StringComparison.Ordinal) >= 0);

                    int _;
                    var ventas = ObtenerVentas(null, null, null, null, null, 1, 10000, out _);

                    if (quiereHtml)
                    {
                        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "ventas_reporte.html");
                        if (File.Exists(path))
                        {
                            context.Response.ContentType = "text/html; charset=utf-8";
                            using var fs = File.OpenRead(path);
                            fs.CopyTo(context.Response.OutputStream);
                            context.Response.OutputStream.Close();
                            return;
                        }
                        string html = ObtenerReporteHtml(ventas);
                        context.Response.ContentEncoding = Encoding.UTF8;
                        context.Response.ContentType = "text/html; charset=utf-8";
                        using var output = new StreamWriter(context.Response.OutputStream, Encoding.UTF8);
                        output.Write(html);
                        return;
                    }
                    else
                    {
                        string texto = ObtenerReporteTextoPlano(ventas);
                        context.Response.ContentEncoding = Encoding.UTF8;
                        context.Response.ContentType = "text/plain; charset=utf-8";
                        using var output = new StreamWriter(context.Response.OutputStream, Encoding.UTF8);
                        output.Write(texto);
                        return;
                    }
                }

                // === CSV ===
                if (ruta == "/ventas/exportar-excel" && metodo == "GET")
                {
                    // filtros
                    string folio = GetQuery(context.Request.Url.Query, "folio");
                    string estado = GetQuery(context.Request.Url.Query, "estado");
                    string ovsr3 = GetQuery(context.Request.Url.Query, "ovsr3");
                    string sMin = GetQuery(context.Request.Url.Query, "min");
                    string sMax = GetQuery(context.Request.Url.Query, "max");
                    string sFull = GetQuery(context.Request.Url.Query, "full");
                    string sFmt = GetQuery(context.Request.Url.Query, "format"); // "xls" para Excel con formato

                    bool full = (sFull ?? "").Equals("1", StringComparison.OrdinalIgnoreCase)
                             || (sFull ?? "").Equals("true", StringComparison.OrdinalIgnoreCase);
                    bool asXls = (sFmt ?? "").Equals("xls", StringComparison.OrdinalIgnoreCase)
                              || (sFmt ?? "").Equals("excel", StringComparison.OrdinalIgnoreCase);

                    decimal? min = decimal.TryParse(sMin, out var tmin) ? (decimal?)tmin : null;
                    decimal? max = decimal.TryParse(sMax, out var tmax) ? (decimal?)tmax : null;

                    using var cn = new MySqlConnection(ConexionBD.CadenaConexion);
                    cn.Open();

                    var where = new List<string>();
                    if (!string.IsNullOrWhiteSpace(folio)) where.Add("t.Folio LIKE @folio");
                    if (!string.IsNullOrWhiteSpace(estado)) where.Add("LOWER(ec.Nombre) = LOWER(@estado)");
                    if (!string.IsNullOrWhiteSpace(ovsr3)) where.Add("v.OVSR3 LIKE @ovsr3");
                    if (min.HasValue) where.Add("(v.Monto * 1.16 * 1.03) >= @min");
                    if (max.HasValue) where.Add("(v.Monto * 1.16 * 1.03) <= @max");
                    string whereSql = where.Count > 0 ? ("WHERE " + string.Join(" AND ", where)) : "";

                    string baseSelect = @"
FROM ventasdetalle v
JOIN cotizaciones c       ON v.CotizacionID = c.CotizacionID
LEFT JOIN tickets t       ON c.TicketID     = t.Id
LEFT JOIN estadoscotizacion ec ON c.EstadoCotizacionID = ec.EstadoCotizacionID";

                    string sql = $@"
SELECT 
  t.Folio,
  v.OVSR3,
  ec.Nombre AS Estado,
  v.Monto,
  v.Cuenta,
  v.RazonSocial,
  v.Domicilio,
  v.FechaAtencion,
  v.AgenteResponsable,
  v.Descripcion,
  v.ComentariosCotizacion,
  IFNULL(v.StatusPago,'Pendiente') AS StatusPago,
  v.FechaCancelacion,
  v.MotivoCancelacion,
  v.UsuarioCancelacion
{baseSelect}
{whereSql}
ORDER BY v.Fecha DESC";

                    using var cmd = new MySqlCommand(sql, cn);
                    if (!string.IsNullOrWhiteSpace(folio)) cmd.Parameters.AddWithValue("@folio", $"%{folio}%");
                    if (!string.IsNullOrWhiteSpace(estado)) cmd.Parameters.AddWithValue("@estado", estado);
                    if (!string.IsNullOrWhiteSpace(ovsr3)) cmd.Parameters.AddWithValue("@ovsr3", $"%{ovsr3}%");
                    if (min.HasValue) cmd.Parameters.AddWithValue("@min", min.Value);
                    if (max.HasValue) cmd.Parameters.AddWithValue("@max", max.Value);

                    // Helper
                    static string Csv(object o)
                    {
                        var s = o == DBNull.Value ? "" : o?.ToString() ?? "";
                        s = s.Replace("\"", "\"\"");
                        return (s.Contains(",") || s.Contains("\"") || s.Contains("\n")) ? $"\"{s}\"" : s;
                    }

                    // Decide salida
                    if (!asXls)
                    {
                        // ==== CSV ====
                        var sb = new StringBuilder();

                        // Compacto ahora incluye columnas de cancelación
                        if (!full)
                            sb.AppendLine("Folio,OVSR3,Estado,Monto,IVA,IVA+Comision,Cuenta,RazonSocial,Agente,StatusPago,FechaCancelacion,MotivoCancelacion,UsuarioCancelacion");
                        else
                            sb.AppendLine("Folio,OVSR3,Estado,Monto,TotalIVA,TotalIVA+Comision,Total+Comision,Cuenta,RazonSocial,Domicilio,FechaAtencion,Agente,Descripcion,Comentarios,StatusPago,FechaCancelacion,MotivoCancelacion,UsuarioCancelacion");

                        using var rd = cmd.ExecuteReader();
                        while (rd.Read())
                        {
                            var monto = rd["Monto"] == DBNull.Value ? 0m : Convert.ToDecimal(rd["Monto"]);
                            var iva = monto * 1.16m;
                            var ivac = iva * 1.03m;
                            var com3 = monto * 1.03m;

                            var fcan = rd["FechaCancelacion"] == DBNull.Value ? "" : Convert.ToDateTime(rd["FechaCancelacion"]).ToString("yyyy-MM-dd");
                            var mcan = rd["MotivoCancelacion"] == DBNull.Value ? "" : rd["MotivoCancelacion"].ToString();
                            var ucan = rd["UsuarioCancelacion"] == DBNull.Value ? "" : rd["UsuarioCancelacion"].ToString();

                            if (!full)
                            {
                                sb.Append(Csv(rd["Folio"])).Append(',')
                                  .Append(Csv(rd["OVSR3"])).Append(',')
                                  .Append(Csv(rd["Estado"])).Append(',')
                                  .Append(monto.ToString("0.##", CultureInfo.InvariantCulture)).Append(',')
                                  .Append(iva.ToString("0.##", CultureInfo.InvariantCulture)).Append(',')
                                  .Append(ivac.ToString("0.##", CultureInfo.InvariantCulture)).Append(',')
                                  .Append(Csv(rd["Cuenta"])).Append(',')
                                  .Append(Csv(rd["RazonSocial"])).Append(',')
                                  .Append(Csv(rd["AgenteResponsable"])).Append(',')
                                  .Append(Csv(rd["StatusPago"])).Append(',')
                                  .Append(Csv(fcan)).Append(',')
                                  .Append(Csv(mcan)).Append(',')
                                  .Append(Csv(ucan)).AppendLine();
                            }
                            else
                            {
                                sb.Append(Csv(rd["Folio"])).Append(',')
                                  .Append(Csv(rd["OVSR3"])).Append(',')
                                  .Append(Csv(rd["Estado"])).Append(',')
                                  .Append(monto.ToString("0.##", CultureInfo.InvariantCulture)).Append(',')
                                  .Append(iva.ToString("0.##", CultureInfo.InvariantCulture)).Append(',')
                                  .Append(ivac.ToString("0.##", CultureInfo.InvariantCulture)).Append(',')
                                  .Append(com3.ToString("0.##", CultureInfo.InvariantCulture)).Append(',')
                                  .Append(Csv(rd["Cuenta"])).Append(',')
                                  .Append(Csv(rd["RazonSocial"])).Append(',')
                                  .Append(Csv(rd["Domicilio"])).Append(',')
                                  .Append(rd["FechaAtencion"] == DBNull.Value ? "" : Convert.ToDateTime(rd["FechaAtencion"]).ToString("yyyy-MM-dd")).Append(',')
                                  .Append(Csv(rd["AgenteResponsable"])).Append(',')
                                  .Append(Csv(rd["Descripcion"])).Append(',')
                                  .Append(Csv(rd["ComentariosCotizacion"])).Append(',')
                                  .Append(Csv(rd["StatusPago"])).Append(',')
                                  .Append(Csv(fcan)).Append(',')
                                  .Append(Csv(mcan)).Append(',')
                                  .Append(Csv(ucan)).AppendLine();
                            }
                        }

                        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
                        context.Response.ContentType = "text/csv; charset=utf-8";
                        context.Response.AddHeader("Content-Disposition", "attachment; filename=reporte_ventas.csv");
                        context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                        context.Response.OutputStream.Close();
                        return;
                    }
                    else
                    {
                        // ==== Excel (HTML) con formato ====
                        var sb = new StringBuilder();
                        sb.Append("<html><head><meta charset='utf-8'>")
                          .Append("<style>table{border-collapse:collapse} td,th{border:1px solid #ccc;padding:4px;} ")
                          .Append(".cur{mso-number-format:'\\0024 #,##0.00'} .dat{mso-number-format:'yyyy-mm-dd'}</style>")
                          .Append("</head><body><table><thead><tr>");

                        string[] headers = !full
                            ? new[] { "Folio", "OVSR3", "Estado", "Monto", "IVA", "IVA+Comision", "Cuenta", "RazonSocial", "Agente", "StatusPago", "FechaCancelacion", "MotivoCancelacion", "UsuarioCancelacion" }
                            : new[] { "Folio", "OVSR3", "Estado", "Monto", "TotalIVA", "TotalIVA+Comision", "Total+Comision", "Cuenta", "RazonSocial", "Domicilio", "FechaAtencion", "Agente", "Descripcion", "Comentarios", "StatusPago", "FechaCancelacion", "MotivoCancelacion", "UsuarioCancelacion" };

                        foreach (var h in headers) sb.Append("<th>").Append(WebUtility.HtmlEncode(h)).Append("</th>");
                        sb.Append("</tr></thead><tbody>");

                        using var rd = cmd.ExecuteReader();
                        while (rd.Read())
                        {
                            var monto = rd["Monto"] == DBNull.Value ? 0m : Convert.ToDecimal(rd["Monto"]);
                            var iva = monto * 1.16m;
                            var ivac = iva * 1.03m;
                            var com3 = monto * 1.03m;

                            var fcan = rd["FechaCancelacion"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(rd["FechaCancelacion"]);
                            var mcan = rd["MotivoCancelacion"] == DBNull.Value ? "" : rd["MotivoCancelacion"].ToString();
                            var ucan = rd["UsuarioCancelacion"] == DBNull.Value ? "" : rd["UsuarioCancelacion"].ToString();

                            sb.Append("<tr>");
                            if (!full)
                            {
                                sb.Append("<td>").Append(WebUtility.HtmlEncode(rd["Folio"].ToString())).Append("</td>")
                                  .Append("<td>").Append(WebUtility.HtmlEncode(rd["OVSR3"].ToString())).Append("</td>")
                                  .Append("<td>").Append(WebUtility.HtmlEncode(rd["Estado"].ToString())).Append("</td>")
                                  .Append("<td class='cur'>").Append(monto.ToString("0.00", CultureInfo.InvariantCulture)).Append("</td>")
                                  .Append("<td class='cur'>").Append(iva.ToString("0.00", CultureInfo.InvariantCulture)).Append("</td>")
                                  .Append("<td class='cur'>").Append(ivac.ToString("0.00", CultureInfo.InvariantCulture)).Append("</td>")
                                  .Append("<td>").Append(WebUtility.HtmlEncode(rd["Cuenta"].ToString())).Append("</td>")
                                  .Append("<td>").Append(WebUtility.HtmlEncode(rd["RazonSocial"].ToString())).Append("</td>")
                                  .Append("<td>").Append(WebUtility.HtmlEncode(rd["AgenteResponsable"].ToString())).Append("</td>")
                                  .Append("<td>").Append(WebUtility.HtmlEncode(rd["StatusPago"].ToString())).Append("</td>")
                                  .Append(fcan.HasValue ? $"<td class='dat'>{fcan:yyyy-MM-dd}</td>" : "<td></td>")
                                  .Append("<td>").Append(WebUtility.HtmlEncode(mcan)).Append("</td>")
                                  .Append("<td>").Append(WebUtility.HtmlEncode(ucan)).Append("</td>");
                            }
                            else
                            {
                                sb.Append("<td>").Append(WebUtility.HtmlEncode(rd["Folio"].ToString())).Append("</td>")
                                  .Append("<td>").Append(WebUtility.HtmlEncode(rd["OVSR3"].ToString())).Append("</td>")
                                  .Append("<td>").Append(WebUtility.HtmlEncode(rd["Estado"].ToString())).Append("</td>")
                                  .Append("<td class='cur'>").Append(monto.ToString("0.00", CultureInfo.InvariantCulture)).Append("</td>")
                                  .Append("<td class='cur'>").Append(iva.ToString("0.00", CultureInfo.InvariantCulture)).Append("</td>")
                                  .Append("<td class='cur'>").Append(ivac.ToString("0.00", CultureInfo.InvariantCulture)).Append("</td>")
                                  .Append("<td class='cur'>").Append(com3.ToString("0.00", CultureInfo.InvariantCulture)).Append("</td>")
                                  .Append("<td>").Append(WebUtility.HtmlEncode(rd["Cuenta"].ToString())).Append("</td>")
                                  .Append("<td>").Append(WebUtility.HtmlEncode(rd["RazonSocial"].ToString())).Append("</td>")
                                  .Append("<td>").Append(WebUtility.HtmlEncode(rd["Domicilio"].ToString())).Append("</td>")
                                  .Append(rd["FechaAtencion"] == DBNull.Value ? "<td></td>" : $"<td class='dat'>{Convert.ToDateTime(rd["FechaAtencion"]):yyyy-MM-dd}</td>")
                                  .Append("<td>").Append(WebUtility.HtmlEncode(rd["AgenteResponsable"].ToString())).Append("</td>")
                                  .Append("<td>").Append(WebUtility.HtmlEncode(rd["Descripcion"].ToString())).Append("</td>")
                                  .Append("<td>").Append(WebUtility.HtmlEncode(rd["ComentariosCotizacion"].ToString())).Append("</td>")
                                  .Append("<td>").Append(WebUtility.HtmlEncode(rd["StatusPago"].ToString())).Append("</td>")
                                  .Append(fcan.HasValue ? $"<td class='dat'>{fcan:yyyy-MM-dd}</td>" : "<td></td>")
                                  .Append("<td>").Append(WebUtility.HtmlEncode(mcan)).Append("</td>")
                                  .Append("<td>").Append(WebUtility.HtmlEncode(ucan)).Append("</td>");
                            }
                            sb.Append("</tr>");
                        }
                        sb.Append("</tbody></table></body></html>");

                        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
                        context.Response.ContentType = "application/vnd.ms-excel; charset=utf-8";
                        context.Response.AddHeader("Content-Disposition", "attachment; filename=reporte_ventas.xls");
                        context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                        context.Response.OutputStream.Close();
                        return;
                    }
                }


                context.Response.StatusCode = 404;
                context.Response.Close();
            }
            catch (Exception ex)
            {
                Error(context.Response, 500, "server_error", ex.Message);
            }
        }

        private static Dictionary<string, string> LeerJson(HttpListenerContext ctx)
        {
            string body;
            using var r = new StreamReader(ctx.Request.InputStream);
            body = r.ReadToEnd();
            return string.IsNullOrWhiteSpace(body) ? new Dictionary<string, string>() :
                JsonSerializer.Deserialize<Dictionary<string, string>>(body) ?? new Dictionary<string, string>();
        }

        private static string GetQuery(string query, string key)
        {
            if (string.IsNullOrEmpty(query)) return null;
            foreach (var kv in query.TrimStart('?').Split('&'))
            {
                int ix = kv.IndexOf('=');
                if (ix > 0)
                {
                    var k = kv.Substring(0, ix);
                    if (k.Equals(key, StringComparison.OrdinalIgnoreCase))
                        return Uri.UnescapeDataString(kv.Substring(ix + 1));
                }
            }
            return null;
        }
        private static bool ValidOVSR3(string v)
        {
            if (string.IsNullOrWhiteSpace(v)) return false;
            if (v.Length < 3 || v.Length > 32) return false;
            foreach (char c in v)
                if (!(char.IsLetterOrDigit(c) || c == '-' || c == '_')) return false;
            return true;
        }

        // ==== DATA ====
        private static bool ExisteFolioEnTickets(string folio)
        {
            using var conn = new MySqlConnection(ConexionBD.CadenaConexion);
            conn.Open();
            using var cmd = new MySqlCommand("SELECT COUNT(*) FROM tickets WHERE Folio = @f", conn);
            cmd.Parameters.AddWithValue("@f", folio);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        private static Dictionary<string, string> ObtenerDatosTicketPorFolio(string folio)
        {
            const string query = @"
SELECT Cuenta, RazonSocial, Domicilio, FechaAtencion, 
       Responsable AS AgenteResponsable, Descripcion
FROM tickets WHERE Folio = @folio";
            using var conn = new MySqlConnection(ConexionBD.CadenaConexion);
            conn.Open();
            using var cmd = new MySqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@folio", folio);
            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;

            string fecha = reader["FechaAtencion"] == DBNull.Value ? "" : Convert.ToDateTime(reader["FechaAtencion"]).ToString("yyyy-MM-dd");

            var dic = new Dictionary<string, string>();
            dic["Cuenta"] = reader["Cuenta"] == DBNull.Value ? "" : reader["Cuenta"].ToString();
            dic["RazonSocial"] = reader["RazonSocial"] == DBNull.Value ? "" : reader["RazonSocial"].ToString();
            dic["Domicilio"] = reader["Domicilio"] == DBNull.Value ? "" : reader["Domicilio"].ToString();
            dic["FechaAtencion"] = fecha;
            dic["AgenteResponsable"] = reader["AgenteResponsable"] == DBNull.Value ? "" : reader["AgenteResponsable"].ToString();
            dic["Descripcion"] = reader["Descripcion"] == DBNull.Value ? "" : reader["Descripcion"].ToString();
            return dic;
        }

        private static int InsertarCotizacion(MySqlConnection cn, MySqlTransaction tx, string folio, string estado, decimal monto)
        {
            const string query = @"
INSERT INTO cotizaciones (TicketID, EstadoCotizacionID, FechaEnvio, Monto) 
VALUES (
    (SELECT Id FROM tickets WHERE Folio = @folio), 
    (SELECT EstadoCotizacionID FROM estadoscotizacion WHERE LOWER(Nombre) = LOWER(@estado) LIMIT 1), 
    CURDATE(), @monto
);
SELECT LAST_INSERT_ID();";
            using var cmd = new MySqlCommand(query, cn, tx);
            cmd.Parameters.AddWithValue("@folio", folio);
            cmd.Parameters.AddWithValue("@estado", estado);
            cmd.Parameters.AddWithValue("@monto", monto);
            object o = cmd.ExecuteScalar();
            return Convert.ToInt32(o);
        }

        private static void InsertarOrdenVenta(MySqlConnection cn, MySqlTransaction tx, int cotizacionID, string ovsr3, decimal comisionTotal)
        {
            const string query = @"INSERT INTO ordenesventa (OVSR3, CotizacionID, FechaVenta, Comision) 
                                   VALUES (@ovsr3, @cid, CURDATE(), @comision)";
            using var cmd = new MySqlCommand(query, cn, tx);
            cmd.Parameters.AddWithValue("@ovsr3", ovsr3);
            cmd.Parameters.AddWithValue("@cid", cotizacionID);
            cmd.Parameters.AddWithValue("@comision", comisionTotal);
            cmd.ExecuteNonQuery();
        }

        private static void InsertarDetalleVenta(
            MySqlConnection cn, MySqlTransaction tx,
            int cotizacionID, string ovsr3, string folio, decimal monto, string estado,
            string cuenta, string razonSocial, string domicilio, DateTime fechaAtencion, string comentariosCot, string statusPago)
        {
            const string query = @"
INSERT INTO ventasdetalle (
    CotizacionID, OVSR3, Fecha, 
    Cuenta, RazonSocial, Regional, Domicilio,
    Descripcion, FechaAtencion, AgenteResponsable, 
    Monto, StatusPago, ConstanciaDe, ComentariosCotizacion
)
SELECT @cid, @ovsr3, NOW(),
       @cuenta, @razon, NULL, @domicilio,
       t.Descripcion, @fechaAtencion, t.Responsable,
       @monto, @status, NULL, @comentarios
FROM tickets t WHERE Folio = @folio";
            using var cmd = new MySqlCommand(query, cn, tx);
            cmd.Parameters.AddWithValue("@cid", cotizacionID);
            cmd.Parameters.AddWithValue("@ovsr3", ovsr3);
            cmd.Parameters.AddWithValue("@folio", folio);
            cmd.Parameters.AddWithValue("@cuenta", (object)cuenta ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@razon", (object)razonSocial ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@domicilio", (object)domicilio ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@fechaAtencion", fechaAtencion);
            cmd.Parameters.AddWithValue("@monto", monto);
            cmd.Parameters.AddWithValue("@comentarios", (object)comentariosCot ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@status", string.IsNullOrWhiteSpace(statusPago) ? "Pendiente" : statusPago);
            cmd.ExecuteNonQuery();
        }

        private static bool CancelarVenta(string ovsr3, string motivo, string usuario)
        {
            const string sql = @"
UPDATE ventasdetalle
SET StatusPago = 'Cancelado',
    FechaCancelacion = NOW(),
    MotivoCancelacion = @m,
    UsuarioCancelacion = @u
WHERE OVSR3 = @ov";
            using var cn = new MySqlConnection(ConexionBD.CadenaConexion);
            cn.Open();
            using var cmd = new MySqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@ov", ovsr3);
            cmd.Parameters.AddWithValue("@m", motivo ?? "");
            cmd.Parameters.AddWithValue("@u", usuario ?? "");
            return cmd.ExecuteNonQuery() > 0;
        }

        private static int? ObtenerTicketIdPorOVSR3(string ovsr3)
        {
            const string sql = @"
SELECT c.TicketID
FROM ventasdetalle v
JOIN cotizaciones c ON v.CotizacionID = c.CotizacionID
WHERE v.OVSR3 = @ov
LIMIT 1";
            using var cn = new MySqlConnection(ConexionBD.CadenaConexion);
            cn.Open();
            using var cmd = new MySqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@ov", ovsr3);
            object o = cmd.ExecuteScalar();
            if (o == null || o == DBNull.Value) return null;
            return Convert.ToInt32(o);
        }

        private static bool ExisteVentaActivaMismoTicketExcepto(int ticketId, string excluirOV)
        {
            const string sql = @"
SELECT COUNT(*)
FROM ventasdetalle v
JOIN cotizaciones c ON v.CotizacionID = c.CotizacionID
WHERE c.TicketID = @t
  AND v.OVSR3 <> @ov
  AND (v.StatusPago IS NULL OR UPPER(v.StatusPago) <> 'CANCELADO')";
            using var cn = new MySqlConnection(ConexionBD.CadenaConexion);
            cn.Open();
            using var cmd = new MySqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@t", ticketId);
            cmd.Parameters.AddWithValue("@ov", excluirOV);
            int n = Convert.ToInt32(cmd.ExecuteScalar());
            return n > 0;
        }

        private static bool ReactivarVenta(string ovsr3)
        {
            const string sql = @"
UPDATE ventasdetalle
SET StatusPago = 'Pendiente',
    FechaCancelacion = NULL,
    MotivoCancelacion = NULL,
    UsuarioCancelacion = NULL
WHERE OVSR3 = @ov
  AND UPPER(IFNULL(StatusPago,'Pendiente')) = 'CANCELADO'";
            using var cn = new MySqlConnection(ConexionBD.CadenaConexion);
            cn.Open();
            using var cmd = new MySqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@ov", ovsr3);
            return cmd.ExecuteNonQuery() > 0;
        }

        private static void ExecNonQuery(string sql, params (string, object)[] args)
        {
            using var cn = new MySqlConnection(ConexionBD.CadenaConexion);
            cn.Open();
            using var cmd = new MySqlCommand(sql, cn);
            foreach (var (k, v) in args) cmd.Parameters.AddWithValue(k, v ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }

        private static List<VentaDetalleEntidad> ObtenerVentas(
            string folio, string estado, string ovsr3, decimal? min, decimal? max, int page, int size, out int total)
        {
            var where = new List<string>();
            if (!string.IsNullOrWhiteSpace(folio)) where.Add("t.Folio LIKE @folio");
            if (!string.IsNullOrWhiteSpace(estado)) where.Add("LOWER(ec.Nombre) = LOWER(@estado)");
            if (!string.IsNullOrWhiteSpace(ovsr3)) where.Add("v.OVSR3 LIKE @ovsr3");
            if (min.HasValue) where.Add("(v.Monto * 1.16 * 1.03) >= @min");
            if (max.HasValue) where.Add("(v.Monto * 1.16 * 1.03) <= @max");
            string whereSql = where.Count > 0 ? ("WHERE " + string.Join(" AND ", where)) : "";

            string baseSelect = @"
FROM ventasdetalle v
JOIN cotizaciones c       ON v.CotizacionID = c.CotizacionID
LEFT JOIN tickets t       ON c.TicketID     = t.Id
LEFT JOIN ordenesventa o  ON o.OVSR3        = v.OVSR3
LEFT JOIN estadoscotizacion ec ON c.EstadoCotizacionID = ec.EstadoCotizacionID";

            string sqlCount = $"SELECT COUNT(*) {baseSelect} {whereSql}";
            string sql = $@"
SELECT 
    v.OVSR3, t.Folio, v.Monto, ec.Nombre AS Estado, o.Comision,
    v.Cuenta, v.RazonSocial, v.Domicilio, v.FechaAtencion, v.AgenteResponsable, 
    v.Descripcion, v.ComentariosCotizacion, v.StatusPago, v.FechaCancelacion, 
    v.MotivoCancelacion, v.UsuarioCancelacion
{baseSelect}
{whereSql}
ORDER BY v.Fecha DESC
LIMIT @limit OFFSET @offset";

            var lista = new List<VentaDetalleEntidad>();
            using var conn = new MySqlConnection(ConexionBD.CadenaConexion);
            conn.Open();

            using (var cmdC = new MySqlCommand(sqlCount, conn))
            {
                if (!string.IsNullOrWhiteSpace(folio)) cmdC.Parameters.AddWithValue("@folio", $"%{folio}%");
                if (!string.IsNullOrWhiteSpace(estado)) cmdC.Parameters.AddWithValue("@estado", estado);
                if (!string.IsNullOrWhiteSpace(ovsr3)) cmdC.Parameters.AddWithValue("@ovsr3", $"%{ovsr3}%");
                if (min.HasValue) cmdC.Parameters.AddWithValue("@min", min.Value);
                if (max.HasValue) cmdC.Parameters.AddWithValue("@max", max.Value);
                total = Convert.ToInt32(cmdC.ExecuteScalar());
            }

            using (var cmd = new MySqlCommand(sql, conn))
            {
                if (!string.IsNullOrWhiteSpace(folio)) cmd.Parameters.AddWithValue("@folio", $"%{folio}%");
                if (!string.IsNullOrWhiteSpace(estado)) cmd.Parameters.AddWithValue("@estado", estado);
                if (!string.IsNullOrWhiteSpace(ovsr3)) cmd.Parameters.AddWithValue("@ovsr3", $"%{ovsr3}%");
                if (min.HasValue) cmd.Parameters.AddWithValue("@min", min.Value);
                if (max.HasValue) cmd.Parameters.AddWithValue("@max", max.Value);
                cmd.Parameters.AddWithValue("@limit", size);
                cmd.Parameters.AddWithValue("@offset", (page - 1) * size);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var v = new VentaDetalleEntidad
                    {
                        OVSR3 = reader["OVSR3"] == DBNull.Value ? "" : reader["OVSR3"].ToString(),
                        Folio = reader["Folio"] == DBNull.Value ? "" : reader["Folio"].ToString(),
                        Monto = reader["Monto"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["Monto"]),
                        Estado = reader["Estado"] == DBNull.Value ? "" : reader["Estado"].ToString(),
                        Comision = reader["Comision"] == DBNull.Value ? (decimal?)null : Convert.ToDecimal(reader["Comision"]),
                        Cuenta = reader["Cuenta"] == DBNull.Value ? "" : reader["Cuenta"].ToString(),
                        RazonSocial = reader["RazonSocial"] == DBNull.Value ? "" : reader["RazonSocial"].ToString(),
                        Domicilio = reader["Domicilio"] == DBNull.Value ? "" : reader["Domicilio"].ToString(),
                        FechaAtencion = reader["FechaAtencion"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader["FechaAtencion"]),
                        AgenteResponsable = reader["AgenteResponsable"] == DBNull.Value ? "" : reader["AgenteResponsable"].ToString(),
                        Descripcion = reader["Descripcion"] == DBNull.Value ? "" : reader["Descripcion"].ToString(),
                        ComentariosCotizacion = reader["ComentariosCotizacion"] == DBNull.Value ? "" : reader["ComentariosCotizacion"].ToString(),
                        StatusPago = reader["StatusPago"] == DBNull.Value ? "" : reader["StatusPago"].ToString(),
                        FechaCancelacion = reader["FechaCancelacion"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader["FechaCancelacion"]),
                        MotivoCancelacion = reader["MotivoCancelacion"] == DBNull.Value ? "" : reader["MotivoCancelacion"].ToString(),
                        UsuarioCancelacion = reader["UsuarioCancelacion"] == DBNull.Value ? "" : reader["UsuarioCancelacion"].ToString()
                    };
                    lista.Add(v);
                }
            }
            return lista;
        }

        // ==== Reportes y CSV (implementación simple) ====
        private static string ObtenerReporteTextoPlano(List<VentaDetalleEntidad> ventas)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Folio\tOVSR3\tEstado\tMonto\tIVA\tIVA+Comisión\tCuenta\tRazón Social\tAgente\tStatusPago");
            foreach (var v in ventas)
            {
                decimal iva = v.Monto * 1.16m;
                decimal ivacom = iva * 1.03m;
                sb.AppendLine(string.Join("\t", new[]{
                    v.Folio, v.OVSR3, v.Estado,
                    v.Monto.ToString("0.00", CultureInfo.InvariantCulture),
                    iva.ToString("0.00", CultureInfo.InvariantCulture),
                    ivacom.ToString("0.00", CultureInfo.InvariantCulture),
                    v.Cuenta, v.RazonSocial, v.AgenteResponsable, v.StatusPago
                }));
            }
            return sb.ToString();
        }

        private static string ObtenerReporteHtml(List<VentaDetalleEntidad> ventas)
        {
            var sb = new StringBuilder();
            sb.Append(@"<!doctype html><html lang='es'><head><meta charset='utf-8'><title>Reporte Ventas</title>
<style>body{font-family:Inter,Arial,sans-serif;padding:16px;background:#f4f5f7}
table{border-collapse:collapse;width:100%;background:#fff}
th,td{border:1px solid #ddd;padding:8px;font-size:12px}
th{background:#2c3e50;color:#fff;text-align:left;position:sticky;top:0}
tr:nth-child(even){background:#fafafa}
.badge{display:inline-block;padding:2px 6px;border-radius:6px;font-size:11px}
.badge--cancel{background:#e74c3c;color:#fff}</style></head><body>");
            sb.Append("<h2>Reporte de Ventas</h2>");
            sb.Append("<table><thead><tr><th>Folio</th><th>OVSR3</th><th>Estado</th><th>Monto</th><th>IVA</th><th>IVA+Comisión</th><th>Cuenta</th><th>Razón Social</th><th>Agente</th><th>Status</th></tr></thead><tbody>");
            foreach (var v in ventas)
            {
                decimal iva = v.Monto * 1.16m;
                decimal ivacom = iva * 1.03m;
                var status = string.Equals(v.StatusPago ?? "", "Cancelado", StringComparison.OrdinalIgnoreCase)
                    ? "<span class='badge badge--cancel'>CANCELADO</span>"
                    : (v.StatusPago ?? "");
                sb.Append("<tr>");
                sb.Append($"<td>{Esc(v.Folio)}</td>");
                sb.Append($"<td>{Esc(v.OVSR3)}</td>");
                sb.Append($"<td>{Esc(v.Estado)}</td>");
                sb.Append($"<td>{v.Monto:0.00}</td>");
                sb.Append($"<td>{iva:0.00}</td>");
                sb.Append($"<td>{ivacom:0.00}</td>");
                sb.Append($"<td>{Esc(v.Cuenta)}</td>");
                sb.Append($"<td>{Esc(v.RazonSocial)}</td>");
                sb.Append($"<td>{Esc(v.AgenteResponsable)}</td>");
                sb.Append($"<td>{status}</td>");
                sb.Append("</tr>");
            }
            sb.Append("</tbody></table></body></html>");
            return sb.ToString();
        }

        private static string GenerarCSV()
        {
            int _;
            var ventas = ObtenerVentas(null, null, null, null, null, 1, 10000, out _);
            var sb = new StringBuilder();
            sb.AppendLine("Folio,OVSR3,Estado,Monto,IVA,IVA+Comision,Cuenta,RazonSocial,Agente,StatusPago");
            foreach (var v in ventas)
            {
                decimal iva = v.Monto * 1.16m;
                decimal ivacom = iva * 1.03m;
                string line = string.Join(",", new[]{
                    Csv(v.Folio), Csv(v.OVSR3), Csv(v.Estado),
                    v.Monto.ToString("0.00", CultureInfo.InvariantCulture),
                    iva.ToString("0.00", CultureInfo.InvariantCulture),
                    ivacom.ToString("0.00", CultureInfo.InvariantCulture),
                    Csv(v.Cuenta), Csv(v.RazonSocial), Csv(v.AgenteResponsable), Csv(v.StatusPago)
                });
                sb.AppendLine(line);
            }
            return sb.ToString();
        }

        private static string Csv(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Replace("\"", "\"\"");
            if (s.Contains(',') || s.Contains('"') || s.Contains('\n')) return $"\"{s}\"";
            return s;
        }
        private static string Esc(string s) => System.Net.WebUtility.HtmlEncode(s ?? "");
    }
}
