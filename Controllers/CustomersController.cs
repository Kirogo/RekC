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
            return int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        }

        private string GetCurrentUserRole()
        {
            return User.FindFirst(ClaimTypes.Role)?.Value ?? "officer";
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponseDto<PaginationDto<CustomerDto>>>> GetCustomers(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                var userId = GetCurrentUserId();
                var role = GetCurrentUserRole();

                IQueryable<Customer> query = _context.Customers
                    .Include(c => c.AssignedToUser)
                    .Where(c => c.IsActive == true);  // FIXED: Compare with true

                // Officers can only see their assigned customers
                if (role == "officer")
                {
                    query = query.Where(c => c.AssignedToUserId == userId);
                }

                var total = await query.CountAsync();  // FIXED: 'total' is now defined

                var customers = await query
                    .OrderBy(c => c.Name)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var customerDtos = customers.Select(c => MapToDto(c)).ToList();

                var pagination = new PaginationDto<CustomerDto>
                {
                    Items = customerDtos,
                    TotalCount = total,  // FIXED: Now using the defined variable
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling(total / (double)pageSize)  // FIXED: Now using the defined variable
                };

                await _activityService.LogActivityAsync(userId, "CUSTOMER_LIST_VIEW", "Viewed customer list");

                return Ok(new ApiResponseDto<PaginationDto<CustomerDto>>
                {
                    Success = true,
                    Message = "Customers retrieved successfully",
                    Data = pagination
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting customers: {ex.Message}");
                return StatusCode(500, new ApiResponseDto
                {
                    Success = false,
                    Message = "Internal server error"
                });
            }
        }

        [HttpGet("assigned-to-me")]
        public async Task<ActionResult<ApiResponseDto<List<CustomerDto>>>> GetMyAssignedCustomers()
        {
            try
            {
                var userId = GetCurrentUserId();

                var customers = await _context.Customers
                    .Include(c => c.AssignedToUser)
                    .Where(c => c.AssignedToUserId == userId && c.IsActive == true)  // FIXED: Compare with true
                    .OrderBy(c => c.Name)
                    .ToListAsync();

                var customerDtos = customers.Select(c => MapToDto(c)).ToList();

                await _activityService.LogActivityAsync(userId, "MY_CUSTOMERS_VIEW", "Viewed assigned customers");

                return Ok(new ApiResponseDto<List<CustomerDto>>
                {
                    Success = true,
                    Message = "Assigned customers retrieved successfully",
                    Data = customerDtos
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting assigned customers: {ex.Message}");
                return StatusCode(500, new ApiResponseDto
                {
                    Success = false,
                    Message = "Internal server error"
                });
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponseDto<CustomerDto>>> GetCustomer(int id)
        {
            try
            {
                var customer = await _context.Customers
                    .Include(c => c.AssignedToUser)
                    .FirstOrDefaultAsync(c => c.Id == id && c.IsActive == true);  // FIXED: Compare with true

                if (customer == null)
                {
                    return NotFound(new ApiResponseDto
                    {
                        Success = false,
                        Message = "Customer not found"
                    });
                }

                var userId = GetCurrentUserId();
                await _activityService.LogActivityAsync(userId, "CUSTOMER_VIEW", 
                    $"Viewed customer: {customer.Name}", "CUSTOMER", id, id);

                return Ok(new ApiResponseDto<CustomerDto>
                {
                    Success = true,
                    Message = "Customer retrieved successfully",
                    Data = MapToDto(customer)
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
                
                // Validate userId
                if (userId == 0)
                {
                    _logger.LogWarning("GetDashboardStats: Unable to extract user ID from token claims");
                    return Unauthorized(new ApiResponseDto
                    {
                        Success = false,
                        Message = "Invalid authentication token - User ID not found"
                    });
                }
                
                var role = GetCurrentUserRole();

                IQueryable<Customer> query = _context.Customers.Where(c => c.IsActive == true);  // FIXED: Compare with true

                // Officers only see stats for their assigned customers
                if (role == "officer")
                {
                    query = query.Where(c => c.AssignedToUserId == userId);
                }

                var totalCustomers = await query.CountAsync();
                var totalLoanBalance = await query.SumAsync(c => c.LoanBalance);
                var totalArrears = await query.SumAsync(c => c.Arrears);
                var activeLoans = await query.CountAsync(c => c.LoanBalance > 0);

                var stats = new DashboardStatsDto
                {
                    TotalCustomers = totalCustomers,
                    TotalLoanBalance = totalLoanBalance,
                    TotalArrears = totalArrears,
                    ActiveLoans = activeLoans,
                    CollectionRate = totalArrears > 0 
                        ? (await _context.Transactions
                            .Where(t => t.Status == "SUCCESS")
                            .SumAsync(t => (decimal?)t.Amount) ?? 0) / totalArrears * 100 
                        : 0
                };

                await _activityService.LogActivityAsync(userId, "DASHBOARD_VIEW", "Viewed dashboard stats");

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
                return StatusCode(500, new ApiResponseDto
                {
                    Success = false,
                    Message = "Internal server error"
                });
            }
        }

        private CustomerDto MapToDto(Customer customer)
        {
            return new CustomerDto
            {
                Id = customer.Id,
                CustomerInternalId = customer.CustomerInternalId ?? string.Empty,
                CustomerId = customer.CustomerId ?? string.Empty,
                PhoneNumber = customer.PhoneNumber ?? string.Empty,
                Name = customer.Name ?? string.Empty,
                AccountNumber = customer.AccountNumber ?? string.Empty,
                LoanBalance = customer.LoanBalance,
                Arrears = customer.Arrears,
                TotalRepayments = customer.TotalRepayments,
                Email = customer.Email,
                NationalId = customer.NationalId,
                LastPaymentDate = customer.LastPaymentDate,
                LastContactDate = customer.LastContactDate,
                Status = customer.Status,
                IsActive = customer.IsActive == true,  // Convert bool? to bool
                AssignedToUserId = customer.AssignedToUserId,
                AssignedToUserName = customer.AssignedToUser?.Username,
                LoanType = customer.LoanType,
                CreatedAt = customer.CreatedAt ?? DateTime.UtcNow,
                UpdatedAt = customer.UpdatedAt ?? DateTime.UtcNow
            };
        }

        // ==================== CUSTOMER CRUD OPERATIONS ====================

        [HttpPost]
        public async Task<ActionResult<ApiResponseDto<CustomerDto>>> CreateCustomer([FromBody] CreateCustomerDto request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var role = GetCurrentUserRole();

                // Only admins and supervisors can create customers
                if (role != "admin" && role != "supervisor")
                {
                    return Forbid();
                }

                // Validation
                if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.PhoneNumber))
                {
                    return BadRequest(new ApiResponseDto
                    {
                        Success = false,
                        Message = "Name and phone number are required"
                    });
                }

                // Check if customer already exists
                var existing = await _context.Customers
                    .FirstOrDefaultAsync(c => c.PhoneNumber == request.PhoneNumber || c.CustomerId == request.CustomerId);

                if (existing != null)
                {
                    return BadRequest(new ApiResponseDto
                    {
                        Success = false,
                        Message = "Customer with this phone number or ID already exists"
                    });
                }

                var customer = new Customer
                {
                    CustomerInternalId = request.CustomerInternalId ?? $"CUS_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                    CustomerId = request.CustomerId ?? request.PhoneNumber,
                    PhoneNumber = request.PhoneNumber,
                    Name = request.Name,
                    AccountNumber = request.AccountNumber,
                    Email = request.Email?.ToLower(),
                    NationalId = request.NationalId,
                    LoanBalance = request.LoanBalance,
                    Arrears = request.Arrears,
                    LoanType = request.LoanType ?? "Standard",
                    Status = "ACTIVE",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Customers.Add(customer);
                await _context.SaveChangesAsync();

                await _activityService.LogActivityAsync(userId, "CUSTOMER_CREATE",
                    $"Created customer: {customer.Name}");

                return Ok(new ApiResponseDto<CustomerDto>
                {
                    Success = true,
                    Message = "Customer created successfully",
                    Data = MapToDto(customer)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating customer: {ex.Message}");
                return StatusCode(500, new ApiResponseDto
                {
                    Success = false,
                    Message = "Internal server error"
                });
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<ApiResponseDto<CustomerDto>>> UpdateCustomer(int id, [FromBody] UpdateCustomerDto request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var role = GetCurrentUserRole();

                // Only admins and supervisors can update customers
                if (role != "admin" && role != "supervisor")
                {
                    return Forbid();
                }

                var customer = await _context.Customers.FindAsync(id);
                if (customer == null)
                {
                    return NotFound(new ApiResponseDto
                    {
                        Success = false,
                        Message = "Customer not found"
                    });
                }

                // Update fields
                if (!string.IsNullOrEmpty(request.Name))
                    customer.Name = request.Name;
                if (!string.IsNullOrEmpty(request.PhoneNumber))
                    customer.PhoneNumber = request.PhoneNumber;
                if (!string.IsNullOrEmpty(request.Email))
                    customer.Email = request.Email.ToLower();
                if (!string.IsNullOrEmpty(request.AccountNumber))
                    customer.AccountNumber = request.AccountNumber;
                if (request.LoanBalance.HasValue && request.LoanBalance.Value >= 0)
                    customer.LoanBalance = request.LoanBalance.Value;
                if (request.Arrears.HasValue && request.Arrears.Value >= 0)
                    customer.Arrears = request.Arrears.Value;
                if (request.AssignedToUserId.HasValue && request.AssignedToUserId.Value > 0)
                    customer.AssignedToUserId = request.AssignedToUserId.Value;
                if (request.IsActive.HasValue)
                    customer.IsActive = request.IsActive.Value;

                customer.UpdatedAt = DateTime.UtcNow;
                _context.Customers.Update(customer);
                await _context.SaveChangesAsync();

                await _activityService.LogActivityAsync(userId, "CUSTOMER_UPDATE",
                    $"Updated customer: {customer.Name}");

                return Ok(new ApiResponseDto<CustomerDto>
                {
                    Success = true,
                    Message = "Customer updated successfully",
                    Data = MapToDto(customer)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating customer: {ex.Message}");
                return StatusCode(500, new ApiResponseDto
                {
                    Success = false,
                    Message = "Internal server error"
                });
            }
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult<ApiResponseDto>> DeleteCustomer(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                var role = GetCurrentUserRole();

                // Only admins can delete customers
                if (role != "admin")
                {
                    return Forbid();
                }

                var customer = await _context.Customers.FindAsync(id);
                if (customer == null)
                {
                    return NotFound(new ApiResponseDto
                    {
                        Success = false,
                        Message = "Customer not found"
                    });
                }

                _context.Customers.Remove(customer);
                await _context.SaveChangesAsync();

                await _activityService.LogActivityAsync(userId, "CUSTOMER_DELETE",
                    $"Deleted customer: {customer.Name}");

                return Ok(new ApiResponseDto
                {
                    Success = true,
                    Message = "Customer deleted successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting customer: {ex.Message}");
                return StatusCode(500, new ApiResponseDto
                {
                    Success = false,
                    Message = "Internal server error"
                });
            }
        }

        [HttpGet("phone/{phoneNumber}")]
        public async Task<ActionResult<ApiResponseDto<CustomerDto>>> GetCustomerByPhone(string phoneNumber)
        {
            try
            {
                var customer = await _context.Customers
                    .Include(c => c.AssignedToUser)
                    .FirstOrDefaultAsync(c => c.PhoneNumber == phoneNumber && c.IsActive == true);

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
                    Data = MapToDto(customer)
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