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
        public async Task<IActionResult> Index(
            string? categoria = null,
            string? precio = null,
            string? distritos = null,
            string? tipos = null,
            bool mostrarTodos = false
        )
        {
            try
            {
                // 1. Obtener todos los eventos de la API de Madrid
                var eventosResponse = await ObtenerEventosDeMadrid();

                // 2. Convertir XML a lista de eventos procesados
                var eventosProcesados = ProcesarEventos(eventosResponse);
                // CONVERSIÓN DE PARÁMETROS DE FILTRADO
                // Los filtros llegan como strings separados por comas desde el frontend
                // Ejemplo: distritos = "Centro,Chamberí,Salamanca"
                // Los convertimos a listas para usar en nuestros métodos de filtrado

                List<string>? distritosLista = null;
                List<string>? tiposLista = null;

                // Convertir distritos: "Centro,Chamberí" → ["Centro", "Chamberí"]
                if (!string.IsNullOrEmpty(distritos))
                {
                    distritosLista = distritos
                        .Split(',')                    // Separar por comas
                        .Select(d => d.Trim())         // Quitar espacios extras
                        .Where(d => !string.IsNullOrEmpty(d))  // Quitar elementos vacíos
                        .ToList();
                }

                // Convertir tipos: igual proceso para tipos de eventos
                if (!string.IsNullOrEmpty(tipos))
                {
                    tiposLista = tipos
                        .Split(',')
                        .Select(t => t.Trim())
                        .Where(t => !string.IsNullOrEmpty(t))
                        .ToList();
                }
                // 3. Aplicar filtros según el contexto
                if (!string.IsNullOrEmpty(categoria))
                {
                    // PESTAÑAS: Mostrar todos los eventos de la categoría específica
                    eventosProcesados = FiltrarPorCategoria(eventosProcesados, categoria);
                    eventosProcesados = AplicarFiltros(eventosProcesados, precio, distritosLista, null);
                    
                    ViewBag.CategoriaActual = categoria;
                    ViewBag.TipoVista = "Categoría";
                }
                else if (mostrarTodos)
                {
                    // TODOS LOS EVENTOS: Mostrar lista completa sin agrupación temporal
                    eventosProcesados = AplicarFiltros(eventosProcesados, precio, distritosLista, tiposLista);
                    
                    ViewBag.CategoriaActual = "Todos los eventos";
                    ViewBag.TipoVista = "TodosEventos";
                }
                else
                {
                    // LANDING NETFLIX: Agrupar por categorías (próximos 7 días)
                    var eventosProximos = FiltrarProximosSieteDias(eventosProcesados);
                    var eventosFiltrados = AplicarFiltros(eventosProximos, precio, distritosLista, tiposLista);
                    var eventosPorCategoria = AgruparEventosFiltrados(eventosFiltrados);
                    
                    ViewBag.EventosPorCategoria = eventosPorCategoria;
                    ViewBag.CategoriaActual = "Esta semana por categorías";
                    ViewBag.TipoVista = "Netflix";
                    
                    // Para mantener compatibilidad, enviamos lista vacía
                    eventosProcesados = new List<Evento>();
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

            // Filtrar solo categorías que nos interesan y ordenar por fecha
            var eventosFiltrados = FiltrarSoloCategoriasValidas(eventos);
            return eventosFiltrados.OrderBy(e => e.FechaEvento).ToList();
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
            // === EXTRAER DATOS DE PRECIO Y DURACIÓN ===
            var precio = BuscarAtributo(atributos, "PRECIO");
            var eventoLargaDuracionStr = BuscarAtributo(atributos, "EVENTO-LARGA-DURACION");
            var diasSemana = BuscarAtributo(atributos, "DIAS-SEMANA");
            var fechaFinStr = BuscarAtributo(atributos, "FECHA-FIN-EVENTO");
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
            // === PROCESAR NUEVOS CAMPOS ===

            // 1. Procesar precio y necesidad de entrada
            evento.Precio = precio ?? (evento.EsGratuito ? "Gratuito" : "Consultar precio");
            evento.NecesitaEntrada = !evento.EsGratuito || 
                (precio?.Contains("descarga", StringComparison.OrdinalIgnoreCase) ?? false);

            // 2. Procesar evento de larga duración
            evento.EsEventoLargaDuracion = eventoLargaDuracionStr == "1";
            evento.DiasSemana = diasSemana;

            
            // 3. Procesar fecha fin (para eventos largos)
            if (!string.IsNullOrEmpty(fechaFinStr) && DateTime.TryParse(fechaFinStr, out DateTime fechaFin))
            {
                evento.FechaFin = fechaFin;
            }

            // 4. Crear fecha formateada inteligente
            evento.FechaFormateada = CrearFechaFormateada(evento);
            return evento;
        }

        // NUEVO: Filtro por precio
        private List<Evento> FiltrarPorPrecio(List<Evento> eventos, string? filtroPrecio)
        {
            if (string.IsNullOrEmpty(filtroPrecio) || filtroPrecio == "todos")
                return eventos;
                
            return filtroPrecio.ToLower() switch
            {
                "gratuito" => eventos.Where(e => e.EsGratuito).ToList(),
                "pago" => eventos.Where(e => !e.EsGratuito).ToList(),
                _ => eventos
            };
        }

        // NUEVO: Filtro por distritos múltiples
        private List<Evento> FiltrarPorDistritos(List<Evento> eventos, List<string>? distritosSeleccionados)
        {
            // Si no hay distritos seleccionados, devolver todos
            if (distritosSeleccionados == null || !distritosSeleccionados.Any())
                return eventos;
                
            return eventos.Where(e => distritosSeleccionados.Contains(e.Distrito, StringComparer.OrdinalIgnoreCase)).ToList();
        }

        // NUEVO: Filtro por tipos específicos (solo para landing "Esta semana")
        private List<Evento> FiltrarPorTipos(List<Evento> eventos, List<string>? tiposSeleccionados)
        {
            if (tiposSeleccionados == null || !tiposSeleccionados.Any())
                return eventos;
                    return eventos.Where(e => tiposSeleccionados.Contains(e.Tipo, StringComparer.OrdinalIgnoreCase)).ToList();
        }

        // MAESTRO: Aplica todos los filtros en secuencia
        private List<Evento> AplicarFiltros(List<Evento> eventos, 
            string? filtroPrecio = null,
            List<string>? distritosSeleccionados = null, 
            List<string>? tiposSeleccionados = null)
        {
            var eventosFiltrados = eventos;
            // Aplicar filtro de distritos
            if (distritosSeleccionados != null && distritosSeleccionados.Any())
            {
                eventosFiltrados = FiltrarPorDistritos(eventosFiltrados, distritosSeleccionados);
            }
            // Aplicar filtro de tipos (solo para landing "Esta semana")
            // (no se aplica en pestañas de categorías)
            if (tiposSeleccionados != null && tiposSeleccionados.Any())
            {
                eventosFiltrados = FiltrarPorTipos(eventosFiltrados, tiposSeleccionados);
            }
            // Aplicar filtro de precio
            if (!string.IsNullOrEmpty(filtroPrecio))
            {
                eventosFiltrados = FiltrarPorPrecio(eventosFiltrados, filtroPrecio);
            }   
            return eventosFiltrados;
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
        // Filtra eventos para mostrar solo los próximos 7 días (landing page)
        private List<Evento> FiltrarProximosSieteDias(List<Evento> eventos)
        {
            var hoy = DateTime.Today;
            var limiteSemanaSiguiente = hoy.AddDays(7);
            
            return eventos.Where(evento => 
                // INCLUIR: Eventos con fecha específica en los próximos 7 días
                !evento.EsEventoLargaDuracion && 
                evento.FechaEvento.Date >= hoy && 
                evento.FechaEvento.Date <= limiteSemanaSiguiente
            ).ToList();
        }
        
        // Filtra eventos para incluir solo las categorías que nos interesan
        private List<Evento> FiltrarSoloCategoriasValidas(List<Evento> eventos)
        {
            var tiposValidos = new[]
            {
                // Teatro
                "/contenido/actividades/TeatroPerformance",
                "/contenido/actividades/TeatroPerformance/MusicalCabaret",
                "/contenido/actividades/TeatroPerformance/ComediaMonologo",
                "/contenido/actividades/CircoMagia",
                
                // Cine
                "/contenido/actividades/CineActividadesAudiovisuales",
                
                // Exposiciones
                "/contenido/actividades/Exposiciones",
                "/contenido/actividades/ActividadesCalleArteUrbano",
                "/contenido/actividades/CursosTalleres/Arte",
                
                // Literatura
                "/contenido/actividades/RecitalesPresentacionesActosLiterarios",
                "/contenido/actividades/ClubesLectura",
                "/contenido/actividades/ConferenciasColoquios/Literatura",
                
                // Música
                "/contenido/actividades/Musica",
                "/contenido/actividades/Musica/JazzSoulFunkySwingReagge",
                "/contenido/actividades/Musica/RockPop",
                "/contenido/actividades/DanzaBaile",
                "/contenido/actividades/DanzaBaile/FolcloreEtnica",
                "/contenido/actividades/DanzaBaile/Flamenco",
                
                // Festivales
                "/contenido/actividades/Festivales",
                "/contenido/actividades/Fiestas",
                "/contenido/actividades/ProgramacionDestacadaAgendaCultura",
                
                // Infantil
                "/contenido/actividades/CuentacuentosTiteresMarionetas",
                "/contenido/actividades/Campamentos"
            };
            
            return eventos.Where(e => tiposValidos.Contains(e.Tipo)).ToList();
        }

        // NUEVO: Agrupa eventos ya filtrados por categorías (sin aplicar filtro temporal)
private Dictionary<string, List<Evento>> AgruparEventosFiltrados(List<Evento> eventosFiltrados)
{
    var eventosPorCategoria = new Dictionary<string, List<Evento>>();
    
    // Definir las mismas categorías que en el método original
    var categorias = new Dictionary<string, string[]>
    {
        ["🎭 TEATRO"] = new[] { 
            "/contenido/actividades/TeatroPerformance",
            "/contenido/actividades/TeatroPerformance/MusicalCabaret",
            "/contenido/actividades/TeatroPerformance/ComediaMonologo",
            "/contenido/actividades/CircoMagia"
        },
        ["🎬 CINE"] = new[] { 
            "/contenido/actividades/CineActividadesAudiovisuales"
        },
        ["🎨 EXPOSICIONES"] = new[] { 
            "/contenido/actividades/Exposiciones",
            "/contenido/actividades/ActividadesCalleArteUrbano"
        },
        ["📚 LITERATURA"] = new[] { 
            "/contenido/actividades/RecitalesPresentacionesActosLiterarios",
            "/contenido/actividades/ClubesLectura"
        },
        ["🎵 MÚSICA"] = new[] { 
            "/contenido/actividades/Musica",
            "/contenido/actividades/Musica/JazzSoulFunkySwingReagge",
            "/contenido/actividades/Musica/RockPop",
            "/contenido/actividades/DanzaBaile",
            "/contenido/actividades/DanzaBaile/FolcloreEtnica",
            "/contenido/actividades/DanzaBaile/Flamenco"
        },
        ["🎉 FESTIVALES"] = new[] { 
            "/contenido/actividades/Festivales",
            "/contenido/actividades/Fiestas",
            "/contenido/actividades/ProgramacionDestacadaAgendaCultura"
        }
    };
    
    // Agrupar eventos filtrados por categoría
    foreach (var categoria in categorias)
    {
        var eventosCategoria = eventosFiltrados
            .Where(e => categoria.Value.Contains(e.Tipo))
            .Take(10) // Máximo 10 por categoría para mantener diseño Netflix
            .ToList();
            
        // Solo agregar categorías que tengan eventos
        if (eventosCategoria.Any())
        {
            eventosPorCategoria[categoria.Key] = eventosCategoria;
        }
    }
    
    return eventosPorCategoria;
}

        // Crea texto de fecha inteligente según el tipo de evento
        private string CrearFechaFormateada(Evento evento)
        {
            var hoy = DateTime.Today;
            var fechaEvento = evento.FechaEvento.Date;

            // CASO 1: Eventos de larga duración (como Matadero para grupos)
            if (evento.EsEventoLargaDuracion)
            {
                if (!string.IsNullOrEmpty(evento.DiasSemana))
                {
                    return $"📅 {ConvertirDiasSemana(evento.DiasSemana)}";
                }

                if (evento.FechaFin.HasValue)
                {
                    return $"📅 Hasta {evento.FechaFin.Value:dd/MM/yyyy}";
                }

                return "📅 Actividad permanente";
            }

            // CASO 2: Eventos con fecha específica
            var diferenciaDias = (fechaEvento - hoy).Days;

            return diferenciaDias switch
            {
                0 => $"🔥 HOY {evento.HoraEvento}",
                1 => $"📅 MAÑANA {evento.HoraEvento}",
                >= 2 and <= 7 => $"📅 {fechaEvento:ddd dd/MM} {evento.HoraEvento}",
                > 7 => $"📅 {fechaEvento:dd/MM/yyyy} {evento.HoraEvento}",
                < 0 => $"⚠️ {fechaEvento:dd/MM/yyyy} {evento.HoraEvento}"
            };
        }

        // Convierte códigos de días a texto legible
        private string ConvertirDiasSemana(string diasCodigos)
        {
            var dias = diasCodigos.Split(',');
            var diasTexto = dias.Select(d => d.Trim() switch
            {
                "L" => "Lun",
                "M" => "Mar", 
                "X" => "Mié",
                "J" => "Jue",
                "V" => "Vie",
                "S" => "Sáb",
                "D" => "Dom",
                _ => d
            });
            
            return string.Join(", ", diasTexto);
        }
   // NUEVO: Método AJAX para filtros (devuelve solo contenido, no layout completo)
    [HttpGet]
    public async Task<IActionResult> FiltrarEventosAjax(
        string? categoria = null,
        string? precio = null,
        string? distritos = null,
        string? tipos = null)
    {
        // Reutilizar toda la lógica del Index()
        // pero devolver solo vista parcial para AJAX
        
        try
        {
            // Misma lógica que Index() - obtener y procesar eventos
            var eventosResponse = await ObtenerEventosDeMadrid();
            var eventosProcesados = ProcesarEventos(eventosResponse);
            
            // Misma lógica - convertir parámetros
            List<string>? distritosLista = null;
            List<string>? tiposLista = null;
            
            if (!string.IsNullOrEmpty(distritos))
            {
                distritosLista = distritos.Split(',').Select(d => d.Trim()).Where(d => !string.IsNullOrEmpty(d)).ToList();
            }
            
            if (!string.IsNullOrEmpty(tipos))
            {
                tiposLista = tipos.Split(',').Select(t => t.Trim()).Where(t => !string.IsNullOrEmpty(t)).ToList();
            }
            
            // Aplicar filtros según contexto (misma lógica que Index)
            if (!string.IsNullOrEmpty(categoria))
            {
                eventosProcesados = FiltrarPorCategoria(eventosProcesados, categoria);
                eventosProcesados = AplicarFiltros(eventosProcesados, precio, distritosLista, null);
                
                ViewBag.CategoriaActual = categoria;
                ViewBag.TipoVista = "Categoría";
                ViewBag.TotalEventos = eventosProcesados.Count;
                
                // DIFERENCIA: Devolver vista parcial para AJAX
                return PartialView("_EventosLista", eventosProcesados);
            }
            else
            {
                var eventosProximos = FiltrarProximosSieteDias(eventosProcesados);
                var eventosFiltrados = AplicarFiltros(eventosProximos, precio, distritosLista, tiposLista);
                var eventosPorCategoria = AgruparEventosFiltrados(eventosFiltrados);
                
                ViewBag.EventosPorCategoria = eventosPorCategoria;
                ViewBag.CategoriaActual = "Esta semana por categorías";
                ViewBag.TipoVista = "Netflix";
                
                // DIFERENCIA: Devolver vista parcial para AJAX
                return PartialView("_EventosNetflix", eventosPorCategoria);
            }
        }
        catch (Exception ex)
        {
            return Json(new { error = ex.Message });
        }
    }
        }
}// Fin del controlador EventosController.cs
// Este controlador maneja la lógica de negocio para obtener, filtrar y procesar eventos en Madrid.     