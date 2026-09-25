using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using PlataformaCreditos.Data;
using PlataformaCreditos.Models;

namespace PlataformaCreditos.Services
{
    public class NotificacionConsumerService : BackgroundService
    {
        private readonly RabbitMqOptions _options;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<NotificacionConsumerService> _logger;
        private IConnection? _connection;
        private IChannel? _channel;

        public NotificacionConsumerService(
            IOptions<RabbitMqOptions> options,
            IServiceScopeFactory scopeFactory,
            ILogger<NotificacionConsumerService> logger)
        {
            _options = options.Value;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.ConsumerEnabled)
            {
                _logger.LogInformation("Consumidor de notificaciones deshabilitado (RabbitMq:ConsumerEnabled=false).");
                return;
            }

            try
            {
                var factory = new ConnectionFactory { Uri = new Uri(_options.ConnectionString) };
                _connection = await factory.CreateConnectionAsync(stoppingToken);
                _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

                await _channel.QueueDeclareAsync(
                    queue: _options.QueueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null,
                    cancellationToken: stoppingToken);

                await _channel.BasicQosAsync(0, 1, false, stoppingToken);

                var consumer = new AsyncEventingBasicConsumer(_channel);
                consumer.ReceivedAsync += async (sender, ea) =>
                {
                    await ProcesarMensajeAsync(ea, stoppingToken);
                };

                await _channel.BasicConsumeAsync(
                    queue: _options.QueueName,
                    autoAck: false,
                    consumer: consumer,
                    cancellationToken: stoppingToken);

                _logger.LogInformation("Consumidor conectado, escuchando la cola {Queue}.", _options.QueueName);

                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // apagado normal de la app
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fatal en el consumidor de notificaciones.");
            }
        }

        private async Task ProcesarMensajeAsync(BasicDeliverEventArgs ea, CancellationToken stoppingToken)
        {
            if (_channel == null) return;

            try
            {
                var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                var mensaje = JsonSerializer.Deserialize<SolicitudRegistradaMessage>(json);

                if (mensaje == null || string.IsNullOrWhiteSpace(mensaje.MessageId))
                {
                    _logger.LogWarning("Mensaje invalido en la cola {Queue}, se rechaza sin reencolar. Contenido: {Json}",
                        _options.QueueName, json);
                    await _channel.BasicRejectAsync(ea.DeliveryTag, requeue: false, stoppingToken);
                    return;
                }

                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var yaExiste = await db.Notificaciones.AnyAsync(n => n.MessageId == mensaje.MessageId, stoppingToken);
                if (yaExiste)
                {
                    // Redelivery de un mensaje ya procesado: confirmar sin insertar de nuevo
                    await _channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
                    return;
                }

                db.Notificaciones.Add(new Notificacion
                {
                    MessageId = mensaje.MessageId,
                    SolicitudId = mensaje.SolicitudId,
                    UsuarioId = mensaje.UsuarioId,
                    Texto = "Recibimos tu solicitud de credito y esta pendiente de evaluacion",
                    FechaProcesamientoUtc = DateTime.UtcNow
                });
                await db.SaveChangesAsync(stoppingToken);

                // ACK manual SOLO despues de guardar exitosamente
                await _channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);

                _logger.LogInformation("Notificacion guardada. MessageId={MessageId} SolicitudId={SolicitudId}",
                    mensaje.MessageId, mensaje.SolicitudId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al procesar mensaje, se rechaza sin reencolar (NACK).");
                if (_channel != null)
                {
                    await _channel.BasicNackAsync(ea.DeliveryTag, false, requeue: false, stoppingToken);
                }
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_channel != null) await _channel.CloseAsync(cancellationToken);
            if (_connection != null) await _connection.CloseAsync(cancellationToken);
            await base.StopAsync(cancellationToken);
        }
    }
}