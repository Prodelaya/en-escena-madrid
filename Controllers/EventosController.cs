using Microsoft.AspNetCore.Mvc;
using System.Xml.Serialization;
using EnEscenaMadrid.Models;

namespace EnEscenaMadrid.Controllers
{
    public class EventosController : Controller
    {
        // HttpClient para hacer peticiones HTTP a APIs externas
        private readonly HttpClient _httpClient;
        
        // Configuration para leer datos del appsettings.json
        private readonly IConfiguration _configuration;

        // Constructor: C# inyecta automáticamente estas dependencias
        public EventosController(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        // Método que se ejecuta cuando alguien visita /Eventos
        public async Task<IActionResult> Index()
        {
            try
            {
                // 1. Leemos la URL de la API desde appsettings.json
                var apiUrl = _configuration["MadridApi:SalasUrl"];
                
                // 2. Hacemos la petición HTTP a la API de Madrid
                var xmlResponse = await _httpClient.GetStringAsync(apiUrl);
                
                // 3. Convertimos el XML en objetos C# usando nuestros modelos
                var salasResponse = DeserializarXml(xmlResponse);
                
                // 4. Enviamos los datos a la vista para mostrarlos
                return View(salasResponse);
            }
            catch (Exception ex)
            {
                // Si algo falla, mostramos el error en la vista
                ViewBag.Error = $"Error al obtener datos de Madrid: {ex.Message}";
                return View();
            }
        }

        // Método privado que convierte el XML en objetos C#
        private MadridSalasResponse DeserializarXml(string xmlContent)
        {
            // XmlSerializer sabe cómo convertir XML a objetos C# usando nuestros modelos
            var serializer = new XmlSerializer(typeof(MadridSalasResponse));
            
            // Creamos un "lector" de strings para procesar el XML
            using var reader = new StringReader(xmlContent);
            
            // Convertimos el XML a objetos C# y lo devolvemos
            return (MadridSalasResponse)serializer.Deserialize(reader);
        }

        private MadridSalasResponse? ObtenerRespuesta()
        {
            // Lógica para obtener la respuesta
        }
    }
}