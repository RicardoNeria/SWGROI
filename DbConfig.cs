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
                // Nota: establecemos una contraseña por defecto para facilitar el arranque
                // en entornos locales cuando no se configuran variables de entorno.
                // Puedes sobreescribirla exportando DB_PASSWORD.
                var pass = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "123456";

                return $"Server={host};Port={port};Database={name};Uid={user};Pwd={pass};";
            }
        }
    }
}

