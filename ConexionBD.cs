using SWGROI_Server.Models;
using System.Collections.Generic;
using MySql.Data.MySqlClient;

namespace SWGROI_Server.DB
{
    public static class ConexionBD
    {
        public static string CadenaConexion = "Server=localhost;Database=swgroi_db;Uid=root;Pwd=123456;";

        public static int ContarRegistros(string tabla)
        {
            int total = 0;

            using (var conexion = new MySqlConnection(CadenaConexion))
            {
                conexion.Open();
                var comando = new MySqlCommand($"SELECT COUNT(*) FROM {tabla}", conexion);
                total = int.Parse(comando.ExecuteScalar().ToString());
            }

            return total;
        }


    }
}