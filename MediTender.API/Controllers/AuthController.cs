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

        private string GetEmailTemplate(string title, string name, string message, string code, string expiryWarning)
        {
            return $@"
            <!DOCTYPE html>
            <html lang='en'>
            <head>
                <meta charset='utf-8'>
                <meta name='viewport' content='width=device-width, initial-scale=1.0'>
            </head>
            <body style='margin: 0; padding: 0; background-color: #f4f7f9; font-family: ""Segoe UI"", Tahoma, Geneva, Verdana, sans-serif;'>
                <table border='0' cellpadding='0' cellspacing='0' width='100%' style='max-width: 600px; margin: 40px auto; background-color: #ffffff; border-radius: 12px; overflow: hidden; box-shadow: 0 10px 25px rgba(0,0,0,0.05);'>
                    <!-- Header -->
                    <tr>
                        <td align='center' style='background: #0f172a; padding: 35px 20px;'>
                            <h1 style='color: #ffffff; margin: 0; font-size: 28px; letter-spacing: 1px; font-weight: 800;'>
                                MediProcure <span style='color: var(--success-color);'>AI</span>
                            </h1>
                            <p style='color: #94a3b8; margin: 10px 0 0 0; font-size: 14px; text-transform: uppercase; letter-spacing: 2px;'>{title}</p>
                        </td>
                    </tr>
                    
                    <!-- Body -->
                    <tr>
                        <td style='padding: 40px 30px;'>
                            <p style='margin: 0 0 20px 0; font-size: 16px; color: #334155;'>Hello <strong>{name}</strong>,</p>
                            <p style='margin: 0 0 30px 0; font-size: 16px; color: #475569; line-height: 1.6;'>
                                {message}
                            </p>
                            
                            <!-- Code Box -->
                            <div style='background-color: #f8fafc; border: 2px solid #e2e8f0; border-radius: 10px; padding: 25px; text-align: center; margin-bottom: 30px;'>
                                <span style='font-family: monospace; font-size: 34px; font-weight: 900; color: #2563eb; letter-spacing: 8px;'>
                                    {code}
                                </span>
                            </div>
                            
                            <!-- Warning / Info Box -->
                            <div style='background-color: #fef2f2; border-left: 4px solid var(--danger-color); padding: 15px 20px; border-radius: 0 8px 8px 0;'>
                                <p style='margin: 0; font-size: 14px; color: #991b1b; font-weight: 600;'>
                                    ⚠️ {expiryWarning}
                                </p>
                                <p style='margin: 5px 0 0 0; font-size: 13px; color: #b91c1c;'>
                                    If you didn't request this action, please ignore this email to keep your account secure.
                                </p>
                            </div>
                        </td>
                    </tr>
                    
                    <!-- Footer -->
                    <tr>
                        <td align='center' style='background-color: #f8fafc; padding: 20px; border-top: 1px solid #e2e8f0;'>
                            <p style='margin: 0; font-size: 12px; color: #64748b; line-height: 1.5;'>
                                &copy; {DateTime.UtcNow.Year} MediTender Smart Assistant.<br>
                                Alexandria, Egypt
                            </p>
                        </td>
                    </tr>
                </table>
            </body>
            </html>";
        }
        
        [HttpGet("billing-config")]
        [AllowAnonymous]
        public IActionResult GetBillingConfig()
        {
            return Ok(new
            {
                FreeTrialQuota = BillingConstants.FreeTrialQuota,
                MonthlyQuota = BillingConstants.MonthlyQuota,
                AnnualQuota = BillingConstants.AnnualQuota,
                MonthlyPlanPrice = BillingConstants.MonthlyPlanPrice,
                AnnualPlanPrice = BillingConstants.AnnualPlanPrice,
                ExtractionCost = BillingConstants.ExtractionCost,
                PerVendorCost = BillingConstants.PerVendorCost
            });
        }

        [HttpPost("login")]
        [EnableRateLimiting("LoginPolicy")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == request.Username);

            if (user != null)
            {
                var passwordHasher = new PasswordHasher<ApplicationUser>();
                var verificationResult = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

                if (verificationResult == PasswordVerificationResult.Success || verificationResult == PasswordVerificationResult.SuccessRehashNeeded)
                {
                    if (!user.IsEmailVerified)
                    {
                        var newCode = System.Security.Cryptography.RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
                        user.VerificationToken = newCode;
                        user.VerificationTokenExpiration = DateTime.UtcNow.AddHours(24);
                        await _dbContext.SaveChangesAsync();

                        var emailBody = GetEmailTemplate(
                            "Verify Your Account", 
                            user.FullName, 
                            "Welcome back! To secure your account, please verify your email using the code below:", 
                            newCode, 
                            "This code will expire in 24 hours.");

                        try { await _emailService.SendEmailAsync(user.Email, "Verify Your Email", emailBody); } catch { }

                        return StatusCode(403, new { Message = "Email not verified. A new code has been sent.", RequiresVerification = true });
                    }

                    if (verificationResult == PasswordVerificationResult.SuccessRehashNeeded)
                    {
                        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
                        await _dbContext.SaveChangesAsync();
                    }

                    var token = GenerateJwtToken(user);
                    return Ok(new { Token = token, Message = "Login Successful", Plan = user.Plan, FullName = user.FullName });
                }
            }

            return Unauthorized(new { Message = "Invalid email or password" });
        }

        [HttpPost("signup")]
        [EnableRateLimiting("LoginPolicy")]
        public async Task<IActionResult> SignUp([FromBody] SignUpRequest request)
        {
            if (await _dbContext.Users.AnyAsync(u => u.Email == request.Email))
            {
                return BadRequest(new { Message = "Email already exists." });
            }

            var verificationCode = System.Security.Cryptography.RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

            var user = new ApplicationUser
            {
                FullName = request.FullName,
                Email = request.Email,
                Plan = "free",
                QuotaPoints = BillingConstants.FreeTrialQuota,
                SubscriptionExpiresAt = DateTime.UtcNow.AddDays(7),
                IsEmailVerified = false,
                VerificationToken = verificationCode,
                VerificationTokenExpiration = DateTime.UtcNow.AddHours(24) 
            };

            var passwordHasher = new PasswordHasher<ApplicationUser>();
            user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
            var expectedSecret = _configuration["SecuritySettings:CommitteeSecret"];
            bool isCommitteeEmail = request.Email.EndsWith("@meditender.gov.eg");
            bool hasValidSecret = !string.IsNullOrEmpty(request.CommitteeSecret) && request.CommitteeSecret == expectedSecret;

            user.Role = (isCommitteeEmail && hasValidSecret) ? "Committee" : "Vendor";
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            var emailBody = GetEmailTemplate(
                "Verify Your Account", 
                user.FullName, 
                "Welcome to the future of intelligent tendering. Please use the verification code below to complete your registration:", 
                verificationCode, 
                "This code will expire in 24 hours.");

            try
            {
                await _emailService.SendEmailAsync(user.Email, "Verify Your Email", emailBody);
                return Ok(new { Message = "User created successfully. Please check your email to verify your account." });
            }
            catch (Exception)
            {
                _dbContext.Users.Remove(user);
                await _dbContext.SaveChangesAsync();
                return StatusCode(500, new { Message = "Account could not be created because the verification email failed to send. Please try again later." });
            }
        }

        [HttpPost("verify-email")]
        [EnableRateLimiting("LoginPolicy")]
        public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            
            if (user == null || user.VerificationToken != request.Code || user.VerificationTokenExpiration < DateTime.UtcNow)
                return BadRequest(new { Message = "Invalid or expired verification code." });

            user.IsEmailVerified = true;
            user.VerificationToken = string.Empty; 
            user.VerificationTokenExpiration = null; 
            await _dbContext.SaveChangesAsync();

            var token = GenerateJwtToken(user);
            return Ok(new { Token = token, Message = "Email verified successfully!", FullName = user.FullName });
        }

        [HttpPost("forgot-password")]
        [EnableRateLimiting("LoginPolicy")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (user == null) 
                return Ok(new { Message = "If the email exists, a reset code will be sent." }); 

            var resetCode = System.Security.Cryptography.RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
            
            user.ResetPasswordToken = resetCode;
            user.ResetPasswordTokenExpiration = DateTime.UtcNow.AddMinutes(15);
            
            await _dbContext.SaveChangesAsync();

            var emailBody = GetEmailTemplate(
                "Reset Your Password", 
                user.FullName, 
                "We received a request to reset the password for your account. Please use the following code to proceed:", 
                resetCode, 
                "This code will expire in 15 minutes.");
            
            try
            {
                await _emailService.SendEmailAsync(user.Email, "Reset Your Password", emailBody);
            }
            catch (Exception)
            {
            }

            return Ok(new { Message = "If the email exists, a reset code will be sent." });
        }

        [HttpPost("reset-password")]
        [EnableRateLimiting("LoginPolicy")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (user == null || user.ResetPasswordToken != request.Code || user.ResetPasswordTokenExpiration < DateTime.UtcNow)
                return BadRequest(new { Message = "Invalid or expired reset code." });

            var passwordHasher = new PasswordHasher<ApplicationUser>();
            user.PasswordHash = passwordHasher.HashPassword(user, request.NewPassword);
            
            user.ResetPasswordToken = string.Empty;
            user.ResetPasswordTokenExpiration = null;
            
            await _dbContext.SaveChangesAsync();

            return Ok(new { Message = "Password has been reset successfully. You can now login." });
        }

        [HttpPost("update-plan")]
        [Authorize] 
        public async Task<IActionResult> UpdatePlan([FromBody] UpdatePlanRequest request)
        {
            if (request.Plan.ToLower() != "free")
            {
                return BadRequest(new { Message = "Unauthorized plan update. Paid subscriptions must be processed via the secure payment gateway." });
            }

            var tokenEmail = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value 
                          ?? User.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value 
                          ?? User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier && c.Value.Contains("@"))?.Value;

            if (string.IsNullOrEmpty(tokenEmail) || tokenEmail != request.Email)
            {
                return Unauthorized(new { Message = "Invalid or unauthorized request." });
            }

            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (user == null) return NotFound(new { Message = "User not found." });

            user.Plan = "free";
            user.QuotaPoints = Models.BillingConstants.FreeTrialQuota; 
            user.SubscriptionExpiresAt = DateTime.UtcNow.AddDays(7);
            
            await _dbContext.SaveChangesAsync();

            return Ok(new { Message = "Free trial activated successfully.", Plan = user.Plan, QuotaPoints = user.QuotaPoints });
        }        
        
        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> Me()
        {
            var userEmail = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value 
                         ?? User.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value 
                         ?? User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier && c.Value.Contains("@"))?.Value;

            if (string.IsNullOrEmpty(userEmail))
                return Unauthorized();

            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == userEmail);
            if (user == null)
                return Unauthorized();

            return Ok(new
            {
                user.FullName,
                user.Email,
                user.Plan,
                user.QuotaPoints,
                user.Role,
                user.SubscriptionExpiresAt,
                IsSubscriptionActive = user.SubscriptionExpiresAt > DateTime.UtcNow
            });
        }

        private string GenerateJwtToken(ApplicationUser user)
        {
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Email),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim("FullName", user.FullName),
                new Claim("Plan", user.Plan),
                new Claim(ClaimTypes.Role, user.Role), 
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
                expires: DateTime.UtcNow.AddHours(24), 
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        [HttpPost("resend-verification")]
        [EnableRateLimiting("LoginPolicy")]
        public async Task<IActionResult> ResendVerification([FromBody] ResendVerificationRequest request)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            
            if (user == null || user.IsEmailVerified)
                return Ok(new { Message = "If your email is registered and unverified, a new code will be sent shortly." });

            var newCode = System.Security.Cryptography.RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
            
            user.VerificationToken = newCode;
            user.VerificationTokenExpiration = DateTime.UtcNow.AddHours(24);

            await _dbContext.SaveChangesAsync();

            try
            {
                var emailBody = GetEmailTemplate(
                    "New Verification Code", 
                    user.FullName, 
                    "You requested a new verification code. Please use the following code to proceed:", 
                    newCode, 
                    "This code will expire in 24 hours.");

                await _emailService.SendEmailAsync(user.Email, "New Verification Code", emailBody);
            }
            catch (Exception)
            {
                return StatusCode(500, new { Message = "An error occurred while sending the email. Please try again later." });
            }

            return Ok(new { Message = "If your email is registered and unverified, a new code will be sent shortly." });
        }
    }

    public class ResendVerificationRequest
    {
        [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    }
    public class VerifyEmailRequest
    {
        [Required, EmailAddress] public string Email { get; set; } = string.Empty;
        [Required] public string Code { get; set; } = string.Empty;
    }
    public class SignUpRequest
    {
        [Required, MinLength(3)]
        public string FullName { get; set; } = string.Empty;

        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$", 
            ErrorMessage = "Password must be at least 8 characters long and contain at least one uppercase letter, one lowercase letter, one number, and one special character.")]
        public string Password { get; set; } = string.Empty;

        public string? CommitteeSecret { get; set; }
    }

    public class ResetPasswordRequest
    {
        [Required, EmailAddress] public string Email { get; set; } = string.Empty;
        [Required] public string Code { get; set; } = string.Empty;
        
        [Required]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$", 
            ErrorMessage = "Password must be at least 8 characters long and contain at least one uppercase letter, one lowercase letter, one number, and one special character.")]
        public string NewPassword { get; set; } = string.Empty;
    }
    public class ForgotPasswordRequest
    {
        [Required, EmailAddress] public string Email { get; set; } = string.Empty;
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
    public class UpdatePlanRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Plan { get; set; } = string.Empty;
    }
}