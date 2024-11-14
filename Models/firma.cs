using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace apiAuditoriaBPM.Models
{
    public class Firma
    {
        [Key]
        public int IdFirma { get; set; }

        [Required]
        public int IdAuditoria { get; set; }

        [Required]
        [MaxLength(200)]
        public string DatosFirma { get; set; }

        [ForeignKey(nameof(IdAuditoria))]
        public Auditoria? Auditoria { get; set; }

        public DateOnly FechaCreacion { get; set; }

    }
}