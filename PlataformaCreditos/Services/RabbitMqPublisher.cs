using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using PlataformaCreditos.Models;

namespace PlataformaCreditos.Services
{
    public class RabbitMqPublisher : IRabbitMqPublisher, IAsyncDisposable
    {
        private readonly RabbitMqOptions _options;
        private readonly ILogger<RabbitMqPublisher> _logger;
        private IConnection? _connection;
        private readonly SemaphoreSlim _lock = new(1, 1);

        public RabbitMqPublisher(IOptions<RabbitMqOptions> options, ILogger<RabbitMqPublisher> logger)
        {
            _options = options.Value;
            _logger = logger;
        }

        private async Task<IConnection> GetConnectionAsync()
        {
            if (_connection is { IsOpen: true })
                return _connection;

            await _lock.WaitAsync();
            try
            {
                if (_connection is { IsOpen: true })
                    return _connection;

                var factory = new ConnectionFactory { Uri = new Uri(_options.ConnectionString) };
                _connection = await factory.CreateConnectionAsync();
                return _connection;
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<bool> PublicarSolicitudRegistradaAsync(SolicitudRegistradaMessage mensaje)
        {
            try
            {
                var connection = await GetConnectionAsync();

                var channelOptions = new CreateChannelOptions(
                    publisherConfirmationsEnabled: true,
                    publisherConfirmationTrackingEnabled: true);

                await using var channel = await connection.CreateChannelAsync(channelOptions);

                await channel.QueueDeclareAsync(
                    queue: _options.QueueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);

                var json = JsonSerializer.Serialize(mensaje);
                var body = Encoding.UTF8.GetBytes(json);

                var props = new BasicProperties
                {
                    Persistent = true,
                    ContentType = "application/json",
                    MessageId = mensaje.MessageId
                };

                // Con confirmaciones de publicador habilitadas, este await espera el ACK del broker.
                // Si el broker rechaza (nack), lanza excepcion y cae al catch de abajo.
                await channel.BasicPublishAsync(
                    exchange: string.Empty,
                    routingKey: _options.QueueName,
                    mandatory: true,
                    basicProperties: props,
                    body: body);

                _logger.LogInformation(
                    "Mensaje SolicitudRegistrada publicado y confirmado. MessageId={MessageId} SolicitudId={SolicitudId}",
                    mensaje.MessageId, mensaje.SolicitudId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Fallo al publicar SolicitudRegistrada. MessageId={MessageId} SolicitudId={SolicitudId}. " +
                    "La solicitud SI quedo guardada; reenviar manualmente con el mismo MessageId cuando el broker este disponible.",
                    mensaje.MessageId, mensaje.SolicitudId);
                return false;
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_connection != null)
            {
                await _connection.CloseAsync();
                _connection.Dispose();
            }
        }
    }
}