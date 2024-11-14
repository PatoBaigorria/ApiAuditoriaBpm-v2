using System.ComponentModel.DataAnnotations;

namespace apiAuditoriaBPM.Models
{
    public class ItemBPM
    {
        [Key]
        public int IdItem { get; set; }

        [Required]
        [MaxLength(150)]
        public string Descripcion { get; set; }

    }
}