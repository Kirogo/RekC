// Models/DTOs/TransactionDtos.cs
namespace RekovaBE_CSharp.Models.DTOs
{
    public class TransactionDto
    {
        public int Id { get; set; }
        public string TransactionInternalId { get; set; } = string.Empty;
        public string TransactionId { get; set; } = string.Empty;
        public int CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public string PhoneNumber { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? PaymentMethod { get; set; }
        public string? MpesaReceiptNumber { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? InitiatedBy { get; set; }
    }

    public class InitiatePaymentDto
    {
        public int CustomerId { get; set; }
        public string PhoneNumber { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string? Description { get; set; }
    }
}