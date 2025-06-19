using Microsoft.AspNetCore.Mvc;
using System.Xml.Serialization;
using EnEscenaMadrid.Models;

namespace EnEscenaMadrid.Controllers
{
    /// <summary>
    /// Controlador principal para gestionar eventos culturales de Madrid
    /// Maneja la obtención de datos desde la API externa, filtrado y presentación
    /// </summary>
    public class EventosController : Controller
    {
        #region Propiedades y Constructor

        // HttpClient para realizar peticiones HTTP a APIs externas
        private readonly HttpClient _httpClient;

        // Configuration para leer configuraciones del appsettings.json
        private readonly IConfiguration _configuration;

        /// <summary>
        /// Constructor: ASP.NET Core inyecta automáticamente las dependencias
        /// </summary>
        /// <param name="httpClient">Cliente HTTP para llamadas a APIs</param>
        /// <param name="configuration">Configuración de la aplicación</param>
        public EventosController(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        #endregion

        #region Métodos Públicos (Actions)

        /// <summary>
        /// Página principal de eventos - Punto de entrada principal
        /// Maneja diferentes vistas: Netflix (landing), Categorías específicas, Todos los eventos
        /// </summary>
        /// <param name="categoria">Categoría específica a mostrar (teatro, música, etc.)</param>
        /// <param name="precio">Filtro de precio (gratuito, pago, todos)</param>
        /// <param name="distritos">Lista de distritos separados por comas</param>
        /// <param name="tipos">Lista de tipos de eventos separados por comas</param>
        /// <param name="mostrarTodos">Si true, muestra todos los eventos sin filtro temporal</param>
        /// <returns>Vista con los eventos filtrados</returns>
        public async Task<IActionResult> Index(
            string? categoria = null,
            string? precio = null,
            string? distritos = null,
            string? tipos = null,
            bool mostrarTodos = false)
        {
            try
            {
                // PASO 1: Obtener eventos de la API externa de Madrid
                var eventosResponse = await ObtenerEventosDeMadrid();
                var eventosProcesados = ProcesarEventos(eventosResponse);

                // PASO 2: Convertir parámetros de string a listas
                var distritosLista = ConvertirStringALista(distritos);
                var tiposLista = ConvertirStringALista(tipos);

                // PASO 3: Aplicar filtros según el contexto de la petición
                if (!string.IsNullOrEmpty(categoria))
                {
                    // CONTEXTO: Pestaña específica (Teatro, Música, etc.)
                    eventosProcesados = FiltrarPorCategoria(eventosProcesados, categoria);
                    eventosProcesados = AplicarFiltros(eventosProcesados, precio, distritosLista);
                    
                    ConfigurarViewBagParaCategoria(categoria, eventosProcesados.Count);
                }
                else if (mostrarTodos)
                {
                    // CONTEXTO: Todos los eventos sin límite temporal
                    eventosProcesados = AplicarFiltros(eventosProcesados, precio, distritosLista, tiposLista);
                    
                    ConfigurarViewBagParaTodos(eventosProcesados.Count);
                }
                else
                {
                    // CONTEXTO: Landing Netflix (próximos 7 días agrupados)
                    var eventosProximos = FiltrarProximosSieteDias(eventosProcesados);
                    var eventosFiltrados = AplicarFiltros(eventosProximos, precio, distritosLista, tiposLista);
                    var eventosPorCategoria = AgruparEventosFiltrados(eventosFiltrados);
                    
                    ConfigurarViewBagParaNetflix(eventosPorCategoria);
                    eventosProcesados = new List<Evento>(); // Netflix no usa lista directa
                }

                return View(eventosProcesados);
            }
            catch (Exception ex)
            {
                // Manejo de errores: mostrar mensaje amigable al usuario
                return ManejarError(ex.Message);
            }
        }

        /// <summary>
        /// Endpoint AJAX para filtros dinámicos sin recarga de página
        /// Usado por el menú lateral de filtros para actualizar contenido
        /// </summary>
        /// <param name="fecha">Filtro temporal: hoy, semana, todos</param>
        /// <param name="categorias">Lista de categorías separadas por comas</param>
        /// <param name="precio">Filtro de precio</param>
        /// <param name="distritos">Lista de distritos separados por comas</param>
        /// <returns>Vista parcial con eventos filtrados</returns>
        [HttpGet]
        public async Task<IActionResult> FiltrarEventosAjax(
            string? fecha = null,
            string? categorias = null,
            string? precio = null,
            string? distritos = null)
        {
            try
            {
                // PASO 1: Obtener y procesar eventos base
                var eventosResponse = await ObtenerEventosDeMadrid();
                var eventosProcesados = ProcesarEventos(eventosResponse);
                
                // PASO 2: Convertir parámetros string a listas
                var distritosLista = ConvertirStringALista(distritos);
                var categoriasLista = ConvertirStringALista(categorias);

                // PASO 3: Aplicar filtros según fecha seleccionada
                eventosProcesados = AplicarFiltroTemporal(eventosProcesados, fecha);
                eventosProcesados = AplicarFiltros(eventosProcesados, precio, distritosLista, categoriasLista);

                // PASO 4: Configurar ViewBag y devolver vista parcial
                ConfigurarViewBagParaAjax(fecha, eventosProcesados.Count);
                return PartialView("_EventosCards", eventosProcesados);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Redirección legacy para navegación por categorías
        /// </summary>
        public async Task<IActionResult> Categoria(string categoria)
        {
            return RedirectToAction("Index", new { categoria = categoria });
        }

        #endregion

        #region Métodos de Obtención y Procesamiento de Datos

        /// <summary>
        /// Obtiene eventos desde la API externa de Datos Abiertos de Madrid
        /// </summary>
        /// <returns>Respuesta deserializada del XML de Madrid</returns>
        private async Task<MadridEventosResponse> ObtenerEventosDeMadrid()
        {
            var apiUrl = _configuration["MadridApi:EventosUrl"] 
                ?? throw new InvalidOperationException("URL de API no configurada");

            var xmlResponse = await _httpClient.GetStringAsync(apiUrl);
            return DeserializarXml(xmlResponse);
        }

        /// <summary>
        /// Convierte el XML de la API de Madrid en objetos C#
        /// </summary>
        /// <param name="xmlContent">Contenido XML crudo</param>
        /// <returns>Objeto MadridEventosResponse deserializado</returns>
        private MadridEventosResponse DeserializarXml(string xmlContent)
        {
            var serializer = new XmlSerializer(typeof(MadridEventosResponse));
            using var reader = new StringReader(xmlContent);
            return (MadridEventosResponse)serializer.Deserialize(reader)!;
        }

        /// <summary>
        /// Procesa la respuesta XML y convierte en lista de eventos utilizables
        /// Filtra eventos válidos y los ordena por fecha
        /// </summary>
        /// <param name="response">Respuesta de la API de Madrid</param>
        /// <returns>Lista de eventos procesados y ordenados</returns>
        private List<Evento> ProcesarEventos(MadridEventosResponse response)
        {
            var eventos = new List<Evento>();

            // Procesar cada elemento del XML
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
                    // Log del error pero continúa procesando otros eventos
                    Console.WriteLine($"Error procesando evento: {ex.Message}");
                }
            }

            // Filtrar solo categorías válidas y ordenar por fecha
            var eventosFiltrados = FiltrarSoloCategoriasValidas(eventos);
            return eventosFiltrados.OrderBy(e => e.FechaEvento).ToList();
        }

        /// <summary>
        /// Extrae y procesa los datos de un evento individual desde el XML
        /// </summary>
        /// <param name="atributos">Lista de atributos del evento en XML</param>
        /// <returns>Objeto Evento procesado o null si no es válido</returns>
        private Evento? ExtraerDatosEvento(List<EventoAtributo> atributos)
        {
            // Extraer atributos básicos del evento
            var id = BuscarAtributo(atributos, "ID-EVENTO");
            var titulo = BuscarAtributo(atributos, "TITULO");
            var tipo = BuscarAtributo(atributos, "TIPO");
            var fechaStr = BuscarAtributo(atributos, "FECHA-EVENTO");
            var hora = BuscarAtributo(atributos, "HORA-EVENTO");
            var gratuitoStr = BuscarAtributo(atributos, "GRATUITO");
            var contentUrl = BuscarAtributo(atributos, "CONTENT-URL");
            var descripcion = BuscarAtributo(atributos, "DESCRIPCION");

            // Extraer datos adicionales
            var precio = BuscarAtributo(atributos, "PRECIO");
            var eventoLargaDuracionStr = BuscarAtributo(atributos, "EVENTO-LARGA-DURACION");
            var diasSemana = BuscarAtributo(atributos, "DIAS-SEMANA");
            var fechaFinStr = BuscarAtributo(atributos, "FECHA-FIN-EVENTO");

            // Validación de datos mínimos requeridos
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(titulo) || string.IsNullOrEmpty(fechaStr))
                return null;

            // Procesar ubicación
            var ubicacion = ExtraerUbicacion(atributos);

            // Crear y configurar objeto Evento
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

            // Procesar fecha del evento
            if (!DateTime.TryParse(fechaStr, out DateTime fecha))
                return null;
            
            evento.FechaEvento = fecha;

            // Procesar campos adicionales
            ProcesarCamposAdicionales(evento, precio, eventoLargaDuracionStr, diasSemana, fechaFinStr);

            return evento;
        }

