using apiAuditoriaBPM.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace apiAuditoriaBPM.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class OperariosController : ControllerBase
    {
        private readonly DataContext contexto;

        public OperariosController(DataContext contexto)
        {
            this.contexto = contexto;
        }

        [HttpGet("byLegajo")]
        public async Task<ActionResult<Operario>> GetNombreByLegajo([FromQuery] int legajo)
        {
            try
            {
                // Encontrar el operario por su legajo
                var operario = await contexto.Operario
                                .FirstOrDefaultAsync(o => o.Legajo == legajo);
                Console.WriteLine(operario);

                if (operario == null)
                {
                    return NotFound(new { Mensaje = "Operario no encontrado" });
                }               

                return Ok(operario);
            }
            catch (Exception ex)
            {
                return BadRequest(new { Mensaje = ex.Message });
            }
        }

        [HttpGet("validar-legajo/{legajo}")]
        public async Task<ActionResult> ValidarLegajo(int legajo)
        {
            try
            {
                var existeLegajo = await contexto.Operario.AnyAsync(o => o.Legajo == legajo);
                if (!existeLegajo)
                {
                    return NotFound(new { Mensaje = "Legajo Erróneo" });
                }

                return Ok(new { Mensaje = "Legajo válido" });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet]
        public async Task<ActionResult<List<Operario>>> Get()
        {
            try
            {
                var operarios = await contexto.Operario.ToListAsync();
                return Ok(operarios);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
