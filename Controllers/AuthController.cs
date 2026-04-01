using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RekovaBE_CSharp.Data;
using RekovaBE_CSharp.Helpers;
using RekovaBE_CSharp.Models;
using RekovaBE_CSharp.Models.DTOs;
using RekovaBE_CSharp.Services;
using System.Security.Claims;

namespace RekovaBE_CSharp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuthService _authService;
        private readonly IActivityService _activityService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            ApplicationDbContext context,
            IAuthService authService,
            IActivityService activityService,
            ILogger<AuthController> logger)
        {
            _context = context;
            _authService = authService;
            _activityService = activityService;
            _logger = logger;
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponseDto<LoginResponseDto>>> Login([FromBody] LoginRequestDto request)
        {
            try
            {
                _logger.LogInformation($"Login attempt for username: {request.Username}");

                if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
                {
                    return BadRequest(new ApiResponseDto
                    {
                        Success = false,
                        Message = "Username and password are required"
                    });
                }

                // Find user by username
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Username == request.Username);

                if (user == null)
                {
                    _logger.LogWarning($"Login failed: User not found - {request.Username}");
                    return Unauthorized(new ApiResponseDto
                    {
                        Success = false,
                        Message = "Invalid username or password"
                    });
                }

                _logger.LogInformation($"User found: {user.Username}, Role: {user.Role}, IsActive: {user.IsActive}");

                // Check if user is active - FIXED: Handle nullable bool
                if (user.IsActive != true)
                {
                    _logger.LogWarning($"Login failed: User account is not active - {request.Username}");
                    return Unauthorized(new ApiResponseDto
                    {
                        Success = false,
                        Message = "Invalid username or password"
                    });
                }

                // Verify password
                bool passwordValid = PasswordHasher.VerifyPassword(request.Password, user.PasswordHash);
                _logger.LogInformation($"Password verification result: {passwordValid}");

                if (!passwordValid)
                {
                    _logger.LogWarning($"Login failed: Invalid password for user {request.Username}");
                    return Unauthorized(new ApiResponseDto
                    {
                        Success = false,
                        Message = "Invalid username or password"
                    });
                }

                // Generate JWT token
                var token = _authService.GenerateJwtToken(user);
                _logger.LogInformation($"Token generated successfully for user {user.Username}");

                // Update last login
                user.LastLogin = DateTime.UtcNow;
                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                // Log successful login
                await _activityService.LogActivityAsync(user.Id, "LOGIN", "User logged in successfully");

                var response = new LoginResponseDto
                {
                    Id = user.Id,
                    Username = user.Username,
                    Email = user.Email,
                    Role = user.Role ?? "officer",
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Phone = user.Phone,
                    Token = token
                };

                return Ok(new ApiResponseDto<LoginResponseDto>
                {
                    Success = true,
                    Message = "Login successful",
                    Data = response
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Login error: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                return StatusCode(500, new ApiResponseDto
                {
                    Success = false,
                    Message = "Internal server error"
                });
            }
        }

        [HttpGet("me")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<ActionResult<ApiResponseDto<UserDto>>> GetCurrentUser()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (!int.TryParse(userId, out var id))
                {
                    return Unauthorized(new ApiResponseDto
                    {
                        Success = false,
                        Message = "Invalid token"
                    });
                }

                var user = await _context.Users
                    .Include(u => u.AssignedCustomers)
                    .FirstOrDefaultAsync(u => u.Id == id);

                if (user == null)
                {
                    return NotFound(new ApiResponseDto
                    {
                        Success = false,
                        Message = "User not found"
                    });
                }

                var userDto = new UserDto
                {
                    Id = user.Id,
                    Username = user.Username,
                    Email = user.Email,
                    Role = user.Role ?? "officer",
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Phone = user.Phone,
                    Department = user.Department,
                    AssignedCustomersCount = user.AssignedCustomers?.Count ?? 0,
                    IsActive = user.IsActive == true,
                    LastLogin = user.LastLogin
                };

                return Ok(new ApiResponseDto<UserDto>
                {
                    Success = true,
                    Message = "User retrieved successfully",
                    Data = userDto
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting current user: {ex.Message}");
                return StatusCode(500, new ApiResponseDto
                {
                    Success = false,
                    Message = "Internal server error"
                });
            }
        }

        [HttpPost("logout")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<ActionResult<ApiResponseDto>> Logout()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (int.TryParse(userId, out var id))
                {
                    await _activityService.LogActivityAsync(id, "LOGOUT", "User logged out");
                }

                return Ok(new ApiResponseDto
                {
                    Success = true,
                    Message = "Logged out successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Logout error: {ex.Message}");
                return StatusCode(500, new ApiResponseDto
                {
                    Success = false,
                    Message = "Internal server error"
                });
            }
        }

        [HttpPut("change-password")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<ActionResult<ApiResponseDto>> ChangePassword([FromBody] ChangePasswordDto request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (!int.TryParse(userId, out var id))
                {
                    return Unauthorized(new ApiResponseDto
                    {
                        Success = false,
                        Message = "Invalid token"
                    });
                }

                var user = await _context.Users.FindAsync(id);

                if (user == null)
                {
                    return NotFound(new ApiResponseDto
                    {
                        Success = false,
                        Message = "User not found"
                    });
                }

                // FIXED: Handle nullable IsActive properly
                if (user.IsActive != true)
                {
                    return BadRequest(new ApiResponseDto
                    {
                        Success = false,
                        Message = "Account is deactivated"
                    });
                }

                if (!PasswordHasher.VerifyPassword(request.CurrentPassword, user.PasswordHash))
                {
                    return BadRequest(new ApiResponseDto
                    {
                        Success = false,
                        Message = "Current password is incorrect"
                    });
                }

                user.PasswordHash = PasswordHasher.HashPassword(request.NewPassword);
                user.UpdatedAt = DateTime.UtcNow;
                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                await _activityService.LogActivityAsync(id, "PASSWORD_CHANGE", "User changed password");

                return Ok(new ApiResponseDto
                {
                    Success = true,
                    Message = "Password changed successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Change password error: {ex.Message}");
                return StatusCode(500, new ApiResponseDto
                {
                    Success = false,
                    Message = "Internal server error"
                });
            }
        }

        // ==================== USER MANAGEMENT ENDPOINTS ====================

        [HttpPost("register")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<ActionResult<ApiResponseDto<UserDto>>> RegisterUser([FromBody] CreateUserDto request)
        {
            try
            {
                var requestingUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                var requestingUserRole = User.FindFirst(ClaimTypes.Role)?.Value ?? "officer";

                // Only admins can create users
                if (requestingUserRole != "admin")
                {
                    return Forbid();
                }

                // Validation
                if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
                {
                    return BadRequest(new ApiResponseDto
                    {
                        Success = false,
                        Message = "Username and password are required"
                    });
                }

                // Check if user exists
                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Username == request.Username || u.Email == request.Email);

                if (existingUser != null)
                {
                    return BadRequest(new ApiResponseDto
                    {
                        Success = false,
                        Message = "User with this username or email already exists"
                    });
                }

                var user = new User
                {
                    Username = request.Username,
                    Email = request.Email?.ToLower() ?? request.Username + "@rekova.local",
                    PasswordHash = PasswordHasher.HashPassword(request.Password),
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    Phone = request.Phone,
                    Role = request.Role ?? "officer",
                    Department = request.Department ?? "Collections",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                await _activityService.LogActivityAsync(requestingUserId, "USER_CREATE",
                    $"Created new user: {user.Username} with role {user.Role}");

                var userDto = new UserDto
                {
                    Id = user.Id,
                    Username = user.Username,
                    Email = user.Email,
                    Role = user.Role ?? "officer",
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Phone = user.Phone,
                    Department = user.Department,
                    IsActive = user.IsActive == true,
                    LastLogin = user.LastLogin
                };

                return Ok(new ApiResponseDto<UserDto>
                {
                    Success = true,
                    Message = "User created successfully",
                    Data = userDto
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating user: {ex.Message}");
                return StatusCode(500, new ApiResponseDto
                {
                    Success = false,
                    Message = "Internal server error"
                });
            }
        }

        [HttpGet("users")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<ActionResult<ApiResponseDto<List<UserDto>>>> GetAllUsers()
        {
            try
            {
                var requestingUserRole = User.FindFirst(ClaimTypes.Role)?.Value ?? "officer";

                // Only admins can view all users
                if (requestingUserRole != "admin")
                {
                    return Forbid();
                }

                var users = await _context.Users
                    .OrderBy(u => u.Username)
                    .ToListAsync();

                var userDtos = users.Select(u => new UserDto
                {
                    Id = u.Id,
                    Username = u.Username,
                    Email = u.Email,
                    Role = u.Role ?? "officer",
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    Phone = u.Phone,
                    Department = u.Department,
                    IsActive = u.IsActive == true,
                    LastLogin = u.LastLogin
                }).ToList();

                return Ok(new ApiResponseDto<List<UserDto>>
                {
                    Success = true,
                    Message = $"Retrieved {userDtos.Count} users",
                    Data = userDtos
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting users: {ex.Message}");
                return StatusCode(500, new ApiResponseDto
                {
                    Success = false,
                    Message = "Internal server error"
                });
            }
        }

        [HttpPut("users/{id}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<ActionResult<ApiResponseDto<UserDto>>> UpdateUser(int id, [FromBody] UpdateUserDto request)
        {
            try
            {
                var requestingUserRole = User.FindFirst(ClaimTypes.Role)?.Value ?? "officer";
                var requestingUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

                // Only admins or the user themselves can update
                if (requestingUserRole != "admin" && requestingUserId != id)
                {
                    return Forbid();
                }

                var user = await _context.Users.FindAsync(id);
                if (user == null)
                {
                    return NotFound(new ApiResponseDto
                    {
                        Success = false,
                        Message = "User not found"
                    });
                }

                // Only admin can change role and status
                if (requestingUserRole == "admin")
                {
                    if (!string.IsNullOrEmpty(request.Role))
                        user.Role = request.Role;
                    if (request.IsActive.HasValue)
                        user.IsActive = request.IsActive;
                }

                // Anyone can update their own basic info
                if (!string.IsNullOrEmpty(request.FirstName))
                    user.FirstName = request.FirstName;
                if (!string.IsNullOrEmpty(request.LastName))
                    user.LastName = request.LastName;
                if (!string.IsNullOrEmpty(request.Phone))
                    user.Phone = request.Phone;

                user.UpdatedAt = DateTime.UtcNow;
                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                await _activityService.LogActivityAsync(requestingUserId, "USER_UPDATE",
                    $"Updated user: {user.Username}");

                var userDto = new UserDto
                {
                    Id = user.Id,
                    Username = user.Username,
                    Email = user.Email,
                    Role = user.Role ?? "officer",
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Phone = user.Phone,
                    Department = user.Department,
                    IsActive = user.IsActive == true,
                    LastLogin = user.LastLogin
                };

                return Ok(new ApiResponseDto<UserDto>
                {
                    Success = true,
                    Message = "User updated successfully",
                    Data = userDto
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating user: {ex.Message}");
                return StatusCode(500, new ApiResponseDto
                {
                    Success = false,
                    Message = "Internal server error"
                });
            }
        }

        [HttpDelete("users/{id}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<ActionResult<ApiResponseDto>> DeleteUser(int id)
        {
            try
            {
                var requestingUserRole = User.FindFirst(ClaimTypes.Role)?.Value ?? "officer";
                var requestingUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

                // Only admins can delete users
                if (requestingUserRole != "admin")
                {
                    return Forbid();
                }

                // Prevent self-deletion
                if (requestingUserId == id)
                {
                    return BadRequest(new ApiResponseDto
                    {
                        Success = false,
                        Message = "Cannot delete your own account"
                    });
                }

                var user = await _context.Users.FindAsync(id);
                if (user == null)
                {
                    return NotFound(new ApiResponseDto
                    {
                        Success = false,
                        Message = "User not found"
                    });
                }

                _context.Users.Remove(user);
                await _context.SaveChangesAsync();

                await _activityService.LogActivityAsync(requestingUserId, "USER_DELETE",
                    $"Deleted user: {user.Username}");

                return Ok(new ApiResponseDto
                {
                    Success = true,
                    Message = "User deleted successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting user: {ex.Message}");
                return StatusCode(500, new ApiResponseDto
                {
                    Success = false,
                    Message = "Internal server error"
                });
            }
        }

        [HttpGet("roles")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public ActionResult<ApiResponseDto<object>> GetRoles()
        {
            var roles = new
            {
                roles = new[]
                {
                    new {
                        name = "admin",
                        description = "Full system administrator with all privileges",
                        permissions = new[] {
                            "Manage all users",
                            "Approve all transactions",
                            "View all performance data",
                            "Export any data",
                            "Manage system settings"
                        }
                    },
                    new {
                        name = "supervisor",
                        description = "Team leader with transaction approval authority",
                        permissions = new[] {
                            "Approve large transactions",
                            "View all team performance",
                            "Export team data",
                            "Manage officers"
                        }
                    },
                    new {
                        name = "officer",
                        description = "Collections officer with standard privileges",
                        permissions = new[] {
                            "Create transactions",
                            "Create and manage promises",
                            "Add customer comments",
                            "View own performance"
                        }
                    }
                }
            };

            return Ok(new ApiResponseDto<object>
            {
                Success = true,
                Message = "Roles retrieved successfully",
                Data = roles
            });
        }

        [HttpGet("leaderboard")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<ActionResult<ApiResponseDto<List<object>>>> GetLeaderboard()
        {
            try
            {
                var leaderboard = await _context.Users
                    .Where(u => u.IsActive == true && u.Role == "officer")
                    .OrderByDescending(u => u.Id) // Can be replaced with performance metric
                    .Take(50)
                    .Select(u => new {
                        UserId = u.Id,
                        Username = u.Username,
                        FullName = $"{u.FirstName} {u.LastName}".Trim(),
                        Department = u.Department,
                        Score = 0 // Placeholder - can be calculated from transactions
                    })
                    .ToListAsync();

                return Ok(new ApiResponseDto<List<object>>
                {
                    Success = true,
                    Message = "Leaderboard retrieved successfully",
                    Data = leaderboard.Cast<object>().ToList()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting leaderboard: {ex.Message}");
                return StatusCode(500, new ApiResponseDto
                {
                    Success = false,
                    Message = "Internal server error"
                });
            }
        }
    }
}