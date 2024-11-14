using apiAuditoriaBPM.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace apiAuditoriaBPM.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class FirmasController : ControllerBase
    {
        private readonly DataContext contexto;

        public FirmasController(DataContext contexto)
        {
            this.contexto = contexto;
        }

        // POST: api/Firmas/alta
        [HttpPost("alta")]
        public async Task<IActionResult> DarDeAlta([FromBody] Firma firma)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                // Verificar que la auditoría existe
                var auditoria = await contexto.Auditoria.FindAsync(firma.IdAuditoria);
                if (auditoria == null)
                {
                     Console.WriteLine($"Auditoría con Id {firma.IdAuditoria} no encontrada.");
                    return NotFound($"Auditoría con Id {firma.IdAuditoria} no encontrada.");
                }

                firma.FechaCreacion = DateOnly.FromDateTime(DateTime.Now);

                await contexto.Firma.AddAsync(firma);
                await contexto.SaveChangesAsync();

                return Ok(new
                {
                    message = "Firma creada correctamente",
                    firma
                });
            }
            catch (Exception ex)
            {
                var errorDetails = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return StatusCode(500, $"Error interno del servidor: {errorDetails}");
            }
        }
    }
}

