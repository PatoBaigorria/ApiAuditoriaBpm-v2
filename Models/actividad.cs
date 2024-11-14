using System.ComponentModel.DataAnnotations;

namespace apiAuditoriaBPM.Models
{
    public class Actividad
    {
        [Key]
        public int IdActividad { get; set; }

        [Required]
        [MaxLength(50)]
        public string Descripcion { get; set; }

    }
}