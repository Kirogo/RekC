//controllers/PromisesController.cs
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
    public class PromisesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IActivityService _activityService;
        private readonly ILogger<PromisesController> _logger;

        public PromisesController(
            ApplicationDbContext context,
            IActivityService activityService,
            ILogger<PromisesController> logger)
        {
            _context = context;
            _activityService = activityService;
            _logger = logger;
        }

        private int GetCurrentUserId()
        {
            return int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponseDto<List<PromiseDto>>>> GetPromises(
            [FromQuery] string? status = null,
            [FromQuery] int limit = 50,
            [FromQuery] string? sortBy = "promiseDate",
            [FromQuery] string? sortOrder = "asc")
        {
            try
            {
                var userId = GetCurrentUserId();
                _logger.LogInformation($"GetPromises: userId={userId}, status={status}, limit={limit}");
                
                // Validate userId
                if (userId == 0)
                {
                    _logger.LogWarning("GetPromises: Unable to extract user ID from token claims");
                    return Unauthorized(new ApiResponseDto
                    {
                        Success = false,
                        Message = "Invalid authentication token - User ID not found"
                    });
                }
                
                var query = _context.Promises
                    .Include(p => p.Customer)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(status))
                {
                    query = query.Where(p => p.Status == status.ToUpper());
                }

                // Apply sorting
                if (sortBy?.ToLower() == "promisedate" && sortOrder?.ToLower() == "asc")
                {
                    query = query.OrderBy(p => p.PromiseDate);
                }
                else if (sortBy?.ToLower() == "promisedate" && sortOrder?.ToLower() == "desc")
                {
                    query = query.OrderByDescending(p => p.PromiseDate);
                }
                else
                {
                    query = query.OrderBy(p => p.PromiseDate);
                }

                var promises = await query
                    .Take(limit)
                    .ToListAsync();

                var promiseDtos = promises.Select(p => MapToDto(p, p.Customer)).ToList();

                await _activityService.LogActivityAsync(userId, "PROMISE_LIST_VIEW", "Viewed promises");

                return Ok(new ApiResponseDto<List<PromiseDto>>
                {
                    Success = true,
                    Message = "Promises retrieved successfully",
                    Data = promiseDtos
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting promises: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    _logger.LogError($"Inner exception: {ex.InnerException.Message}");
                    _logger.LogError($"Inner stack trace: {ex.InnerException.StackTrace}");
                }
                return StatusCode(500, new ApiResponseDto
                {
                    Success = false,
                    Message = $"Internal server error: {ex.InnerException?.Message ?? ex.Message}"
                });
            }
        }

        [HttpGet("my-promises")]
        public async Task<ActionResult<ApiResponseDto<List<PromiseDto>>>> GetMyPromises(
            [FromQuery] string? status = null,
            [FromQuery] int limit = 50,
            [FromQuery] string? sortBy = "promiseDate",
            [FromQuery] string? sortOrder = "asc")
        {
            try
            {
                var userId = GetCurrentUserId();
                var query = _context.Promises
                    .Include(p => p.Customer)
                    .Where(p => p.CreatedByUserId == userId);

                if (!string.IsNullOrWhiteSpace(status))
                {
                    query = query.Where(p => p.Status == status.ToUpper());
                }

                // Apply sorting
                if (sortBy?.ToLower() == "promisedate" && sortOrder?.ToLower() == "asc")
                {
                    query = query.OrderBy(p => p.PromiseDate);
                }
                else if (sortBy?.ToLower() == "promisedate" && sortOrder?.ToLower() == "desc")
                {
                    query = query.OrderByDescending(p => p.PromiseDate);
                }
                else
                {
                    query = query.OrderBy(p => p.PromiseDate);
                }

                var promises = await query
                    .Take(limit)
                    .ToListAsync();

                var promiseDtos = promises.Select(p => MapToDto(p, p.Customer)).ToList();

                await _activityService.LogActivityAsync(userId, "MY_PROMISES_VIEW", "Viewed their promises");

                return Ok(new ApiResponseDto<List<PromiseDto>>
                {
                    Success = true,
                    Message = "Your promises retrieved successfully",
                    Data = promiseDtos
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting my promises: {ex.Message}");
                return StatusCode(500, new ApiResponseDto
                {
                    Success = false,
                    Message = "Internal server error"
                });
            }
        }

        [HttpGet("customer/{customerId}")]
        public async Task<ActionResult<ApiResponseDto<List<PromiseDto>>>> GetCustomerPromises(int customerId)
        {
            try
            {
                var userId = GetCurrentUserId();

                var promises = await _context.Promises
                    .Include(p => p.Customer)
                    .Where(p => p.CustomerId == customerId)
                    .OrderByDescending(p => p.PromiseDate)
                    .ToListAsync();

                var promiseDtos = promises.Select(p => MapToDto(p, p.Customer)).ToList();

                await _activityService.LogActivityAsync(userId, "CUSTOMER_PROMISES_VIEW",
                    $"Viewed promises for customer {customerId}", "CUSTOMER", null, customerId);

                return Ok(new ApiResponseDto<List<PromiseDto>>
                {
                    Success = true,
                    Message = "Customer promises retrieved successfully",
                    Data = promiseDtos
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting customer promises: {ex.Message}");
                return StatusCode(500, new ApiResponseDto
                {
                    Success = false,
                    Message = "Internal server error"
                });
            }
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponseDto<PromiseDto>>> CreatePromise([FromBody] CreatePromiseDto request)
        {
            try
            {
                var userId = GetCurrentUserId();

                // Check if customer exists
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

                var promise = new Promise
                {
                    PromiseId = GeneratePromiseId(),
                    CustomerId = request.CustomerId,
                    CustomerName = customer.Name,
                    PhoneNumber = customer.PhoneNumber,
                    PromiseAmount = request.PromiseAmount,
                    PromiseDate = request.PromiseDate,
                    PromiseType = request.PromiseType ?? "FULL_PAYMENT",
                    Status = "PENDING",
                    Notes = request.Notes,
                    CreatedByUserId = userId,
                    CreatedByName = $"{user.FirstName} {user.LastName}".Trim() ?? user.Username ?? "Unknown"
                };

                _context.Promises.Add(promise);
                await _context.SaveChangesAsync();

                await _activityService.LogActivityAsync(userId, "PROMISE_CREATE",
                    $"Created promise for customer {customer.Name}", "PROMISE", promise.Id, customer.Id);

                return CreatedAtAction(nameof(GetPromises), new { id = promise.Id },
                    new ApiResponseDto<PromiseDto>
                    {
                        Success = true,
                        Message = "Promise created successfully",
                        Data = MapToDto(promise, customer)
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating promise: {ex.Message}");
                return StatusCode(500, new ApiResponseDto
                {
                    Success = false,
                    Message = "Internal server error"
                });
            }
        }

        [HttpPatch("{id}/status")]
        public async Task<ActionResult<ApiResponseDto<PromiseDto>>> UpdatePromiseStatus(int id, [FromBody] UpdatePromiseStatusDto request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var promise = await _context.Promises
                    .Include(p => p.Customer)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (promise == null)
                {
                    return NotFound(new ApiResponseDto
                    {
                        Success = false,
                        Message = "Promise not found"
                    });
                }

                promise.Status = request.Status.ToUpper();
                promise.UpdatedAt = DateTime.UtcNow;

                if (request.FulfillmentAmount.HasValue && request.Status.ToUpper() == "FULFILLED")
                {
                    promise.FulfillmentAmount = request.FulfillmentAmount.Value;
                    promise.FulfillmentDate = DateTime.UtcNow;
                }

                _context.Promises.Update(promise);
                await _context.SaveChangesAsync();

                await _activityService.LogActivityAsync(userId, "PROMISE_UPDATE",
                    $"Updated promise status to {request.Status}", "PROMISE", promise.Id, promise.CustomerId);

                return Ok(new ApiResponseDto<PromiseDto>
                {
                    Success = true,
                    Message = "Promise status updated successfully",
                    Data = MapToDto(promise, promise.Customer)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating promise: {ex.Message}");
                return StatusCode(500, new ApiResponseDto
                {
                    Success = false,
                    Message = "Internal server error"
                });
            }
        }

        private PromiseDto MapToDto(Promise promise, Customer? customer = null)
        {
            return new PromiseDto
            {
                Id = promise.Id,
                PromiseId = promise.PromiseId ?? string.Empty,
                CustomerId = promise.CustomerId,
                CustomerName = customer?.Name ?? promise.CustomerName ?? "Unknown Customer",
                PhoneNumber = customer?.PhoneNumber ?? promise.PhoneNumber ?? "",
                PromiseAmount = promise.PromiseAmount,
                PromiseDate = promise.PromiseDate,
                PromiseType = promise.PromiseType,
                Status = promise.Status ?? "PENDING",
                FulfillmentAmount = promise.FulfillmentAmount,
                FulfillmentDate = promise.FulfillmentDate,
                Notes = promise.Notes,
                CreatedByName = promise.CreatedByName ?? "System",
                CreatedAt = promise.CreatedAt,
                UpdatedAt = promise.UpdatedAt
            };
        }

        private string GeneratePromiseId()
        {
            var timestamp = DateTime.UtcNow.Ticks.ToString();
            timestamp = timestamp.Substring(Math.Max(0, timestamp.Length - 8));
            var random = new Random().Next(1000).ToString().PadLeft(3, '0');
            return $"PRM{timestamp}{random}";
        }
    }
}