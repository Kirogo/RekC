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

        private (int userId, string userName, string userRole) GetCurrentUserInfo()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userNameClaim = User.FindFirst(ClaimTypes.Name)?.Value;
            var userRoleClaim = User.FindFirst(ClaimTypes.Role)?.Value;
            
            return (
                userId: int.TryParse(userIdClaim, out var id) ? id : 0,
                userName: userNameClaim ?? "Unknown",
                userRole: userRoleClaim ?? "officer"
            );
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponseDto<object>>> GetPromises(
            [FromQuery] string? status = null,
            [FromQuery] string? promiseType = null,
            [FromQuery] string? startDate = null,
            [FromQuery] string? endDate = null,
            [FromQuery] string? customerName = null,
            [FromQuery] int page = 1,
            [FromQuery] int limit = 20,
            [FromQuery] string sortBy = "promiseDate",
            [FromQuery] string sortOrder = "desc")
        {
            try
            {
                var userInfo = GetCurrentUserInfo();
                
                if (userInfo.userId == 0)
                {
                    return Unauthorized(new ApiResponseDto
                    {
                        Success = false,
                        Message = "Invalid authentication token"
                    });
                }
                
                var query = _context.Promises.AsQueryable();

                // Role-based filtering
                if (userInfo.userRole == "officer")
                {
                    query = query.Where(p => p.CreatedByUserId == userInfo.userId);
                }

                // Apply filters
                if (!string.IsNullOrWhiteSpace(status))
                {
                    query = query.Where(p => p.Status == status.ToUpper());
                }

                if (!string.IsNullOrWhiteSpace(promiseType))
                {
                    query = query.Where(p => p.PromiseType == promiseType);
                }

                if (!string.IsNullOrWhiteSpace(startDate) && DateTime.TryParse(startDate, out var start))
                {
                    query = query.Where(p => p.PromiseDate >= start.Date);
                }

                if (!string.IsNullOrWhiteSpace(endDate) && DateTime.TryParse(endDate, out var end))
                {
                    var endOfDay = end.Date.AddDays(1).AddTicks(-1);
                    query = query.Where(p => p.PromiseDate <= endOfDay);
                }

                if (!string.IsNullOrWhiteSpace(customerName))
                {
                    query = query.Where(p => p.CustomerName != null && p.CustomerName.Contains(customerName));
                }

                var totalCount = await query.CountAsync();

                // Apply sorting
                query = sortBy.ToLower() switch
                {
                    "promiseamount" => sortOrder == "asc" ? query.OrderBy(p => p.PromiseAmount) : query.OrderByDescending(p => p.PromiseAmount),
                    "status" => sortOrder == "asc" ? query.OrderBy(p => p.Status) : query.OrderByDescending(p => p.Status),
                    "promisedate" => sortOrder == "asc" ? query.OrderBy(p => p.PromiseDate) : query.OrderByDescending(p => p.PromiseDate),
                    _ => sortOrder == "asc" ? query.OrderBy(p => p.PromiseDate) : query.OrderByDescending(p => p.PromiseDate)
                };

                var promises = await query
                    .Skip((page - 1) * limit)
                    .Take(limit)
                    .ToListAsync();

                var statistics = new
                {
                    Total = await _context.Promises
                        .Where(p => userInfo.userRole == "officer" ? p.CreatedByUserId == userInfo.userId : true)
                        .CountAsync(),
                    Pending = await _context.Promises
                        .Where(p => (userInfo.userRole == "officer" ? p.CreatedByUserId == userInfo.userId : true) && p.Status == "PENDING")
                        .CountAsync(),
                    Fulfilled = await _context.Promises
                        .Where(p => (userInfo.userRole == "officer" ? p.CreatedByUserId == userInfo.userId : true) && p.Status == "FULFILLED")
                        .CountAsync(),
                    Broken = await _context.Promises
                        .Where(p => (userInfo.userRole == "officer" ? p.CreatedByUserId == userInfo.userId : true) && p.Status == "BROKEN")
                        .CountAsync(),
                    FulfillmentRate = 0m
                };

                if (statistics.Total > 0)
                {
                    statistics = statistics with { FulfillmentRate = (decimal)statistics.Fulfilled / statistics.Total * 100m };
                }

                var promiseDtos = promises.Select(p => new PromiseDto
                {
                    Id = p.Id,
                    PromiseId = p.PromiseId ?? string.Empty,
                    CustomerId = p.CustomerId,
                    CustomerName = p.CustomerName ?? "Unknown Customer",
                    PhoneNumber = p.PhoneNumber ?? "",
                    PromiseAmount = p.PromiseAmount,
                    PromiseDate = p.PromiseDate,
                    PromiseType = p.PromiseType,
                    Status = p.Status ?? "PENDING",
                    FulfillmentAmount = p.FulfillmentAmount,
                    FulfillmentDate = p.FulfillmentDate,
                    Notes = p.Notes,
                    CreatedByName = p.CreatedByName ?? "System",
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt
                }).ToList();

                await _activityService.LogActivityAsync(userInfo.userId, "PROMISE_LIST_VIEW", "Viewed promises");

                return Ok(new ApiResponseDto<object>
                {
                    Success = true,
                    Message = "Promises retrieved successfully",
                    Data = new
                    {
                        Promises = promiseDtos,
                        Statistics = statistics,
                        Pagination = new
                        {
                            Page = page,
                            Limit = limit,
                            Total = totalCount,
                            Pages = (int)Math.Ceiling((double)totalCount / limit)
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting promises: {ex.Message}");
                return StatusCode(500, new ApiResponseDto
                {
                    Success = false,
                    Message = $"Internal server error: {ex.Message}"
                });
            }
        }

        [HttpGet("my-promises")]
        public async Task<ActionResult<ApiResponseDto<object>>> GetMyPromises(
            [FromQuery] string? status = null,
            [FromQuery] string? promiseType = null,
            [FromQuery] string? startDate = null,
            [FromQuery] string? endDate = null,
            [FromQuery] string? customerName = null)
        {
            try
            {
                var userInfo = GetCurrentUserInfo();
                if (userInfo.userId == 0)
                {
                    return Unauthorized(new ApiResponseDto
                    {
                        Success = false,
                        Message = "Invalid authentication token"
                    });
                }

                var query = _context.Promises
                    .Where(p => p.CreatedByUserId == userInfo.userId);

                if (!string.IsNullOrWhiteSpace(status))
                {
                    query = query.Where(p => p.Status == status.ToUpper());
                }

                if (!string.IsNullOrWhiteSpace(promiseType))
                {
                    query = query.Where(p => p.PromiseType == promiseType);
                }

                if (!string.IsNullOrWhiteSpace(startDate) && DateTime.TryParse(startDate, out var start))
                {
                    query = query.Where(p => p.PromiseDate >= start.Date);
                }

                if (!string.IsNullOrWhiteSpace(endDate) && DateTime.TryParse(endDate, out var end))
                {
                    var endOfDay = end.Date.AddDays(1).AddTicks(-1);
                    query = query.Where(p => p.PromiseDate <= endOfDay);
                }

                if (!string.IsNullOrWhiteSpace(customerName))
                {
                    query = query.Where(p => p.CustomerName != null && p.CustomerName.Contains(customerName));
                }

                var promises = await query
                    .OrderByDescending(p => p.PromiseDate)
                    .ToListAsync();

                var promiseDtos = promises.Select(p => new PromiseDto
                {
                    Id = p.Id,
                    PromiseId = p.PromiseId ?? string.Empty,
                    CustomerId = p.CustomerId,
                    CustomerName = p.CustomerName ?? "Unknown Customer",
                    PhoneNumber = p.PhoneNumber ?? "",
                    PromiseAmount = p.PromiseAmount,
                    PromiseDate = p.PromiseDate,
                    PromiseType = p.PromiseType,
                    Status = p.Status ?? "PENDING",
                    FulfillmentAmount = p.FulfillmentAmount,
                    FulfillmentDate = p.FulfillmentDate,
                    Notes = p.Notes,
                    CreatedByName = p.CreatedByName ?? "System",
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt
                }).ToList();

                var summary = new
                {
                    Total = promiseDtos.Count,
                    Pending = promiseDtos.Count(p => p.Status == "PENDING"),
                    Fulfilled = promiseDtos.Count(p => p.Status == "FULFILLED"),
                    Broken = promiseDtos.Count(p => p.Status == "BROKEN"),
                    FulfillmentRate = promiseDtos.Count > 0 
                        ? (decimal)promiseDtos.Count(p => p.Status == "FULFILLED") / promiseDtos.Count * 100m 
                        : 0m
                };

                await _activityService.LogActivityAsync(userInfo.userId, "MY_PROMISES_VIEW", "Viewed their promises");

                return Ok(new ApiResponseDto<object>
                {
                    Success = true,
                    Message = "Your promises retrieved successfully",
                    Data = new
                    {
                        Promises = promiseDtos,
                        Summary = summary
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting my promises: {ex.Message}");
                return StatusCode(500, new ApiResponseDto
                {
                    Success = false,
                    Message = $"Internal server error: {ex.Message}"
                });
            }
        }

        [HttpGet("customer/{customerId}")]
        public async Task<ActionResult<ApiResponseDto<List<PromiseDto>>>> GetCustomerPromises(
            int customerId, 
            [FromQuery] int limit = 5)
        {
            try
            {
                var userInfo = GetCurrentUserInfo();

                var promises = await _context.Promises
                    .Where(p => p.CustomerId == customerId)
                    .OrderByDescending(p => p.PromiseDate)
                    .Take(limit)
                    .Select(p => new PromiseDto
                    {
                        Id = p.Id,
                        PromiseId = p.PromiseId ?? string.Empty,
                        CustomerId = p.CustomerId,
                        CustomerName = p.CustomerName ?? "Unknown",
                        PhoneNumber = p.PhoneNumber ?? "",
                        PromiseAmount = p.PromiseAmount,
                        PromiseDate = p.PromiseDate,
                        PromiseType = p.PromiseType,
                        Status = p.Status ?? "PENDING",
                        FulfillmentAmount = p.FulfillmentAmount,
                        FulfillmentDate = p.FulfillmentDate,
                        Notes = p.Notes,
                        CreatedByName = p.CreatedByName ?? "System",
                        CreatedAt = p.CreatedAt,
                        UpdatedAt = p.UpdatedAt
                    })
                    .ToListAsync();

                await _activityService.LogActivityAsync(userInfo.userId, "CUSTOMER_PROMISES_VIEW",
                    $"Viewed promises for customer {customerId}");

                return Ok(new ApiResponseDto<List<PromiseDto>>
                {
                    Success = true,
                    Message = "Customer promises retrieved successfully",
                    Data = promises
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting customer promises: {ex.Message}");
                return StatusCode(500, new ApiResponseDto
                {
                    Success = false,
                    Message = $"Internal server error: {ex.Message}"
                });
            }
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponseDto<PromiseDto>>> CreatePromise([FromBody] CreatePromiseDto createDto)
        {
            try
            {
                var userInfo = GetCurrentUserInfo();
                if (userInfo.userId == 0)
                {
                    return Unauthorized(new ApiResponseDto
                    {
                        Success = false,
                        Message = "Invalid authentication token"
                    });
                }

                var customer = await _context.Customers.FindAsync(createDto.CustomerId);
                if (customer == null)
                {
                    return BadRequest(new ApiResponseDto
                    {
                        Success = false,
                        Message = "Customer not found"
                    });
                }

                var promiseId = $"PROM-{DateTime.Now:yyyyMMddHHmmss}-{new Random().Next(1000, 9999)}";

                var promise = new Promise
                {
                    PromiseId = promiseId,
                    CustomerId = createDto.CustomerId,
                    CustomerName = customer.Name,
                    PhoneNumber = customer.PhoneNumber,
                    PromiseAmount = createDto.PromiseAmount,
                    PromiseDate = createDto.PromiseDate,
                    PromiseType = createDto.PromiseType ?? "FULL_PAYMENT",
                    Status = "PENDING",
                    Notes = createDto.Notes,
                    CreatedByUserId = userInfo.userId,
                    CreatedByName = userInfo.userName,
                    ReminderSent = false,
                    FollowUpCount = 0,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Promises.Add(promise);
                await _context.SaveChangesAsync();

                // Update customer's promise statistics
                customer.PromiseCount = (customer.PromiseCount ?? 0) + 1;
                customer.LastPromiseDate = DateTime.UtcNow;
                customer.HasOutstandingPromise = true;
                await _context.SaveChangesAsync();

                await _activityService.LogActivityAsync(userInfo.userId, "PROMISE_CREATED",
                    $"Created promise of {createDto.PromiseAmount:C} for customer {customer.Name}");

                var promiseDto = new PromiseDto
                {
                    Id = promise.Id,
                    PromiseId = promise.PromiseId,
                    CustomerId = promise.CustomerId,
                    CustomerName = promise.CustomerName,
                    PhoneNumber = promise.PhoneNumber,
                    PromiseAmount = promise.PromiseAmount,
                    PromiseDate = promise.PromiseDate,
                    PromiseType = promise.PromiseType,
                    Status = promise.Status,
                    FulfillmentAmount = promise.FulfillmentAmount,
                    FulfillmentDate = promise.FulfillmentDate,
                    Notes = promise.Notes,
                    CreatedByName = promise.CreatedByName,
                    CreatedAt = promise.CreatedAt,
                    UpdatedAt = promise.UpdatedAt
                };

                return Ok(new ApiResponseDto<PromiseDto>
                {
                    Success = true,
                    Message = "Promise created successfully",
                    Data = promiseDto
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating promise: {ex.Message}");
                return StatusCode(500, new ApiResponseDto
                {
                    Success = false,
                    Message = $"Internal server error: {ex.Message}"
                });
            }
        }

        [HttpPatch("{id}/status")]
        public async Task<ActionResult<ApiResponseDto<PromiseDto>>> UpdatePromiseStatus(
            int id, 
            [FromBody] UpdatePromiseStatusDto updateDto)
        {
            try
            {
                var userInfo = GetCurrentUserInfo();
                if (userInfo.userId == 0)
                {
                    return Unauthorized(new ApiResponseDto
                    {
                        Success = false,
                        Message = "Invalid authentication token"
                    });
                }

                var promise = await _context.Promises.FindAsync(id);
                if (promise == null)
                {
                    return NotFound(new ApiResponseDto
                    {
                        Success = false,
                        Message = "Promise not found"
                    });
                }

                var customer = await _context.Customers.FindAsync(promise.CustomerId);
                var oldStatus = promise.Status;

                promise.Status = updateDto.Status.ToUpper();
                promise.UpdatedAt = DateTime.UtcNow;

                if (updateDto.Status.ToUpper() == "FULFILLED")
                {
                    promise.FulfillmentAmount = updateDto.FulfillmentAmount ?? promise.PromiseAmount;
                    promise.FulfillmentDate = DateTime.UtcNow;
                    
                    if (customer != null)
                    {
                        customer.FulfilledPromiseCount = (customer.FulfilledPromiseCount ?? 0) + 1;
                        if ((customer.PromiseCount ?? 0) > 0)
                        {
                            // Convert to double first to avoid decimal/int issues
                            var fulfillmentRate = ((double)(customer.FulfilledPromiseCount ?? 0) / (customer.PromiseCount ?? 1)) * 100;
                            customer.PromiseFulfillmentRate = (decimal)Math.Round(fulfillmentRate, 2);
                        }
                        customer.HasOutstandingPromise = false;
                    }
                    
                    var transaction = new Transaction
                    {
                        TransactionId = $"TXN-{DateTime.Now:yyyyMMddHHmmss}-{new Random().Next(1000, 9999)}",
                        CustomerId = promise.CustomerId,
                        Amount = promise.FulfillmentAmount.Value,
                        PaymentMethod = "PROMISE_FULFILLMENT",
                        Status = "SUCCESS",
                        Description = $"Promise fulfillment - {promise.PromiseId}",
                        InitiatedBy = userInfo.userName,
                        InitiatedByUserId = userInfo.userId,
                        ProcessedAt = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    
                    _context.Transactions.Add(transaction);
                }
                else if (updateDto.Status.ToUpper() == "BROKEN")
                {
                    promise.FollowUpCount = (promise.FollowUpCount ?? 0) + 1;
                    promise.NextFollowUpDate = DateTime.UtcNow.AddDays(1);
                    
                    if (customer != null)
                    {
                        customer.HasOutstandingPromise = false;
                    }
                }

                await _context.SaveChangesAsync();

                await _activityService.LogActivityAsync(userInfo.userId, "PROMISE_STATUS_UPDATED",
                    $"Updated promise {promise.PromiseId} status from {oldStatus} to {updateDto.Status}");

                var promiseDto = new PromiseDto
                {
                    Id = promise.Id,
                    PromiseId = promise.PromiseId,
                    CustomerId = promise.CustomerId,
                    CustomerName = promise.CustomerName,
                    PhoneNumber = promise.PhoneNumber,
                    PromiseAmount = promise.PromiseAmount,
                    PromiseDate = promise.PromiseDate,
                    PromiseType = promise.PromiseType,
                    Status = promise.Status,
                    FulfillmentAmount = promise.FulfillmentAmount,
                    FulfillmentDate = promise.FulfillmentDate,
                    Notes = promise.Notes,
                    CreatedByName = promise.CreatedByName,
                    CreatedAt = promise.CreatedAt,
                    UpdatedAt = promise.UpdatedAt
                };

                return Ok(new ApiResponseDto<PromiseDto>
                {
                    Success = true,
                    Message = $"Promise marked as {updateDto.Status}",
                    Data = promiseDto
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating promise status: {ex.Message}");
                return StatusCode(500, new ApiResponseDto
                {
                    Success = false,
                    Message = $"Internal server error: {ex.Message}"
                });
            }
        }

        [HttpGet("statistics/officer/{officerId}")]
        [Authorize(Roles = "admin,supervisor")]
        public async Task<ActionResult<ApiResponseDto<object>>> GetOfficerStatistics(int officerId)
        {
            try
            {
                var userInfo = GetCurrentUserInfo();
                
                var promises = await _context.Promises
                    .Where(p => p.CreatedByUserId == officerId)
                    .ToListAsync();

                var statistics = new
                {
                    OfficerId = officerId,
                    OfficerName = promises.FirstOrDefault()?.CreatedByName ?? "Unknown",
                    TotalPromises = promises.Count,
                    FulfilledPromises = promises.Count(p => p.Status == "FULFILLED"),
                    BrokenPromises = promises.Count(p => p.Status == "BROKEN"),
                    PendingPromises = promises.Count(p => p.Status == "PENDING"),
                    FulfillmentRate = promises.Count > 0 
                        ? (decimal)promises.Count(p => p.Status == "FULFILLED") / promises.Count * 100m 
                        : 0m,
                    TotalAmount = promises.Sum(p => p.PromiseAmount),
                    FulfilledAmount = promises.Where(p => p.Status == "FULFILLED").Sum(p => p.FulfillmentAmount ?? 0)
                };

                await _activityService.LogActivityAsync(userInfo.userId, "OFFICER_STATS_VIEW",
                    $"Viewed statistics for officer {officerId}");

                return Ok(new ApiResponseDto<object>
                {
                    Success = true,
                    Message = "Officer statistics retrieved successfully",
                    Data = statistics
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting officer statistics: {ex.Message}");
                return StatusCode(500, new ApiResponseDto
                {
                    Success = false,
                    Message = $"Internal server error: {ex.Message}"
                });
            }
        }

        [HttpGet("reminders/due")]
        public async Task<ActionResult<ApiResponseDto<List<PromiseDto>>>> GetDueReminders()
        {
            try
            {
                var userInfo = GetCurrentUserInfo();
                
                var today = DateTime.UtcNow.Date;
                var tomorrow = today.AddDays(1);
                
                var duePromises = await _context.Promises
                    .Where(p => p.Status == "PENDING" 
                        && p.PromiseDate.Date <= tomorrow
                        && (p.ReminderSent == false || p.ReminderSent == null))
                    .Take(50)
                    .ToListAsync();

                var promiseDtos = duePromises.Select(p => new PromiseDto
                {
                    Id = p.Id,
                    PromiseId = p.PromiseId ?? string.Empty,
                    CustomerId = p.CustomerId,
                    CustomerName = p.CustomerName ?? "Unknown Customer",
                    PhoneNumber = p.PhoneNumber ?? "",
                    PromiseAmount = p.PromiseAmount,
                    PromiseDate = p.PromiseDate,
                    PromiseType = p.PromiseType,
                    Status = p.Status ?? "PENDING",
                    FulfillmentAmount = p.FulfillmentAmount,
                    FulfillmentDate = p.FulfillmentDate,
                    Notes = p.Notes,
                    CreatedByName = p.CreatedByName ?? "System",
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt
                }).ToList();

                return Ok(new ApiResponseDto<List<PromiseDto>>
                {
                    Success = true,
                    Message = "Due reminders retrieved successfully",
                    Data = promiseDtos
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting due reminders: {ex.Message}");
                return StatusCode(500, new ApiResponseDto
                {
                    Success = false,
                    Message = $"Internal server error: {ex.Message}"
                });
            }
        }

        [HttpPost("{id}/mark-reminder-sent")]
        public async Task<ActionResult<ApiResponseDto>> MarkReminderSent(int id)
        {
            try
            {
                var userInfo = GetCurrentUserInfo();
                
                var promise = await _context.Promises.FindAsync(id);
                if (promise == null)
                {
                    return NotFound(new ApiResponseDto
                    {
                        Success = false,
                        Message = "Promise not found"
                    });
                }

                promise.ReminderSent = true;
                promise.FollowUpCount = (promise.FollowUpCount ?? 0) + 1;
                await _context.SaveChangesAsync();

                await _activityService.LogActivityAsync(userInfo.userId, "REMINDER_SENT",
                    $"Marked reminder sent for promise {promise.PromiseId}");

                return Ok(new ApiResponseDto
                {
                    Success = true,
                    Message = "Reminder marked as sent"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error marking reminder sent: {ex.Message}");
                return StatusCode(500, new ApiResponseDto
                {
                    Success = false,
                    Message = $"Internal server error: {ex.Message}"
                });
            }
        }
    }
}