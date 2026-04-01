using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RekovaBE_CSharp.Data;
using RekovaBE_CSharp.Models;
using RekovaBE_CSharp.Models.DTOs;
using RekovaBE_CSharp.Services;
using System.Security.Claims;

namespace RekovaBE_CSharp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class PaymentsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IMpesaService _mpesaService;
        private readonly IActivityService _activityService;
        private readonly ILogger<PaymentsController> _logger;

        public PaymentsController(
            ApplicationDbContext context,
            IMpesaService mpesaService,
            IActivityService activityService,
            ILogger<PaymentsController> logger)
        {
            _context = context;
            _mpesaService = mpesaService;
            _activityService = activityService;
            _logger = logger;
        }

        private int GetCurrentUserId()
        {
            return int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        }

        [HttpPost("initiate")]
        public async Task<ActionResult<ApiResponseDto<TransactionDto>>> InitiatePayment([FromBody] InitiatePaymentDto request)
        {
            try
            {
                var userId = GetCurrentUserId();

                // Validate customer exists
                var customer = await _context.Customers.FindAsync(request.CustomerId);
                if (customer == null)
                {
                    return NotFound(new ApiResponseDto
                    {
                        Success = false,
                        Message = "Customer not found"
                    });
                }

                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    return Unauthorized(new ApiResponseDto
                    {
                        Success = false,
                        Message = "User not found"
                    });
                }

                // Create transaction record - FIXED: Use ArrearsAfter (correct spelling)
                var transaction = new Transaction
                {
                    TransactionInternalId = GenerateTransactionId(),
                    TransactionId = GenerateTransactionId(),
                    CustomerId = request.CustomerId,
                    CustomerInternalId = customer.CustomerInternalId,
                    PhoneNumber = request.PhoneNumber,
                    Amount = request.Amount,
                    Description = request.Description ?? "Loan Repayment",
                    Status = "PENDING",
                    LoanBalanceBefore = customer.LoanBalance,
                    LoanBalanceAfter = customer.LoanBalance - request.Amount,
                    ArrearsBefore = customer.Arrears,
                    ArrearsAfter = Math.Max(0, customer.Arrears - request.Amount), // FIXED: Correct property name
                    PaymentMethod = "MPESA",
                    InitiatedBy = $"{user.FirstName} {user.LastName}".Trim() ?? user.Username ?? "Unknown",
                    InitiatedByUserId = userId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Transactions.Add(transaction);
                await _context.SaveChangesAsync();

                // Initiate M-Pesa STK Push
                var stkPushSuccess = await _mpesaService.InitiateStkPushAsync(
                    request.PhoneNumber,
                    request.Amount,
                    customer.CustomerId ?? "");

                if (stkPushSuccess)
                {
                    transaction.Status = "INITIATED";
                    _context.Transactions.Update(transaction);
                    await _context.SaveChangesAsync();
                }

                await _activityService.LogActivityAsync(userId, "TRANSACTION_INITIATE",
                    $"Initiated payment for customer {customer.Name}", "TRANSACTION", transaction.Id,
                    request.CustomerId);

                var transactionDto = MapToDto(transaction, customer);

                return Ok(new ApiResponseDto<TransactionDto>
                {
                    Success = true,
                    Message = "Payment initiated successfully",
                    Data = transactionDto
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error initiating payment: {ex.Message}");
                return StatusCode(500, new ApiResponseDto
                {
                    Success = false,
                    Message = "Internal server error"
                });
            }
        }

        [HttpPost("callback")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponseDto>> MpesaCallback([FromBody] Dictionary<string, object> callback)
        {
            try
            {
                _logger.LogInformation("M-Pesa callback received with data: {@CallbackData}", callback);

                // Handle callback data
                var success = await _mpesaService.HandleCallbackAsync(callback);

                return Ok(new ApiResponseDto
                {
                    Success = success,
                    Message = success ? "Callback processed successfully" : "Callback processing failed"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing callback: {ex.Message}\n{ex.StackTrace}");
                return StatusCode(500, new ApiResponseDto
                {
                    Success = false,
                    Message = "Internal server error processing callback"
                });
            }
        }

        [HttpGet("transaction/{id}")]
        public async Task<ActionResult<ApiResponseDto<TransactionDto>>> GetTransaction(int id)
        {
            try
            {
                var transaction = await _context.Transactions
                    .Include(t => t.Customer)
                    .FirstOrDefaultAsync(t => t.Id == id);

                if (transaction == null)
                {
                    return NotFound(new ApiResponseDto
                    {
                        Success = false,
                        Message = "Transaction not found"
                    });
                }

                return Ok(new ApiResponseDto<TransactionDto>
                {
                    Success = true,
                    Message = "Transaction retrieved successfully",
                    Data = MapToDto(transaction, transaction.Customer)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting transaction: {ex.Message}");
                return StatusCode(500, new ApiResponseDto
                {
                    Success = false,
                    Message = "Internal server error"
                });
            }
        }

        private TransactionDto MapToDto(Transaction transaction, Customer? customer = null)
        {
            return new TransactionDto
            {
                Id = transaction.Id,
                TransactionInternalId = transaction.TransactionInternalId ?? string.Empty,
                TransactionId = transaction.TransactionId ?? string.Empty,
                CustomerId = transaction.CustomerId ?? 0,
                CustomerName = customer?.Name ?? "Unknown Customer",
                PhoneNumber = transaction.PhoneNumber ?? "",
                Amount = transaction.Amount,
                Status = transaction.Status ?? "PENDING",
                PaymentMethod = transaction.PaymentMethod,
                MpesaReceiptNumber = transaction.MpesaReceiptNumber,
                ProcessedAt = transaction.ProcessedAt,
                CreatedAt = transaction.CreatedAt
            };
        }

        private string GenerateTransactionId()
        {
            var timestamp = DateTime.UtcNow.Ticks.ToString();
            timestamp = timestamp.Substring(Math.Max(0, timestamp.Length - 8));
            var random = new Random().Next(10000).ToString().PadLeft(4, '0');
            return $"TXN{timestamp}{random}";
        }
    }
}