        #endregion

        #region Métodos de Filtrado

        /// <summary>
        /// Aplica filtro temporal según la opción seleccionada
        /// </summary>
        /// <param name="eventos">Lista de eventos base</param>
        /// <param name="filtroFecha">Tipo de filtro: hoy, semana, todos</param>
        /// <returns>Eventos filtrados por fecha</returns>
        private List<Evento> AplicarFiltroTemporal(List<Evento> eventos, string? filtroFecha)
        {
            return filtroFecha switch
            {
                "hoy" => FiltrarEventosHoy(eventos),
                "semana" => FiltrarProximosSieteDias(eventos),
                "todos" => eventos, // Sin filtro temporal
                _ => FiltrarProximosSieteDias(eventos) // Default: esta semana
            };
        }

        /// <summary>
        /// Método maestro que aplica todos los filtros en secuencia
        /// </summary>
        /// <param name="eventos">Lista de eventos base</param>
        /// <param name="filtroPrecio">Filtro de precio opcional</param>
        /// <param name="distritosSeleccionados">Lista de distritos seleccionados</param>
        /// <param name="categoriasSeleccionadas">Lista de categorías seleccionadas</param>
        /// <returns>Lista de eventos filtrados</returns>
        private List<Evento> AplicarFiltros(
            List<Evento> eventos, 
            string? filtroPrecio = null,
            List<string>? distritosSeleccionados = null, 
            List<string>? categoriasSeleccionadas = null)
        {
            var eventosFiltrados = eventos;

            // Aplicar filtro de distritos
            if (distritosSeleccionados != null && distritosSeleccionados.Any())
            {
                eventosFiltrados = FiltrarPorDistritos(eventosFiltrados, distritosSeleccionados);
            }

            // Aplicar filtro de categorías
            if (categoriasSeleccionadas != null && categoriasSeleccionadas.Any())
            {
                eventosFiltrados = FiltrarPorMultiplesCategorias(eventosFiltrados, categoriasSeleccionadas);
            }

            // Aplicar filtro de precio
            if (!string.IsNullOrEmpty(filtroPrecio))
            {
                eventosFiltrados = FiltrarPorPrecio(eventosFiltrados, filtroPrecio);
            }

            return eventosFiltrados;
        }

