using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


namespace apiAuditoriaBPM.Models
{
    public class SupervisorEstadisticaDTO
    {
        public Supervisor? Supervisor { get; set; }
        public int TotalAudits { get; set; }
        public int PositiveAudits { get; set; }
        public int NegativeAudits { get; set; }
    }
}
