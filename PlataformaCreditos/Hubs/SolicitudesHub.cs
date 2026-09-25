using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace PlataformaCreditos.Hubs
{
    [Authorize]
    public class SolicitudesHub : Hub
    {
        // El grupo se nombra con el Id del usuario autenticado (server-side, nunca del cliente)
        public override async Task OnConnectedAsync()
        {
            var userId = Context.UserIdentifier; // viene del NameIdentifier claim de Identity
            if (!string.IsNullOrEmpty(userId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, userId);
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.UserIdentifier;
            if (!string.IsNullOrEmpty(userId))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, userId);
            }
            await base.OnDisconnectedAsync(exception);
        }
    }
}