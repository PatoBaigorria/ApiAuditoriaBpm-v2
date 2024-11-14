using apiAuditoriaBPM.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace apiAuditoriaBPM.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ActividadesController : ControllerBase
    {
        private readonly DataContext contexto;
        public ActividadesController(DataContext contexto)
        {
            this.contexto = contexto;
        }
        [HttpGet("byLegajo")]
        public async Task<ActionResult<List<Actividad>>> GetActividadesByLegajo([FromQuery] int legajo)
        {
            try
            {
                // Encontrar el operario por su legajo
                var operario = await contexto.Operario.Include(o => o.Actividad)
                                .FirstOrDefaultAsync(o => o.Legajo == legajo);

                if (operario == null)
                {
                    return NotFound("Operario no encontrado");
                }

                // Obtener la actividad asociada
                var actividad = await contexto.Actividad
                                  .Where(a => a.IdActividad == operario.IdActividad)
                                  .ToListAsync();

                return Ok(actividad);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("todas")]
        public async Task<ActionResult<List<Actividad>>> GetTodasActividades()
        {
            try
            {
                var actividades = await contexto.Actividad.ToListAsync();
                return Ok(actividades);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }

}