using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlataformaCreditos.Data;
using PlataformaCreditos.Models;

namespace PlataformaCreditos.Controllers
{
    [Authorize]
    public class SolicitudesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public SolicitudesController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: /Solicitudes/Mis
        public async Task<IActionResult> Mis(
            EstadoSolicitud? estado,
            decimal? montoMin,
            decimal? montoMax,
            DateTime? fechaInicio,
            DateTime? fechaFin)
        {
            var userId = _userManager.GetUserId(User);
            var cliente = await _context.Clientes.FirstOrDefaultAsync(c => c.UsuarioId == userId);

            if (cliente == null)
            {
                ViewBag.Mensaje = "No se encontró un cliente asociado a tu usuario.";
                return View(new List<SolicitudCredito>());
            }

            // --- Validaciones server-side de los filtros ---
            if (montoMin.HasValue && montoMin < 0)
            {
                ModelState.AddModelError(string.Empty, "El monto mínimo no puede ser negativo.");
            }
            if (montoMax.HasValue && montoMax < 0)
            {
                ModelState.AddModelError(string.Empty, "El monto máximo no puede ser negativo.");
            }
            if (fechaInicio.HasValue && fechaFin.HasValue && fechaInicio > fechaFin)
            {
                ModelState.AddModelError(string.Empty, "La fecha de inicio no puede ser mayor a la fecha de fin.");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Errores = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                return View(new List<SolicitudCredito>());
            }

            var query = _context.Solicitudes
                .Where(s => s.ClienteId == cliente.Id)
                .AsQueryable();

            if (estado.HasValue)
                query = query.Where(s => s.Estado == estado.Value);

            if (montoMin.HasValue)
                query = query.Where(s => s.MontoSolicitado >= montoMin.Value);

            if (montoMax.HasValue)
                query = query.Where(s => s.MontoSolicitado <= montoMax.Value);

            if (fechaInicio.HasValue)
                query = query.Where(s => s.FechaSolicitud >= fechaInicio.Value);

            if (fechaFin.HasValue)
                query = query.Where(s => s.FechaSolicitud <= fechaFin.Value);

            var solicitudes = await query.OrderByDescending(s => s.FechaSolicitud).ToListAsync();

            ViewBag.Estado = estado;
            ViewBag.MontoMin = montoMin;
            ViewBag.MontoMax = montoMax;
            ViewBag.FechaInicio = fechaInicio?.ToString("yyyy-MM-dd");
            ViewBag.FechaFin = fechaFin?.ToString("yyyy-MM-dd");

            return View(solicitudes);
        }

        // GET: /Solicitudes/Detalle/5
        public async Task<IActionResult> Detalle(int id)
        {
            var userId = _userManager.GetUserId(User);
            var cliente = await _context.Clientes.FirstOrDefaultAsync(c => c.UsuarioId == userId);

            if (cliente == null) return NotFound();

            var solicitud = await _context.Solicitudes
                .Include(s => s.Cliente)
                .FirstOrDefaultAsync(s => s.Id == id && s.ClienteId == cliente.Id);

            if (solicitud == null) return NotFound();

            return View(solicitud);
        }
    }
}