        /// <summary>
        /// Filtra eventos por precio (gratuito, pago, todos)
        /// </summary>
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

        /// <summary>
        /// Filtra eventos por múltiples distritos seleccionados
        /// </summary>
        private List<Evento> FiltrarPorDistritos(List<Evento> eventos, List<string>? distritosSeleccionados)
        {
            if (distritosSeleccionados == null || !distritosSeleccionados.Any())
                return eventos;
                
            return eventos.Where(e => 
                distritosSeleccionados.Contains(e.Distrito, StringComparer.OrdinalIgnoreCase)
            ).ToList();
        }

        /// <summary>
        /// Filtra eventos por múltiples categorías seleccionadas
        /// </summary>
        private List<Evento> FiltrarPorMultiplesCategorias(List<Evento> eventos, List<string> categoriasSeleccionadas)
        {
            var eventosFiltrados = new List<Evento>();
            
            foreach (var categoria in categoriasSeleccionadas)
            {
                var eventosCategoria = FiltrarPorCategoria(eventos, categoria);
                eventosFiltrados.AddRange(eventosCategoria);
            }
            
            // Eliminar duplicados
            return eventosFiltrados.Distinct().ToList();
        }

        /// <summary>
        /// Filtra eventos por categoría específica (teatro, música, etc.)
        /// </summary>
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

        /// <summary>
        /// Filtra eventos para mostrar solo los próximos 7 días
        /// </summary>
        private List<Evento> FiltrarProximosSieteDias(List<Evento> eventos)
        {
            var hoy = DateTime.Today;
            var limiteSemanaSiguiente = hoy.AddDays(7);
            
            return eventos.Where(evento => 
                !evento.EsEventoLargaDuracion && 
                evento.FechaEvento.Date >= hoy && 
                evento.FechaEvento.Date <= limiteSemanaSiguiente
            ).ToList();
        }

