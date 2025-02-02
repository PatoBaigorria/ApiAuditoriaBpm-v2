using Microsoft.EntityFrameworkCore;

namespace apiAuditoriaBPM.Models
{
	public class DataContext : DbContext
	{
		public DataContext(DbContextOptions<DataContext> options) : base(options)
		{ }
		public DbSet<Actividad> Actividad { get; set; }
		public DbSet<Auditoria> Auditoria { get; set; }
		public DbSet<AuditoriaItemBPM> AuditoriaItemBPM { get; set; }
		public DbSet<Firma> Firma { get; set; }
		public DbSet<ItemBPM> ItemBPM { get; set; }
		public DbSet<Linea> Linea { get; set; }
		public DbSet<Operario> Operario { get; set; }
		public DbSet<Supervisor> Supervisor { get; set; }
		public DbSet<FirmaPatron> FirmaPatron { get; set; }

	}
}