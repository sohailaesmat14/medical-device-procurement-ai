using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MediTender.API.Services;
using MediTender.API.Data;
using MediTender.API.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Microsoft.Extensions.Logging; 

namespace MediTender.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentController : ControllerBase
    {
        private readonly IPaymobService _paymobService;
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<PaymentController> _logger; 

        public PaymentController(IPaymobService paymobService, ApplicationDbContext dbContext, ILogger<PaymentController> logger)
        {
            _paymobService = paymobService;
            _dbContext = dbContext;
            _logger = logger;
        }

        [HttpPost("initiate")]
        [Authorize]
        public async Task<IActionResult> InitiatePayment([FromBody] PaymentRequest request)
        {
            var userEmail = User.Claims.FirstOrDefault(c => c.Type == System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;

            if (string.IsNullOrEmpty(userEmail))
            {
                return BadRequest(new { Message = "User email is missing or invalid token." });
            }

            if (request.PlanType != "monthly" && request.PlanType != "annually")
            {
                return BadRequest(new { Message = "Invalid plan type." });
            }

            decimal amount = request.PlanType == "monthly" ? BillingConstants.MonthlyPlanPrice : BillingConstants.AnnualPlanPrice;

            try
            {
                var iframeUrl = await _paymobService.GetPaymentIframeUrlAsync(amount, userEmail, request.PlanType);
                return Ok(new { CheckoutUrl = iframeUrl });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error connecting to Paymob payment gateway for user {Email}", userEmail);
                return StatusCode(500, new { Message = "Error connecting to payment gateway." });
            }
        }

        [HttpPost("webhook")]
        public async Task<IActionResult> PaymobWebhook([FromQuery] string hmac, [FromBody] JsonElement payload)
        {
            bool isValidHmac = false;
            bool success = false;
            string orderId = "";
            string userEmail = "";
            int amountCents = 0;

            try
            {
                isValidHmac = _paymobService.VerifyHmac(hmac, payload.GetRawText());

                var obj = payload.GetProperty("obj");
                success = obj.GetProperty("success").GetBoolean();
                orderId = obj.GetProperty("order").GetProperty("id").GetRawText();
                
                if (success)
                {
                    userEmail = obj.GetProperty("order").GetProperty("billing_data").GetProperty("email").GetString() ?? string.Empty;
                    amountCents = obj.GetProperty("amount_cents").GetInt32();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Webhook payload parsing error: {Message}. Payload might be missing expected fields.", ex.Message);
                return Unauthorized(new { Message = "Invalid payload structure or HMAC verification failed." });
            }

            if (!isValidHmac)
            {
                _logger.LogWarning("Unauthorized webhook attempt. Invalid HMAC signature.");
                return Unauthorized();
            }

            if (success)
            {
                using var transaction = await _dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
                try
                {
                    var isProcessed = await _dbContext.PaymentTransactions.AnyAsync(pt => pt.OrderId == orderId);
                    if (isProcessed)
                    {
                        return Ok();
                    }

                    var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == userEmail);
                    if (user != null)
                    {
                        bool planUpdated = false;

                        if (amountCents == (int)(Models.BillingConstants.MonthlyPlanPrice * 100))
                        {
                            if (user.Plan != "annually")
                            {
                                user.Plan = "monthly";
                            }
                            
                            user.QuotaPoints += Models.BillingConstants.MonthlyQuota;
                            planUpdated = true;
                        }
                        else if (amountCents == (int)(Models.BillingConstants.AnnualPlanPrice * 100))
                        {
                            user.Plan = "annually";
                            user.QuotaPoints += Models.BillingConstants.AnnualQuota;
                            planUpdated = true;
                        }
                        else
                        {
                            _logger.LogCritical("CRITICAL: AMOUNT MISMATCH DETECTED! OrderId: {OrderId}, User: {Email}, Expected: {Monthly} or {Annual}, Received: {ReceivedCents}. User charged but NO PLAN ASSIGNED!", 
                                orderId, user.Email, 
                                (int)(Models.BillingConstants.MonthlyPlanPrice * 100), 
                                (int)(Models.BillingConstants.AnnualPlanPrice * 100), 
                                amountCents);
                        }

                        _dbContext.PaymentTransactions.Add(new PaymentTransaction { OrderId = orderId });
                        await _dbContext.SaveChangesAsync();
                        await transaction.CommitAsync();

                        if (planUpdated)
                        {
                            _logger.LogInformation("Payment successful and plan updated for user: {Email}", user.Email);
                        }
                        else
                        {
                            _logger.LogWarning("Payment recorded for OrderId: {OrderId}, but plan was NOT updated due to amount mismatch.", orderId);
                        }
                    }
                    else
                    {
                        _logger.LogWarning(
                            "CRITICAL: ORPHAN PAYMENT DETECTED! OrderId: {OrderId}, Amount: {AmountCents} cents, Billed Email: {Email}. " +
                            "The payment was successful, but no matching user was found in the database. Manual reconciliation is required.", 
                            orderId, amountCents, userEmail);
                            
                        await transaction.RollbackAsync();
                    }
                }
                catch (DbUpdateException dbEx)
                {
                    _logger.LogWarning(dbEx, "Concurrency issue while processing OrderId: {OrderId}. Likely a duplicate webhook call.", orderId);
                    await transaction.RollbackAsync();
                    return Ok(); 
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error processing webhook for OrderId: {OrderId}", orderId);
                    await transaction.RollbackAsync();
                    throw;
                }
            }

            return Ok(); 
        }    
    }

    public class PaymentRequest
    {
        public string PlanType { get; set; } = string.Empty; 
    }
}