using apiAuditoriaBPM.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace apiAuditoriaBPM.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class LineasController : ControllerBase
    {
        private readonly DataContext contexto;
        public LineasController(DataContext contexto)
        {
            this.contexto = contexto;
        }
        [HttpGet("byLegajo")]
        public async Task<ActionResult<List<Linea>>> GetLineasByLegajo([FromQuery] int legajo)
        {
            try
            {
                // Encontrar el operario por su legajo
                var operario = await contexto.Operario.Include(o => o.Linea)
                                .FirstOrDefaultAsync(o => o.Legajo == legajo);

                if (operario == null)
                {
                    return NotFound("Operario no encontrado");
                }

                // Obtener la línea asociada
                var linea = await contexto.Linea
                                  .Where(l => l.IdLinea == operario.IdLinea)
                                  .ToListAsync();

                return Ok(linea);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("todas")]
        public async Task<ActionResult<List<Linea>>> GetTodasLineas()
        {
            try
            {
                var lineas = await contexto.Linea.ToListAsync();
                return Ok(lineas);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

    }

}
