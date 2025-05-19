using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Sockets;
using System.Security.Claims;
using System.Text;
using apiAuditoriaBPM.Models;
using MailKit.Net.Smtp;  // Descomentado
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MimeKit;  // Descomentado

namespace apiAuditoriaBPM.Controllers
{
    [Route("[controller]")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class SupervisoresController : ControllerBase
    {
        private readonly DataContext contexto;
        private readonly IConfiguration config;

        public SupervisoresController(DataContext contexto, IConfiguration config)
        {
            this.contexto = contexto;
            this.config = config;
        }

        // Método auxiliar para obtener la dirección IP local
        private string GetLocalIpAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            return "localhost";
        }

        [HttpGet]
        public async Task<ActionResult<Supervisor>> Get()
        {
            try
            {
                var legajo = int.Parse(User.FindFirstValue("Legajo"));

                // Asegúrate de usar el nombre correcto de la tabla
                var supervisor = await contexto.Supervisor.SingleOrDefaultAsync(x => x.Legajo == legajo);

                if (supervisor == null)
                {
                    return NotFound("Supervisor no encontrado.");
                }

                return Ok(supervisor);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromForm] LoginView loginView)
        {
            try
            {
                byte[] saltBytes = Encoding.ASCII.GetBytes(config["Salt"]);
                string hashed = Convert.ToBase64String(KeyDerivation.Pbkdf2(
                    password: loginView.Clave,
                    salt: saltBytes,
                    prf: KeyDerivationPrf.HMACSHA1,
                    iterationCount: 1000,
                    numBytesRequested: 256 / 8));
                var u = await contexto.Supervisor.FirstOrDefaultAsync(x => x.Legajo == loginView.Legajo);

                // Asegúrate de usar el nombre correcto del campo (Password o Clave)
                if (u == null || u.Password != hashed)
                {
                    return BadRequest("Nombre de Supervisor o Password incorrecta");
                }
                else
                {
                    var key = new SymmetricSecurityKey(
                        Encoding.ASCII.GetBytes(config["TokenAuthentication:SecretKey"]));
                    var credenciales = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.Name, u.Legajo.ToString()),
                        new Claim("Legajo", u.Legajo.ToString()),
                        new Claim("FullName", u.Nombre + " " + u.Apellido),
                        new Claim(ClaimTypes.Role, "Supervisor"),
                    };
                    var token = new JwtSecurityToken(
                        issuer: config["TokenAuthentication:Issuer"],
                        audience: config["TokenAuthentication:Audience"],
                        claims: claims,
                        expires: DateTime.Now.AddHours(6),
                        signingCredentials: credenciales
                    );
                    return Ok(new JwtSecurityTokenHandler().WriteToken(token));
                }
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("todos")]
        public async Task<ActionResult<IEnumerable<Supervisor>>> GetTodos()
        {
            try
            {
                var supervisores = await contexto.Supervisor.ToListAsync();
                return Ok(supervisores);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("olvidecontrasena")]
        [AllowAnonymous]
        public async Task<IActionResult> EnviarEmail([FromForm] string email)
        {
            try
            {
                // Validar que el email no sea nulo
                if (string.IsNullOrEmpty(email))
                {
                    return BadRequest("El correo electrónico es obligatorio");
                }

                // Asegúrate de usar el nombre correcto de la tabla
                var supervisor = await contexto.Supervisor.FirstOrDefaultAsync(x => x.Email == email);
                if (supervisor == null)
                {
                    return NotFound("No se encontró ningún supervisor con esta dirección de correo electrónico.");
                }

                // Validar que el supervisor tenga un email válido
                if (string.IsNullOrEmpty(supervisor.Email))
                {
                    return BadRequest("El supervisor no tiene una dirección de correo electrónico válida.");
                }

                var key = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(config["TokenAuthentication:SecretKey"]));
                var credenciales = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
                var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, supervisor.Email),
            new Claim("FullName", $"{supervisor.Nombre ?? "Usuario"} {supervisor.Apellido ?? ""}"),
            new Claim(ClaimTypes.Role, "Supervisor"),
        };
                var token = new JwtSecurityToken(
                    issuer: config["TokenAuthentication:Issuer"],
                    audience: config["TokenAuthentication:Audience"],
                    claims: claims,
                    expires: DateTime.Now.AddMinutes(5),
                    signingCredentials: credenciales
                );
                var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
                var dominio = HttpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString();
                var baseUrl = "http://localhost:5000";
                var resetLink = "/Account/CambiarPassword";
                var rutaCompleta = baseUrl + resetLink;
                var message = new MimeMessage();

