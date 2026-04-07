// Models/DTOs/CustomerDtos.cs
namespace RekovaBE_CSharp.Models.DTOs
{
      public class CustomerDto
    {
        public int Id { get; set; }
        public string CustomerInternalId { get; set; } = string.Empty;
        public string CustomerId { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? AccountNumber { get; set; }
        public decimal LoanBalance { get; set; }
        public decimal Arrears { get; set; }
        public decimal TotalRepayments { get; set; }
        public string? Email { get; set; }
        public string? NationalId { get; set; }
        public DateTime? LastPaymentDate { get; set; }
        public DateTime? LastContactDate { get; set; }
        public string? Status { get; set; }
        public bool IsActive { get; set; }
        public int? AssignedToUserId { get; set; }
        public string? AssignedToUserName { get; set; }  // ADDED: This was missing
        public string? LoanType { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class CreateCustomerDto
    {
        public string CustomerInternalId { get; set; } = string.Empty;
        public string CustomerId { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? AccountNumber { get; set; }
        public decimal LoanBalance { get; set; }
        public decimal Arrears { get; set; }
        public string? Email { get; set; }
        public string? NationalId { get; set; }
        public string? LoanType { get; set; }
    }

    public class UpdateCustomerDto
    {
        public string? Name { get; set; }
        public string? PhoneNumber { get; set; }
        public string? AccountNumber { get; set; }
        public decimal? LoanBalance { get; set; }
        public decimal? Arrears { get; set; }
        public string? Email { get; set; }
        public string? Status { get; set; }
        public int? AssignedToUserId { get; set; }
        public bool? IsActive { get; set; }
    }
}