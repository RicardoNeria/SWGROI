using System;

namespace SWGROI_Server.Models
{
    public class VentaDetalleEntidad
    {
        public string OVSR3 { get; set; }
        public string Folio { get; set; }
        public decimal Monto { get; set; }
        public string Estado { get; set; }

        public decimal? Comision { get; set; }

        public string Cuenta { get; set; }
        public string RazonSocial { get; set; }
        public string Domicilio { get; set; }

        public DateTime? FechaAtencion { get; set; }

        public string AgenteResponsable { get; set; }
        public string Descripcion { get; set; }
        public string ComentariosCotizacion { get; set; }

        public string StatusPago { get; set; }
        public DateTime? FechaCancelacion { get; set; }
        public string MotivoCancelacion { get; set; }
        public string UsuarioCancelacion { get; set; }
    }
}
