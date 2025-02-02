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

        public bool NoConforme { get; set; }

        [Required]
        public string DatosFirma { get; set; }

        [ForeignKey(nameof(IdAuditoria))]
        public Auditoria? Auditoria { get; set; }

        public string FechaCreacion { get; set; }

    }
}