        /// <summary>
        /// Filtra eventos para mostrar solo los de hoy
        /// </summary>
        private List<Evento> FiltrarEventosHoy(List<Evento> eventos)
        {
            var hoy = DateTime.Today;
            
            return eventos.Where(evento => 
                !evento.EsEventoLargaDuracion && 
                evento.FechaEvento.Date == hoy
            ).ToList();
        }

        /// <summary>
        /// Filtra eventos para incluir solo las categorías que nos interesan
        /// </summary>
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

        #endregion

        #region Métodos de Agrupación y Presentación

        /// <summary>
        /// Agrupa eventos filtrados por categorías para vista Netflix
        /// </summary>
        /// <param name="eventosFiltrados">Lista de eventos ya filtrados</param>
        /// <returns>Diccionario con eventos agrupados por categoría</returns>
        private Dictionary<string, List<Evento>> AgruparEventosFiltrados(List<Evento> eventosFiltrados)
        {
            var eventosPorCategoria = new Dictionary<string, List<Evento>>();
            
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
                    .Take(10) // Máximo 10 por categoría para diseño Netflix
                    .ToList();
                    
                if (eventosCategoria.Any())
                {
                    eventosPorCategoria[categoria.Key] = eventosCategoria;
                }
            }
            
            return eventosPorCategoria;
        }

        #endregion

        #region Métodos de Utilidad

        /// <summary>
        /// Convierte string separado por comas en lista
        /// </summary>
        /// <param name="input">String con elementos separados por comas</param>
        /// <returns>Lista de strings o null si está vacío</returns>
        private List<string>? ConvertirStringALista(string? input)
        {
            if (string.IsNullOrEmpty(input))
                return null;
                
            return input
                .Split(',')
                .Select(item => item.Trim())
                .Where(item => !string.IsNullOrEmpty(item))
                .ToList();
        }

        /// <summary>
        /// Busca un atributo específico por nombre en la lista de atributos XML
        /// </summary>
        /// <param name="atributos">Lista de atributos del evento</param>
        /// <param name="nombre">Nombre del atributo a buscar</param>
        /// <returns>Valor del atributo o null si no existe</returns>
        private string? BuscarAtributo(List<EventoAtributo> atributos, string nombre)
        {
            return atributos.FirstOrDefault(a => a.Nombre == nombre)?.Valor;
        }

        /// <summary>
        /// Extrae información de ubicación del evento (atributo anidado LOCALIZACION)
        /// </summary>
        /// <param name="atributos">Lista de atributos del evento</param>
        /// <returns>Tupla con datos de ubicación</returns>
        private (string Nombre, string Direccion, string Distrito, double? Latitud, double? Longitud) 
            ExtraerUbicacion(List<EventoAtributo> atributos)
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

        /// <summary>
        /// Procesa campos adicionales del evento (precio, duración, etc.)
        /// </summary>
        /// <param name="evento">Evento a modificar</param>
        /// <param name="precio">Precio del evento</param>
        /// <param name="eventoLargaDuracionStr">Indicador de evento de larga duración</param>
        /// <param name="diasSemana">Días de la semana del evento</param>
        /// <param name="fechaFinStr">Fecha de fin del evento</param>
        private void ProcesarCamposAdicionales(
            Evento evento, 
            string? precio, 
            string? eventoLargaDuracionStr, 
            string? diasSemana, 
            string? fechaFinStr)
        {
            // Procesar precio y necesidad de entrada
            evento.Precio = precio ?? (evento.EsGratuito ? "Gratuito" : "Consultar precio");
            evento.NecesitaEntrada = !evento.EsGratuito || 
                (precio?.Contains("descarga", StringComparison.OrdinalIgnoreCase) ?? false);

            // Procesar evento de larga duración
            evento.EsEventoLargaDuracion = eventoLargaDuracionStr == "1";
            evento.DiasSemana = diasSemana;

            // Procesar fecha fin para eventos largos
            if (!string.IsNullOrEmpty(fechaFinStr) && DateTime.TryParse(fechaFinStr, out DateTime fechaFin))
            {
                evento.FechaFin = fechaFin;
            }

            // Crear fecha formateada inteligente
            evento.FechaFormateada = CrearFechaFormateada(evento);
        }

