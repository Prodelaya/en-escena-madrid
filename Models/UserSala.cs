using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace EnEscenaMadrid.Models
{
    // Esta clase representa la relación entre un usuario y una sala
    // Maneja tanto favoritos como salas visitadas
    public class UserSala
    {
        // Clave primaria compuesta: UserId + SalaId
        // Un usuario solo puede tener UNA relación por sala
        
        [Required]
        public string UserId { get; set; } = string.Empty; // FK a AspNetUsers
        
        [Required]
        public int SalaId { get; set; } // FK a Salas
        
        [Required]
        public EstadoSala Estado { get; set; } // Favorito o Visitada
        
        // Fecha cuando se agregó como favorito
        public DateTime FechaAñadido { get; set; } = DateTime.UtcNow;
        
        // Fecha cuando se marcó como visitada (solo si Estado = Visitada)
        public DateTime? FechaVisitado { get; set; }
        
        // Rating de 1-5 estrellas (solo si Estado = Visitada)
        [Range(1, 5)]
        public int? Rating { get; set; }
        
        // Notas personales del usuario
        [StringLength(500)]
        public string? Notas { get; set; }
        
        // Propiedades de navegación - EF Core las usa para joins automáticos
        public virtual IdentityUser User { get; set; } = null!;
        public virtual Sala Sala { get; set; } = null!;
    }
}
