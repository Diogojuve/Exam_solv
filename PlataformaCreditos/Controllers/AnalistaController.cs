using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using PlataformaCreditos.Data;
using PlataformaCreditos.Models;

namespace PlataformaCreditos.Controllers
{
    [Authorize(Roles = "Analista")]
    public class AnalistaController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IDistributedCache _cache;

        public AnalistaController(ApplicationDbContext context, IDistributedCache cache)
        {
            _context = context;
            _cache = cache;
        }

        // GET: /Analista
        public async Task<IActionResult> Index()
        {
            var pendientes = await _context.Solicitudes
                .Include(s => s.Cliente)
                .Where(s => s.Estado == EstadoSolicitud.Pendiente)
                .OrderBy(s => s.FechaSolicitud)
                .ToListAsync();

            return View(pendientes);
        }

        // POST: /Analista/Aprobar/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Aprobar(int id)
        {
            var solicitud = await _context.Solicitudes
                .Include(s => s.Cliente)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (solicitud == null)
            {
                TempData["Error"] = "Solicitud no encontrada.";
                return RedirectToAction(nameof(Index));
            }

            if (solicitud.Estado != EstadoSolicitud.Pendiente)
            {
                TempData["Error"] = "Solo se pueden procesar solicitudes en estado Pendiente.";
                return RedirectToAction(nameof(Index));
            }

            if (solicitud.Cliente == null)
            {
                TempData["Error"] = "No se encontró el cliente asociado.";
                return RedirectToAction(nameof(Index));
            }

            // Regla: no aprobar si el monto excede 5 veces los ingresos
            if (solicitud.MontoSolicitado > solicitud.Cliente.IngresosMensuales * 5)
            {
                TempData["Error"] = $"No se puede aprobar: el monto excede 5 veces los ingresos mensuales del cliente (máximo permitido: {(solicitud.Cliente.IngresosMensuales * 5):C}).";
                return RedirectToAction(nameof(Index));
            }

            solicitud.Estado = EstadoSolicitud.Aprobado;
            await _context.SaveChangesAsync();

            // Invalidamos el caché de "Mis solicitudes" del cliente, ya que cambió el estado
            await _cache.RemoveAsync($"solicitudes:cliente:{solicitud.ClienteId}");

            TempData["Exito"] = $"Solicitud #{solicitud.Id} aprobada correctamente.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Analista/Rechazar/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Rechazar(int id, string motivoRechazo)
        {
            var solicitud = await _context.Solicitudes.FirstOrDefaultAsync(s => s.Id == id);

            if (solicitud == null)
            {
                TempData["Error"] = "Solicitud no encontrada.";
                return RedirectToAction(nameof(Index));
            }

            if (solicitud.Estado != EstadoSolicitud.Pendiente)
            {
                TempData["Error"] = "Solo se pueden procesar solicitudes en estado Pendiente.";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(motivoRechazo))
            {
                TempData["Error"] = "Debe indicar un motivo de rechazo.";
                return RedirectToAction(nameof(Index));
            }

            solicitud.Estado = EstadoSolicitud.Rechazado;
            solicitud.MotivoRechazo = motivoRechazo;
            await _context.SaveChangesAsync();

            await _cache.RemoveAsync($"solicitudes:cliente:{solicitud.ClienteId}");

            TempData["Exito"] = $"Solicitud #{solicitud.Id} rechazada correctamente.";
            return RedirectToAction(nameof(Index));
        }
    }
}