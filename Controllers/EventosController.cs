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

        // Página principal de eventos - muestra todas las categorías
        public async Task<IActionResult> Index(string? categoria = null)
        {
            try
            {
                // 1. Obtener todos los eventos de la API de Madrid
                var eventosResponse = await ObtenerEventosDeMadrid();
                
                // 2. Convertir XML a lista de eventos procesados
                var eventosProcesados = ProcesarEventos(eventosResponse);
                
                // 3. Filtrar por categoría si se especifica
                if (!string.IsNullOrEmpty(categoria))
                {
                    eventosProcesados = FiltrarPorCategoria(eventosProcesados, categoria);
                    ViewBag.CategoriaActual = categoria;
                }
                
                // 4. Pasar datos a la vista
                ViewBag.TotalEventos = eventosProcesados.Count;
                return View(eventosProcesados);
            }
            catch (Exception ex)
            {
                // Si algo falla, mostramos el error en la vista
                ViewBag.Error = $"Error al obtener eventos de Madrid: {ex.Message}";
                return View(new List<Evento>());
            }
        }

        // Obtener eventos por categoría específica (para navegación por pestañas)
        public async Task<IActionResult> Categoria(string categoria)
        {
            // Redirigir al Index con parámetro de categoría
            return RedirectToAction("Index", new { categoria = categoria });
        }

        // MÉTODOS PRIVADOS - Lógica de procesamiento
        
        // Obtiene y deserializa los eventos de la API de Madrid
        private async Task<MadridEventosResponse> ObtenerEventosDeMadrid()
        {
            // Leer URL de la API desde configuración
            var apiUrl = _configuration["MadridApi:EventosUrl"];
            
            // Hacer petición HTTP a la API
            var xmlResponse = await _httpClient.GetStringAsync(apiUrl);
            
            // Convertir XML a objetos C#
            return DeserializarXml(xmlResponse);
        }

        // Convierte el XML de Madrid en objetos C# usando XmlSerializer
        private MadridEventosResponse DeserializarXml(string xmlContent)
        {
            var serializer = new XmlSerializer(typeof(MadridEventosResponse));
            using var reader = new StringReader(xmlContent);
            return (MadridEventosResponse)serializer.Deserialize(reader)!;
        }

        // Convierte los datos XML en objetos Evento más fáciles de manejar
        private List<Evento> ProcesarEventos(MadridEventosResponse response)
        {
            var eventos = new List<Evento>();

            foreach (var contenido in response.Contenidos)
            {
                if (contenido.Atributos?.ListaAtributos == null) continue;

                try
                {
                    var evento = ExtraerDatosEvento(contenido.Atributos.ListaAtributos);
                    if (evento != null)
                    {
                        eventos.Add(evento);
                    }
                }
                catch (Exception ex)
                {
                    // Log error pero continúa procesando otros eventos
                    Console.WriteLine($"Error procesando evento: {ex.Message}");
                }
            }

            // Ordenar por fecha (eventos más próximos primero)
            return eventos.OrderBy(e => e.FechaEvento).ToList();
        }

        // Extrae los datos importantes de un evento individual
        private Evento? ExtraerDatosEvento(List<EventoAtributo> atributos)
        {
            // Buscar atributos principales
            var id = BuscarAtributo(atributos, "ID-EVENTO");
            var titulo = BuscarAtributo(atributos, "TITULO");
            var tipo = BuscarAtributo(atributos, "TIPO");
            var fechaStr = BuscarAtributo(atributos, "FECHA-EVENTO");
            var hora = BuscarAtributo(atributos, "HORA-EVENTO");
            var gratuitoStr = BuscarAtributo(atributos, "GRATUITO");
            var contentUrl = BuscarAtributo(atributos, "CONTENT-URL");
            var descripcion = BuscarAtributo(atributos, "DESCRIPCION");

            // Validar datos mínimos requeridos
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(titulo) || string.IsNullOrEmpty(fechaStr))
                return null;

            // Procesar ubicación (atributo anidado)
            var ubicacion = ExtraerUbicacion(atributos);

            // Crear objeto Evento
            var evento = new Evento
            {
                Id = id,
                Titulo = titulo,
                Tipo = tipo ?? "",
                HoraEvento = hora ?? "",
                EsGratuito = gratuitoStr == "1",
                ContentUrl = contentUrl ?? "",
                Descripcion = descripcion,
                NombreInstalacion = ubicacion.Nombre,
                DireccionCompleta = ubicacion.Direccion,
                Distrito = ubicacion.Distrito,
                Latitud = ubicacion.Latitud,
                Longitud = ubicacion.Longitud
            };

            // Convertir fecha
            if (DateTime.TryParse(fechaStr, out DateTime fecha))
            {
                evento.FechaEvento = fecha;
            }
            else
            {
                return null; // Si no podemos parsear la fecha, descartar evento
            }

            return evento;
        }

        // Busca un atributo específico por nombre
        private string? BuscarAtributo(List<EventoAtributo> atributos, string nombre)
        {
            return atributos.FirstOrDefault(a => a.Nombre == nombre)?.Valor;
        }

        // Extrae información de ubicación (atributo anidado LOCALIZACION)
        private (string Nombre, string Direccion, string Distrito, double? Latitud, double? Longitud) ExtraerUbicacion(List<EventoAtributo> atributos)
        {
            var localizacion = atributos.FirstOrDefault(a => a.Nombre == "LOCALIZACION");
            if (localizacion?.SubAtributos == null)
                return ("", "", "", null, null);

            var nombre = BuscarAtributo(localizacion.SubAtributos, "NOMBRE-INSTALACION") ?? "";
            var direccion = BuscarAtributo(localizacion.SubAtributos, "DIRECCION-INSTALACION") ?? "";
            var distrito = BuscarAtributo(localizacion.SubAtributos, "DISTRITO") ?? "";
            
            var latStr = BuscarAtributo(localizacion.SubAtributos, "LATITUD");
            var lonStr = BuscarAtributo(localizacion.SubAtributos, "LONGITUD");
            
            double? latitud = double.TryParse(latStr, out double lat) ? lat : null;
            double? longitud = double.TryParse(lonStr, out double lon) ? lon : null;

            return (nombre, direccion, distrito, latitud, longitud);
        }

        // Filtra eventos por categoría según nuestras pestañas definidas
        private List<Evento> FiltrarPorCategoria(List<Evento> eventos, string categoria)
        {
            var tiposFiltro = categoria.ToLower() switch
            {
                "teatro" => new[] { 
                    "/contenido/actividades/TeatroPerformance",
                    "/contenido/actividades/TeatroPerformance/MusicalCabaret", 
                    "/contenido/actividades/TeatroPerformance/ComediaMonologo",
                    "/contenido/actividades/CircoMagia"
                },
                "cine" => new[] { 
                    "/contenido/actividades/CineActividadesAudiovisuales" 
                },
                "exposiciones" => new[] { 
                    "/contenido/actividades/Exposiciones",
                    "/contenido/actividades/ActividadesCalleArteUrbano",
                    "/contenido/actividades/CursosTalleres/Arte"
                },
                "literatura" => new[] { 
                    "/contenido/actividades/RecitalesPresentacionesActosLiterarios",
                    "/contenido/actividades/ClubesLectura",
                    "/contenido/actividades/ConferenciasColoquios/Literatura"
                },
                "musica" => new[] { 
                    "/contenido/actividades/Musica",
                    "/contenido/actividades/Musica/JazzSoulFunkySwingReagge",
                    "/contenido/actividades/Musica/RockPop",
                    "/contenido/actividades/DanzaBaile",
                    "/contenido/actividades/DanzaBaile/FolcloreEtnica",
                    "/contenido/actividades/DanzaBaile/Flamenco"
                },
                "festivales" => new[] { 
                    "/contenido/actividades/Festivales",
                    "/contenido/actividades/Fiestas",
                    "/contenido/actividades/ProgramacionDestacadaAgendaCultura"
                },
                "infantil" => new[] { 
                    "/contenido/actividades/CuentacuentosTiteresMarionetas",
                    "/contenido/actividades/Campamentos"
                },
                _ => Array.Empty<string>()
            };

            if (tiposFiltro.Length == 0)
                return eventos;

            return eventos.Where(e => tiposFiltro.Contains(e.Tipo)).ToList();
        }
    }
}