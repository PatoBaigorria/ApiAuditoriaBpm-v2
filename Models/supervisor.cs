using System.ComponentModel.DataAnnotations;

namespace apiAuditoriaBPM.Models
{
    public class Supervisor
    {

        [Key]
        public int IdSupervisor { get; set; }

        [Required]
        [MaxLength(50)]
        public string Nombre { get; set; }

        [Required]
        [MaxLength(50)]
        public string Apellido { get; set; }

        [Required]
        [MaxLength(50)]
        public string Email { get; set; }

        [Required]
        [Display(Name = "Legajo")]
        public int Legajo { get; set; }

        [Required]
        [MaxLength(255)]
        public string Password { get; set; }

        public override string ToString()
        {
            return $"{Apellido}, {Nombre}";
        }

    }
}