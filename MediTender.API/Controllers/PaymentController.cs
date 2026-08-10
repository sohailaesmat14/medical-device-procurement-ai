using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MediTender.API.Services;
using MediTender.API.Data;
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
            var userEmail = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value 
                            ?? request.Email;

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

        // Paymob will call this endpoint automatically after payment
        [HttpPost("webhook")]
        public async Task<IActionResult> PaymobWebhook([FromQuery] string hmac, [FromBody] JsonElement payload)
        {
            // 1. Verify Request Authentication
            if (!_paymobService.VerifyHmac(hmac, payload.GetRawText()))
            {
                return Unauthorized();
            }

            var obj = payload.GetProperty("obj");
            var success = obj.GetProperty("success").GetBoolean();
            
            if (success)
            {
                var userEmail = obj.GetProperty("order").GetProperty("billing_data").GetProperty("email").GetString();
                var amountCents = obj.GetProperty("amount_cents").GetInt32();

                var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == userEmail);
                if (user != null)
                {
                    if (amountCents == 250000) // 2500 EGP
                    {
                        user.Plan = "monthly";
                        user.QuotaPoints += 2000;
                    }
                    else if (amountCents == 2200000) 
                    {
                        user.Plan = "annually";
                        user.QuotaPoints += 99999;
                    }
                    
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