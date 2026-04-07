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
    public class TransactionsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IActivityService _activityService;
        private readonly ILogger<TransactionsController> _logger;

        public TransactionsController(
            ApplicationDbContext context,
            IActivityService activityService,
            ILogger<TransactionsController> logger)
        {
            _context = context;
            _activityService = activityService;
            _logger = logger;
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out var id) ? id : 0;
        }

        private string GetCurrentUserRole()
        {
            return User.FindFirst(ClaimTypes.Role)?.Value ?? "officer";
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponseDto<List<TransactionDto>>>> GetTransactions(
            [FromQuery] int? customerId = null,
            [FromQuery] int limit = 10,
            [FromQuery] string? sort = "-createdAt")
        {
            try
            {
                var userId = GetCurrentUserId();
                var role = GetCurrentUserRole();

                IQueryable<Transaction> query = _context.Transactions;

                if (customerId.HasValue)
                {
                    query = query.Where(t => t.CustomerId == customerId.Value);
                }

                if (role == "officer")
                {
                    query = query.Where(t => t.InitiatedByUserId == userId ||
                        _context.Customers.Any(c => c.Id == t.CustomerId && c.AssignedToUserId == userId));
                }

                if (sort == "-createdAt" || string.IsNullOrEmpty(sort))
                {
                    query = query.OrderByDescending(t => t.CreatedAt);
                }
                else if (sort == "createdAt")
                {
                    query = query.OrderBy(t => t.CreatedAt);
                }

                var transactions = await query
                    .Take(limit)
                    .ToListAsync();

                // Load customer names separately to avoid null reference issues
                var customerIds = transactions.Where(t => t.CustomerId.HasValue).Select(t => t.CustomerId.Value).Distinct().ToList();
                var customers = await _context.Customers
                    .Where(c => customerIds.Contains(c.Id))
                    .ToDictionaryAsync(c => c.Id, c => c.Name ?? "Unknown");

                var transactionDtos = transactions.Select(t => new TransactionDto
                {
                    Id = t.Id,
                    TransactionInternalId = t.TransactionInternalId ?? string.Empty,
                    TransactionId = t.TransactionId ?? string.Empty,
                    CustomerId = t.CustomerId ?? 0,
                    CustomerName = t.CustomerId.HasValue && customers.ContainsKey(t.CustomerId.Value) ? customers[t.CustomerId.Value] : "Unknown",
                    PhoneNumber = t.PhoneNumber ?? "",
                    Amount = t.Amount,
                    Status = t.Status ?? "PENDING",
                    PaymentMethod = t.PaymentMethod,
                    MpesaReceiptNumber = t.MpesaReceiptNumber,
                    ProcessedAt = t.ProcessedAt,
                    CreatedAt = t.CreatedAt,
                    InitiatedBy = t.InitiatedBy ?? "Unknown"
                }).ToList();

                return Ok(new ApiResponseDto<List<TransactionDto>>
                {
                    Success = true,
                    Message = "Transactions retrieved successfully",
                    Data = transactionDtos
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting transactions: {ex.Message}");
                return StatusCode(500, new ApiResponseDto
                {
                    Success = false,
                    Message = $"Internal server error: {ex.Message}"
                });
            }
        }

        [HttpGet("my-transactions")]
        public async Task<ActionResult<ApiResponseDto<List<TransactionDto>>>> GetMyTransactions(
            [FromQuery] int limit = 100)
        {
            try
            {
                var userId = GetCurrentUserId();
                
                _logger.LogInformation($"GetMyTransactions: userId={userId}, limit={limit}");

                if (userId == 0)
                {
                    return Unauthorized(new ApiResponseDto
                    {
                        Success = false,
                        Message = "Invalid authentication token"
                    });
                }

                var transactions = await _context.Transactions
                    .Where(t => t.InitiatedByUserId == userId)
                    .OrderByDescending(t => t.CreatedAt)
                    .Take(limit)
                    .ToListAsync();

                var customerIds = transactions.Where(t => t.CustomerId.HasValue).Select(t => t.CustomerId.Value).Distinct().ToList();
                var customers = await _context.Customers
                    .Where(c => customerIds.Contains(c.Id))
                    .ToDictionaryAsync(c => c.Id, c => c.Name ?? "Unknown");

                var transactionDtos = transactions.Select(t => new TransactionDto
                {
                    Id = t.Id,
                    TransactionInternalId = t.TransactionInternalId ?? string.Empty,
                    TransactionId = t.TransactionId ?? string.Empty,
                    CustomerId = t.CustomerId ?? 0,
                    CustomerName = t.CustomerId.HasValue && customers.ContainsKey(t.CustomerId.Value) ? customers[t.CustomerId.Value] : "Unknown",
                    PhoneNumber = t.PhoneNumber ?? "",
                    Amount = t.Amount,
                    Status = t.Status ?? "PENDING",
                    PaymentMethod = t.PaymentMethod,
                    MpesaReceiptNumber = t.MpesaReceiptNumber,
                    ProcessedAt = t.ProcessedAt,
                    CreatedAt = t.CreatedAt,
                    InitiatedBy = t.InitiatedBy ?? "Unknown"
                }).ToList();

                _logger.LogInformation($"Retrieved {transactionDtos.Count} transactions for user {userId}");

                await _activityService.LogActivityAsync(userId, "MY_TRANSACTIONS_VIEW", $"Viewed their transactions. Found {transactionDtos.Count} transactions");

                return Ok(new ApiResponseDto<List<TransactionDto>>
                {
                    Success = true,
                    Message = "Your transactions retrieved successfully",
                    Data = transactionDtos
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting my transactions: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                return StatusCode(500, new ApiResponseDto
                {
                    Success = false,
                    Message = $"Internal server error: {ex.Message}"
                });
            }
        }

        [HttpGet("my-collections")]
        public async Task<ActionResult<ApiResponseDto<List<object>>>> GetMyCollections()
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
                    .Where(t => t.InitiatedByUserId == userId && (t.Status == "SUCCESS" || t.Status == "COMPLETED"))
                    .Select(t => new
                    {
                        t.Id,
                        t.TransactionId,
                        t.Amount,
                        t.Status,
                        t.PaymentMethod,
                        t.MpesaReceiptNumber,
                        t.CreatedAt
                    })
                    .OrderByDescending(t => t.CreatedAt)
                    .Take(50)
                    .ToListAsync();

                _logger.LogInformation($"Retrieved {collections.Count} collections for user {userId}");

                return Ok(new ApiResponseDto<List<object>>
                {
                    Success = true,
                    Message = "Collections retrieved successfully",
                    Data = collections.Cast<object>().ToList()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting my collections: {ex.Message}");
                return StatusCode(500, new ApiResponseDto
                {
                    Success = false,
                    Message = $"Internal server error: {ex.Message}"
                });
            }
        }
    }
}