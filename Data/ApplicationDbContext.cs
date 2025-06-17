using Microsoft.EntityFrameworkCore;
using EnEscenaMadrid.Models;

namespace EnEscenaMadrid.Data
{
    // Contexto de base de datos para En Escena Madrid
    // Maneja todas las tablas relacionadas con usuarios y eventos
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        // Tabla de eventos favoritos y visitados por usuario
        public DbSet<UserEvento> UserEventos { get; set; }

        // Configuración de relaciones entre tablas
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configurar tabla UserEventos
            modelBuilder.Entity<UserEvento>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UserId).IsRequired();
                entity.Property(e => e.EventoId).IsRequired();
                entity.Property(e => e.Estado).IsRequired();
                entity.Property(e => e.FechaAgregado).IsRequired();
                
                // Índice para búsquedas rápidas por usuario
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => new { e.UserId, e.EventoId }).IsUnique();
            });
        }
    }
}