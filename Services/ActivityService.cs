using RekovaBE_CSharp.Data;
using RekovaBE_CSharp.Models;
using Microsoft.EntityFrameworkCore;

namespace RekovaBE_CSharp.Services
{
    public interface IActivityService
    {
        Task LogActivityAsync(int userId, string action, string description, 
            string? resourceType = null, int? resourceId = null, int? customerId = null);
        Task<List<Activity>> GetUserActivitiesAsync(int userId, int days = 30);
        Task<List<Activity>> GetAllActivitiesAsync(int page = 1, int pageSize = 50);
    }

    public class ActivityService : IActivityService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ActivityService> _logger;

        public ActivityService(ApplicationDbContext context, ILogger<ActivityService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task LogActivityAsync(int userId, string action, string description,
            string? resourceType = null, int? resourceId = null, int? customerId = null)
        {
            try
            {
                var activity = new Activity
                {
                    UserId = userId,
                    Action = action,
                    Description = description,
                    ResourceType = resourceType,
                    ResourceId = resourceId,
                    CustomerId = customerId,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Activities.Add(activity);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Activity logged: {action} by user {userId}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error logging activity: {ex.Message}");
                // Don't throw - activity logging should not break the main flow
            }
        }

        public async Task<List<Activity>> GetUserActivitiesAsync(int userId, int days = 30)
        {
            var startDate = DateTime.UtcNow.AddDays(-days);
            return await _context.Activities
                .Where(a => a.UserId == userId && a.CreatedAt >= startDate)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<Activity>> GetAllActivitiesAsync(int page = 1, int pageSize = 50)
        {
            return await _context.Activities
                .OrderByDescending(a => a.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }
    }
}