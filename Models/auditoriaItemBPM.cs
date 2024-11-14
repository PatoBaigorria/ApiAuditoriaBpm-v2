using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace apiAuditoriaBPM.Models
{
    public enum EstadoEnum { OK = 1, NOOK = 2, NA = 3 }
    public class AuditoriaItemBPM
    {
        [Key]
        public int IdAuditoriaItemBPM { get; set; }

        [Required] 
        public int IdAuditoria{ get; set; }

        [Required]
        public int IdItemBPM { get; set; }

        [Required]
        public EstadoEnum Estado { get; set; }

        public string? Comentario { get; set; }

        [ForeignKey(nameof(IdAuditoria))]
        [JsonIgnore] 
        public Auditoria? Auditoria { get; set; }

        [ForeignKey(nameof(IdItemBPM))]
        public ItemBPM? ItemBPM { get; set; }	
     }
}
