//Models/DTOs/CommonDtos.cs
namespace RekovaBE_CSharp.Models.DTOs
{
    public class ApiResponseDto<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }
        
        // Helper methods for common responses
        public static ApiResponseDto<T> Ok(T data, string message = "Success")
        {
            return new ApiResponseDto<T>
            {
                Success = true,
                Message = message,
                Data = data
            };
        }

        public static ApiResponseDto<T> Fail(string message)
        {
            return new ApiResponseDto<T>
            {
                Success = false,
                Message = message,
                Data = default
            };
        }
    }

    public class ApiResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        
        public static ApiResponseDto Ok(string message = "Success")
        {
            return new ApiResponseDto
            {
                Success = true,
                Message = message
            };
        }

        public static ApiResponseDto Fail(string message)
        {
            return new ApiResponseDto
            {
                Success = false,
                Message = message
            };
        }
    }

    public class PaginationDto<T>
    {
        public List<T> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        
        // Helper to calculate total pages
        public void CalculateTotalPages()
        {
            TotalPages = (int)Math.Ceiling(TotalCount / (double)PageSize);
        }
    }

    public class DashboardStatsDto
    {
        public int TotalCustomers { get; set; }
        public decimal TotalLoanBalance { get; set; }
        public decimal TotalArrears { get; set; }
        public int ActiveLoans { get; set; }
        public decimal CollectionRate { get; set; }
        public OfficerStatsDto? OfficerStats { get; set; }
        // Calculated properties
        public decimal ArrearsPercentage => TotalLoanBalance > 0 
            ? Math.Round((TotalArrears / TotalLoanBalance) * 100, 2) 
            : 0;
    }

    public class OfficerStatsDto
    {
        public int AssignedCustomers { get; set; }
        public decimal TotalLoanBalance { get; set; }
        public decimal TotalArrears { get; set; }
        public int CollectionAttempts { get; set; }
        public int SuccessfulCollections { get; set; }
        public decimal SuccessRate { get; set; }
        public decimal TotalCollected { get; set; }
        
        // Calculated properties
        public decimal CollectionEfficiency => AssignedCustomers > 0 
            ? Math.Round((decimal)SuccessfulCollections / AssignedCustomers * 100, 2) 
            : 0;
            
        public decimal AverageCollection => SuccessfulCollections > 0 
            ? Math.Round(TotalCollected / SuccessfulCollections, 2) 
            : 0;
    }

    public class OfficerPerformanceDto
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public int AssignedCustomers { get; set; }
        public decimal CollectionRate { get; set; }
        public decimal TotalCollected { get; set; }
        public int TransactionCount { get; set; }
        public int PromisesFulfilled { get; set; }
        
        // Helper for full name
        public string FullName => $"{FirstName} {LastName}".Trim();
    }

    public class SupervisorDashboardDto
    {
        public int TotalOfficers { get; set; }
        public decimal TeamCollectionRate { get; set; }
        public int TotalCustomersAssigned { get; set; }
        public decimal TotalLoanBalance { get; set; }
        public decimal TotalArrears { get; set; }
        public List<OfficerPerformanceDto> OfficerPerformance { get; set; } = new();
        
        // Summary statistics
        public int TotalActiveOfficers => OfficerPerformance.Count(o => o.AssignedCustomers > 0);
        public decimal AverageOfficerPerformance => OfficerPerformance.Any() 
            ? Math.Round(OfficerPerformance.Average(o => o.CollectionRate), 2) 
            : 0;
        public decimal TotalTeamCollected => OfficerPerformance.Sum(o => o.TotalCollected);
        public int TotalTeamTransactions => OfficerPerformance.Sum(o => o.TransactionCount);
    }

    public class CommentDto
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public string Text { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class CreateCommentDto
    {
        public string Text { get; set; } = string.Empty;
    }
}