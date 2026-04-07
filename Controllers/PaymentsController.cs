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
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out var id) ? id : 0;
        }

        [HttpPost("initiate")]
        public async Task<ActionResult<ApiResponseDto<TransactionDto>>> InitiatePayment([FromBody] InitiatePaymentDto request)
        {
            try
            {
                var userId = GetCurrentUserId();
                _logger.LogInformation($"InitiatePayment called by user {userId} for customer {request.CustomerId}, amount {request.Amount}");

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

                var transactionId = GenerateTransactionId();
                var transactionInternalId = GenerateTransactionInternalId();

                var newArrears = Math.Max(0, customer.Arrears - request.Amount);
                var newLoanBalance = customer.LoanBalance - request.Amount;

                var transaction = new Transaction
                {
                    TransactionInternalId = transactionInternalId,
                    TransactionId = transactionId,
                    CustomerId = request.CustomerId,
                    CustomerInternalId = customer.CustomerInternalId,
                    PhoneNumber = request.PhoneNumber,
                    Amount = request.Amount,
                    Description = request.Description ?? "Loan Repayment",
                    Status = "PENDING",
                    LoanBalanceBefore = customer.LoanBalance,
                    LoanBalanceAfter = newLoanBalance,
                    ArrearsBefore = customer.Arrears,
                    ArrearsAfter = newArrears,
                    PaymentMethod = "MPESA",
                    InitiatedBy = $"{user.FirstName} {user.LastName}".Trim(),
                    InitiatedByUserId = userId,
                    StkPushSentAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Transactions.Add(transaction);
                await _context.SaveChangesAsync();

                var stkPushSuccess = await _mpesaService.InitiateStkPushAsync(
                    request.PhoneNumber,
                    request.Amount,
                    customer.CustomerId ?? customer.Id.ToString());

                if (stkPushSuccess)
                {
                    _logger.LogInformation($"STK Push initiated successfully for transaction {transactionId}");
                }
                else
                {
                    _logger.LogWarning($"STK Push initiation failed for transaction {transactionId}");
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
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                return StatusCode(500, new ApiResponseDto
                {
                    Success = false,
                    Message = $"Internal server error: {ex.Message}"
                });
            }
        }

        [HttpPost("verify-pin")]
        public async Task<ActionResult<ApiResponseDto>> VerifyPin([FromBody] VerifyPinDto request)
        {
            try
            {
                var userId = GetCurrentUserId();
                _logger.LogInformation($"VerifyPin called for transaction: {request.TransactionId}");

                if (string.IsNullOrEmpty(request.TransactionId))
                {
                    return BadRequest(new ApiResponseDto
                    {
                        Success = false,
                        Message = "Transaction ID is required"
                    });
                }

                var transaction = await _context.Transactions
                    .Include(t => t.Customer)
                    .FirstOrDefaultAsync(t => t.TransactionId == request.TransactionId);

                if (transaction == null)
                {
                    _logger.LogWarning($"Transaction not found: {request.TransactionId}");
                    return NotFound(new ApiResponseDto
                    {
                        Success = false,
                        Message = "Transaction not found"
                    });
                }

                _logger.LogInformation($"Found transaction: ID={transaction.TransactionId}, Status={transaction.Status}, Amount={transaction.Amount}");

                if (transaction.Status == "SUCCESS" || transaction.Status == "COMPLETED")
                {
                    return Ok(new ApiResponseDto<object>
                    {
                        Success = true,
                        Message = "Transaction already completed",
                        Data = new { transaction.TransactionId, transaction.Status, transaction.MpesaReceiptNumber }
                    });
                }

                if (transaction.Status == "EXPIRED")
                {
                    return BadRequest(new ApiResponseDto
                    {
                        Success = false,
                        Message = "Transaction has expired. Please initiate a new payment."
                    });
                }

                if (string.IsNullOrEmpty(request.Pin) || request.Pin.Length != 4 || !request.Pin.All(char.IsDigit))
                {
                    return BadRequest(new ApiResponseDto
                    {
                        Success = false,
                        Message = "Invalid PIN format. Please enter a 4-digit PIN."
                    });
                }

                // For demo, accept any 4-digit PIN
                var pinVerified = true;

                if (!pinVerified)
                {
                    transaction.PinAttempts = (transaction.PinAttempts ?? 0) + 1;
                    transaction.ErrorMessage = "Invalid PIN";
                    transaction.FailureReason = "WRONG_PIN";
                    transaction.UpdatedAt = DateTime.UtcNow;
                    
                    _context.Transactions.Update(transaction);
                    await _context.SaveChangesAsync();

                    return BadRequest(new ApiResponseDto
                    {
                        Success = false,
                        Message = "Invalid PIN. Please try again."
                    });
                }

                var customer = transaction.Customer;
                if (customer != null)
                {
                    var newArrears = Math.Max(0, customer.Arrears - transaction.Amount);
                    var newLoanBalance = Math.Max(0, customer.LoanBalance - transaction.Amount);
                    
                    customer.Arrears = newArrears;
                    customer.LoanBalance = newLoanBalance;
                    customer.TotalRepayments = customer.TotalRepayments + transaction.Amount;
                    customer.LastPaymentDate = DateTime.UtcNow;
                    customer.UpdatedAt = DateTime.UtcNow;
                    
                    _context.Customers.Update(customer);
                    _logger.LogInformation($"Updated customer {customer.Name}: New Balance={newLoanBalance}, New Arrears={newArrears}");
                }

                transaction.Status = "SUCCESS";
                transaction.ProcessedAt = DateTime.UtcNow;
                transaction.UpdatedAt = DateTime.UtcNow;
                transaction.MpesaReceiptNumber = GenerateMpesaReceiptNumber();
                
                _context.Transactions.Update(transaction);
                await _context.SaveChangesAsync();

                await _activityService.LogActivityAsync(userId, "TRANSACTION_SUCCESS",
                    $"Payment successful for transaction {transaction.TransactionId}", "TRANSACTION", transaction.Id, transaction.CustomerId);

                _logger.LogInformation($"Transaction {transaction.TransactionId} marked as SUCCESS");

                return Ok(new ApiResponseDto<object>
                {
                    Success = true,
                    Message = "Payment completed successfully",
                    Data = new
                    {
                        transaction.TransactionId,
                        transaction.Status,
                        transaction.MpesaReceiptNumber,
                        transaction.Amount,
                        transaction.ProcessedAt,
                        CustomerName = customer?.Name
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error verifying PIN: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                return StatusCode(500, new ApiResponseDto
                {
                    Success = false,
                    Message = $"Internal server error: {ex.Message}"
                });
            }
        }

        [HttpPost("callback")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponseDto>> MpesaCallback([FromBody] Dictionary<string, object> callback)
        {
            try
            {
                _logger.LogInformation("M-Pesa callback received");
                var success = await _mpesaService.HandleCallbackAsync(callback);
                return Ok(new ApiResponseDto
                {
                    Success = success,
                    Message = success ? "Callback processed successfully" : "Callback processing failed"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing callback: {ex.Message}");
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

        [HttpPost("mark-failed/{transactionId}")]
        public async Task<ActionResult<ApiResponseDto>> MarkTransactionFailed(string transactionId, [FromBody] MarkFailedDto request)
        {
            try
            {
                var userId = GetCurrentUserId();
                _logger.LogInformation($"Marking transaction {transactionId} as failed");

                var transaction = await _context.Transactions
                    .FirstOrDefaultAsync(t => t.TransactionId == transactionId);

                if (transaction == null)
                {
                    return NotFound(new ApiResponseDto
                    {
                        Success = false,
                        Message = "Transaction not found"
                    });
                }

                if (transaction.Status == "SUCCESS" || transaction.Status == "COMPLETED")
                {
                    return Ok(new ApiResponseDto
                    {
                        Success = true,
                        Message = "Transaction already completed"
                    });
                }

                transaction.Status = "FAILED";
                transaction.FailureReason = request.FailureReason ?? "USER_CANCELLED";
                transaction.ErrorMessage = request.ErrorMessage ?? "Payment was not completed";
                transaction.UpdatedAt = DateTime.UtcNow;
                
                _context.Transactions.Update(transaction);
                await _context.SaveChangesAsync();

                await _activityService.LogActivityAsync(userId, "TRANSACTION_FAIL",
                    $"Transaction {transaction.TransactionId} failed: {transaction.FailureReason}", "TRANSACTION", transaction.Id, transaction.CustomerId);

                return Ok(new ApiResponseDto
                {
                    Success = true,
                    Message = "Transaction marked as failed"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error marking transaction as failed: {ex.Message}");
                return StatusCode(500, new ApiResponseDto
                {
                    Success = false,
                    Message = $"Internal server error: {ex.Message}"
                });
            }
        }

        [HttpGet("status/{transactionId}")]
        public async Task<ActionResult<ApiResponseDto<object>>> GetTransactionStatus(string transactionId)
        {
            try
            {
                var transaction = await _context.Transactions
                    .Include(t => t.Customer)
                    .FirstOrDefaultAsync(t => t.TransactionId == transactionId);

                if (transaction == null)
                {
                    return NotFound(new ApiResponseDto
                    {
                        Success = false,
                        Message = "Transaction not found"
                    });
                }

                if (transaction.Status == "PENDING")
                {
                    var stkSentAt = transaction.StkPushSentAt ?? transaction.CreatedAt;
                    var timeSinceSent = DateTime.UtcNow - stkSentAt;
                    
                    if (timeSinceSent.TotalSeconds > 30)
                    {
                        transaction.Status = "EXPIRED";
                        transaction.UpdatedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                        _logger.LogInformation($"Transaction {transactionId} expired after 30 seconds");
                    }
                }

                return Ok(new ApiResponseDto<object>
                {
                    Success = true,
                    Message = "Transaction status retrieved",
                    Data = new
                    {
                        transaction.TransactionId,
                        transaction.Status,
                        transaction.Amount,
                        transaction.MpesaReceiptNumber,
                        transaction.ProcessedAt,
                        transaction.CreatedAt,
                        transaction.FailureReason,
                        CustomerName = transaction.Customer?.Name,
                        InitiatedBy = transaction.InitiatedBy
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting transaction status: {ex.Message}");
                return StatusCode(500, new ApiResponseDto
                {
                    Success = false,
                    Message = $"Internal server error: {ex.Message}"
                });
            }
        }

        [HttpGet("my-collections")]
        public async Task<ActionResult<ApiResponseDto<object>>> GetMyPaymentCollections()
        {
            try
            {
                var userId = GetCurrentUserId();
                
                if (userId == 0)
                {
                    return Unauthorized(new ApiResponseDto
                    {
                        Success = false,
                        Message = "Invalid user ID"
                    });
                }

                var collections = await _context.Transactions
                    .Where(t => t.InitiatedByUserId == userId && t.PaymentMethod == "MPESA")
                    .OrderByDescending(t => t.CreatedAt)
                    .Take(50)
                    .Select(t => new
                    {
                        t.Id,
                        t.TransactionId,
                        t.Amount,
                        t.Status,
                        t.MpesaReceiptNumber,
                        t.CreatedAt,
                        CustomerName = t.Customer != null ? t.Customer.Name : "Unknown",
                        InitiatedBy = t.InitiatedBy
                    })
                    .ToListAsync();

                return Ok(new ApiResponseDto<object>
                {
                    Success = true,
                    Message = "Payment collections retrieved successfully",
                    Data = collections
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting payment collections: {ex.Message}");
                return StatusCode(500, new ApiResponseDto
                {
                    Success = false,
                    Message = $"Internal server error: {ex.Message}"
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
                CreatedAt = transaction.CreatedAt,
                InitiatedBy = transaction.InitiatedBy ?? "Unknown"
            };
        }

        private string GenerateTransactionId()
        {
            var timestamp = DateTime.UtcNow.Ticks.ToString();
            timestamp = timestamp.Substring(Math.Max(0, timestamp.Length - 8));
            var random = new Random().Next(10000).ToString().PadLeft(4, '0');
            return $"TXN{timestamp}{random}";
        }

        private string GenerateTransactionInternalId()
        {
            var timestamp = DateTime.UtcNow.Ticks.ToString();
            timestamp = timestamp.Substring(Math.Max(0, timestamp.Length - 8));
            var random = new Random().Next(10000).ToString().PadLeft(4, '0');
            return $"TXNINT{timestamp}{random}";
        }

        private string GenerateMpesaReceiptNumber()
        {
            var date = DateTime.UtcNow;
            var dateStr = date.ToString("yyMMdd");
            var random = new Random().Next(100000, 999999);
            return $"MP{dateStr}{random}";
        }
    }

    // DTO classes
    public class VerifyPinDto
    {
        public string TransactionId { get; set; } = string.Empty;
        public string Pin { get; set; } = string.Empty;
    }

    public class MarkFailedDto
    {
        public string? FailureReason { get; set; }
        public string? ErrorMessage { get; set; }
    }
}