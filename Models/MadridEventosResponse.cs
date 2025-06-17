using System.Xml.Serialization;

namespace EnEscenaMadrid.Models
{
    // Modelo principal para la respuesta XML de eventos de Madrid
    [XmlRoot("Contenidos")]
    public class MadridEventosResponse
    {
        [XmlElement("contenido")]
        public List<EventoContenido> Contenidos { get; set; } = new List<EventoContenido>();
    }

    // Evento individual dentro de la respuesta  
    public class EventoContenido
    {
        [XmlElement("tipo")]
        public string? Tipo { get; set; }

        [XmlElement("atributos")]
        public EventoAtributos? Atributos { get; set; }
    }

    // Contenedor de atributos del evento
    public class EventoAtributos
    {
        [XmlElement("atributo")]
        public List<EventoAtributo> ListaAtributos { get; set; } = new List<EventoAtributo>();
    }

    // Atributo individual (nombre-valor)
    public class EventoAtributo  
    {
        [XmlAttribute("nombre")]
        public string? Nombre { get; set; }

        [XmlText]
        public string? Valor { get; set; }

        // Para atributos anidados como LOCALIZACION
        [XmlElement("atributo")]
        public List<EventoAtributo>? SubAtributos { get; set; }
    }
}