                // Usar un valor predeterminado para el nombre si es nulo
                string nombreSupervisor = supervisor.Nombre ?? "Usuario";
                message.To.Add(new MailboxAddress(nombreSupervisor, supervisor.Email));
                message.From.Add(new MailboxAddress("Sistema BPM", "mpbaigorria.01@gmail.com"));
                message.Subject = "Restablecimiento de Contraseña";
                message.Body = new TextPart("html")
                {
                    Text = $@"<h1>Hola {nombreSupervisor},</h1>
                   <p>Hemos recibido una solicitud para restablecer la contraseña de tu cuenta.
                    <p>Por favor, haz clic en el siguiente enlace para crear una nueva contraseña:</p>
                   <a href='{rutaCompleta}?access_token={tokenString}'>{rutaCompleta}?access_token={tokenString}</a>"
                };

                // Verificar la configuración SMTP
                string smtpServer = config["SMTP:Server"];
                int smtpPort = int.Parse(config["SMTP:Port"]);
                string smtpUser = config["SMTP:User"];
                string smtpPass = config["SMTP:Pass"];

                if (string.IsNullOrEmpty(smtpServer) || string.IsNullOrEmpty(smtpUser) || string.IsNullOrEmpty(smtpPass))
                {
                    return BadRequest("La configuración de correo electrónico no está completa.");
                }

