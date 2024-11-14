using System.ComponentModel.DataAnnotations;
namespace apiAuditoriaBPM.Models
{
	public class LoginView
	{
		[Range(1, int.MaxValue, ErrorMessage = "El Legajo debe ser un número positivo")]
		public int Legajo { get; set; }
		[DataType(DataType.Password)]
		public string Clave { get; set; }
	}
}