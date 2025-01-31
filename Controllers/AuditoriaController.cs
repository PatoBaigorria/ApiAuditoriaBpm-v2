using System.Numerics;
using System.Security.Claims;
using apiAuditoriaBPM.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using MimeKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using System.Text;
using Microsoft.Extensions.Configuration;
using SendGrid;
using SendGrid.Helpers.Mail;
using System.Threading.Tasks;

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
    }
}
