using System.ComponentModel.DataAnnotations;

namespace apiAuditoriaBPM.Models
{
    public class Linea
    {
        [Key]
        public int IdLinea { get; set; }

        [Required]
        [MaxLength(10)]
        public string Descripcion { get; set; }

    }
}