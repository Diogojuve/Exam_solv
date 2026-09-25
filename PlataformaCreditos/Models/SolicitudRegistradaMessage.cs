namespace PlataformaCreditos.Models
{
    public class SolicitudRegistradaMessage
    {
        public string MessageId { get; set; } = string.Empty;
        public int SolicitudId { get; set; }
        public string UsuarioId { get; set; } = string.Empty;
        public DateTime FechaEventoUtc { get; set; }
    }
}