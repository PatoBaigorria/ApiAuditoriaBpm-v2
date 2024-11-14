using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace apiAuditoriaBPM.Models
{
    public class Auditoria
    {
        [Key]
        public int IdAuditoria { get; set; }

        [Required]
        public int IdOperario { get; set; }

        [ForeignKey(nameof(IdOperario))]
        public Operario? Operario { get; set; }

        [Required]
        public int IdActividad { get; set; }

        [ForeignKey(nameof(IdActividad))]
        public Actividad? Actividad { get; set; }

        [Required]
        public int IdLinea { get; set; }

        [ForeignKey(nameof(IdLinea))]
        public Linea? Linea { get; set; }

        [Required]
        public int IdSupervisor { get; set; }

        [ForeignKey(nameof(IdSupervisor))]
        public Supervisor? Supervisor { get; set; }

        public DateOnly Fecha { get; set; }

        public string? Comentario { get; set; }
        
    }
}