using apiAuditoriaBPM.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace apiAuditoriaBPM.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ItemsBPMController : ControllerBase
    {
        private readonly DataContext contexto;
        public ItemsBPMController(DataContext contexto)
        {
            this.contexto = contexto;
        }
        [HttpGet]
        public async Task<ActionResult<List<ItemBPM>>> Get()
        {
            try
            {
                var itemsBPM = await contexto.ItemBPM.ToListAsync();
                return Ok(itemsBPM);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
            
        }
    }
}