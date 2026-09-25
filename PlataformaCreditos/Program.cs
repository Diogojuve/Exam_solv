using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PlataformaCreditos.Data;
using Microsoft.AspNetCore.Http;
using PlataformaCreditos.Hubs;



var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));
    var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConnectionString;
    options.InstanceName = "PlataformaCreditos:";
});

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseSession();

app.UseAuthentication(); 

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapHub<SolicitudesHub>("/hubs/solicitudes"); 

app.MapRazorPages()
   .WithStaticAssets();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    var db = services.GetRequiredService<ApplicationDbContext>();

    db.Database.Migrate();

    // 1. Crear rol Analista si no existe
    if (!await roleManager.RoleExistsAsync("Analista"))
    {
        await roleManager.CreateAsync(new IdentityRole("Analista"));
    }

    // 2. Crear usuario Analista si no existe
    var analistaEmail = "analista@creditos.com";
    var analista = await userManager.FindByEmailAsync(analistaEmail);
    if (analista == null)
    {
        analista = new IdentityUser
        {
            UserName = analistaEmail,
            Email = analistaEmail,
            EmailConfirmed = true // para que pueda iniciar sesión sin confirmar correo
        };
        await userManager.CreateAsync(analista, "Analista123!");
        await userManager.AddToRoleAsync(analista, "Analista");
    }

    // 3. Crear 2 usuarios clientes (para vincular a los Clientes)
    async Task<IdentityUser> CrearUsuarioSiNoExiste(string email, string password)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new IdentityUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true
            };
            await userManager.CreateAsync(user, password);
        }
        return user;
    }

    var usuarioCliente1 = await CrearUsuarioSiNoExiste("cliente1@creditos.com", "Cliente123!");
    var usuarioCliente2 = await CrearUsuarioSiNoExiste("cliente2@creditos.com", "Cliente123!");

    // 4. Crear 2 clientes si no existen
    if (!db.Clientes.Any())
    {
        var cliente1 = new PlataformaCreditos.Models.Cliente
        {
            UsuarioId = usuarioCliente1.Id,
            IngresosMensuales = 2000,
            Activo = true
        };

        var cliente2 = new PlataformaCreditos.Models.Cliente
        {
            UsuarioId = usuarioCliente2.Id,
            IngresosMensuales = 3500,
            Activo = true
        };

        db.Clientes.AddRange(cliente1, cliente2);
        await db.SaveChangesAsync();

        // 5. Crear 2 solicitudes: una Pendiente y una Aprobada
        db.Solicitudes.AddRange(
            new PlataformaCreditos.Models.SolicitudCredito
            {
                ClienteId = cliente1.Id,
                MontoSolicitado = 5000,
                FechaSolicitud = DateTime.Now,
                Estado = PlataformaCreditos.Models.EstadoSolicitud.Pendiente
            },
            new PlataformaCreditos.Models.SolicitudCredito
            {
                ClienteId = cliente2.Id,
                MontoSolicitado = 8000,
                FechaSolicitud = DateTime.Now.AddDays(-5),
                Estado = PlataformaCreditos.Models.EstadoSolicitud.Aprobado
            }
        );

        await db.SaveChangesAsync();
    }
}
// --- FIN SEED ---

app.Run();