        /// <summary>
        /// Crea texto de fecha inteligente según el tipo de evento
        /// </summary>
        /// <param name="evento">Evento a procesar</param>
        /// <returns>Texto formateado de la fecha</returns>
        private string CrearFechaFormateada(Evento evento)
        {
            var hoy = DateTime.Today;
            var fechaEvento = evento.FechaEvento.Date;

            // Eventos de larga duración
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

            // Eventos con fecha específica
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

        /// <summary>
        /// Convierte códigos de días a texto legible
        /// </summary>
        /// <param name="diasCodigos">Códigos de días separados por comas (L,M,X,J,V,S,D)</param>
        /// <returns>Texto legible de los días</returns>
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

        #endregion

        #region Métodos de Configuración de ViewBag

        /// <summary>
        /// Configura ViewBag para vista de categoría específica
        /// </summary>
        /// <param name="categoria">Nombre de la categoría</param>
        /// <param name="totalEventos">Número total de eventos</param>
        private void ConfigurarViewBagParaCategoria(string categoria, int totalEventos)
        {
            ViewBag.CategoriaActual = categoria;
            ViewBag.TipoVista = "Categoría";
            ViewBag.TotalEventos = totalEventos;
        }

        /// <summary>
        /// Configura ViewBag para vista de todos los eventos
        /// </summary>
        /// <param name="totalEventos">Número total de eventos</param>
        private void ConfigurarViewBagParaTodos(int totalEventos)
        {
            ViewBag.CategoriaActual = "Todos los eventos";
            ViewBag.TipoVista = "TodosEventos";
            ViewBag.TotalEventos = totalEventos;
        }

        /// <summary>
        /// Configura ViewBag para vista Netflix (landing)
        /// </summary>
        /// <param name="eventosPorCategoria">Eventos agrupados por categoría</param>
        private void ConfigurarViewBagParaNetflix(Dictionary<string, List<Evento>> eventosPorCategoria)
        {
            ViewBag.EventosPorCategoria = eventosPorCategoria;
            ViewBag.CategoriaActual = "Esta semana por categorías";
            ViewBag.TipoVista = "Netflix";
            ViewBag.TotalEventos = 0; // Netflix no muestra total
        }

        /// <summary>
        /// Configura ViewBag para respuestas AJAX
        /// </summary>
        /// <param name="fecha">Filtro de fecha aplicado</param>
        /// <param name="totalEventos">Número total de eventos</param>
        private void ConfigurarViewBagParaAjax(string? fecha, int totalEventos)
        {
            ViewBag.CategoriaActual = fecha switch
            {
                "hoy" => "Eventos de hoy",
                "semana" => "Esta semana",
                "todos" => "Todos los eventos",
                _ => "Esta semana"
            };
            ViewBag.TipoVista = "Categoría";
            ViewBag.TotalEventos = totalEventos;
        }

        /// <summary>
        /// Maneja errores y devuelve vista con mensaje de error
        /// </summary>
        /// <param name="mensajeError">Mensaje de error a mostrar</param>
        /// <returns>Vista con lista vacía y mensaje de error</returns>
        private IActionResult ManejarError(string mensajeError)
        {
            ViewBag.Error = $"Error al obtener eventos de Madrid: {mensajeError}";
            return View(new List<Evento>());
        }

        #endregion
    /// <summary>
/// MÉTODO TEMPORAL PARA ANÁLISIS - Eliminar después de usar
/// Analiza todos los tipos de eventos en la API para tomar decisiones
/// </summary>
[HttpGet]
public async Task<IActionResult> AnalizarTiposEventos()
{
    try
    {
        // Obtener eventos de la API
        var eventosResponse = await ObtenerEventosDeMadrid();
        var eventosProcesados = ProcesarEventos(eventosResponse);
        
        // Agrupar por tipo y contar
        var analisisTipos = eventosProcesados
            .GroupBy(e => e.Tipo)
            .Select(g => new { 
                Tipo = g.Key, 
                Cantidad = g.Count(),
                EjemploTitulo = g.First().Titulo 
            })
            .OrderByDescending(x => x.Cantidad)
            .ToList();
        
        // Crear HTML para mostrar resultados
        var html = "<h2>📊 Análisis de Tipos de Eventos en la API</h2>";
        html += "<table border='1' style='border-collapse: collapse; width: 100%;'>";
        html += "<tr style='background: #f0f0f0;'><th>Tipo</th><th>Cantidad</th><th>Ejemplo</th></tr>";
        
        foreach (var item in analisisTipos)
        {
            html += $"<tr>";
            html += $"<td style='padding: 8px;'>{item.Tipo}</td>";
            html += $"<td style='padding: 8px; text-align: center;'><strong>{item.Cantidad}</strong></td>";
            html += $"<td style='padding: 8px;'>{item.EjemploTitulo}</td>";
            html += $"</tr>";
        }
        
        html += "</table>";
        html += $"<p><strong>Total eventos analizados:</strong> {eventosProcesados.Count}</p>";
        
        return Content(html, "text/html");
    }
    catch (Exception ex)
    {
        return Content($"Error: {ex.Message}", "text/plain");
    }
}
}}