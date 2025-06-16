using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using EnEscenaMadrid.Models;

namespace EnEscenaMadrid.Data
{
    // DbContext principal de la aplicación
    // Hereda de IdentityDbContext para tener usuarios automáticamente
    public class ApplicationDbContext : IdentityDbContext
    {
        // Constructor que recibe opciones de configuración
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }
        
        // DbSet = Tabla en la base de datos
        // Cada DbSet se convierte en una tabla SQL
        public DbSet<Sala> Salas { get; set; }
        public DbSet<UserSala> UserSalas { get; set; }
        
        // OnModelCreating: Aquí configuramos las relaciones y constraints
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Llamar al método base para que Identity funcione
            base.OnModelCreating(modelBuilder);
            
            // Configurar la tabla UserSala
            modelBuilder.Entity<UserSala>(entity =>
            {
                // Clave primaria compuesta: UserId + SalaId
                entity.HasKey(us => new { us.UserId, us.SalaId });
                
                // Relación: UserSala -> User (muchos a uno)
                entity.HasOne(us => us.User)
                      .WithMany() // Un usuario puede tener muchas UserSalas
                      .HasForeignKey(us => us.UserId)
                      .OnDelete(DeleteBehavior.Cascade); // Si borro usuario, borro sus relaciones
                
                // Relación: UserSala -> Sala (muchos a uno)
                entity.HasOne(us => us.Sala)
                      .WithMany(s => s.UserSalas) // Una sala puede estar en muchas UserSalas
                      .HasForeignKey(us => us.SalaId)
                      .OnDelete(DeleteBehavior.Cascade); // Si borro sala, borro sus relaciones
            });
        }
    }
}