using System.Numerics;
using System.Security.Claims;
using apiAuditoriaBPM.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using SendGrid;
using SendGrid.Helpers.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Text;
using System;
using System.IO;
using System.Linq;

namespace apiAuditoriaBPM.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AuditoriasController : ControllerBase
    {
        private readonly DataContext contexto;

        private readonly IConfiguration config;

        public AuditoriasController(DataContext contexto, IConfiguration config)
        {
            this.contexto = contexto;
            this.config = config;
        }


        // GET: Cantidad Auditorias realizadas por Supervisor
        [HttpGet("cantidad-auditorias-mes-a-mes")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<ActionResult<Dictionary<string, object>>> GetAuditoriasMesAMes([FromQuery] int anioInicio, [FromQuery] int anioFin)
        {
            try
            {
                // Obtener el legajo del usuario autenticado
                var legajo = User.FindFirstValue("Legajo");

                if (legajo == null)
                {
                    return Unauthorized("Legajo no encontrado en el token.");
                }

                var legajoInt = int.Parse(legajo);

                // Buscar el supervisor por su legajo
                var supervisor = await contexto.Supervisor
                    .Where(s => s.Legajo == legajoInt)
                    .FirstOrDefaultAsync();

                // Verificar si el supervisor existe
                if (supervisor == null)
                {
                    return NotFound("Supervisor no encontrado.");
                }

                // Diccionario para almacenar los resultados por mes y totales
                var auditoriasPorMes = new Dictionary<string, object>();
                int totalAnual = 0;
                int totalConEstadoNoOkAnual = 0;
                int totalConEstadoOkAnual = 0;

                // Iterar por cada año y mes en el rango
                for (int anio = anioInicio; anio <= anioFin; anio++)
                {
                    for (int mes = 1; mes <= 12; mes++)
                    {
                        // Obtener todas las auditorías del supervisor para el mes y año actuales
                        var auditoriasMes = await contexto.Auditoria
                            .Where(a => a.IdSupervisor == supervisor.IdSupervisor &&
                                        a.Fecha.Year == anio &&
                                        a.Fecha.Month == mes)
                            .ToListAsync();

                        // Contar las auditorías en base a los ítems que tengan estado (1: no ok, 2: ok, 3: n/a)
                        var auditoriasConEstadoNoOk = 0;
                        var auditoriasConEstadoOk = 0;

                        foreach (var auditoria in auditoriasMes)
                        {
                            // Verificar si al menos un ítem tiene estado NOOK (2)
                            var tieneEstadoNoOK = await contexto.AuditoriaItemBPM
                                .AnyAsync(ai => ai.IdAuditoria == auditoria.IdAuditoria && ai.Estado == EstadoEnum.NOOK);

                            // Si no tiene ítems en estado NOOk, consideramos que todos los ítems son OK o n/a
                            if (tieneEstadoNoOK)
                            {
                                auditoriasConEstadoNoOk++;
                            }
                            else
                            {
                                // Considerar tanto estado OK como null para las auditorías con estado OK
                                auditoriasConEstadoOk++;
                            }
                        }

                        // Formato de clave "YYYY-MM"
                        string clave = $"{anio}-{mes:D2}";

                        // Almacenar los resultados por mes
                        auditoriasPorMes[clave] = new
                        {
                            Total = auditoriasMes.Count,
                            ConEstadoNoOk = auditoriasConEstadoNoOk,
                            ConEstadoOK = auditoriasConEstadoOk
                        };

                        // Sumar los resultados al total anual
                        totalAnual += auditoriasMes.Count;
                        totalConEstadoNoOkAnual += auditoriasConEstadoNoOk;
                        totalConEstadoOkAnual += auditoriasConEstadoOk;
                    }
                }

                // Añadir los totales anuales al resultado final
                auditoriasPorMes["TotalesAnuales"] = new
                {
                    TotalAnual = totalAnual,
                    TotalConEstadoNoOkAnual = totalConEstadoNoOkAnual,
                    TotalConEstadoOkAnual = totalConEstadoOkAnual
                };

                return Ok(auditoriasPorMes);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno del servidor: {ex.Message}");
            }
        }
        [HttpGet("por-supervisor")]
        public async Task<ActionResult<IEnumerable<Auditoria>>> GetAuditoriasPorSupervisor(
            DateTime desde,
            DateTime hasta,
            int? supervisorId = null)
        {
            // Usar Auditoria (singular) en lugar de Auditorias (plural)
            var query = contexto.Auditoria
                .Include(a => a.Supervisor)
                .Include(a => a.Operario)
                .Include(a => a.Actividad)
                .Include(a => a.Linea)
                .Where(a => a.Fecha >= DateOnly.FromDateTime(desde) &&
                           a.Fecha <= DateOnly.FromDateTime(hasta));

            if (supervisorId.HasValue)
            {
                query = query.Where(a => a.IdSupervisor == supervisorId.Value);
            }

            var auditorias = await query.ToListAsync();

            return Ok(auditorias);
        }


        [HttpGet]
        public async Task<ActionResult<IEnumerable<Auditoria>>> GetAuditorias(DateTime desde, DateTime hasta, int? supervisorId = null)
        {
            var query = contexto.Auditoria
                .Include(a => a.Supervisor)
                .Include(a => a.Operario)
                .Include(a => a.Actividad)
                .Include(a => a.Linea)
                .Include(a => a.AuditoriaItems)
                    .ThenInclude(ai => ai.ItemBPM)
                .Where(a => a.Fecha >= DateOnly.FromDateTime(desde) &&
                           a.Fecha <= DateOnly.FromDateTime(hasta));

            if (supervisorId.HasValue)
            {
                query = query.Where(a => a.IdSupervisor == supervisorId.Value);
            }

            var auditorias = await query.ToListAsync();
            return Ok(auditorias);
        }



        [HttpPost("enviar-notificacion-auditoria")]
        public async Task<IActionResult> EnviarNotificacionAuditoria([FromForm] int idAuditoria)
        {
            try
            {
                var auditoria = await contexto.Auditoria
                    .Include(a => a.Operario)
                    .Include(a => a.Supervisor)
                    .Include(a => a.Actividad)
                    .FirstOrDefaultAsync(a => a.IdAuditoria == idAuditoria);

                if (auditoria == null) return NotFound("No se encontró la auditoría especificada.");

                var operario = auditoria.Operario;
                if (operario == null || string.IsNullOrEmpty(operario.Email)) return BadRequest("El operario no tiene un correo electrónico registrado.");

                var supervisor = auditoria.Supervisor;
                if (supervisor == null || string.IsNullOrEmpty(supervisor.Nombre)) return BadRequest("El supervisor no tiene un nombre registrado.");

                var auditoriaItems = await contexto.AuditoriaItemBPM.Include(i => i.ItemBPM).Where(ai => ai.IdAuditoria == idAuditoria).ToListAsync();
                if (auditoriaItems == null || !auditoriaItems.Any()) return BadRequest("No hay ítems asociados a esta auditoría.");

                var cuerpoCorreo = $@"
                    <h2>Hola {operario.ObtenerNombreCompleto()}</h2>
                    <p>Se ha completado una auditoría con los siguientes detalles:</p>
                    <ul>
                        <li><strong>ID Auditoría:</strong> {auditoria.IdAuditoria}</li>
                        <li><strong>Fecha:</strong> {auditoria.Fecha:dd/MM/yyyy}</li>
                        <li><strong>Supervisor:</strong> {supervisor.Nombre} {supervisor.Apellido}</li>
                        <li><strong>Línea:</strong> {auditoria.IdLinea}</li>
                        <li><strong>Actividad Realizada:</strong> {auditoria.Actividad?.Descripcion}</li>
                        <li><strong>Total de Ítems Auditados:</strong> {auditoriaItems.Count}</li>
                        <li><strong>Comentario:</strong> {auditoria.Comentario}</li>
                        <li><strong>Firma:</strong> {auditoria.NoConforme}</li>
                    </ul>
                    <br>
                    <h3>Resumen de Ítems Auditados:</h3>
                    <table border='1' aling='center' cellpadding='5' cellspacing='0'>
                        <tr>
                            <th>Ítem</th>
                            <th style='text-align: center;'>Estado</th>
                        </tr>";

                foreach (var item in auditoriaItems)
                {
                    cuerpoCorreo += $@"
                        <tr>
                            <td>{item.ItemBPM.Descripcion}</td>
                            <td style='text-align: center;'>{item.Estado}</td>
                        </tr>";
                }
                cuerpoCorreo += "</table>";

                // Acceder a la configuración del SMTP
                var smtpServer = config["SMTP:Server"];
                var smtpPort = int.Parse(config["SMTP:Port"]);
                var smtpUser = config["SMTP:User"];
                var smtpPass = config["SMTP:Pass"];

                var client = new SendGridClient(smtpPass);  // Usando la clave API como contraseña
                var from = new EmailAddress("mpbaigorria.01@gmail.com", "Sistema de Auditorías");
                var to = new EmailAddress(operario.Email, operario.ObtenerNombreCompleto());
                var subject = $"Notificación de Auditoría #{auditoria.IdAuditoria}";
                var plainTextContent = "Este es el cuerpo del correo de auditoría.";
                var htmlContent = cuerpoCorreo;

                var msg = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent, htmlContent);

                // Enviar el correo
                var response = await client.SendEmailAsync(msg);

                if (response.StatusCode == System.Net.HttpStatusCode.Accepted)
                {
                    return Ok("Se ha enviado la notificación de la auditoría correctamente.");
                }
                else
                {
                    return StatusCode((int)response.StatusCode, "Error al enviar el correo.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al enviar el correo: {ex.Message}");
                Console.WriteLine($"Detalles: {ex.StackTrace}");
                return StatusCode(500, $"Error al enviar el correo: {ex.Message}. Detalles: {ex.StackTrace}");
            }
        }



        [HttpGet("auditorias-operario")]
        public IActionResult GetOperariosSinAuditoria()
        {
            // Cargar los operarios con las relaciones incluidas
            var operarios = contexto.Operario
                .Include(o => o.Linea)
                .Include(o => o.Actividad)
                .Where(o => !contexto.Auditoria.Any(a => a.IdOperario == o.IdOperario))
                .ToList();

            var operariosSinAuditoria = operarios.Select(o => new
            {
                o.IdOperario,
                NombreCompleto = $"{o.Nombre} {o.Apellido}",
                o.Legajo,
                IdLinea = o.Linea != null ? o.Linea.IdLinea : 0,
                DescripcionLinea = o.Linea != null ? o.Linea.Descripcion : "Sin Línea",
                DescripcionActividad = o.Actividad != null ? o.Actividad.Descripcion : null
            }).ToList();

            if (!operariosSinAuditoria.Any())
            {
                return NotFound("Todos los operarios tienen auditorías realizadas.");
            }

            return Ok(operariosSinAuditoria);
        }

        public class AltaAuditoriaRequest
        {
            public int IdOperario { get; set; }
            public int IdSupervisor { get; set; }
            public int IdActividad { get; set; }
            public int IdLinea { get; set; }

            public bool NoConforme { get; set; }
            public string Firma { get; set; }
            public string? Comentario { get; set; }
            public List<ItemAuditoriaRequest> Items { get; set; } = new();
        }

        public class ItemAuditoriaRequest
        {
            public int IdItemBPM { get; set; }
            public string Estado { get; set; }  // Enum o string (por ejemplo, "OK", "NO_OK", "N/A")
        }


        // POST: Auditorias/alta
        [HttpPost("alta-auditoria-completa")]
        public async Task<IActionResult> DarDeAltaAuditoriaCompleta([FromBody] AltaAuditoriaRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var nuevaAuditoria = new Auditoria
                {
                    IdSupervisor = request.IdSupervisor,
                    IdOperario = request.IdOperario,
                    IdActividad = request.IdActividad,
                    IdLinea = request.IdLinea,
                    Fecha = DateOnly.FromDateTime(DateTime.Now),
                    NoConforme = request.NoConforme,
                    Firma = request.Firma,
                    Comentario = request.Comentario
                };

                await contexto.Auditoria.AddAsync(nuevaAuditoria);
                await contexto.SaveChangesAsync();

                foreach (var item in request.Items)
                {
                    var nuevoAuditoriaItem = new AuditoriaItemBPM
                    {
                        IdAuditoria = nuevaAuditoria.IdAuditoria,
                        IdItemBPM = item.IdItemBPM,
                        Estado = Enum.Parse<EstadoEnum>(item.Estado),
                    };
                    await contexto.AuditoriaItemBPM.AddAsync(nuevoAuditoriaItem);
                }

                await contexto.SaveChangesAsync();
                await EnviarNotificacionAuditoria(nuevaAuditoria.IdAuditoria);
                return CreatedAtAction(nameof(DarDeAltaAuditoriaCompleta), new { id = nuevaAuditoria.IdAuditoria }, new { message = "Auditoría completa creada correctamente", auditoria = nuevaAuditoria });
            }
            catch (Exception ex)
            {
                var errorMessage = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return StatusCode(500, $"Error interno del servidor: {errorMessage}");
            }


        }

        // GET: Auditorias por rango de fechas
        [HttpGet("por-fecha")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<ActionResult<List<Auditoria>>> GetAuditoriasPorFecha(
            [FromQuery] string fromDate,
            [FromQuery] string toDate,
            [FromQuery] int supervisorId)
        {
            try
            {
                // Convertir las fechas string a DateOnly usando el formato dd-MM-yyyy
                if (!DateOnly.TryParseExact(fromDate, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out DateOnly fechaInicio) ||
                    !DateOnly.TryParseExact(toDate, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out DateOnly fechaFin))
                {
                    return BadRequest("Formato de fecha inválido. Use el formato dd/MM/yyyy");
                }

                // Verificar que el supervisor existe
                var supervisor = await contexto.Supervisor
                    .FirstOrDefaultAsync(s => s.Legajo == supervisorId);

                if (supervisor == null)
                {
                    return NotFound($"No se encontró el supervisor con ID {supervisorId}");
                }

                // Obtener las auditorías con todas las relaciones necesarias
                var auditorias = await contexto.Auditoria
                    .Include(a => a.Operario)
                    .Include(a => a.Supervisor)
                    .Include(a => a.Actividad)
                    .Include(a => a.Linea)
                    .Include(a => a.AuditoriaItems)
                        .ThenInclude(ai => ai.ItemBPM)
                    .Where(a => a.IdSupervisor == supervisorId &&
                               a.Fecha >= fechaInicio &&
                               a.Fecha <= fechaFin)
                    .OrderByDescending(a => a.Fecha)
                    .ToListAsync();

                if (!auditorias.Any())
                {
                    return Ok(new List<Auditoria>()); // Retorna lista vacía si no hay resultados
                }

                return Ok(auditorias);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno del servidor: {ex.Message}");
            }
        }

        [HttpGet("resumen-por-operario")]
        public async Task<ActionResult<List<OperarioAuditoriaResumenDTO>>> GetResumenPorOperario(
            [FromQuery] DateTime desde,
            [FromQuery] DateTime hasta,
            [FromQuery] int? legajo = null)
        {
            // Traer auditorías en el rango de fechas y opcionalmente filtrar por legajo
            var query = contexto.Auditoria
                .Include(a => a.Operario)
                .Include(a => a.AuditoriaItems)
                .Where(a => a.Fecha >= DateOnly.FromDateTime(desde) && a.Fecha <= DateOnly.FromDateTime(hasta));

            // Aplicar filtro de legajo si es necesario
            if (legajo.HasValue)
            {
                query = query.Where(a => a.Operario.Legajo == legajo.Value);
            }

            var auditorias = await query.ToListAsync();

            // Registrar información detallada para depuración
            Console.WriteLine($"Total de auditorías encontradas: {auditorias.Count}");
            Console.WriteLine($"Auditorías sin operario: {auditorias.Count(a => a.Operario == null)}");
            Console.WriteLine($"Auditorías con operario sin legajo: {auditorias.Count(a => a.Operario != null && a.Operario.Legajo == 0)}");

            // Filtrar auditorías que tienen operario válido
            var auditoriasConOperarioValido = auditorias.Where(a => a.Operario != null).ToList();
            Console.WriteLine($"Auditorías con operario válido: {auditoriasConOperarioValido.Count}");

            // Contar auditorías positivas y negativas totales
            int totalPositivas = auditorias.Count(a => !a.AuditoriaItems.Any(ai => ai.Estado == EstadoEnum.NOOK));
            int totalNegativas = auditorias.Count(a => a.AuditoriaItems.Any(ai => ai.Estado == EstadoEnum.NOOK));
            Console.WriteLine($"Total auditorías positivas: {totalPositivas}, Total auditorías negativas: {totalNegativas}");

            var resumen = auditoriasConOperarioValido
                .GroupBy(a => new
                {
                    Legajo = a.Operario.Legajo,
                    Nombre = (a.Operario.Nombre ?? "Sin nombre") + " " + (a.Operario.Apellido ?? "Sin apellido")
                })
                .Select(g => new OperarioAuditoriaResumenDTO
                {
                    Legajo = g.Key.Legajo,
                    Nombre = g.Key.Nombre,
                    // Una auditoría es positiva si NO tiene ningún ítem NO OK (puede tener ítems N/A)
                    AuditoriasPositivas = g.Count(a => !a.AuditoriaItems.Any(ai => ai.Estado == EstadoEnum.NOOK)),
                    // Una auditoría es negativa si tiene al menos un ítem NO OK
                    AuditoriasNegativas = g.Count(a => a.AuditoriaItems.Any(ai => ai.Estado == EstadoEnum.NOOK))
                })
                .OrderBy(r => r.Legajo)
                .ToList();

            // Agregar un registro para las auditorías sin operario válido, si existen
            int auditoriasInvalidas = auditorias.Count - auditoriasConOperarioValido.Count;
            if (auditoriasInvalidas > 0)
            {
                int positivasSinOperario = auditorias.Where(a => a.Operario == null)
                    .Count(a => !a.AuditoriaItems.Any(ai => ai.Estado == EstadoEnum.NOOK));

                int negativasSinOperario = auditorias.Where(a => a.Operario == null)
                    .Count(a => a.AuditoriaItems.Any(ai => ai.Estado == EstadoEnum.NOOK));

                resumen.Add(new OperarioAuditoriaResumenDTO
                {
                    Legajo = 0,
                    Nombre = "Auditorías sin operario asignado",
                    AuditoriasPositivas = positivasSinOperario,
                    AuditoriasNegativas = negativasSinOperario
                });
            }

            return Ok(resumen);
        }

        /// <summary>
        /// Exporta los datos de una auditoría específica a un archivo CSV compatible con Excel
        /// </summary>
        /// <param name="id">ID de la auditoría a exportar</param>
        /// <returns>Archivo CSV para descargar</returns>
        [HttpGet("ExportarAuditoriaExcel/{id}")]
        public async Task<IActionResult> ExportarAuditoriaExcel(int id)
        {
            try
            {
                // Obtener la auditoría directamente de la base de datos con todas sus relaciones
                var auditoria = await contexto.Auditoria
                    .Include(a => a.Supervisor)
                    .Include(a => a.Operario)
                    .Include(a => a.Actividad)
                    .Include(a => a.Linea)
                    .Include(a => a.AuditoriaItems)
                        .ThenInclude(i => i.ItemBPM)
                    .FirstOrDefaultAsync(a => a.IdAuditoria == id);

                if (auditoria == null)
                {
                    return NotFound("No se encontró la auditoría solicitada para exportar.");
                }

                // Crear el encabezado del CSV
                StringBuilder csv = new StringBuilder();

                // Datos de la auditoría
                csv.AppendLine("DATOS DE LA AUDITORÍA");
                // Usar formato de texto para Excel
                csv.AppendLine($"ID Auditoría;{auditoria.IdAuditoria}");
                csv.AppendLine($"Fecha;{auditoria.Fecha.ToString("dd/MM/yyyy")}");
                csv.AppendLine($"Supervisor;{auditoria.Supervisor?.Apellido}, {auditoria.Supervisor?.Nombre}");
                csv.AppendLine($"Legajo Supervisor;{auditoria.Supervisor?.Legajo}");
                csv.AppendLine($"Operario;{auditoria.Operario?.Apellido}, {auditoria.Operario?.Nombre}");
                csv.AppendLine($"Legajo Operario;{auditoria.Operario?.Legajo}");
                csv.AppendLine($"Actividad;{auditoria.Actividad?.Descripcion}");
                csv.AppendLine($"Línea;{auditoria.Linea?.Descripcion}");

                // Verificar si la auditoría tiene al menos un ítem NO OK
                bool tieneItemNoOk = auditoria.AuditoriaItems?.Any(i => i.Estado == EstadoEnum.NOOK) ?? false;
                string estadoTexto = tieneItemNoOk ? "No Conforme" : "Conforme";
                csv.AppendLine($"Firma;{estadoTexto}");
                csv.AppendLine("");

                // Ítems de la auditoría
                csv.AppendLine("ITEMS DE LA AUDITORÍA");
                csv.AppendLine("Descripción;Estado");

                if (auditoria.AuditoriaItems != null && auditoria.AuditoriaItems.Any())
                {
                    foreach (var item in auditoria.AuditoriaItems.OrderBy(i => i.IdAuditoriaItemBPM))
                    {
                        string estado = "";
                        switch (item.Estado)
                        {
                            case EstadoEnum.OK:
                                estado = "OK";
                                break;
                            case EstadoEnum.NOOK:
                                estado = "NO OK";
                                break;
                            case EstadoEnum.NA:
                                estado = "N/A";
                                break;
                            default:
                                estado = item.Estado.ToString();
                                break;
                        }

                        csv.AppendLine($"{item.ItemBPM?.Descripcion};{estado}");
                    }
                }
                else
                {
                    csv.AppendLine("No hay items asociados a esta auditoría");
                }

                // Agregar el BOM (Byte Order Mark) para Excel
                byte[] preamble = System.Text.Encoding.UTF8.GetPreamble();
                byte[] csvBytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());

                // Combinar el BOM con el contenido CSV
                byte[] fileBytes = new byte[preamble.Length + csvBytes.Length];
                Buffer.BlockCopy(preamble, 0, fileBytes, 0, preamble.Length);
                Buffer.BlockCopy(csvBytes, 0, fileBytes, preamble.Length, csvBytes.Length);

                // Convertir a stream
                var stream = new MemoryStream(fileBytes);

                // Crear el resultado para descarga
                var resultado = new FileStreamResult(stream, "text/csv; charset=utf-8");
                resultado.FileDownloadName = $"Auditoria_{auditoria.IdAuditoria}_{auditoria.Fecha:yyyy-MM-dd}.csv";

                return resultado;
            }
            catch (Exception ex)
            {
                return StatusCode(500, "No se pudo generar el archivo de exportación: " + ex.Message);
            }
        }
    }
}
