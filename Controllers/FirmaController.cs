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
        public async Task<ActionResult<Firma>> DarDeAlta([FromBody] Firma firma)
        {
            try
            {
                Console.WriteLine("=== Guardando firma ===");
                Console.WriteLine($"ID Auditoria: {firma.IdAuditoria}");
                Console.WriteLine($"No Conforme: {firma.NoConforme}");

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Buscar y actualizar la auditoría
                var auditoria = await contexto.Auditoria
                    .FirstOrDefaultAsync(a => a.IdAuditoria == firma.IdAuditoria);

                if (auditoria == null)
                {
                    return NotFound($"Auditoría con Id {firma.IdAuditoria} no encontrada.");
                }

                // Actualizar la auditoría con los datos de la firma
                auditoria.Firma = firma.DatosFirma;  // Guardar los datos SVG de la firma
                auditoria.NoConforme = firma.NoConforme;

                // Guardar la firma en su tabla
                await contexto.Firma.AddAsync(firma);

                // Guardar todos los cambios
                await contexto.SaveChangesAsync();

                Console.WriteLine("✅ Firma y auditoría actualizadas correctamente");
                return Ok(new { message = "Firma creada y auditoría actualizada correctamente", firma });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
                return StatusCode(500, $"Error interno del servidor: {ex.Message}");
            }
        }
    }
}


