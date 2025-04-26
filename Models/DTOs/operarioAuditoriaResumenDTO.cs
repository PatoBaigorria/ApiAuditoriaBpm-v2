using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


namespace apiAuditoriaBPM.Models
{
    public class OperarioAuditoriaResumenDTO
    {
        public int Legajo { get; set; }
        public string Nombre { get; set; }
        public int AuditoriasPositivas { get; set; }
        public int AuditoriasNegativas { get; set; }
    }
}