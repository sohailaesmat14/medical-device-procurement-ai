using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MediTender.API.Data;
using MediTender.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.RateLimiting;
using MediTender.API.Services;

namespace MediTender.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _dbContext;
        private readonly IEmailService _emailService;

        public AuthController(IConfiguration configuration, ApplicationDbContext dbContext, IEmailService emailService)
        {
            _configuration = configuration;
            _dbContext = dbContext;
            _emailService = emailService;
        }

        
        [HttpPost("login")]
        [EnableRateLimiting("LoginPolicy")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var adminUser = _configuration["AdminConfig:Username"];
            var adminPass = _configuration["AdminConfig:Password"];

            if (!string.IsNullOrEmpty(adminUser) && request.Username == adminUser && request.Password == adminPass)
            {
                var adminToken = GenerateAdminJwtToken(adminUser, "Committee");
                return Ok(new { Token = adminToken, Message = "Login Successful", Plan = "annually", FullName = "Committee Admin" });
            }

            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == request.Username);

            if (user != null)
            {
                if (!user.IsEmailVerified)
                    return Unauthorized(new { Message = "Please verify your email before logging in." });

                var passwordHasher = new PasswordHasher<ApplicationUser>();
                var verificationResult = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

                if (verificationResult == PasswordVerificationResult.Success || verificationResult == PasswordVerificationResult.SuccessRehashNeeded)
                {
                    var token = GenerateJwtToken(user);
                    return Ok(new { Token = token, Message = "Login Successful", Plan = user.Plan, FullName = user.FullName });
                }
            }

            return Unauthorized(new { Message = "Invalid email or password" });
        }       
        [HttpPost("signup")]
        public async Task<IActionResult> SignUp([FromBody] SignUpRequest request)
        {
            if (await _dbContext.Users.AnyAsync(u => u.Email == request.Email))
            {
                return BadRequest(new { Message = "Email already exists." });
            }

            // 1. Use Cryptographically Secure RNG
            var verificationCode = System.Security.Cryptography.RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

            var user = new ApplicationUser
            {
                FullName = request.FullName,
                Email = request.Email,
                Plan = "free",
                QuotaPoints = 200,
                SubscriptionExpiresAt = DateTime.UtcNow.AddDays(7),
                IsEmailVerified = false,
                VerificationToken = verificationCode,
                // 2. Add expiration time for the verification token (e.g., 24 hours)
                TokenExpiration = DateTime.UtcNow.AddHours(24) 
            };

            var passwordHasher = new PasswordHasher<ApplicationUser>();
            user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            var emailBody = $"<h3>Welcome to MediProcure AI!</h3><p>Your verification code is: <strong>{verificationCode}</strong></p><p>This code expires in 24 hours.</p>";
            await _emailService.SendEmailAsync(user.Email, "Verify Your Email", emailBody);

            return Ok(new { Message = "User created successfully. Please check your email to verify your account." });
        }

        [HttpPost("verify-email")]
        [EnableRateLimiting("LoginPolicy")] // 3. Prevent Brute Force Attacks
        public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            
            // 2. Check for token expiration alongside code validity
            if (user == null || user.VerificationToken != request.Code || user.TokenExpiration < DateTime.UtcNow)
                return BadRequest(new { Message = "Invalid or expired verification code." });

            user.IsEmailVerified = true;
            user.VerificationToken = string.Empty; 
            user.TokenExpiration = null; // Clear expiration after success
            await _dbContext.SaveChangesAsync();

            var token = GenerateJwtToken(user);
            return Ok(new { Token = token, Message = "Email verified successfully!" });
        }

        [HttpPost("forgot-password")]
        [EnableRateLimiting("LoginPolicy")] // 3. Prevent Brute Force Attacks
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (user == null) 
                return Ok(new { Message = "If the email exists, a reset code will be sent." }); 

            // 1. Use Cryptographically Secure RNG
            var resetCode = System.Security.Cryptography.RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
            
            user.ResetPasswordToken = resetCode;
            user.TokenExpiration = DateTime.UtcNow.AddMinutes(15);
            
            await _dbContext.SaveChangesAsync();

            var emailBody = $"<h3>Password Reset</h3><p>Your password reset code is: <strong>{resetCode}</strong></p><p>This code expires in 15 minutes.</p>";
            await _emailService.SendEmailAsync(user.Email, "Reset Your Password", emailBody);

            return Ok(new { Message = "If the email exists, a reset code will be sent." });
        }

        [HttpPost("reset-password")]
        [EnableRateLimiting("LoginPolicy")] // 3. Prevent Brute Force Attacks
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (user == null || user.ResetPasswordToken != request.Code || user.TokenExpiration < DateTime.UtcNow)
                return BadRequest(new { Message = "Invalid or expired reset code." });

            var passwordHasher = new PasswordHasher<ApplicationUser>();
            user.PasswordHash = passwordHasher.HashPassword(user, request.NewPassword);
            
            user.ResetPasswordToken = string.Empty;
            user.TokenExpiration = null;
            
            await _dbContext.SaveChangesAsync();

            return Ok(new { Message = "Password has been reset successfully. You can now login." });
        }
        [HttpPost("update-plan")]
        [Authorize]
        public async Task<IActionResult> UpdatePlan([FromBody] UpdatePlanRequest request)
        {
            // 1. Security Check: Block any attempt to manually set a paid plan
            if (string.IsNullOrWhiteSpace(request.Plan) || request.Plan.ToLowerInvariant() != "free")
            {
                return BadRequest(new { Message = "Unauthorized action. Paid plans can only be activated through the secure payment gateway." });
            }

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

            // 2. Hardcode the values server-side (Ignore request.QuotaPoints completely)
            user.Plan = "free";
            user.QuotaPoints = 200; 
            
            await _dbContext.SaveChangesAsync();

            return Ok(new { Message = "Free plan activated successfully" });
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

    public class VerifyEmailRequest
    {
        [Required, EmailAddress] public string Email { get; set; } = string.Empty;
        [Required] public string Code { get; set; } = string.Empty;
    }

    public class ForgotPasswordRequest
    {
        [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    }

    public class ResetPasswordRequest
    {
        [Required, EmailAddress] public string Email { get; set; } = string.Empty;
        [Required] public string Code { get; set; } = string.Empty;
        [Required, MinLength(6)] public string NewPassword { get; set; } = string.Empty;
    }
    public class LoginRequest
    {
        [Required]
        [EmailAddress]
        public string Username { get; set; } = string.Empty;

        [Required]
        [MinLength(6)]
        public string Password { get; set; } = string.Empty;
    }

    public class SignUpRequest
    {
        [Required]
        [MinLength(3)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MinLength(6)]
        public string Password { get; set; } = string.Empty;
    }
    
    public class UpdatePlanRequest
    {
        [Required]
        public string Plan { get; set; } = string.Empty;
        
        [Required]
        public int QuotaPoints { get; set; }
    }
}