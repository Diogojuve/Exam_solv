# Plataforma de Créditos

Plataforma web para gestión de solicitudes de crédito, con evaluación por analistas de riesgo, notificaciones en tiempo real (WebSocket/SignalR) y mensajería asíncrona (RabbitMQ/CloudAMQP).

## Stack
- ASP.NET Core MVC (.NET 10) + Identity
- Entity Framework Core + SQLite
- Redis (sesión y caché)
- SignalR (notificaciones en tiempo real)
- RabbitMQ / CloudAMQP (mensajería asíncrona)
- Render.com (despliegue, Docker)

## Cómo correr el proyecto localmente

### Requisitos
- .NET 10 SDK
- Una instancia de Redis (local o Redis Cloud)
- Una cola en CloudAMQP (o RabbitMQ local)

### Pasos

```bash
cd PlataformaCreditos
dotnet restore
```

Configura los secretos locales (nunca se suben al repo):

```bash
dotnet user-secrets set "ConnectionStrings:Redis" "TU_REDIS_CONNECTION_STRING"
dotnet user-secrets set "RabbitMq:ConnectionString" "TU_AMQPS_URL"
dotnet user-secrets set "RabbitMq:QueueName" "solicitudes.notificaciones"
```

Aplica las migraciones (crea la base SQLite local):

```bash
dotnet ef database update
```

Corre la aplicación:

```bash
dotnet run
```

La app arranca con datos semilla: un usuario Analista (`analista@creditos.com` / `Analista123!`) y dos clientes de prueba (`cliente1@creditos.com` y `cliente2@creditos.com`, password `Cliente123!`).

## Variables de entorno (producción / Render)

| Variable | Descripción |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ConnectionStrings__DefaultConnection` | Cadena de conexión SQLite, ej. `Data Source=app.db` |
| `Redis__ConnectionString` | Cadena de conexión a Redis Cloud |
| `RabbitMq__ConnectionString` | URL AMQPS de CloudAMQP |
| `RabbitMq__QueueName` | `solicitudes.notificaciones` |
| `RabbitMq__ConsumerEnabled` | `true` |

`PORT` la inyecta Render automáticamente; el código la lee directamente en `Program.cs` (no depende de que `${PORT}` se expanda dentro de otra variable).

## Migraciones de base de datos

Las migraciones se aplican automáticamente al arrancar la aplicación (`db.Database.Migrate()` en `Program.cs`), tanto en local como en producción. No es necesario ejecutar `dotnet ef database update` manualmente en Render.

## Persistencia de SQLite en Render (plan Free)

**Importante:** el plan Free de Render no incluye disco persistente. El sistema de archivos es efímero: en cada reinicio o redeploy del servicio, el contenedor se recrea desde cero y el archivo `app.db` se pierde, volviendo a generarse solo con los datos semilla iniciales (el Analista y los 2 clientes de prueba).

Para conservar los datos entre despliegues sería necesario:
- Contratar un plan de Render con **Persistent Disk**, montando el volumen en la ruta donde vive `app.db`, o
- Migrar a una base de datos gestionada externa (ej. PostgreSQL en Render, o SQLite en un volumen S3-compatible), fuera del alcance de esta práctica.

## Arquitectura de notificaciones en tiempo real (Pregunta 6)

- Hub de SignalR en `/hubs/solicitudes`, protegido con `[Authorize]`.
- Cada usuario se agrupa por su `UserIdentifier` (Id de Identity) al conectarse — el servidor nunca confía en un `UsuarioId` enviado por el navegador.
- Cuando un analista aprueba/rechaza: se guarda en BD → se invalida el caché Redis → se emite el evento `SolicitudEstadoActualizado` solo al grupo del dueño de la solicitud.
- El cliente JS (`wwwroot/js/solicitudes-hub.js`) usa reconexión automática y, al reconectar, llama a `/Solicitudes/MisEstadosJson` para resincronizar cualquier cambio ocurrido durante la desconexión.

## Arquitectura de mensajería asíncrona (Pregunta 7)

- Cola durable `solicitudes.notificaciones` en CloudAMQP (RabbitMQ gestionado), conexión por AMQPS.
- Al registrar una solicitud Pendiente (y solo si la persistencia fue exitosa), se publica un mensaje `SolicitudRegistrada` con confirmación de publicador (publisher confirms).
- Un `BackgroundService` consume la cola, guarda la notificación en SQLite y confirma con **ACK manual** solo después de guardar exitosamente.
- El `MessageId` (UUID) es único en la tabla `Notificaciones`: si RabbitMQ reentrega un mensaje ya procesado, se confirma sin insertar de nuevo (evita duplicados).
- Si falla la publicación: la solicitud queda guardada igual, se registra el error en logs, y se muestra una advertencia al usuario. El reenvío del mismo `MessageId` sería manual (no se implementó patrón outbox, según lo permite el enunciado).
- Si falla el procesamiento de un mensaje: se hace NACK sin reencolar (`requeue: false`) y se deja evidencia en logs, evitando reintentos infinitos.

## Despliegue en Render

- **Tipo:** Web Service, runtime Docker (`Dockerfile` en la raíz del proyecto).
- **Rama desplegada:** `main`.
- **Instancia:** única (Free), corriendo tanto el Web Service como el consumidor de RabbitMQ (`BackgroundService`) dentro del mismo proceso.
- **URL en producción:** _(completar tras el despliegue)_

## Verificación en producción

Una vez desplegado, se verificó manualmente:
- [ ] Login y registro de usuarios
- [ ] Registro de solicitud con validaciones de negocio
- [ ] Panel de Analista (aprobar/rechazar) con validaciones
- [ ] Caché y sesión respaldadas por Redis
- [ ] Conexión WebSocket segura (`wss://`) y notificación en tiempo real
- [ ] Publicación y consumo de mensajes en CloudAMQP