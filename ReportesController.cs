using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using MySql.Data.MySqlClient;
using SWGROI_Server.DB;

public class ReportesController
{
    public static void ManejarSolicitud(HttpListenerContext context)
    {
        if (context.Request.HttpMethod == "GET")
        {
            ObtenerTodosLosTickets(context);
        }
        else if (context.Request.HttpMethod == "POST")
        {
            BuscarTicketsAvanzado(context);
        }
        else
        {
            context.Response.StatusCode = 405;
            using var writer = new StreamWriter(context.Response.OutputStream);
            writer.Write("Método no permitido");
        }
    }

    private static void ObtenerTodosLosTickets(HttpListenerContext context)
    {
        var lista = new List<TicketEntidad>();

        using (var conexion = new MySqlConnection(ConexionBD.CadenaConexion))
        {
            conexion.Open();
            string query = "SELECT * FROM tickets ORDER BY FechaRegistro DESC";

            using (var cmd = new MySqlCommand(query, conexion))
            using (var rdr = cmd.ExecuteReader())
            {
                while (rdr.Read())
                {
                    // Declaración previa para evitar error de compatibilidad
                    DateTime? fechaAsignada = rdr["FechaAsignada"] != DBNull.Value
                        ? Convert.ToDateTime(rdr["FechaAsignada"])
                        : (DateTime?)null;

                    DateTime? fechaCierre = rdr["FechaCierre"] != DBNull.Value
                        ? Convert.ToDateTime(rdr["FechaCierre"])
                        : (DateTime?)null;

                    lista.Add(new TicketEntidad
                    {
                        Id = Convert.ToInt32(rdr["Id"]),
                        Folio = rdr["Folio"].ToString(),
                        Descripcion = rdr["Descripcion"].ToString(),
                        Estado = rdr["Estado"].ToString(),
                        Responsable = rdr["Responsable"].ToString(),
                        Tecnico = rdr["Tecnico"]?.ToString(),
                        FechaRegistro = Convert.ToDateTime(rdr["FechaRegistro"]),
                        FechaAsignada = fechaAsignada,
                        HoraAsignada = rdr["HoraAsignada"]?.ToString(),
                        FechaCierre = fechaCierre,
                        Cotizacion = rdr["Cotizacion"]?.ToString()
                    });
                }
            }
        }

        string json = ConvertirTicketsAJson(lista);
        context.Response.ContentType = "application/json";
        using var writer = new StreamWriter(context.Response.OutputStream);
        writer.Write(json);
    }


    private static string ConvertirTicketsAJson(List<TicketEntidad> tickets)
    {
        var sb = new StringBuilder();
        sb.Append("[");

        for (int i = 0; i < tickets.Count; i++)
        {
            var t = tickets[i];

            string fechaAsignada = t.FechaAsignada.HasValue ? t.FechaAsignada.Value.ToString("yyyy-MM-dd") : "";
            string fechaCierre = t.FechaCierre.HasValue ? t.FechaCierre.Value.ToString("yyyy-MM-dd HH:mm:ss") : "";
            string fechaRegistro = t.FechaRegistro.ToString("yyyy-MM-dd HH:mm:ss");

            sb.Append("{");
            sb.Append($"\"Id\":{t.Id},");
            sb.Append($"\"Folio\":\"{t.Folio}\",");
            sb.Append($"\"Descripcion\":\"{t.Descripcion}\",");
            sb.Append($"\"Estado\":\"{t.Estado}\",");
            sb.Append($"\"Responsable\":\"{t.Responsable}\",");
            sb.Append($"\"Tecnico\":\"{t.Tecnico}\",");
            sb.Append($"\"FechaRegistro\":\"{fechaRegistro}\",");
            sb.Append($"\"FechaAsignada\":\"{fechaAsignada}\",");
            sb.Append($"\"HoraAsignada\":\"{t.HoraAsignada}\",");
            sb.Append($"\"FechaCierre\":\"{fechaCierre}\",");
            sb.Append($"\"Cotizacion\":\"{t.Cotizacion}\"");
            sb.Append("}");

            if (i < tickets.Count - 1)
                sb.Append(",");
        }

        sb.Append("]");
        return sb.ToString();
    }


    private static void BuscarTicketsAvanzado(HttpListenerContext context)
    {
        string body;
        using (var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
        {
            body = reader.ReadToEnd();
        }

        var filtros = ParsearJson(body);
        var resultados = new List<Dictionary<string, string>>();

        using (var conexion = new MySqlConnection(ConexionBD.CadenaConexion))
        {
            conexion.Open();
            string query = "SELECT Folio, FechaRegistro, Estado, Responsable, Descripcion FROM tickets WHERE 1=1";

            if (!string.IsNullOrEmpty(filtros["folio"]))
                query += " AND Folio LIKE @Folio";
            if (!string.IsNullOrEmpty(filtros["estado"]))
                query += " AND Estado = @Estado";
            if (!string.IsNullOrEmpty(filtros["fecha"]))
                query += " AND FechaRegistro = @Fecha";
            if (!string.IsNullOrEmpty(filtros["responsable"]))
                query += " AND Responsable LIKE @Responsable";

            using (var cmd = new MySqlCommand(query, conexion))
            {
                if (!string.IsNullOrEmpty(filtros["folio"]))
                    cmd.Parameters.AddWithValue("@Folio", $"%{filtros["folio"]}%");
                if (!string.IsNullOrEmpty(filtros["estado"]))
                    cmd.Parameters.AddWithValue("@Estado", filtros["estado"]);
                if (!string.IsNullOrEmpty(filtros["fecha"]))
                    cmd.Parameters.AddWithValue("@Fecha", filtros["fecha"]);
                if (!string.IsNullOrEmpty(filtros["responsable"]))
                    cmd.Parameters.AddWithValue("@Responsable", $"%{filtros["responsable"]}%");

                using (var rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        var ticket = new Dictionary<string, string>
                        {
                            ["Folio"] = rdr["Folio"].ToString(),
                            ["FechaRegistro"] = rdr["FechaRegistro"].ToString(),
                            ["Estado"] = rdr["Estado"].ToString(),
                            ["Responsable"] = rdr["Responsable"].ToString(),
                            ["Descripcion"] = rdr["Descripcion"].ToString()
                        };
                        resultados.Add(ticket);
                    }
                }
            }
        }

        string json = ConvertirListaAJson(resultados);
        context.Response.ContentType = "application/json";
        using var writer = new StreamWriter(context.Response.OutputStream);
        writer.Write(json);
    }

    private static string ConvertirListaAJson(List<Dictionary<string, string>> lista)
    {
        var sb = new StringBuilder();
        sb.Append("[");

        for (int i = 0; i < lista.Count; i++)
        {
            sb.Append("{");
            var elemento = lista[i];
            int j = 0;
            foreach (var kv in elemento)
            {
                sb.Append($"\"{kv.Key}\":\"{kv.Value}\"");
                if (++j < elemento.Count)
                    sb.Append(",");
            }
            sb.Append("}");
            if (i < lista.Count - 1)
                sb.Append(",");
        }

        sb.Append("]");
        return sb.ToString();
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
                dict[kv[0].Trim()] = kv[1].Trim();
        }

        return dict;
    }
}
