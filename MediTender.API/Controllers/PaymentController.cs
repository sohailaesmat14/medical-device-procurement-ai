using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MediTender.API.Services;
using MediTender.API.Data;
using MediTender.API.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace MediTender.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentController : ControllerBase
    {
        private readonly IPaymobService _paymobService;
        private readonly ApplicationDbContext _dbContext;

        public PaymentController(IPaymobService paymobService, ApplicationDbContext dbContext)
        {
            _paymobService = paymobService;
            _dbContext = dbContext;
        }

        [HttpPost("initiate")]
        [Authorize]
        public async Task<IActionResult> InitiatePayment([FromBody] PaymentRequest request)
        {
            var userEmail = User.Claims.FirstOrDefault(c => c.Type == System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value 
                            ?? request.Email;

            if (string.IsNullOrEmpty(userEmail))
            {
                return BadRequest(new { Message = "User email is missing." });
            }

            if (request.PlanType != "monthly" && request.PlanType != "annually")
            {
                return BadRequest(new { Message = "Invalid plan type." });
            }

            decimal amount = request.PlanType == "monthly" ? 2500 : 22000;

            try
            {
                var iframeUrl = await _paymobService.GetPaymentIframeUrlAsync(amount, userEmail, request.PlanType);
                return Ok(new { CheckoutUrl = iframeUrl });
            }
            catch (Exception)
            {
                return StatusCode(500, new { Message = "Error connecting to payment gateway." });
            }
        }

        [HttpPost("webhook")]
        public async Task<IActionResult> PaymobWebhook([FromQuery] string hmac, [FromBody] JsonElement payload)
        {
            if (!_paymobService.VerifyHmac(hmac, payload.GetRawText()))
            {
                return Unauthorized();
            }

            var obj = payload.GetProperty("obj");
            var success = obj.GetProperty("success").GetBoolean();
            var orderId = obj.GetProperty("order").GetProperty("id").GetRawText();
            
            if (success)
            {
                var isProcessed = await _dbContext.PaymentTransactions.AnyAsync(pt => pt.OrderId == orderId);
                if (isProcessed)
                {
                    return Ok();
                }

                var userEmail = obj.GetProperty("order").GetProperty("billing_data").GetProperty("email").GetString();
                var amountCents = obj.GetProperty("amount_cents").GetInt32();

                var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == userEmail);
                if (user != null)
                {
                    if (amountCents == 250000)
                    {
                        user.Plan = "monthly";
                        user.QuotaPoints += 2000;
                    }
                    else if (amountCents == 2200000) 
                    {
                        user.Plan = "annually";
                        user.QuotaPoints += 99999;
                    }
                    
                    _dbContext.PaymentTransactions.Add(new PaymentTransaction { OrderId = orderId });
                    await _dbContext.SaveChangesAsync();
                }
            }

            return Ok(); 
        }        
    }

    public class PaymentRequest
    {
        public string Email { get; set; } = string.Empty;
        public string PlanType { get; set; } = string.Empty; 
    }
}