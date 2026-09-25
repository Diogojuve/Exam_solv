namespace PlataformaCreditos.Services
{
    public class RabbitMqOptions
    {
        public string ConnectionString { get; set; } = string.Empty;
        public string QueueName { get; set; } = "solicitudes.notificaciones";
        public bool ConsumerEnabled { get; set; } = true;
    }
}