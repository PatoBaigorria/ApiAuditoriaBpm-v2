using apiAuditoriaBPM.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace apiAuditoriaBPM.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AuditoriasItemBPMController : ControllerBase
    {
        private readonly DataContext contexto;

        public AuditoriasItemBPMController(DataContext contexto)
        {
            this.contexto = contexto;
        }
        [HttpGet]
        public async Task<ActionResult<List<AuditoriaItemBPM>>> GetAuditoriaItems()
        {
            var items = await contexto.AuditoriaItemBPM
                                      .Include(a => a.Auditoria)
                                      .Include(a => a.ItemBPM)
                                      .Select(item => new AuditoriaItemBPM
                                      {
                                          IdAuditoriaItemBPM = item.IdAuditoriaItemBPM,
                                          IdAuditoria = item.IdAuditoria,
                                          IdItemBPM = item.IdItemBPM,
                                          Estado = item.Estado,
                                          Comentario = item.Auditoria.Comentario != null ? item.Auditoria.Comentario : ""
                                      })
                                      .ToListAsync();

            return items;
        }

        // GET Items No Ok con comentarios de Auditoria e Items
        [HttpGet("estado-nook")]
        public async Task<ActionResult<List<object>>> GetAuditoriaItemsWithNoOkEstado([FromForm] int legajo)
        {
            var operario = await contexto.Operario
                .FirstOrDefaultAsync(o => o.Legajo == legajo);

            if (operario == null)
            {
                return NotFound("Operario no encontrado.");
            }

            // Obtener los items con estado NoOk
            var items = await contexto.AuditoriaItemBPM
                                      .Where(a => a.Estado == EstadoEnum.NOOK)
                                      .Include(a => a.Auditoria)
                                      .ThenInclude(a => a.Operario)
                                      .Where(a => a.Auditoria.Operario.Legajo == legajo)
                                      .Include(a => a.ItemBPM)
                                      .ToListAsync(); // Traer todos los items

            if (items == null || !items.Any())
            {
                return NotFound("No se encontraron items con estado No Ok.");
            }

            // Agrupar por Descripción
            var groupedItems = items.GroupBy(a => a.ItemBPM.Descripcion)
                                    .Select(g => new
                                    {
                                        Operario = g.FirstOrDefault().Auditoria.Operario.ObtenerNombreCompleto(),
                                        Descripcion = g.Key, // Descripción del item
                                        Count = g.Count(), // Contar la cantidad de veces que se repite

                                        // Incluir todos los comentarios de Auditoria y AuditoriaItemBPM
                                        ComentariosAuditoria = g.Select(a => a.Auditoria.Comentario ?? "").Distinct().ToList(),
                                        ComentariosItem = g.Select(a => a.Comentario ?? "").Distinct().ToList()
                                    })
                                    .ToList(); // Convertir a lista

            // Retornar los items encontrados, sus cantidades y todos los comentarios
            return Ok(groupedItems);
        }

        [HttpPost("alta-items")]
        public async Task<IActionResult> DarDeAltaItem([FromBody] AuditoriaItemBPM auditoriaItem)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);

                
            }

            try
            {
                await contexto.AuditoriaItemBPM.AddAsync(auditoriaItem);
                await contexto.SaveChangesAsync();
                return Ok(new
                {
                    message = "Ítem de auditoría creado correctamente",
                    auditoriaItem
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno del servidor: {ex.Message}");
            }
        }




    }
}