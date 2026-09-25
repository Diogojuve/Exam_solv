using PlataformaCreditos.Models;

namespace PlataformaCreditos.Services
{
    public interface IRabbitMqPublisher
    {
        Task<bool> PublicarSolicitudRegistradaAsync(SolicitudRegistradaMessage mensaje);
    }
}