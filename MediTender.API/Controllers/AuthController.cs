using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MediTender.API.Data;
using MediTender.API.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;


namespace MediTender.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _dbContext;

        public AuthController(IConfiguration configuration, ApplicationDbContext dbContext)
        {
            _configuration = configuration;
            _dbContext = dbContext;
        }

        [HttpPost("signup")]
        public async Task<IActionResult> SignUp([FromBody] SignUpRequest request)
        {
            if (await _dbContext.Users.AnyAsync(u => u.Email == request.Email))
            {
                return BadRequest(new { Message = "Email already exists." });
            }

            var user = new ApplicationUser
            {
                FullName = request.FullName,
                Email = request.Email,
                PasswordHash = HashPassword(request.Password),
                Plan = "free", // Default plan initially
                QuotaPoints = 200 // Default Quota
            };

            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            var token = GenerateJwtToken(user);
            return Ok(new { Token = token, Message = "User created successfully" });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var adminUser = _configuration["AdminConfig:Username"];
            var adminPass = _configuration["AdminConfig:Password"];

            if (!string.IsNullOrEmpty(adminUser) && request.Username == adminUser && request.Password == adminPass)
            {
                var adminToken = GenerateAdminJwtToken(adminUser, "Committee");
                return Ok(new { Token = adminToken, Message = "Login Successful", Plan = "annually", FullName = "Committee Admin" });
            }

            var hashedInputPassword = HashPassword(request.Password);
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == request.Username && u.PasswordHash == hashedInputPassword);

            if (user != null)
            {
                var token = GenerateJwtToken(user);
                return Ok(new { Token = token, Message = "Login Successful", Plan = user.Plan, FullName = user.FullName });
            }

            return Unauthorized(new { Message = "Invalid email or password" });
        }
        [HttpPost("update-plan")]
        [Authorize]
        public async Task<IActionResult> UpdatePlan([FromBody] UpdatePlanRequest request)
        {
            var userEmailStr = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value 
                                ?? User.Claims.FirstOrDefault(c => c.Type == System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
            
            if (string.IsNullOrEmpty(userEmailStr))
            {
                return Unauthorized(new { Message = "User email not found in token." });
            }

            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == userEmailStr);
            
            if (user == null)
            {
                return NotFound(new { Message = "User not found." });
            }

            user.Plan = request.Plan;
            user.QuotaPoints = request.QuotaPoints;
            
            await _dbContext.SaveChangesAsync();

            return Ok(new { Message = "Plan updated successfully" });
        }        
                
        private string HashPassword(string password)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(password));
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }

        private string GenerateJwtToken(ApplicationUser user)
        {
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Email),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim("FullName", user.FullName),
                new Claim("Plan", user.Plan),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };
            
            return CreateToken(claims);
        }

        private string GenerateAdminJwtToken(string username, string role)
        {
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, username),
                new Claim(ClaimTypes.Role, role),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };
            return CreateToken(claims);
        }
        
        private string CreateToken(Claim[] claims)
        {
            var jwtKey = _configuration["Jwt:Key"] ?? throw new ArgumentNullException("Jwt:Key is missing in appsettings.json");
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.Now.AddHours(24), 
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }

    public class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class SignUpRequest
    {
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
    
    public class UpdatePlanRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Plan { get; set; } = string.Empty;
        public int QuotaPoints { get; set; }
    }
}