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
    public class CommentsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IActivityService _activityService;
        private readonly ILogger<CommentsController> _logger;

        public CommentsController(
            ApplicationDbContext context,
            IActivityService activityService,
            ILogger<CommentsController> logger)
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

        [HttpGet("customer/{customerId}")]
        public async Task<ActionResult<ApiResponseDto<List<CommentDto>>>> GetCustomerComments(int customerId)
        {
            try
            {
                var userId = GetCurrentUserId();

                var customer = await _context.Customers.FindAsync(customerId);
                if (customer == null)
                {
                    return NotFound(new ApiResponseDto
                    {
                        Success = false,
                        Message = "Customer not found"
                    });
                }

                var comments = await _context.Comments
                    .Include(c => c.User)
                    .Where(c => c.CustomerId == customerId)
                    .OrderByDescending(c => c.CreatedAt)
                    .Select(c => new CommentDto
                    {
                        Id = c.Id,
                        CustomerId = c.CustomerId,
                        Text = c.CommentText,
                        CreatedBy = c.User != null ? 
                            (c.User.FirstName + " " + c.User.LastName).Trim() : "System",
                        CreatedAt = c.CreatedAt,
                        UpdatedAt = c.UpdatedAt
                    })
                    .ToListAsync();

                await _activityService.LogActivityAsync(userId, "COMMENTS_VIEW",
                    $"Viewed comments for customer {customerId}");

                return Ok(new ApiResponseDto<List<CommentDto>>
                {
                    Success = true,
                    Message = $"Retrieved {comments.Count} comments",
                    Data = comments
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting comments: {ex.Message}");
                return StatusCode(500, new ApiResponseDto
                {
                    Success = false,
                    Message = "Internal server error"
                });
            }
        }

        [HttpPost("customer/{customerId}")]
        public async Task<ActionResult<ApiResponseDto<CommentDto>>> AddComment(int customerId, [FromBody] CreateCommentDto request)
        {
            try
            {
                var userId = GetCurrentUserId();

                var customer = await _context.Customers.FindAsync(customerId);
                if (customer == null)
                {
                    return NotFound(new ApiResponseDto
                    {
                        Success = false,
                        Message = "Customer not found"
                    });
                }

                if (string.IsNullOrWhiteSpace(request.Text))
                {
                    return BadRequest(new ApiResponseDto
                    {
                        Success = false,
                        Message = "Comment text is required"
                    });
                }

                var user = await _context.Users.FindAsync(userId);
                var authorName = user != null ? $"{user.FirstName} {user.LastName}".Trim() : "System";
                if (string.IsNullOrWhiteSpace(authorName)) authorName = user?.Username ?? "System";

                var comment = new Comment
                {
                    CustomerId = customerId,
                    CommentText = request.Text.Trim(),
                    UserId = userId,
                    Author = authorName,
                    CustomerName = customer.Name,
                    CommentType = "follow_up",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Comments.Add(comment);
                await _context.SaveChangesAsync();

                await _activityService.LogActivityAsync(userId, "COMMENT_CREATE",
                    $"Added comment to customer {customerId}");

                var commentDto = new CommentDto
                {
                    Id = comment.Id,
                    CustomerId = comment.CustomerId,
                    Text = comment.CommentText,
                    CreatedBy = authorName,
                    CreatedAt = comment.CreatedAt,
                    UpdatedAt = comment.UpdatedAt
                };

                return Ok(new ApiResponseDto<CommentDto>
                {
                    Success = true,
                    Message = "Comment added successfully",
                    Data = commentDto
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error adding comment: {ex.Message}");
                return StatusCode(500, new ApiResponseDto
                {
                    Success = false,
                    Message = "Internal server error"
                });
            }
        }
    }
}