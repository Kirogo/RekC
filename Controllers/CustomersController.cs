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
    public class CustomersController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IActivityService _activityService;
        private readonly ILogger<CustomersController> _logger;

        public CustomersController(
            ApplicationDbContext context,
            IActivityService activityService,
            ILogger<CustomersController> logger)
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

        [HttpGet("assigned-to-me")]
        public async Task<ActionResult<ApiResponseDto<List<CustomerDto>>>> GetMyAssignedCustomers()
        {
            try
            {
                var userId = GetCurrentUserId();
                var role = GetCurrentUserRole();
                
                if (userId == 0)
                {
                    return Unauthorized(new ApiResponseDto
                    {
                        Success = false,
                        Message = "Invalid user ID in token"
                    });
                }

                _logger.LogInformation($"Fetching customers for user: {userId} with role: {role}");

                IQueryable<Customer> query = _context.Customers;

                if (role == "officer")
                {
                    query = query.Where(c => c.AssignedToUserId == userId);
                }

                var customers = await query
                    .Where(c => c.IsActive == true)
                    .OrderBy(c => c.Name)
                    .Select(c => new CustomerDto
                    {
                        Id = c.Id,
                        CustomerInternalId = c.CustomerInternalId ?? string.Empty,
                        CustomerId = c.CustomerId ?? string.Empty,
                        PhoneNumber = c.PhoneNumber ?? string.Empty,
                        Name = c.Name ?? "Unknown",
                        AccountNumber = c.AccountNumber ?? string.Empty,
                        LoanBalance = c.LoanBalance,
                        Arrears = c.Arrears,
                        TotalRepayments = c.TotalRepayments,
                        Email = c.Email,
                        NationalId = c.NationalId,
                        LastPaymentDate = c.LastPaymentDate,
                        LastContactDate = c.LastContactDate,
                        Status = c.Status ?? "ACTIVE",
                        LoanType = c.LoanType,
                        IsActive = c.IsActive == true,
                        AssignedToUserId = c.AssignedToUserId,
                        CreatedAt = c.CreatedAt ?? DateTime.UtcNow,
                        UpdatedAt = c.UpdatedAt ?? DateTime.UtcNow
                    })
                    .ToListAsync();

                _logger.LogInformation($"Found {customers.Count} customers for user {userId}");

                try
                {
                    await _activityService.LogActivityAsync(userId, "MY_CUSTOMERS_VIEW", $"Viewed assigned customers. Found {customers.Count} customers");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to log activity: {ex.Message}");
                }

                return Ok(new ApiResponseDto<List<CustomerDto>>
                {
                    Success = true,
                    Message = "Assigned customers retrieved successfully",
                    Data = customers
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting assigned customers: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                return StatusCode(500, new ApiResponseDto
                {
                    Success = false,
                    Message = $"Internal server error: {ex.Message}"
                });
            }
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponseDto<PaginationDto<CustomerDto>>>> GetCustomers(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            [FromQuery] string? search = null)
        {
            try
            {
                var userId = GetCurrentUserId();
                var role = GetCurrentUserRole();

                _logger.LogInformation($"GetCustomers called by user {userId} with role {role}");

                IQueryable<Customer> query = _context.Customers;

                if (role == "officer")
                {
                    query = query.Where(c => c.AssignedToUserId == userId);
                }

                query = query.Where(c => c.IsActive == true);

                if (!string.IsNullOrWhiteSpace(search))
                {
                    query = query.Where(c => c.Name.Contains(search) || 
                                             c.PhoneNumber.Contains(search) || 
                                             c.CustomerId.Contains(search));
                }

                var total = await query.CountAsync();

                var customers = await query
                    .OrderBy(c => c.Name)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(c => new CustomerDto
                    {
                        Id = c.Id,
                        CustomerInternalId = c.CustomerInternalId ?? string.Empty,
                        CustomerId = c.CustomerId ?? string.Empty,
                        PhoneNumber = c.PhoneNumber ?? string.Empty,
                        Name = c.Name ?? "Unknown",
                        AccountNumber = c.AccountNumber ?? string.Empty,
                        LoanBalance = c.LoanBalance,
                        Arrears = c.Arrears,
                        TotalRepayments = c.TotalRepayments,
                        Email = c.Email,
                        NationalId = c.NationalId,
                        LastPaymentDate = c.LastPaymentDate,
                        LastContactDate = c.LastContactDate,
                        Status = c.Status ?? "ACTIVE",
                        LoanType = c.LoanType,
                        IsActive = c.IsActive == true,
                        AssignedToUserId = c.AssignedToUserId,
                        CreatedAt = c.CreatedAt ?? DateTime.UtcNow,
                        UpdatedAt = c.UpdatedAt ?? DateTime.UtcNow
                    })
                    .ToListAsync();

                _logger.LogInformation($"Retrieved {customers.Count} customers out of {total} total");

                var result = new PaginationDto<CustomerDto>
                {
                    Items = customers,
                    TotalCount = total,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling(total / (double)pageSize)
                };

                return Ok(new ApiResponseDto<PaginationDto<CustomerDto>>
                {
                    Success = true,
                    Message = "Customers retrieved successfully",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting customers: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                return StatusCode(500, new ApiResponseDto
                {
                    Success = false,
                    Message = $"Internal server error: {ex.Message}"
                });
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponseDto<CustomerDto>>> GetCustomer(int id)
        {
            try
            {
                var customer = await _context.Customers
                    .Where(c => c.Id == id)
                    .Select(c => new CustomerDto
                    {
                        Id = c.Id,
                        CustomerInternalId = c.CustomerInternalId ?? string.Empty,
                        CustomerId = c.CustomerId ?? string.Empty,
                        PhoneNumber = c.PhoneNumber ?? string.Empty,
                        Name = c.Name ?? "Unknown",
                        AccountNumber = c.AccountNumber ?? string.Empty,
                        LoanBalance = c.LoanBalance,
                        Arrears = c.Arrears,
                        TotalRepayments = c.TotalRepayments,
                        Email = c.Email,
                        NationalId = c.NationalId,
                        LastPaymentDate = c.LastPaymentDate,
                        LastContactDate = c.LastContactDate,
                        Status = c.Status ?? "ACTIVE",
                        LoanType = c.LoanType,
                        IsActive = c.IsActive == true,
                        AssignedToUserId = c.AssignedToUserId,
                        CreatedAt = c.CreatedAt ?? DateTime.UtcNow,
                        UpdatedAt = c.UpdatedAt ?? DateTime.UtcNow
                    })
                    .FirstOrDefaultAsync();

                if (customer == null)
                {
                    return NotFound(new ApiResponseDto
                    {
                        Success = false,
                        Message = "Customer not found"
                    });
                }

                return Ok(new ApiResponseDto<CustomerDto>
                {
                    Success = true,
                    Message = "Customer retrieved successfully",
                    Data = customer
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting customer: {ex.Message}");
                return StatusCode(500, new ApiResponseDto
                {
                    Success = false,
                    Message = "Internal server error"
                });
            }
        }

        [HttpGet("dashboard/stats")]
        public async Task<ActionResult<ApiResponseDto<DashboardStatsDto>>> GetDashboardStats()
        {
            try
            {
                var userId = GetCurrentUserId();
                var role = GetCurrentUserRole();

                _logger.LogInformation($"Getting dashboard stats for user {userId} with role {role}");

                IQueryable<Customer> query = _context.Customers;
                IQueryable<Transaction> transactionQuery = _context.Transactions;

                if (role == "officer")
                {
                    query = query.Where(c => c.AssignedToUserId == userId);
                    transactionQuery = transactionQuery.Where(t => t.InitiatedByUserId == userId);
                }

                query = query.Where(c => c.IsActive == true);

                var totalCustomers = await query.CountAsync();
                var totalLoanBalance = await query.SumAsync(c => c.LoanBalance);
                var totalArrears = await query.SumAsync(c => c.Arrears);
                var activeLoans = await query.CountAsync(c => c.LoanBalance > 0);
                
                var totalCollected = await transactionQuery
                    .Where(t => t.Status == "SUCCESS" || t.Status == "COMPLETED")
                    .SumAsync(t => (decimal?)t.Amount) ?? 0;

                var collectionRate = totalArrears > 0 ? (totalCollected / totalArrears) * 100 : 0;

                var stats = new DashboardStatsDto
                {
                    TotalCustomers = totalCustomers,
                    TotalLoanBalance = totalLoanBalance,
                    TotalArrears = totalArrears,
                    ActiveLoans = activeLoans,
                    CollectionRate = Math.Round(collectionRate, 2)
                };

                _logger.LogInformation($"Dashboard stats: TotalCustomers={totalCustomers}, TotalLoanBalance={totalLoanBalance}, TotalArrears={totalArrears}");

                return Ok(new ApiResponseDto<DashboardStatsDto>
                {
                    Success = true,
                    Message = "Dashboard stats retrieved successfully",
                    Data = stats
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting dashboard stats: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                return StatusCode(500, new ApiResponseDto
                {
                    Success = false,
                    Message = $"Internal server error: {ex.Message}"
                });
            }
        }

        [HttpGet("phone/{phoneNumber}")]
        public async Task<ActionResult<ApiResponseDto<CustomerDto>>> GetCustomerByPhone(string phoneNumber)
        {
            try
            {
                var customer = await _context.Customers
                    .Where(c => c.PhoneNumber == phoneNumber && c.IsActive == true)
                    .Select(c => new CustomerDto
                    {
                        Id = c.Id,
                        CustomerInternalId = c.CustomerInternalId ?? string.Empty,
                        CustomerId = c.CustomerId ?? string.Empty,
                        PhoneNumber = c.PhoneNumber ?? string.Empty,
                        Name = c.Name ?? "Unknown",
                        AccountNumber = c.AccountNumber ?? string.Empty,
                        LoanBalance = c.LoanBalance,
                        Arrears = c.Arrears,
                        TotalRepayments = c.TotalRepayments,
                        Email = c.Email,
                        NationalId = c.NationalId,
                        LastPaymentDate = c.LastPaymentDate,
                        LastContactDate = c.LastContactDate,
                        Status = c.Status ?? "ACTIVE",
                        LoanType = c.LoanType,
                        IsActive = c.IsActive == true,
                        AssignedToUserId = c.AssignedToUserId,
                        CreatedAt = c.CreatedAt ?? DateTime.UtcNow,
                        UpdatedAt = c.UpdatedAt ?? DateTime.UtcNow
                    })
                    .FirstOrDefaultAsync();

                if (customer == null)
                {
                    return NotFound(new ApiResponseDto
                    {
                        Success = false,
                        Message = "Customer not found"
                    });
                }

                return Ok(new ApiResponseDto<CustomerDto>
                {
                    Success = true,
                    Message = "Customer retrieved successfully",
                    Data = customer
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting customer by phone: {ex.Message}");
                return StatusCode(500, new ApiResponseDto
                {
                    Success = false,
                    Message = "Internal server error"
                });
            }
        }
    }
}