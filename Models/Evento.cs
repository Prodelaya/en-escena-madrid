namespace EnEscenaMadrid.Models
{
    // Modelo simple para representar un evento procesado
    // (datos extraídos de la API de Madrid)
    public class Evento
    {
        public string Id { get; set; } = string.Empty;
        public string Titulo { get; set; } = string.Empty;
        public string Tipo { get; set; } = string.Empty;
        public DateTime FechaEvento { get; set; }
        public string HoraEvento { get; set; } = string.Empty;
        public bool EsGratuito { get; set; }
        public string NombreInstalacion { get; set; } = string.Empty;
        public string DireccionCompleta { get; set; } = string.Empty;
        public string Distrito { get; set; } = string.Empty;
        public string ContentUrl { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public double? Latitud { get; set; }
        public double? Longitud { get; set; }
        // === NUEVOS CAMPOS PARA PRECIOS Y ENTRADAS ===
        public string? Precio { get; set; }
        public bool NecesitaEntrada { get; set; }

        // === NUEVOS CAMPOS PARA EVENTOS DE LARGA DURACIÓN ===
        public bool EsEventoLargaDuracion { get; set; }
        public string? DiasSemana { get; set; }
        public DateTime? FechaFin { get; set; }

        // === CAMPO HELPER PARA MOSTRAR FECHAS BONITAS ===
        public string FechaFormateada { get; set; } = string.Empty;
    }
}