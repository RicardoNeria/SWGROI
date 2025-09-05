using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using MySql.Data.MySqlClient;
using SWGROI_Server.DB;

public class AsignacionesController
{
    public static void ManejarSolicitud(HttpListenerContext context)
    {
        if (context.Request.HttpMethod == "GET")
        {
            var query = context.Request.QueryString["tipo"];
            if (query == "tickets")
                ObtenerTicketsPendientes(context);
            else if (query == "tecnicos")
                ObtenerTecnicosDisponibles(context);
            else
                EnviarRespuesta(context, "Solicitud no válida.", 400);
        }
        else if (context.Request.HttpMethod == "POST")
        {
            GuardarAsignacion(context);
        }
        else
        {
            EnviarRespuesta(context, "Método no permitido.", 405);
        }
    }

    private static void ObtenerTicketsPendientes(HttpListenerContext context)
    {
        var lista = new List<Dictionary<string, string>>();

        using (var conexion = new MySqlConnection(ConexionBD.CadenaConexion))
        {
            conexion.Open();
            string query = "SELECT Folio, Descripcion FROM tickets WHERE Estado = 'Capturado'";

            using (var cmd = new MySqlCommand(query, conexion))
            using (var rdr = cmd.ExecuteReader())
            {
                while (rdr.Read())
                {
                    var ticket = new Dictionary<string, string>
                    {
                        ["Folio"] = rdr["Folio"].ToString(),
                        ["Descripcion"] = rdr["Descripcion"].ToString()
                    };
                    lista.Add(ticket);
                }
            }
        }

        string json = ConvertirListaAJson(lista);
        context.Response.ContentType = "application/json";
        using var writer = new StreamWriter(context.Response.OutputStream);
        writer.Write(json);
    }


    private static void ObtenerTecnicosDisponibles(HttpListenerContext context)
    {
        var lista = new List<Dictionary<string, string>>();

        using (var conexion = new MySqlConnection(ConexionBD.CadenaConexion))
        {
            conexion.Open();
            string query = "SELECT Nombre FROM tecnicos";

            using (var cmd = new MySqlCommand(query, conexion))
            using (var rdr = cmd.ExecuteReader())
            {
                while (rdr.Read())
                {
                    var tecnico = new Dictionary<string, string>
                    {
                        ["Nombre"] = rdr["Nombre"].ToString()
                    };
                    lista.Add(tecnico);
                }
            }
        }

        string json = ConvertirListaAJson(lista);
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

    private static void GuardarAsignacion(HttpListenerContext context)
    {
        string body;
        using (var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
        {
            body = reader.ReadToEnd();
        }

        var datos = new Dictionary<string, string>();
        body = body.Trim('{', '}').Replace("\"", "");
        var pares = body.Split(',');

        foreach (var par in pares)
        {
            var kv = par.Split(':');
            if (kv.Length == 2)
            {
                string clave = kv[0].Trim();
                string valor = kv[1].Trim();
                datos[clave] = valor;
            }
        }


        using (var conexion = new MySqlConnection(ConexionBD.CadenaConexion))
        {
            conexion.Open();
            string query = "UPDATE tickets SET Estado = 'Asignado', Tecnico = @Tecnico, FechaAsignada = @Fecha, HoraAsignada = @Hora WHERE Folio = @Folio";

            using (var cmd = new MySqlCommand(query, conexion))
            {
                cmd.Parameters.AddWithValue("@Folio", datos["folio"]);
                cmd.Parameters.AddWithValue("@Tecnico", datos["tecnico"]);
                cmd.Parameters.AddWithValue("@Fecha", datos["fecha"]);
                cmd.Parameters.AddWithValue("@Hora", datos["hora"]);

                int resultado = cmd.ExecuteNonQuery();
                if (resultado > 0)
                {
                    EnviarRespuesta(context, "Asignación realizada correctamente.");
                }
                else
                {
                    EnviarRespuesta(context, "No se pudo asignar el servicio.", 400);
                }
            }
        }
    }

    private static void EnviarRespuesta(HttpListenerContext context, string mensaje, int codigo = 200)
    {
        context.Response.StatusCode = codigo;
        using var writer = new StreamWriter(context.Response.OutputStream);
        writer.Write(mensaje);
    }
}
