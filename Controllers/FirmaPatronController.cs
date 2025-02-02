using apiAuditoriaBPM.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace apiAuditoriaBPM.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class FirmaPatronController : ControllerBase
    {
        private readonly DataContext contexto;

        public FirmaPatronController(DataContext contexto)
        {
            this.contexto = contexto;
        }

        // POST: FirmaPatron/alta
        [HttpPost("alta")]
        public async Task<IActionResult> DarDeAlta([FromBody] FirmaPatron firmaPatron)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                // Verificar que el operario existe
                var operario = await contexto.Operario.FindAsync(firmaPatron.IdOperario);
                if (operario == null)
                {
                    return NotFound($"Operario con Id {firmaPatron.IdOperario} no encontrado.");
                }

                // Desactivar firmas patrón anteriores del operario
                var firmasAnteriores = await contexto.FirmaPatron
                    .Where(f => f.IdOperario == firmaPatron.IdOperario)
                    .ToListAsync();

                foreach (var firma in firmasAnteriores)
                {
                    firma.Activa = false;
                }

                // Configurar la nueva firma patrón
                firmaPatron.FechaCreacion = DateTime.Now;
                firmaPatron.Activa = true;

                // Generar hash de la firma
                firmaPatron.Hash = GenerarHashFirma(firmaPatron.Firma);

                await contexto.FirmaPatron.AddAsync(firmaPatron);
                await contexto.SaveChangesAsync();

                return Ok(new
                {
                    message = "Firma patrón creada correctamente",
                    firmaPatron
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno del servidor: {ex.Message}");
            }
        }

        // GET: FirmaPatron/operario/{idOperario}
        [HttpGet("operario/{idOperario}")]
        public async Task<IActionResult> ObtenerFirmaPatron(int idOperario)
        {
            try
            {
                var firmaPatron = await contexto.FirmaPatron
                    .Where(f => f.IdOperario == idOperario && f.Activa)
                    .FirstOrDefaultAsync();

                if (firmaPatron == null)
                {
                    return NotFound($"No se encontró firma patrón activa para el operario {idOperario}");
                }

                return Ok(firmaPatron);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno del servidor: {ex.Message}");
            }
        }

        // POST: FirmaPatron/verificar
        [HttpPost("verificar")]
        public async Task<IActionResult> VerificarFirma([FromBody] FirmaPatron firmaVerificar)
        {
            try
            {
                // Obtener la firma patrón activa del operario
                var firmaPatron = await contexto.FirmaPatron
                    .Where(f => f.IdOperario == firmaVerificar.IdOperario && f.Activa)
                    .FirstOrDefaultAsync();

                if (firmaPatron == null)
                {
                    return NotFound($"No se encontró firma patrón activa para el operario");
                }

                // Verificar la firma usando múltiples criterios
                bool coincide = VerificarFirmaCompleta(firmaVerificar, firmaPatron);

                return Ok(coincide);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno del servidor: {ex.Message}");
            }
        }

        private string GenerarHashFirma(string firma)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(firma);
                var hash = sha256.ComputeHash(bytes);
                return Convert.ToBase64String(hash);
            }
        }

        private bool VerificarFirmaCompleta(FirmaPatron firmaVerificar, FirmaPatron firmaPatron)
        {
            // Calcular diferencias
            float margenPuntos = Math.Abs(firmaVerificar.PuntosTotales - firmaPatron.PuntosTotales) / (float)firmaPatron.PuntosTotales;
            float margenVelocidad = Math.Abs(firmaVerificar.VelocidadMedia - firmaPatron.VelocidadMedia) / firmaPatron.VelocidadMedia;
            float margenPresion = Math.Abs(firmaVerificar.PresionMedia - firmaPatron.PresionMedia) / firmaPatron.PresionMedia;

            // Loguear las diferencias
            Console.WriteLine($"=== Verificación de firma para operario {firmaVerificar.IdOperario} ===");
            Console.WriteLine($"Puntos - Patrón: {firmaPatron.PuntosTotales}, Actual: {firmaVerificar.PuntosTotales}, Diferencia: {margenPuntos * 100:F2}% (máx 30%)");
            Console.WriteLine($"Velocidad - Patrón: {firmaPatron.VelocidadMedia:F2}, Actual: {firmaVerificar.VelocidadMedia:F2}, Diferencia: {margenVelocidad * 100:F2}% (máx 40%)");
            Console.WriteLine($"Presión - Patrón: {firmaPatron.PresionMedia:F2}, Actual: {firmaVerificar.PresionMedia:F2}, Diferencia: {margenPresion * 100:F2}% (máx 40%)");

            // Verificar puntos totales (permitir un margen de error del 30%)
            if (margenPuntos > 0.3f)
            {
                Console.WriteLine($"❌ Firma rechazada: diferencia en puntos ({margenPuntos * 100:F2}%) excede el límite de 30%");
                return false;
            }

            // Verificar velocidad media (permitir un margen de error del 40%)
            if (margenVelocidad > 0.4f)
            {
                Console.WriteLine($"❌ Firma rechazada: diferencia en velocidad ({margenVelocidad * 100:F2}%) excede el límite de 40%");
                return false;
            }

            // Verificar presión media (permitir un margen de error del 40%)
            if (margenPresion > 0.4f)
            {
                Console.WriteLine($"❌ Firma rechazada: diferencia en presión ({margenPresion * 100:F2}%) excede el límite de 40%");
                return false;
            }

            Console.WriteLine("✅ Firma verificada correctamente");
            Console.WriteLine("=====================================");
            return true;
        }
    }
}