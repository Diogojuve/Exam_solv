using System.ComponentModel.DataAnnotations;

namespace PlataformaCreditos.Models
{
    public class Notificacion
    {
        public int Id { get; set; }

        [Required]
        public required string MessageId { get; set; } // UUID del mensaje original, usado para evitar duplicados

        public int SolicitudId { get; set; }

        [Required]
        public required string UsuarioId { get; set; }

        [Required]
        public required string Texto { get; set; }

        public DateTime FechaProcesamientoUtc { get; set; }
    }
}