using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RekovaBE_CSharp.Data;
using RekovaBE_CSharp.Models.DTOs;
using RekovaBE_CSharp.Services;
using System.Security.Claims;

namespace RekovaBE_CSharp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class ActivitiesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IActivityService _activityService;
        private readonly ILogger<ActivitiesController> _logger;

        public ActivitiesController(
            ApplicationDbContext context,
            IActivityService activityService,
            ILogger<ActivitiesController> logger)
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
        [Authorize(Roles = "admin,supervisor")]
        public async Task<ActionResult<ApiResponseDto<List<Dictionary<string, object>>>>> GetActivities(
      [FromQuery] int days = 30,
      [FromQuery] int page = 1,
      [FromQuery] int pageSize = 50)
        {
            try
            {
                var activities = await _activityService.GetAllActivitiesAsync(page, pageSize);

                var activityDtos = activities.Select(a =>
                {
                    var dict = new Dictionary<string, object>();

                    dict["id"] = a.Id;
                    dict["userId"] = a.UserId ?? 0;  // FIXED: Handle nullable int
                    dict["action"] = a.Action ?? string.Empty;
                    dict["description"] = a.Description ?? string.Empty;
                    dict["resourceType"] = a.ResourceType ?? string.Empty;
                    dict["customerId"] = a.CustomerId ?? 0;  // FIXED: Handle nullable int
                    dict["createdAt"] = a.CreatedAt;

                    return dict;
                }).ToList();

                var userId = GetCurrentUserId();
                await _activityService.LogActivityAsync(userId, "ACTIVITIES_VIEW", "Viewed activity logs");

                return Ok(new ApiResponseDto<List<Dictionary<string, object>>>
                {
                    Success = true,
                    Message = "Activities retrieved successfully",
                    Data = activityDtos
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting activities: {ex.Message}");
                return StatusCode(500, new ApiResponseDto
                {
                    Success = false,
                    Message = "Internal server error"
                });
            }
        }
    }
}