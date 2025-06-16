using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using EnEscenaMadrid.Data;

var builder = WebApplication.CreateBuilder(args);

// CONFIGURAR BASE DE DATOS Y ENTITY FRAMEWORK
// Agregar DbContext con SQLite
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// CONFIGURAR SISTEMA DE USUARIOS (ASP.NET CORE IDENTITY)
builder.Services.AddDefaultIdentity<IdentityUser>(options => 
{
    // Configuración de contraseñas (relajada para desarrollo)
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 4; // Mínimo 4 caracteres para testing
    
    // Configuración de email
    options.SignIn.RequireConfirmedEmail = false; // No requerir confirmación de email
})
.AddEntityFrameworkStores<ApplicationDbContext>();

// Add services to the container.
builder.Services.AddControllersWithViews();

// Añadir HttpClient para consumir APIs externas
builder.Services.AddHttpClient();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// MIDDLEWARE DE AUTENTICACIÓN (ORDEN IMPORTANTE)
app.UseAuthentication(); // ¿Quién eres? (identificar usuario)
app.UseAuthorization();  // ¿Qué puedes hacer? (permisos)

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Mapear rutas de Identity (login, register, etc.)
app.MapRazorPages();

app.Run();