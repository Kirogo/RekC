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
    public class ReportsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IActivityService _activityService;
        private readonly ILogger<ReportsController> _logger;

        public ReportsController(
            ApplicationDbContext context,
            IActivityService activityService,
            ILogger<ReportsController> logger)
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

        [HttpGet("summary")]
        public async Task<ActionResult<ApiResponseDto<object>>> GetSummaryReport()
        {
            try
            {
                var userId = GetCurrentUserId();
                var role = GetCurrentUserRole();

                // Get summary statistics
                var totalCustomers = await _context.Customers.CountAsync(c => c.IsActive == true);
                var totalLoanBalance = await _context.Customers
                    .Where(c => c.IsActive == true)
                    .SumAsync(c => c.LoanBalance);
                var totalArrears = await _context.Customers
                    .Where(c => c.IsActive == true)
                    .SumAsync(c => c.Arrears);
                var totalTransactions = await _context.Transactions.CountAsync();
                var completedTransactions = await _context.Transactions
                    .CountAsync(t => t.Status == "COMPLETED" || t.Status == "SUCCESS" || t.Status == "PROCESSED");
                var totalCollected = await _context.Transactions
                    .Where(t => t.Status == "COMPLETED" || t.Status == "SUCCESS" || t.Status == "PROCESSED")
                    .SumAsync(t => (decimal?)t.Amount) ?? 0;

                var summary = new
                {
                    TotalCustomers = totalCustomers,
                    TotalLoanBalance = totalLoanBalance,
                    TotalArrears = totalArrears,
                    TotalTransactions = totalTransactions,
                    CompletedTransactions = completedTransactions,
                    FailedTransactions = totalTransactions - completedTransactions,
                    TotalCollected = totalCollected,
                    CollectionRate = totalArrears > 0 ? Math.Round((totalCollected / totalArrears) * 100, 2) : 0,
                    AverageTransactionAmount = totalTransactions > 0 ? Math.Round(totalCollected / totalTransactions, 2) : 0,
                    PendingTransactions = await _context.Transactions.CountAsync(t => t.Status == "PENDING" || t.Status == "INITIATED"),
                    ReportGeneratedAt = DateTime.UtcNow
                };

                await _activityService.LogActivityAsync(userId, "REPORT_VIEW", "Viewed summary report");

                return Ok(new ApiResponseDto<object>
                {
                    Success = true,
                    Message = "Summary report retrieved successfully",
                    Data = summary
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error generating summary report: {ex.Message}");
                return StatusCode(500, new ApiResponseDto
                {
                    Success = false,
                    Message = "Internal server error"
                });
            }
        }

        [HttpGet("transactions")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<ActionResult<ApiResponseDto<object>>> GetTransactionReport(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] string? status = null)
        {
            try
            {
                var userId = GetCurrentUserId();
                var role = GetCurrentUserRole();

                IQueryable<Transaction> query = _context.Transactions
                    .Include(t => t.Customer)
                    .AsQueryable();

                // Apply date filters
                if (startDate.HasValue)
                    query = query.Where(t => t.CreatedAt >= startDate.Value);
                if (endDate.HasValue)
                    query = query.Where(t => t.CreatedAt < endDate.Value.AddDays(1));

                // Apply status filter
                if (!string.IsNullOrEmpty(status))
                    query = query.Where(t => t.Status == status.ToUpper());

                // Officers see only their transactions
                if (role == "officer")
                    query = query.Where(t => t.InitiatedByUserId == userId);

                var transactions = await query
                    .OrderByDescending(t => t.CreatedAt)
                    .ToListAsync();

                var report = new
                {
                    TotalTransactions = transactions.Count,
                    TotalAmount = transactions.Sum(t => t.Amount),
                    ByStatus = transactions
                        .GroupBy(t => t.Status)
                        .Select(g => new { Status = g.Key, Count = g.Count(), Amount = g.Sum(t => t.Amount) }),
                    Transactions = transactions.Take(100).Select(t => new {
                        t.Id,
                        t.TransactionId,
                        t.Amount,
                        t.Status,
                        t.PhoneNumber,
                        CustomerName = t.Customer?.Name,
                        t.CreatedAt
                    }).ToList(),
                    ReportGeneratedAt = DateTime.UtcNow,
                    Filters = new { startDate, endDate, status }
                };

                await _activityService.LogActivityAsync(userId, "TRANSACTION_REPORT",
                    "Generated transaction report");

                return Ok(new ApiResponseDto<object>
                {
                    Success = true,
                    Message = "Transaction report generated successfully",
                    Data = report
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error generating transaction report: {ex.Message}");
                return StatusCode(500, new ApiResponseDto
                {
                    Success = false,
                    Message = "Internal server error"
                });
            }
        }

        [HttpGet("promises")]
        public async Task<ActionResult<ApiResponseDto<object>>> GetPromiseReport()
        {
            try
            {
                var userId = GetCurrentUserId();
                var role = GetCurrentUserRole();

                IQueryable<Promise> query = _context.Promises
                    .Include(p => p.Customer)
                    .Include(p => p.CreatedByUser)
                    .AsQueryable();

                // Officers see only their promises
                if (role == "officer")
                    query = query.Where(p => p.CreatedByUserId == userId);

                var promises = await query.ToListAsync();

                var report = new
                {
                    TotalPromises = promises.Count,
                    ByStatus = promises
                        .GroupBy(p => p.Status ?? "UNKNOWN")
                        .Select(g => new { Status = g.Key, Count = g.Count() }),
                    FulfilledPromises = promises.Count(p => p.Status == "FULFILLED"),
                    BrokenPromises = promises.Count(p => p.Status == "BROKEN"),
                    PendingPromises = promises.Count(p => p.Status == "PENDING" || p.Status == null),
                    Promises = promises.Take(50).Select(p => new {
                        p.Id,
                        p.PromiseId,
                        Amount = p.PromiseAmount,
                        p.Status,
                        PromisedDate = p.PromiseDate,
                        CustomerName = p.Customer?.Name,
                        p.CreatedAt
                    }).ToList(),
                    ReportGeneratedAt = DateTime.UtcNow
                };

                await _activityService.LogActivityAsync(userId, "PROMISE_REPORT",
                    "Generated promise report");

                return Ok(new ApiResponseDto<object>
                {
                    Success = true,
                    Message = "Promise report generated successfully",
                    Data = report
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error generating promise report: {ex.Message}");
                return StatusCode(500, new ApiResponseDto
                {
                    Success = false,
                    Message = "Internal server error"
                });
            }
        }

        [HttpGet("customers")]
        public async Task<ActionResult<ApiResponseDto<object>>> GetCustomerReport()
        {
            try
            {
                var userId = GetCurrentUserId();
                var role = GetCurrentUserRole();

                IQueryable<Customer> query = _context.Customers
                    .Include(c => c.AssignedToUser)
                    .Where(c => c.IsActive == true);

                // Officers see only their assigned customers
                if (role == "officer")
                    query = query.Where(c => c.AssignedToUserId == userId);

                var customers = await query.ToListAsync();

                var report = new
                {
                    TotalCustomers = customers.Count,
                    TotalLoanBalance = customers.Sum(c => c.LoanBalance),
                    TotalArrears = customers.Sum(c => c.Arrears),
                    TotalRepayments = customers.Sum(c => c.TotalRepayments),
                    ByStatus = customers
                        .GroupBy(c => c.Status ?? "UNKNOWN")
                        .Select(g => new { Status = g.Key, Count = g.Count(), Balance = g.Sum(c => c.LoanBalance) }),
                    ByOfficer = customers
                        .GroupBy(c => c.AssignedToUser != null ? $"{c.AssignedToUser.FirstName} {c.AssignedToUser.LastName}" : "Unassigned")
                        .Select(g => new { Officer = g.Key, CustomerCount = g.Count(), Balance = g.Sum(c => c.LoanBalance) }),
                    TopCustomersByBalance = customers
                        .OrderByDescending(c => c.LoanBalance)
                        .Take(10)
                        .Select(c => new {
                            c.Id,
                            c.Name,
                            c.PhoneNumber,
                            c.LoanBalance,
                            c.Arrears,
                            c.Status
                        }).ToList(),
                    ReportGeneratedAt = DateTime.UtcNow
                };

                await _activityService.LogActivityAsync(userId, "CUSTOMER_REPORT",
                    "Generated customer report");

                return Ok(new ApiResponseDto<object>
                {
                    Success = true,
                    Message = "Customer report generated successfully",
                    Data = report
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error generating customer report: {ex.Message}");
                return StatusCode(500, new ApiResponseDto
                {
                    Success = false,
                    Message = "Internal server error"
                });
            }
        }

        [HttpGet("performance")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<ActionResult<ApiResponseDto<object>>> GetPerformanceReport()
        {
            try
            {
                var userId = GetCurrentUserId();
                var role = GetCurrentUserRole();

                // Only admins and supervisors can view all performance
                if (role != "admin" && role != "supervisor")
                    return Forbid();

                // Get all officers with their performance metrics
                var officers = await _context.Users
                    .Where(u => u.Role == "officer" && u.IsActive == true)
                    .Select(u => new
                    {
                        u.Id,
                        FullName = $"{u.FirstName} {u.LastName}".Trim(),
                        u.Department,
                        u.LastLogin,
                        CustomersAssigned = u.AssignedCustomers.Count(),
                        TotalArrears = u.AssignedCustomers.Sum(c => c.Arrears),
                        TotalBalance = u.AssignedCustomers.Sum(c => c.LoanBalance),
                        TransactionCount = _context.Transactions
                            .Count(t => t.InitiatedByUserId == u.Id),
                        SuccessfulTransactions = _context.Transactions
                            .Count(t => t.InitiatedByUserId == u.Id && 
                                (t.Status == "COMPLETED" || t.Status == "SUCCESS")),
                        TotalCollected = _context.Transactions
                            .Where(t => t.InitiatedByUserId == u.Id &&
                                (t.Status == "COMPLETED" || t.Status == "SUCCESS"))
                            .Sum(t => (decimal?)t.Amount) ?? 0m
                    })
                    .OrderByDescending(o => o.TotalCollected)
                    .ToListAsync();

                var report = new
                {
                    TotalOfficers = officers.Count,
                    Officers = officers.Select(o => new {
                        o.Id,
                        o.FullName,
                        o.Department,
                        o.CustomersAssigned,
                        o.TotalBalance,
                        o.TotalArrears,
                        o.TransactionCount,
                        o.SuccessfulTransactions,
                        o.TotalCollected,
                        SuccessRate = o.TransactionCount > 0 
                            ? Math.Round((decimal)o.SuccessfulTransactions / o.TransactionCount * 100, 2)
                            : 0m,
                        AverageTransactionAmount = o.TransactionCount > 0
                            ? Math.Round(o.TotalCollected / o.TransactionCount, 2)
                            : 0m,
                        o.LastLogin
                    }).ToList(),
                    ReportGeneratedAt = DateTime.UtcNow
                };

                await _activityService.LogActivityAsync(userId, "PERFORMANCE_REPORT",
                    "Generated officer performance report");

                return Ok(new ApiResponseDto<object>
                {
                    Success = true,
                    Message = "Performance report generated successfully",
                    Data = report
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error generating performance report: {ex.Message}");
                return StatusCode(500, new ApiResponseDto
                {
                    Success = false,
                    Message = "Internal server error"
                });
            }
        }
    }
}