                using var client = new SmtpClient();
                client.ServerCertificateValidationCallback = (s, c, h, e) => true;
                await client.ConnectAsync(smtpServer, smtpPort, MailKit.Security.SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(smtpUser, smtpPass);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);
                return Ok("Se ha enviado el enlace de restablecimiento de contraseña correctamente.");
            }
            catch (Exception ex)
            {
                return BadRequest($"Error: {ex.Message}");
            }
        }

        [HttpPost("cambiarpassword")]
        public async Task<IActionResult> CambiarPassword([FromForm] string claveNueva, [FromForm] string repetirClaveNueva)
        {
            try
            {
                // Validar que las contraseñas no sean nulas
                if (string.IsNullOrEmpty(claveNueva) || string.IsNullOrEmpty(repetirClaveNueva))
                {
                    return BadRequest("Debe proporcionar una nueva contraseña");
                }

                // Validar que las contraseñas coincidan
                if (claveNueva != repetirClaveNueva)
                {
                    return BadRequest("Las contraseñas no coinciden");
                }

                // Obtener el email del usuario desde el token
                var identity = HttpContext.User.Identity as ClaimsIdentity;
                if (identity == null)
                {
                    return BadRequest("No se pudo identificar al usuario");
                }

                var emailClaim = identity.FindFirst(ClaimTypes.Name);
                if (emailClaim == null || string.IsNullOrEmpty(emailClaim.Value))
                {
                    return BadRequest("No se pudo obtener el correo electrónico del usuario");
                }

                var email = emailClaim.Value;

                // Buscar al supervisor por su email
                var supervisor = await contexto.Supervisor.FirstOrDefaultAsync(x => x.Email == email);
                if (supervisor == null)
                {
                    return NotFound("No se encontró ningún supervisor con esta dirección de correo electrónico");
                }

                // Actualizar la contraseña
                supervisor.Password = Convert.ToBase64String(KeyDerivation.Pbkdf2(
                    password: claveNueva,
                    salt: Encoding.ASCII.GetBytes(config["Salt"]),
                    prf: KeyDerivationPrf.HMACSHA1,
                    iterationCount: 1000,
                    numBytesRequested: 256 / 8));

                contexto.Supervisor.Update(supervisor);
                await contexto.SaveChangesAsync();

                // Enviar correo de confirmación
                var message = new MimeMessage();
                message.To.Add(new MailboxAddress(supervisor.Nombre ?? "Usuario", supervisor.Email));
                message.From.Add(new MailboxAddress("Sistema BPM", "noreply@bpm.com"));
                message.Subject = "Confirmación de cambio de contraseña";
                message.Body = new TextPart("html")
                {
                    Text = $@"<h1>Hola {supervisor.Nombre ?? "Usuario"},</h1>
                   <p>Tu contraseña ha sido cambiada exitosamente.</p>
                   <p>Si no realizaste este cambio, por favor contacta al administrador del sistema.</p>"
                };

                // Verificar la configuración SMTP
                string smtpServer = config["SMTP:Server"];
                int smtpPort = int.Parse(config["SMTP:Port"]);
                string smtpUser = config["SMTP:User"];
                string smtpPass = config["SMTP:Pass"];

                if (string.IsNullOrEmpty(smtpServer) || string.IsNullOrEmpty(smtpUser) || string.IsNullOrEmpty(smtpPass))
                {
                    // Si no hay configuración SMTP, solo devolver éxito sin enviar correo
                    return Ok("Contraseña cambiada exitosamente");
                }

                try
                {
                    using var client = new SmtpClient();
                    client.ServerCertificateValidationCallback = (s, c, h, e) => true;
                    await client.ConnectAsync(smtpServer, smtpPort, MailKit.Security.SecureSocketOptions.StartTls);
                    await client.AuthenticateAsync(smtpUser, smtpPass);
                    await client.SendAsync(message);
                    await client.DisconnectAsync(true);
                }
                catch (Exception ex)
                {
                    // Si falla el envío del correo, solo registrar el error pero no fallar la operación
                    Console.WriteLine($"Error al enviar correo: {ex.Message}");
                }

                return Ok("Contraseña cambiada exitosamente");
            }
            catch (Exception ex)
            {
                return BadRequest($"Error: {ex.Message}");
            }
        }

        [HttpPut("cambiarviejacontrasena")]
        public async Task<IActionResult> CambiarPasswordPorInput([FromForm] string claveVieja, [FromForm] string claveNueva, [FromForm] string repetirClaveNueva)
        {
            try
            {
                if (string.IsNullOrEmpty(claveVieja) || string.IsNullOrEmpty(claveNueva) || string.IsNullOrEmpty(repetirClaveNueva))
                {
                    return BadRequest("Todos los campos son obligatorios");
                }

                if (claveNueva != repetirClaveNueva)
                {
                    return BadRequest("La clave nueva no coincide");
                }

                var usuario = User.Identity.Name;
                // Asegúrate de usar el nombre correcto de la tabla
                var supervisor = await contexto.Supervisor.AsNoTracking().FirstOrDefaultAsync(x => x.Email == usuario);
                if (supervisor == null)
                {
                    return NotFound("Supervisor no encontrado");
                }

                string hashedVieja = Convert.ToBase64String(KeyDerivation.Pbkdf2(
                        password: claveVieja,
                        salt: Encoding.ASCII.GetBytes(config["Salt"]),
                        prf: KeyDerivationPrf.HMACSHA1,
                        iterationCount: 1000,
                        numBytesRequested: 256 / 8));

                // Asegúrate de usar el nombre correcto del campo (Password o Clave)
                if (supervisor.Password != hashedVieja)
                {
                    return BadRequest("Clave incorrecta");
                }

                string hashedNueva = Convert.ToBase64String(KeyDerivation.Pbkdf2(
                    password: claveNueva,
                    salt: Encoding.ASCII.GetBytes(config["Salt"]),
                    prf: KeyDerivationPrf.HMACSHA1,
                    iterationCount: 1000,
                    numBytesRequested: 256 / 8));

                // Asegúrate de usar el nombre correcto del campo (Password o Clave)
                supervisor.Password = hashedNueva;
                contexto.Supervisor.Update(supervisor);
                await contexto.SaveChangesAsync();
                return Ok("Contraseña cambiada con éxito");
            }
            catch (Exception ex)
            {
                return BadRequest($"Error: {ex.Message}");
            }
        }
    }
}