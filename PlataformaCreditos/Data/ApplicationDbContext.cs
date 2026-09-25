using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PlataformaCreditos.Models;

namespace PlataformaCreditos.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext(options)
{
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<SolicitudCredito> Solicitudes => Set<SolicitudCredito>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Un cliente solo puede tener UNA solicitud en estado Pendiente (0 = Pendiente en el enum).
        builder.Entity<SolicitudCredito>()
            .HasIndex(s => new { s.ClienteId, s.Estado })
            .IsUnique()
            .HasFilter("\"Estado\" = 0");

        builder.Entity<SolicitudCredito>()
            .Property(s => s.MontoSolicitado)
            .HasColumnType("decimal(18,2)");

        builder.Entity<Cliente>()
            .Property(c => c.IngresosMensuales)
            .HasColumnType("decimal(18,2)");

        builder.Entity<Cliente>()
            .HasOne<Microsoft.AspNetCore.Identity.IdentityUser>()
            .WithMany()
            .HasForeignKey(c => c.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}