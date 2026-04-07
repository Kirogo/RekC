// Controllers/SupervisorController.cs
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RekovaBE_CSharp.Data;
using RekovaBE_CSharp.Models.DTOs;
using RekovaBE_CSharp.Services;
using System.Security.Claims;

namespace RekovaBE_CSharp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "admin,supervisor")]
    public class SupervisorController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IActivityService _activityService;
        private readonly ILogger<SupervisorController> _logger;

        public SupervisorController(
            ApplicationDbContext context,
            IActivityService activityService,
            ILogger<SupervisorController> logger)
        {
            _context = context;
            _activityService = activityService;
            _logger = logger;
        }

        private int GetCurrentUserId()
        {
            return int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        }

        [HttpGet("dashboard")]
        public async Task<ActionResult<ApiResponseDto<SupervisorDashboardDto>>> GetSupervisorDashboard()
        {
            try
            {
                var userId = GetCurrentUserId();

                // FIXED: N+1 Query Problem - Load all data in single queries
                var officers = await _context.Users
                    .Where(u => u.IsActive == true && u.Role == "officer")
                    .ToListAsync();

                var officerIds = officers.Select(o => o.Id).ToList();

                // Load all customers, transactions, and promises in single queries
                var allCustomers = await _context.Customers
                    .Where(c => c.IsActive == true)
                    .ToListAsync();

                var officerCustomers = await _context.Customers
                    .Where(c => c.IsActive == true && c.AssignedToUserId != null && officerIds.Contains(c.AssignedToUserId.Value))
                    .ToListAsync();

                var officerTransactions = await _context.Transactions
                    .Where(t => t.InitiatedByUserId.HasValue && officerIds.Contains(t.InitiatedByUserId.Value) && (t.Status == "SUCCESS" || t.Status == "COMPLETED"))
                    .ToListAsync();

                var officerPromises = await _context.Promises
                    .Where(p => p.CreatedByUserId.HasValue && officerIds.Contains(p.CreatedByUserId.Value) && p.Status == "FULFILLED")
                    .ToListAsync();

                var totalTeamCollections = officerTransactions.Sum(t => t.Amount);
                var totalTeamCustomers = allCustomers.Count;
                var totalTeamLoanBalance = allCustomers.Sum(c => c.LoanBalance);
                var totalTeamArrears = allCustomers.Sum(c => c.Arrears);

                // Process data in memory - no more database queries in the loop
                var officerPerformance = new List<OfficerPerformanceDto>();

                foreach (var officer in officers)
                {
                    var assignedCustomers = officerCustomers
                        .Where(c => c.AssignedToUserId == officer.Id)
                        .ToList();

                    var successfulTransactions = officerTransactions
                        .Where(t => t.InitiatedByUserId == officer.Id)
                        .ToList();

                    var fulfilledPromises = officerPromises
                        .Where(p => p.CreatedByUserId == officer.Id)
                        .ToList();

                    var officerTotalCollected = successfulTransactions.Sum(t => t.Amount);

                    var performance = new OfficerPerformanceDto
                    {
                        UserId = officer.Id,
                        Username = officer.Username,
                        FirstName = officer.FirstName,
                        LastName = officer.LastName,
                        AssignedCustomers = assignedCustomers.Count,
                        CollectionRate = assignedCustomers.Count > 0
                            ? Math.Round((decimal)successfulTransactions.Count / assignedCustomers.Count * 100, 2)
                            : 0,
                        TotalCollected = officerTotalCollected,
                        TransactionCount = successfulTransactions.Count,
                        PromisesFulfilled = fulfilledPromises.Count
                    };

                    officerPerformance.Add(performance);
                }

                var dashboard = new SupervisorDashboardDto
                {
                    TotalOfficers = officers.Count,
                    TotalCustomersAssigned = totalTeamCustomers,
                    TotalLoanBalance = totalTeamLoanBalance,
                    TotalArrears = totalTeamArrears,
                    TeamCollectionRate = totalTeamArrears > 0
                        ? Math.Round((totalTeamCollections / totalTeamArrears) * 100, 2)
                        : 0,
                    OfficerPerformance = officerPerformance.OrderByDescending(o => o.TotalCollected).ToList()
                };

                await _activityService.LogActivityAsync(userId, "SUPERVISOR_DASHBOARD_VIEW", "Viewed supervisor dashboard");

                return Ok(new ApiResponseDto<SupervisorDashboardDto>
                {
                    Success = true,
                    Message = "Supervisor dashboard retrieved successfully",
                    Data = dashboard
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting supervisor dashboard: {ex.Message}");
                return StatusCode(500, new ApiResponseDto
                {
                    Success = false,
                    Message = "Internal server error"
                });
            }
        }

        [HttpGet("officers/performance")]
        public async Task<ActionResult<ApiResponseDto<List<OfficerPerformanceDto>>>> GetOfficersPerformance()
        {
            try
            {
                var userId = GetCurrentUserId();

                // FIXED: Handle nullable IsActive
                var officers = await _context.Users
                    .Where(u => (u.IsActive == true) && u.Role == "officer")
                    .ToListAsync();

                var officerPerformance = new List<OfficerPerformanceDto>();

                foreach (var officer in officers)
                {
                    var assignedCustomers = await _context.Customers
                        .Where(c => c.AssignedToUserId == officer.Id && c.IsActive == true)
                        .ToListAsync();

                    var successfulTransactions = await _context.Transactions
                        .Where(t => t.InitiatedByUserId == officer.Id && t.Status == "SUCCESS")
                        .ToListAsync();

                    var performance = new OfficerPerformanceDto
                    {
                        UserId = officer.Id,
                        Username = officer.Username,
                        FirstName = officer.FirstName,
                        LastName = officer.LastName,
                        AssignedCustomers = assignedCustomers.Count,
                        CollectionRate = assignedCustomers.Count > 0 
                            ? Math.Round((decimal)successfulTransactions.Count / assignedCustomers.Count * 100, 2) 
                            : 0,
                        TotalCollected = successfulTransactions.Sum(t => t.Amount),
                        TransactionCount = successfulTransactions.Count,
                        PromisesFulfilled = await _context.Promises
                            .Where(p => p.CreatedByUserId == officer.Id && p.Status == "FULFILLED")
                            .CountAsync()
                    };

                    officerPerformance.Add(performance);
                }

                await _activityService.LogActivityAsync(userId, "OFFICER_PERFORMANCE_VIEW", "Viewed officer performance");

                return Ok(new ApiResponseDto<List<OfficerPerformanceDto>>
                {
                    Success = true,
                    Message = "Officer performance retrieved successfully",
                    Data = officerPerformance.OrderByDescending(o => o.TotalCollected).ToList()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting officer performance: {ex.Message}");
                return StatusCode(500, new ApiResponseDto
                {
                    Success = false,
                    Message = "Internal server error"
                });
            }
        }

        [HttpPost("assignments/bulk")]
        public async Task<ActionResult<ApiResponseDto>> BulkAssignCustomers([FromBody] Dictionary<int, List<int>> assignments)
        {
            try
            {
                var userId = GetCurrentUserId();

                foreach (var assignment in assignments)
                {
                    var officerId = assignment.Key;
                    var customerIds = assignment.Value;

                    var customers = await _context.Customers
                        .Where(c => customerIds.Contains(c.Id))
                        .ToListAsync();

                    foreach (var customer in customers)
                    {
                        customer.AssignedToUserId = officerId;
                        _context.Customers.Update(customer);

                        await _activityService.LogActivityAsync(userId, "CUSTOMER_ASSIGN",
                            $"Assigned customer {customer.Name} to officer {officerId}", "CUSTOMER", customer.Id);
                    }
                }

                await _context.SaveChangesAsync();

                return Ok(new ApiResponseDto
                {
                    Success = true,
                    Message = "Customers assigned successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error assigning customers: {ex.Message}");
                return StatusCode(500, new ApiResponseDto
                {
                    Success = false,
                    Message = "Internal server error"
                });
            }
        }
    }
}