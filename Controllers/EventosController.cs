using Microsoft.AspNetCore.Mvc;

namespace EnEscenaMadrid.Controllers
{
    public class EventosController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public EventosController(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public async Task<IActionResult> Index()
        {
            // ✅ BIEN - Leer API key desde configuración
            var apiKey = _configuration["TicketmasterApi:ApiKey"];
            var baseUrl = _configuration["TicketmasterApi:BaseUrl"];
           
            var url = $"{baseUrl}/events.json?apikey={apiKey}&city=Madrid&countryCode=ES&keyword=teatro&size=10";

               
            try
            {
                // Hacer llamada a la API
                var response = await _httpClient.GetStringAsync(url);
               
                // Por ahora, mostrar el JSON crudo en pantalla
                ViewBag.JsonResponse = response;
               
                return View();
            }
            catch (Exception ex)
            {
                ViewBag.Error = $"Error: {ex.Message}";
                return View();
            }
        }
    }
}