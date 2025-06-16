using System.ComponentModel.DataAnnotations;

namespace EnEscenaMadrid.Models
{
    // Esta clase representa una sala de teatro, cine, auditorio, etc.
    public class Sala
    {
        // Clave primaria - EF Core lo detecta automáticamente por convención
        public int Id { get; set; }
        
        // Campos obligatorios - [Required] hace que no puedan ser null
        [Required]
        [StringLength(200)] // Máximo 200 caracteres
        public string Nombre { get; set; } = string.Empty;
        
        [Required]
        [StringLength(300)]
        public string Direccion { get; set; } = string.Empty;
        
        [Required]
        [StringLength(100)]
        public string Municipio { get; set; } = string.Empty;
        
        [Required]
        [StringLength(50)]
        public string TipoSala { get; set; } = string.Empty; // Teatro, Cine, Auditorio, etc.
        
        // Campos opcionales - pueden ser null
        [StringLength(20)]
        public string? Telefono { get; set; }
        
        [StringLength(100)]
        public string? Email { get; set; }
        
        // Fecha de cuando se agregó a nuestra BD
        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
        
        // Navegación: Una sala puede estar en muchas relaciones UserSala
        public virtual ICollection<UserSala> UserSalas { get; set; } = new List<UserSala>();
    }
}