/*

using System;
using MySql.Data.MySqlClient;

namespace SWGROI_Server
{
    class PruebaConexion
    {
        static void Main()
        {
            try
           {
                var conn = new MySqlConnection("Server=201.141.105.254;Database=SWGROI_DB;Uid=swgroi_user;Pwd=admin123;");
               conn.Open();
                Console.WriteLine("✅ Conexión exitosa a MySQL.");
                conn.Close();
           }
          catch (Exception ex)
            {
                Console.WriteLine("❌ Error de conexión: " + ex.Message);
            }
        }
   }
}

*/