using System.ComponentModel.DataAnnotations;

namespace EnEscenaMadrid.Models
{
    // Relación entre usuarios y eventos (favoritos, visitados, ratings)
    public class UserEvento
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty; // ID del usuario

        [Required]  
        public string EventoId { get; set; } = string.Empty; // ID del evento de Madrid

        [Required]
        public EstadoEvento Estado { get; set; } // Favorito, Visitado

        public int? Rating { get; set; } // Puntuación 1-5 estrellas (solo para visitados)

        public string? Notas { get; set; } // Comentarios personales opcionales

        public DateTime FechaAgregado { get; set; } = DateTime.Now; // Cuándo se agregó

        public DateTime? FechaVisita { get; set; } // Cuándo se marcó como visitado
    }

    // Estados posibles de un evento para un usuario
    public enum EstadoEvento
    {
        Favorito = 1,    // ❤️ "Quiero ir"
        Visitado = 2     // ✅ "Ya fui"
    }
}