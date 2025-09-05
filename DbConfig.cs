using System;

namespace SWGROI_Server.DB
{
    public static class DbConfig
    {
        public static string ConnectionString
        {
            get
            {
                // Leer desde variables de entorno
                var host = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";
                var port = Environment.GetEnvironmentVariable("DB_PORT") ?? "3306";
                var name = Environment.GetEnvironmentVariable("DB_NAME") ?? "swgroi_db";
                var user = Environment.GetEnvironmentVariable("DB_USER") ?? "root";
                var pass = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? string.Empty;

                return $"Server={host};Port={port};Database={name};Uid={user};Pwd={pass};";
            }
        }
    }
}

