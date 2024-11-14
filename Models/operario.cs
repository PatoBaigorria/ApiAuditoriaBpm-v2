using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace apiAuditoriaBPM.Models
{
    public class Operario
    {
        [Key]
        public int IdOperario { get; set; }

        [Required]
        [MaxLength(50)]
        public string Nombre { get; set; }

        [Required]
        [MaxLength(50)]
        public string Apellido { get; set; }

        [Required]
        [MaxLength(11)]
        public int Legajo { get; set; }

        [Required]
        public int IdActividad { get; set; }

        [ForeignKey(nameof(IdActividad))]
        public Actividad? Actividad { get; set; }

        [Required]
        public int IdLinea { get; set; }

        [ForeignKey(nameof(IdLinea))]
        public Linea? Linea { get; set; }

        public string Email { get; set; } = "";

        // Método para obtener el nombre completo
        public string ObtenerNombreCompleto()
        {
            return $"{Nombre} {Apellido}";
        }

        // Para facilitar la visualización en logs o debug
        public override string ToString()
        {
            return ObtenerNombreCompleto(); // Devuelve el nombre completo
        }
    }
}
