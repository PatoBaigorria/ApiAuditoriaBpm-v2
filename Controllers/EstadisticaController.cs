using apiAuditoriaBPM.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace apiAuditoriaBPM.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EstadisticasController : ControllerBase
    {
        private readonly DataContext contexto;

        public EstadisticasController(DataContext contexto)
        {
            this.contexto = contexto;
        }

        [HttpGet("por-supervisor")]
        [AllowAnonymous] 
        public async Task<ActionResult<IEnumerable<SupervisorEstadisticaDTO>>> GetEstadisticasPorSupervisor([FromQuery] DateTime desde, [FromQuery] DateTime hasta)
        {
            var desdeDateOnly = DateOnly.FromDateTime(desde);
            var hastaDateOnly = DateOnly.FromDateTime(hasta);

            var auditorias = await contexto.Auditoria
                .Include(a => a.Supervisor)
                .Include(a => a.AuditoriaItems)
                .Where(a => a.Fecha >= desdeDateOnly && a.Fecha <= hastaDateOnly)
                .ToListAsync();

            var estadisticas = auditorias
                .GroupBy(a => a.Supervisor)
                .Select(g =>
                {
                    var total = g.Count();
                    var negativas = g.Count(a => a.AuditoriaItems.Any(i => i.Estado == EstadoEnum.NOOK));
                    var positivas = total - negativas;

                    return new SupervisorEstadisticaDTO
                    {
                        Supervisor = g.Key,
                        TotalAudits = total,
                        PositiveAudits = positivas,
                        NegativeAudits = negativas
                    };
                })
                .ToList();

            return Ok(estadisticas);
        }

        [HttpGet("no-conforme")]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<SupervisorEstadisticaDTO>>> GetEstadisticasNoConformes([FromQuery] DateTime desde, [FromQuery] DateTime hasta)
        {
            Console.WriteLine($"Desde: {desde}, Hasta: {hasta}");

            var desdeDate = DateOnly.FromDateTime(desde);
            var hastaDate = DateOnly.FromDateTime(hasta);

            var estadisticas = await contexto.Auditoria
                .Where(a => a.Fecha >= desdeDate && a.Fecha <= hastaDate && a.NoConforme)
                .AsNoTracking()
                .ToListAsync();

            Console.WriteLine($"Estadisticas: {estadisticas.Count}");

            return Ok(estadisticas);
        }
    }
}
