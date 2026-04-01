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
            return int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        }

        private string GetCurrentUserRole()
        {
            return User.FindFirst(ClaimTypes.Role)?.Value ?? "officer";
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponseDto<PaginationDto<TransactionDto>>>> GetTransactions(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            [FromQuery] string? status = null,
            [FromQuery] int? customerId = null,
            [FromQuery] string? sort = "-createdAt")
        {
            try
            {
                var userId = GetCurrentUserId();
                var role = GetCurrentUserRole();

                IQueryable<Transaction> query = _context.Transactions
                    .Include(t => t.Customer)
                    .AsQueryable();

                // Filter by customer if provided
                if (customerId.HasValue)
                {
                    query = query.Where(t => t.CustomerId == customerId.Value);
                }

                // Filter by status if provided
                if (!string.IsNullOrWhiteSpace(status))
                {
                    query = query.Where(t => t.Status == status.ToUpper());
                }

                // Officers can only see their own customers' transactions
                if (role == "officer")
                {
                    query = query.Where(t => t.InitiatedByUserId == userId ||
                        _context.Customers.Any(c => c.Id == t.CustomerId && c.AssignedToUserId == userId));
                }

                var total = await query.CountAsync();

                // Apply sorting
                if (sort == "-createdAt" || string.IsNullOrEmpty(sort))
                {
                    query = query.OrderByDescending(t => t.CreatedAt);
                }
                else if (sort == "createdAt")
                {
                    query = query.OrderBy(t => t.CreatedAt);
                }
                else if (sort == "-amount")
                {
                    query = query.OrderByDescending(t => t.Amount);
                }
                else if (sort == "amount")
                {
                    query = query.OrderBy(t => t.Amount);
                }

                var transactions = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var transactionDtos = transactions.Select(t => MapToDto(t, t.Customer)).ToList();

                var pagination = new PaginationDto<TransactionDto>
                {
                    Items = transactionDtos,
                    TotalCount = total,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling(total / (double)pageSize)
                };

                await _activityService.LogActivityAsync(userId, "TRANSACTION_LIST_VIEW", "Viewed transactions");

                return Ok(new ApiResponseDto<PaginationDto<TransactionDto>>
                {
                    Success = true,
                    Message = "Transactions retrieved successfully",
                    Data = pagination
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting transactions: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    _logger.LogError($"Inner exception: {ex.InnerException.Message}");
                }
                return StatusCode(500, new ApiResponseDto
                {
                    Success = false,
                    Message = $"Internal server error: {ex.Message}"
                });
            }
        }

        [HttpGet("my-transactions")]
        public async Task<ActionResult<ApiResponseDto<List<TransactionDto>>>> GetMyTransactions(
            [FromQuery] int limit = 100,
            [FromQuery] string? sort = "-createdAt")
        {
            try
            {
                var userId = GetCurrentUserId();
                
                _logger.LogInformation($"GetMyTransactions: userId={userId}, limit={limit}");

                if (userId == 0)
                {
                    _logger.LogWarning("GetMyTransactions: Unable to extract user ID from token");
                    return Unauthorized(new ApiResponseDto
                    {
                        Success = false,
                        Message = "Invalid authentication token"
                    });
                }

                IQueryable<Transaction> query = _context.Transactions
                    .Include(t => t.Customer)
                    .Where(t => t.InitiatedByUserId == userId);

                // Apply sorting
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

                var transactionDtos = transactions.Select(t => MapToDto(t, t.Customer)).ToList();

                await _activityService.LogActivityAsync(userId, "MY_TRANSACTIONS_VIEW", "Viewed their transactions");

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
                if (ex.InnerException != null)
                {
                    _logger.LogError($"Inner exception: {ex.InnerException.Message}");
                }
                return StatusCode(500, new ApiResponseDto
                {
                    Success = false,
                    Message = $"Internal server error: {ex.Message}"
                });
            }
        }

        [HttpGet("export")]
        public async Task<IActionResult> ExportTransactions(
            [FromQuery] string? format = "csv",
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                var userId = GetCurrentUserId();
                var role = GetCurrentUserRole();

                IQueryable<Transaction> query = _context.Transactions
                    .Include(t => t.Customer)
                    .Include(t => t.InitiatedByUser)
                    .AsQueryable();

                // Apply date filters
                if (startDate.HasValue)
                {
                    query = query.Where(t => t.CreatedAt >= startDate.Value);
                }
                if (endDate.HasValue)
                {
                    var endDateEnd = endDate.Value.AddDays(1);
                    query = query.Where(t => t.CreatedAt < endDateEnd);
                }

                // Officers can only see their own transactions
                if (role == "officer")
                {
                    query = query.Where(t => t.InitiatedByUserId == userId);
                }

                var transactions = await query
                    .OrderByDescending(t => t.CreatedAt)
                    .ToListAsync();

                // Generate CSV
                var csv = new System.Text.StringBuilder();
                csv.AppendLine("Transaction ID,Date,Time,Customer,Phone,Amount,Status,Payment Method,Receipt Number,Initiated By");
                
                foreach (var t in transactions)
                {
                    csv.AppendLine($"\"{t.TransactionId}\",{t.CreatedAt:yyyy-MM-dd},{t.CreatedAt:HH:mm:ss},\"{t.Customer?.Name ?? "Unknown"}\",{t.PhoneNumber},{t.Amount:N0},{t.Status},{t.PaymentMethod},{t.MpesaReceiptNumber},{t.InitiatedByUser?.Username ?? "System"}");
                }

                var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
                var filename = $"transactions_export_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

                await _activityService.LogActivityAsync(userId, "TRANSACTIONS_EXPORT", "Exported transactions");

                return File(bytes, "text/csv", filename);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error exporting transactions: {ex.Message}");
                return StatusCode(500, new ApiResponseDto
                {
                    Success = false,
                    Message = "Internal server error"
                });
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponseDto<TransactionDto>>> GetTransaction(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                var transaction = await _context.Transactions
                    .Include(t => t.Customer)
                    .Include(t => t.InitiatedByUser)
                    .FirstOrDefaultAsync(t => t.Id == id);

                if (transaction == null)
                {
                    return NotFound(new ApiResponseDto
                    {
                        Success = false,
                        Message = "Transaction not found"
                    });
                }

                await _activityService.LogActivityAsync(userId, "TRANSACTION_VIEW", 
                    $"Viewed transaction {transaction.TransactionId}", "TRANSACTION", id, transaction.CustomerId);

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
    }
}