using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Sockets;
using System.Security.Claims;
using System.Text;
using apiAuditoriaBPM.Models;
//using MailKit.Net.Smtp;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
//using MimeKit;

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

        [HttpGet]
        public async Task<ActionResult<Supervisor>> Get()
        {

            try
            {
                // Extrae el Legajo del claim
                /*var legajoClaim = User.Claims.FirstOrDefault(c => c.Name == "Legajo");
                if (legajoClaim == null)
                {
                    return Unauthorized("No se pudo encontrar el Legajo en el token.");
                }*/
                var legajo = int.Parse(User.FindFirstValue("Legajo"));
                

                // Busca al usuario en la base de datos
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

    }


}