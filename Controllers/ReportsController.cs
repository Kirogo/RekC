using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RekovaBE_CSharp.Data;
using RekovaBE_CSharp.Models;
using RekovaBE_CSharp.Models.DTOs;
using RekovaBE_CSharp.Services;
using System.Security.Claims;
using System.Text;

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
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out var id) ? id : 0;
        }

        private string GetCurrentUserRole()
        {
            return User.FindFirst(ClaimTypes.Role)?.Value ?? "officer";
        }

        // ==================== CUSTOMERS REPORT ====================
        [HttpGet("customers")]
        public async Task<ActionResult<ApiResponseDto<object>>> GetCustomerReport(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                var userId = GetCurrentUserId();
                var role = GetCurrentUserRole();

                IQueryable<Customer> query = _context.Customers
                    .Include(c => c.AssignedToUser)
                    .Where(c => c.IsActive == true);

                if (startDate.HasValue)
                    query = query.Where(c => c.CreatedAt >= startDate.Value);
                if (endDate.HasValue)
                    query = query.Where(c => c.CreatedAt <= endDate.Value);

                if (role == "officer")
                    query = query.Where(c => c.AssignedToUserId == userId);

                var customers = await query.ToListAsync();

                var summary = new
                {
                    TotalCustomers = customers.Count,
                    TotalLoanPortfolio = customers.Sum(c => c.LoanBalance),
                    TotalArrears = customers.Sum(c => c.Arrears),
                    ActiveCustomers = customers.Count(c => c.Status == "ACTIVE"),
                    InArrears = customers.Count(c => c.Arrears > 0)
                };

                var reportData = customers.Select(c => new
                {
                    c.Id,
                    c.Name,
                    c.PhoneNumber,
                    c.Email,
                    c.LoanBalance,
                    c.Arrears,
                    c.Status,
                    c.IsActive,
                    c.LoanType,
                    LastPaymentDate = c.LastPaymentDate,
                    CreatedAt = c.CreatedAt
                }).ToList();

                var result = new
                {
                    Customers = reportData,
                    Summary = summary,
                    TotalCount = reportData.Count,
                    ReportGeneratedAt = DateTime.UtcNow
                };

                return Ok(new ApiResponseDto<object>
                {
                    Success = true,
                    Message = "Customer report generated successfully",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error generating customer report: {ex.Message}");
                return StatusCode(500, new ApiResponseDto<object>
                {
                    Success = false,
                    Message = $"Internal server error: {ex.Message}"
                });
            }
        }

        [HttpGet("customers/export")]
        public async Task<IActionResult> ExportCustomersReport(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                var userId = GetCurrentUserId();
                var role = GetCurrentUserRole();

                IQueryable<Customer> query = _context.Customers.Where(c => c.IsActive == true);

                if (startDate.HasValue)
                    query = query.Where(c => c.CreatedAt >= startDate.Value);
                if (endDate.HasValue)
                    query = query.Where(c => c.CreatedAt <= endDate.Value);

                if (role == "officer")
                    query = query.Where(c => c.AssignedToUserId == userId);

                var customers = await query.ToListAsync();

                var csv = new StringBuilder();
                csv.AppendLine("Customer Name,Phone Number,Email,Loan Balance,Arrears,Status,Last Payment Date");

                foreach (var customer in customers)
                {
                    csv.AppendLine($"\"{customer.Name}\",{customer.PhoneNumber},{customer.Email},{customer.LoanBalance},{customer.Arrears},{customer.Status},{customer.LastPaymentDate?.ToString("yyyy-MM-dd")}");
                }

                var bytes = Encoding.UTF8.GetBytes(csv.ToString());
                var filename = $"customers_report_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

                return File(bytes, "text/csv", filename);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error exporting customers report: {ex.Message}");
                return StatusCode(500, "Export failed");
            }
        }

        // ==================== ASSIGNED CUSTOMERS REPORT ====================
        [HttpGet("assigned-to-me/customers")]
        public async Task<ActionResult<ApiResponseDto<object>>> GetMyAssignedCustomersReport(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                var userId = GetCurrentUserId();

                IQueryable<Customer> query = _context.Customers
                    .Where(c => c.IsActive == true)
                    .Where(c => c.AssignedToUserId == userId);

                if (startDate.HasValue)
                    query = query.Where(c => c.CreatedAt >= startDate.Value);
                if (endDate.HasValue)
                    query = query.Where(c => c.CreatedAt <= endDate.Value);

                var customers = await query.ToListAsync();

                var summary = new
                {
                    TotalCustomers = customers.Count,
                    TotalLoanPortfolio = customers.Sum(c => c.LoanBalance),
                    TotalArrears = customers.Sum(c => c.Arrears),
                    ActiveCustomers = customers.Count(c => c.Status == "ACTIVE"),
                    InArrears = customers.Count(c => c.Arrears > 0)
                };

                var reportData = customers.Select(c => new
                {
                    c.Id,
                    c.Name,
                    c.PhoneNumber,
                    c.Email,
                    c.LoanBalance,
                    c.Arrears,
                    c.Status,
                    c.IsActive,
                    c.LoanType,
                    LastPaymentDate = c.LastPaymentDate,
                    CreatedAt = c.CreatedAt
                }).ToList();

                var result = new
                {
                    Customers = reportData,
                    Summary = summary,
                    TotalCount = reportData.Count,
                    ReportGeneratedAt = DateTime.UtcNow
                };

                return Ok(new ApiResponseDto<object>
                {
                    Success = true,
                    Message = "Assigned customers report generated successfully",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error generating assigned customers report: {ex.Message}");
                return StatusCode(500, new ApiResponseDto<object>
                {
                    Success = false,
                    Message = $"Internal server error: {ex.Message}"
                });
            }
        }

        [HttpGet("assigned-to-me/customers/export")]
        public async Task<IActionResult> ExportMyAssignedCustomersReport(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                var userId = GetCurrentUserId();

                IQueryable<Customer> query = _context.Customers
                    .Where(c => c.IsActive == true)
                    .Where(c => c.AssignedToUserId == userId);

                if (startDate.HasValue)
                    query = query.Where(c => c.CreatedAt >= startDate.Value);
                if (endDate.HasValue)
                    query = query.Where(c => c.CreatedAt <= endDate.Value);

                var customers = await query.ToListAsync();

                var csv = new StringBuilder();
                csv.AppendLine("Customer Name,Phone Number,Email,Loan Balance,Arrears,Status,Last Payment Date");

                foreach (var customer in customers)
                {
                    csv.AppendLine($"\"{customer.Name}\",{customer.PhoneNumber},{customer.Email},{customer.LoanBalance},{customer.Arrears},{customer.Status},{customer.LastPaymentDate?.ToString("yyyy-MM-dd")}");
                }

                var bytes = Encoding.UTF8.GetBytes(csv.ToString());
                var filename = $"my_customers_report_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

                return File(bytes, "text/csv", filename);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error exporting assigned customers report: {ex.Message}");
                return StatusCode(500, "Export failed");
            }
        }

        // ==================== TRANSACTIONS REPORT ====================
        [HttpGet("transactions")]
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

                if (startDate.HasValue)
                    query = query.Where(t => t.CreatedAt >= startDate.Value);
                if (endDate.HasValue)
                    query = query.Where(t => t.CreatedAt <= endDate.Value);

                if (!string.IsNullOrEmpty(status))
                    query = query.Where(t => t.Status == status.ToUpper());

                if (role == "officer")
                    query = query.Where(t => t.InitiatedByUserId == userId);

                var transactions = await query
                    .OrderByDescending(t => t.CreatedAt)
                    .ToListAsync();

                var summary = new
                {
                    TotalTransactions = transactions.Count,
                    TotalAmount = transactions.Sum(t => t.Amount),
                    SuccessfulCount = transactions.Count(t => t.Status == "SUCCESS"),
                    FailedCount = transactions.Count(t => t.Status == "FAILED"),
                    PendingCount = transactions.Count(t => t.Status == "PENDING"),
                    ExpiredCount = transactions.Count(t => t.Status == "EXPIRED"),
                    AverageAmount = transactions.Count > 0 ? transactions.Average(t => t.Amount) : 0
                };

                var reportData = transactions.Select(t => new
                {
                    t.Id,
                    t.TransactionId,
                    CustomerName = t.Customer != null ? t.Customer.Name : "Unknown",
                    t.PhoneNumber,
                    t.Amount,
                    t.Status,
                    t.PaymentMethod,
                    t.MpesaReceiptNumber,
                    t.InitiatedBy,
                    CreatedAt = t.CreatedAt
                }).ToList();

                var result = new
                {
                    Transactions = reportData,
                    Summary = summary,
                    TotalCount = reportData.Count,
                    ReportGeneratedAt = DateTime.UtcNow
                };

                return Ok(new ApiResponseDto<object>
                {
                    Success = true,
                    Message = "Transaction report generated successfully",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error generating transaction report: {ex.Message}");
                return StatusCode(500, new ApiResponseDto<object>
                {
                    Success = false,
                    Message = $"Internal server error: {ex.Message}"
                });
            }
        }

        [HttpGet("transactions/export")]
        public async Task<IActionResult> ExportTransactionsReport(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                var userId = GetCurrentUserId();
                var role = GetCurrentUserRole();

                IQueryable<Transaction> query = _context.Transactions
                    .Include(t => t.Customer);

                if (startDate.HasValue)
                    query = query.Where(t => t.CreatedAt >= startDate.Value);
                if (endDate.HasValue)
                    query = query.Where(t => t.CreatedAt <= endDate.Value);

                if (role == "officer")
                    query = query.Where(t => t.InitiatedByUserId == userId);

                var transactions = await query.ToListAsync();

                var csv = new StringBuilder();
                csv.AppendLine("Transaction ID,Customer Name,Phone Number,Amount,Status,Date,Receipt Number");

                foreach (var transaction in transactions)
                {
                    csv.AppendLine($"\"{transaction.TransactionId}\",\"{transaction.Customer?.Name}\",{transaction.PhoneNumber},{transaction.Amount},{transaction.Status},{transaction.CreatedAt:yyyy-MM-dd HH:mm},{transaction.MpesaReceiptNumber}");
                }

                var bytes = Encoding.UTF8.GetBytes(csv.ToString());
                var filename = $"transactions_report_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

                return File(bytes, "text/csv", filename);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error exporting transactions report: {ex.Message}");
                return StatusCode(500, "Export failed");
            }
        }

        // ==================== PROMISES REPORT ====================
        [HttpGet("promises")]
        public async Task<ActionResult<ApiResponseDto<object>>> GetPromiseReport(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] string? status = null)
        {
            try
            {
                var userId = GetCurrentUserId();
                var role = GetCurrentUserRole();

                IQueryable<Promise> query = _context.Promises
                    .Include(p => p.Customer)
                    .AsQueryable();

                if (startDate.HasValue)
                    query = query.Where(p => p.PromiseDate >= startDate.Value);
                if (endDate.HasValue)
                    query = query.Where(p => p.PromiseDate <= endDate.Value);

                if (!string.IsNullOrEmpty(status))
                    query = query.Where(p => p.Status == status.ToUpper());

                if (role == "officer")
                    query = query.Where(p => p.CreatedByUserId == userId);

                var promises = await query
                    .OrderByDescending(p => p.PromiseDate)
                    .ToListAsync();

                var summary = new
                {
                    TotalPromises = promises.Count,
                    TotalAmount = promises.Sum(p => p.PromiseAmount),
                    FulfilledCount = promises.Count(p => p.Status == "FULFILLED"),
                    BrokenCount = promises.Count(p => p.Status == "BROKEN"),
                    PendingCount = promises.Count(p => p.Status == "PENDING"),
                    FulfillmentRate = promises.Count > 0 ? (double)promises.Count(p => p.Status == "FULFILLED") / promises.Count * 100 : 0
                };

                var reportData = promises.Select(p => new
                {
                    p.Id,
                    p.PromiseId,
                    CustomerName = p.Customer != null ? p.Customer.Name : "Unknown",
                    p.PhoneNumber,
                    PromiseAmount = p.PromiseAmount,
                    PromiseDate = p.PromiseDate,
                    p.Status,
                    p.PromiseType,
                    p.Notes,
                    CreatedByName = p.CreatedByUser != null ? $"{p.CreatedByUser.FirstName} {p.CreatedByUser.LastName}".Trim() : "System",
                    CreatedAt = p.CreatedAt
                }).ToList();

                var result = new
                {
                    Promises = reportData,
                    Summary = summary,
                    TotalCount = reportData.Count,
                    ReportGeneratedAt = DateTime.UtcNow
                };

                return Ok(new ApiResponseDto<object>
                {
                    Success = true,
                    Message = "Promise report generated successfully",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error generating promise report: {ex.Message}");
                return StatusCode(500, new ApiResponseDto<object>
                {
                    Success = false,
                    Message = $"Internal server error: {ex.Message}"
                });
            }
        }

        [HttpGet("promises/export")]
        public async Task<IActionResult> ExportPromisesReport(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                var userId = GetCurrentUserId();
                var role = GetCurrentUserRole();

                IQueryable<Promise> query = _context.Promises
                    .Include(p => p.Customer);

                if (startDate.HasValue)
                    query = query.Where(p => p.PromiseDate >= startDate.Value);
                if (endDate.HasValue)
                    query = query.Where(p => p.PromiseDate <= endDate.Value);

                if (role == "officer")
                    query = query.Where(p => p.CreatedByUserId == userId);

                var promises = await query.ToListAsync();

                var csv = new StringBuilder();
                csv.AppendLine("Promise ID,Customer Name,Phone Number,Amount,Due Date,Status,Type");

                foreach (var promise in promises)
                {
                    csv.AppendLine($"\"{promise.PromiseId}\",\"{promise.Customer?.Name}\",{promise.PhoneNumber},{promise.PromiseAmount},{promise.PromiseDate:yyyy-MM-dd},{promise.Status},{promise.PromiseType}");
                }

                var bytes = Encoding.UTF8.GetBytes(csv.ToString());
                var filename = $"promises_report_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

                return File(bytes, "text/csv", filename);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error exporting promises report: {ex.Message}");
                return StatusCode(500, "Export failed");
            }
        }

        // ==================== DASHBOARD STATS FOR CHARTS ====================
        [HttpGet("dashboard-stats")]
        public async Task<ActionResult<ApiResponseDto<object>>> GetDashboardStats()
        {
            try
            {
                var userId = GetCurrentUserId();
                var role = GetCurrentUserRole();

                var today = DateTime.UtcNow.Date;
                var startOfWeek = today.AddDays(-(int)today.DayOfWeek);
                var startOfMonth = new DateTime(today.Year, today.Month, 1);

                IQueryable<Transaction> transactionQuery = _context.Transactions;
                IQueryable<Promise> promiseQuery = _context.Promises;
                IQueryable<Customer> customerQuery = _context.Customers.Where(c => c.IsActive == true);

                if (role == "officer")
                {
                    transactionQuery = transactionQuery.Where(t => t.InitiatedByUserId == userId);
                    promiseQuery = promiseQuery.Where(p => p.CreatedByUserId == userId);
                    customerQuery = customerQuery.Where(c => c.AssignedToUserId == userId);
                }

                // Weekly transactions
                var weeklyTransactions = await transactionQuery
                    .Where(t => t.CreatedAt >= startOfWeek)
                    .GroupBy(t => t.CreatedAt.Date)
                    .Select(g => new { Date = g.Key, Count = g.Count(), Amount = g.Sum(t => t.Amount) })
                    .ToListAsync();

                // Monthly transactions
                var monthlyTransactions = await transactionQuery
                    .Where(t => t.CreatedAt >= startOfMonth)
                    .GroupBy(t => t.CreatedAt.Date)
                    .Select(g => new { Date = g.Key, Count = g.Count(), Amount = g.Sum(t => t.Amount) })
                    .ToListAsync();

                // Transaction status distribution
                var transactionStatusDistribution = await transactionQuery
                    .GroupBy(t => t.Status ?? "UNKNOWN")
                    .Select(g => new { Status = g.Key, Count = g.Count() })
                    .ToListAsync();

                // Promise status distribution
                var promiseStatusDistribution = await promiseQuery
                    .GroupBy(p => p.Status ?? "PENDING")
                    .Select(g => new { Status = g.Key, Count = g.Count() })
                    .ToListAsync();

                // Top customers by arrears
                var topCustomersByArrears = await customerQuery
                    .OrderByDescending(c => c.Arrears)
                    .Take(10)
                    .Select(c => new { c.Name, c.Arrears, c.LoanBalance })
                    .ToListAsync();

                // Officer performance (for supervisors/admins)
                object officerPerformance = null;
                if (role == "admin" || role == "supervisor")
                {
                    officerPerformance = await _context.Users
                        .Where(u => u.Role == "officer" && u.IsActive == true)
                        .Select(u => new
                        {
                            u.Id,
                            Name = (u.FirstName != null ? u.FirstName + " " : "") + (u.LastName ?? ""),
                            TotalCustomers = u.AssignedCustomers != null ? u.AssignedCustomers.Count() : 0,
                            TotalCollected = _context.Transactions
                                .Where(t => t.InitiatedByUserId == u.Id && t.Status == "SUCCESS")
                                .Sum(t => (decimal?)t.Amount) ?? 0,
                            SuccessRate = _context.Transactions
                                .Where(t => t.InitiatedByUserId == u.Id).Count() > 0
                                ? (double)_context.Transactions
                                    .Count(t => t.InitiatedByUserId == u.Id && t.Status == "SUCCESS") * 100.0 /
                                  _context.Transactions.Count(t => t.InitiatedByUserId == u.Id)
                                : 0
                        })
                        .OrderByDescending(o => o.TotalCollected)
                        .ToListAsync();
                }

                var result = new
                {
                    Summary = new
                    {
                        TotalCustomers = await customerQuery.CountAsync(),
                        TotalLoanBalance = await customerQuery.SumAsync(c => c.LoanBalance),
                        TotalArrears = await customerQuery.SumAsync(c => c.Arrears),
                        TotalCollected = await transactionQuery
                            .Where(t => t.Status == "SUCCESS")
                            .SumAsync(t => (decimal?)t.Amount) ?? 0,
                        SuccessRate = await transactionQuery.CountAsync() > 0
                            ? (double)await transactionQuery.CountAsync(t => t.Status == "SUCCESS") * 100.0 / await transactionQuery.CountAsync()
                            : 0
                    },
                    Trends = new
                    {
                        Weekly = weeklyTransactions,
                        Monthly = monthlyTransactions
                    },
                    Distribution = new
                    {
                        TransactionStatus = transactionStatusDistribution,
                        PromiseStatus = promiseStatusDistribution
                    },
                    TopCustomers = topCustomersByArrears,
                    OfficerPerformance = officerPerformance,
                    GeneratedAt = DateTime.UtcNow
                };

                return Ok(new ApiResponseDto<object>
                {
                    Success = true,
                    Message = "Dashboard stats retrieved successfully",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting dashboard stats: {ex.Message}");
                return StatusCode(500, new ApiResponseDto<object>
                {
                    Success = false,
                    Message = $"Internal server error: {ex.Message}"
                });
            }
        }

        // ==================== SUMMARY REPORT ====================
        [HttpGet("summary")]
        public async Task<ActionResult<ApiResponseDto<object>>> GetSummaryReport()
        {
            try
            {
                var totalCustomers = await _context.Customers.CountAsync(c => c.IsActive == true);
                var totalLoanBalance = await _context.Customers
                    .Where(c => c.IsActive == true)
                    .SumAsync(c => c.LoanBalance);
                var totalArrears = await _context.Customers
                    .Where(c => c.IsActive == true)
                    .SumAsync(c => c.Arrears);
                var totalTransactions = await _context.Transactions.CountAsync();
                var completedTransactions = await _context.Transactions
                    .CountAsync(t => t.Status == "SUCCESS");
                var totalCollected = await _context.Transactions
                    .Where(t => t.Status == "SUCCESS")
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
                    ReportGeneratedAt = DateTime.UtcNow
                };

                return Ok(new ApiResponseDto<object>
                {
                    Success = true,
                    Message = "Summary report generated successfully",
                    Data = summary
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error generating summary report: {ex.Message}");
                return StatusCode(500, new ApiResponseDto<object>
                {
                    Success = false,
                    Message = $"Internal server error: {ex.Message}"
                });
            }
        }
    }
}