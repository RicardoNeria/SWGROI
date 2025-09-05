using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using MySql.Data.MySqlClient;
using SWGROI_Server.DB;

public class MetricasController
{
    public static void ManejarSolicitud(HttpListenerContext context)
    {
        string tipo = context.Request.QueryString["tipo"];
        if (tipo == "tiempos")
            ObtenerTiemposPorEstado(context);
        else if (tipo == "tecnicos")
            ObtenerTicketsPorTecnico(context);
        else
            EnviarRespuesta(context, "Solicitud inválida", 400);
    }

    private static void ObtenerTiemposPorEstado(HttpListenerContext context)
    {
        var lista = new List<Dictionary<string, string>>();

        using (var conexion = new MySqlConnection(ConexionBD.CadenaConexion))
        {
            conexion.Open();
            string query = @"
                SELECT Estado, 
                ROUND(AVG(TIMESTAMPDIFF(HOUR, FechaRegistro, FechaCierre)), 2) AS Promedio 
                FROM tickets 
                WHERE FechaCierre IS NOT NULL 
                GROUP BY Estado";

            using (var cmd = new MySqlCommand(query, conexion))
            using (var rdr = cmd.ExecuteReader())
            {
                while (rdr.Read())
                {
                    lista.Add(new Dictionary<string, string>
                    {
                        ["Estado"] = rdr["Estado"].ToString(),
                        ["Promedio"] = rdr["Promedio"].ToString()
                    });
                }
            }
        }

        string json = ConvertirListaAJson(lista);
        context.Response.ContentType = "application/json";
        using var writer = new StreamWriter(context.Response.OutputStream);
        writer.Write(json);
    }

    private static void ObtenerTicketsPorTecnico(HttpListenerContext context)
    {
        var lista = new List<Dictionary<string, string>>();

        using (var conexion = new MySqlConnection(ConexionBD.CadenaConexion))
        {
            conexion.Open();
            string query = @"
                SELECT Tecnico, COUNT(*) AS Total 
                FROM tickets 
                WHERE Estado = 'Cerrado' 
                GROUP BY Tecnico";

            using (var cmd = new MySqlCommand(query, conexion))
            using (var rdr = cmd.ExecuteReader())
            {
                while (rdr.Read())
                {
                    lista.Add(new Dictionary<string, string>
                    {
                        ["Tecnico"] = rdr["Tecnico"].ToString(),
                        ["Total"] = rdr["Total"].ToString()
                    });
                }
            }
        }

        string json = ConvertirListaAJson(lista);
        context.Response.ContentType = "application/json";
        using var writer = new StreamWriter(context.Response.OutputStream);
        writer.Write(json);
    }

    private static void EnviarRespuesta(HttpListenerContext context, string mensaje, int codigo = 200)
    {
        context.Response.StatusCode = codigo;
        using var writer = new StreamWriter(context.Response.OutputStream);
        writer.Write(mensaje);
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
                string clave = kv.Key.Replace("\"", "\\\"");
                string valor = kv.Value.Replace("\"", "\\\"");
                sb.Append($"\"{clave}\":\"{valor}\"");

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
}
