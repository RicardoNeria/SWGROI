using System;
using System.IO;
using System.Threading;

namespace SWGROI_Server.Utils
{
    // Logger sencillo y thread-safe para trazabilidad y soporte.
    public static class Logger
    {
        private static readonly object _lock = new object();

        public static string NewRequestId() => Guid.NewGuid().ToString("N");

        public static void Info(string msg, string requestId = null)
            => Write("INFO", msg, requestId);

        public static void Warn(string msg, string requestId = null)
            => Write("WARN", msg, requestId);

        public static void Error(string msg, string requestId = null)
            => Write("ERROR", msg, requestId);

        private static void Write(string level, string msg, string requestId)
        {
            // No exponer datos sensibles si aparecen accidentalmente.
            msg = Redact(msg);
            var line = $"[{DateTime.UtcNow:O}] {level} {(requestId ?? "-")} {msg}";
            lock (_lock)
            {
                Console.WriteLine(line);
            }
        }

        private static string Redact(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            s = s.Replace("Contrasena=", "Contrasena=[REDACTED]");
            s = s.Replace("password=", "password=[REDACTED]");
            s = s.Replace("Authorization:", "Authorization:[REDACTED]");
            return s;
        }
    }
}

