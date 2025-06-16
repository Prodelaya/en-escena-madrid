// Importamos la librería necesaria para trabajar con XML
using System.Xml.Serialization;

namespace EnEscenaMadrid.Models
{
    // Esta clase representa la estructura completa del XML que nos devuelve Madrid
    // [XmlRoot] le dice a C# que esta clase corresponde al elemento raíz "<Contenidos>"
    [XmlRoot("Contenidos")]
    public class MadridSalasResponse
    {
        // Información general del dataset (nombre, descripción, etc.)
        // [XmlElement] mapea esta propiedad con el elemento XML <infoDataset>
        [XmlElement("infoDataset")]
        public InfoDataset? InfoDataset { get; set; }

        // Lista de todas las salas/teatros que nos devuelve la API
        // Cada elemento <contenido> del XML se convierte en un objeto Contenido
        [XmlElement("contenido")]
        public List<Contenido> Contenidos { get; set; } = new List<Contenido>();
    }

    // Clase que representa la información general del dataset
    public class InfoDataset
    {
        // Nombre del dataset: "Salas de espectáculos artísticos..."
        [XmlElement("nombre")]
        public string? Nombre { get; set; }

        // ID único del dataset: "208862-7650046"
        [XmlElement("id")]
        public string? Id { get; set; }
    }

    // Clase que representa cada sala/teatro individual
    public class Contenido
    {
        // Tipo de contenido: "EntidadesYOrganismos"
        [XmlElement("tipo")]
        public string? Tipo { get; set; }

        // Todos los datos específicos de la sala (nombre, dirección, horarios, etc.)
        [XmlElement("atributos")]
        public Atributos? Atributos { get; set; }
    }

    // Clase que contiene todos los atributos/propiedades de una sala
    public class Atributos
    {
        // Idioma de los datos: "es" (español)
        [XmlAttribute("idioma")]
        public string? Idioma { get; set; }

        // Lista de todos los atributos de la sala
        // Cada <atributo> del XML se convierte en un objeto Atributo
        // Ejemplo: nombre="NOMBRE", valor="Centro Danza Matadero"
        [XmlElement("atributo")]
        public List<Atributo> ListaAtributos { get; set; } = new List<Atributo>();
    }

    // Clase que representa cada atributo individual de una sala
    // Un atributo es un dato específico como el nombre, la descripción, el horario, etc.
    public class Atributo
    {
        // El "nombre" del atributo (ID-ENTIDAD, NOMBRE, DESCRIPCION-ENTIDAD, etc.)
        [XmlAttribute("nombre")]
        public string? Nombre { get; set; }

        // El "valor" del atributo (el contenido real del dato)
        // Ejemplo: si nombre="NOMBRE", entonces Valor="Centro Danza Matadero"
        [XmlText]
        public string? Valor { get; set; }

        // Algunos atributos tienen sub-elementos (como LOCALIZACION)
        // LOCALIZACION contiene: NOMBRE-VIA, CLASE-VIAL, NUM, DISTRITO, etc.
        [XmlElement("atributo")]
        public List<Atributo>? SubAtributos { get; set; }
    }
}