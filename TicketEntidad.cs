using System;

public class TicketEntidad
{
    public int Id { get; set; }
    public string Folio { get; set; }
    public string Descripcion { get; set; }
    public string Estado { get; set; }
    public DateTime FechaRegistro { get; set; }
    public DateTime? FechaAsignada { get; set; }
    public string HoraAsignada { get; set; }
    public DateTime? FechaCierre { get; set; }
    public string Tecnico { get; set; }
    public string Responsable { get; set; }
    public string Cotizacion { get; set; }
}
