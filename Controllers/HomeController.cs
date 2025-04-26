using apiAuditoriaBPM.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace apiAuditoriaBPM.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DashboardController : ControllerBase
    {
        private readonly DataContext contexto;
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(DataContext contexto, ILogger<DashboardController> logger)
        {
            this.contexto = contexto;
            _logger = logger;
        }

        [HttpGet("resumen")]
        [AllowAnonymous]
        public async Task<IActionResult> GetResumen([FromQuery] DateTime desde, [FromQuery] DateTime hasta)
        {
            try
            {
                var desdeDateOnly = DateOnly.FromDateTime(desde);
                var hastaDateOnly = DateOnly.FromDateTime(hasta);

                // Obtener todas las auditorías en el rango de fechas con sus items
                var auditorias = await contexto.Auditoria
                    .Include(a => a.AuditoriaItems)
                    .Where(a => a.Fecha >= desdeDateOnly && a.Fecha <= hastaDateOnly)
                    .ToListAsync();
                
                if (auditorias == null || !auditorias.Any())
                {
                    var resultadoVacio = new 
                    {
                        AuditoriasHoy = 0,
                        AuditoriasTotal = 0,
                        PorcentajeConformidad = 0
                    };
                    return Ok(resultadoVacio);
                }
                
                // Calcular auditorías de hoy
                var hoy = DateOnly.FromDateTime(DateTime.Today);
                var auditoriasHoy = auditorias.Count(a => a.Fecha == hoy);
                
                // Calcular porcentaje de conformidad (auditorías positivas)
                // Una auditoría es positiva si no tiene ítems con estado NOOK
                // Las auditorías que solo tienen ítems OK o N/A se consideran positivas
                int auditoriasPositivas = auditorias.Count(a => !a.AuditoriaItems.Any(i => i.Estado == EstadoEnum.NOOK));
                
                // Loguear información detallada para depuración
                _logger.LogInformation($"Total de auditorías: {auditorias.Count}");
                _logger.LogInformation($"Auditorías positivas (sin NO OK): {auditoriasPositivas}");
                
                // Detallar las auditorías con sus estados para verificar
                foreach (var auditoria in auditorias.Take(5)) // Mostrar solo las primeras 5 para no saturar el log
                {
                    var estadosItems = auditoria.AuditoriaItems
                        .GroupBy(i => i.Estado)
                        .Select(g => $"{g.Key}: {g.Count()}")
                        .ToList();
                    
                    _logger.LogInformation($"Auditoría ID {auditoria.IdAuditoria}: {string.Join(", ", estadosItems)}");
                }
                
                decimal porcentajeConformidad = auditorias.Count > 0 
                    ? Math.Round((decimal)auditoriasPositivas / auditorias.Count * 100, 1) 
                    : 0;
                
                var resultado = new 
                {
                    AuditoriasHoy = auditoriasHoy,
                    AuditoriasTotal = auditorias.Count,
                    PorcentajeConformidad = porcentajeConformidad
                };
                
                _logger.LogInformation($"Resumen obtenido: {auditoriasHoy} auditorías hoy, {auditorias.Count} total, {porcentajeConformidad}% conformidad");
                return Ok(resultado);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno del servidor: {ex.Message}");
            }
        }

        [HttpGet("indicadores")]
        [AllowAnonymous]
        public async Task<IActionResult> GetIndicadores([FromQuery] DateTime desde, [FromQuery] DateTime hasta)
        {
            try
            {
                var desdeDateOnly = DateOnly.FromDateTime(desde);
                var hastaDateOnly = DateOnly.FromDateTime(hasta);

                // Obtener todas las auditorías en el rango de fechas con sus items
                var auditorias = await contexto.Auditoria
                    .Include(a => a.AuditoriaItems)
                    .Where(a => a.Fecha >= desdeDateOnly && a.Fecha <= hastaDateOnly)
                    .ToListAsync();
                
                if (auditorias == null || !auditorias.Any())
                {
                    var resultadoVacio = new 
                    {
                        PorcentajeAuditoriasCompletadas = 0,
                        PorcentajeConformidadGeneral = 0,
                        PorcentajeOperariosAuditados = 0
                    };
                    return Ok(resultadoVacio);
                }
                
                // Calcular porcentaje de auditorías completadas
                int mesesTranscurridos = (hasta.Year - desde.Year) * 12 + hasta.Month - desde.Month + 1;
                int objetivoAuditorias = mesesTranscurridos * 20; // 20 auditorías por mes como objetivo
                
                decimal porcentajeAuditoriasCompletadas;
                if (objetivoAuditorias > 0)
                {
                    decimal porcentaje = (decimal)auditorias.Count / objetivoAuditorias * 100;
                    porcentajeAuditoriasCompletadas = Math.Min(Math.Round(porcentaje, 1), 100);
                }
                else
                {
                    porcentajeAuditoriasCompletadas = 0;
                }
                
                // Eliminamos el cálculo de promedio de ítems por auditoría ya que siempre son 11 ítems
                
                // Encontrar el ítem con mayor incidencia de No OK
                var itemsNoOk = auditorias
                    .SelectMany(a => a.AuditoriaItems)
                    .Where(i => i.Estado == EstadoEnum.NOOK)
                    .GroupBy(i => i.IdItemBPM)
                    .Select(g => new { IdItemBPM = g.Key, Cantidad = g.Count() })
                    .OrderByDescending(x => x.Cantidad)
                    .FirstOrDefault();
                
                string itemConMayorIncidencia = "Ninguno";
                int cantidadNoOk = 0;
                
                if (itemsNoOk != null)
                {
                    var itemBPM = await contexto.ItemBPM.FindAsync(itemsNoOk.IdItemBPM);
                    if (itemBPM != null)
                    {
                        itemConMayorIncidencia = itemBPM.Descripcion;
                        cantidadNoOk = itemsNoOk.Cantidad;
                    }
                }
                
                _logger.LogInformation($"Ítem con mayor incidencia: {itemConMayorIncidencia} (No OK {cantidadNoOk} veces)");
                
                // Calcular operarios auditados
                // Usando IdOperario directamente de la auditoría
                var operariosAuditadosIds = auditorias
                    .Select(a => a.IdOperario) // Usar IdOperario directamente
                    .Distinct()
                    .Count();
                
                var totalOperariosCount = await contexto.Operario.CountAsync();
                
                decimal porcentajeOperariosAuditados;
                if (totalOperariosCount > 0)
                {
                    porcentajeOperariosAuditados = Math.Round((decimal)operariosAuditadosIds / totalOperariosCount * 100, 1);
                }
                else
                {
                    porcentajeOperariosAuditados = 0;
                }
                
                var resultado = new 
                {
                    PorcentajeAuditoriasCompletadas = porcentajeAuditoriasCompletadas,
                    PorcentajeOperariosAuditados = porcentajeOperariosAuditados,
                    ItemConMayorIncidencia = itemConMayorIncidencia,
                    CantidadNoOk = cantidadNoOk
                };
                
                _logger.LogInformation($"Indicadores obtenidos: {porcentajeAuditoriasCompletadas}% completadas, {porcentajeOperariosAuditados}% operarios auditados, ítem con mayor incidencia: {itemConMayorIncidencia} ({cantidadNoOk} veces)");
                return Ok(resultado);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno del servidor: {ex.Message}");
            }
        }
    }
}