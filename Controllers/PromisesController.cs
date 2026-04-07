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
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out var id) ? id : 0;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponseDto<List<PromiseDto>>>> GetPromises(
            [FromQuery] string? status = null,
            [FromQuery] int limit = 50)
        {
            try
            {
                var userId = GetCurrentUserId();
                
                if (userId == 0)
                {
                    return Unauthorized(new ApiResponseDto
                    {
                        Success = false,
                        Message = "Invalid authentication token"
                    });
                }
                
                // Build query without any JOINs to avoid column issues
                var query = _context.Promises.AsQueryable();

                if (!string.IsNullOrWhiteSpace(status))
                {
                    query = query.Where(p => p.Status == status.ToUpper());
                }

                var promises = await query
                    .OrderByDescending(p => p.PromiseDate)
                    .Take(limit)
                    .ToListAsync();

                // Map to DTOs directly without loading Customer navigation property
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
        var userId = GetCurrentUserId();

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

        await _activityService.LogActivityAsync(userId, "CUSTOMER_PROMISES_VIEW",
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

        [HttpGet("my-promises")]
        public async Task<ActionResult<ApiResponseDto<List<PromiseDto>>>> GetMyPromises(
            [FromQuery] string? status = null,
            [FromQuery] int limit = 50)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == 0)
                {
                    return Unauthorized(new ApiResponseDto
                    {
                        Success = false,
                        Message = "Invalid authentication token"
                    });
                }

                var query = _context.Promises
                    .Where(p => p.CreatedByUserId == userId);

                if (!string.IsNullOrWhiteSpace(status))
                {
                    query = query.Where(p => p.Status == status.ToUpper());
                }

                var promises = await query
                    .OrderByDescending(p => p.PromiseDate)
                    .Take(limit)
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
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                return StatusCode(500, new ApiResponseDto
                {
                    Success = false,
                    Message = $"Internal server error: {ex.Message}"
                });
            }
        }
    }
}