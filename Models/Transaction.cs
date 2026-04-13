// Models/Transaction.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RekovaBE_CSharp.Models
{
    [Table("transactions")]
    public class Transaction
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("transaction_internal_id")]
        [StringLength(100)]
        public string? TransactionInternalId { get; set; }

        [Column("transaction_id")]
        [StringLength(100)]
        public string? TransactionId { get; set; }

        [Column("customer_id")]
        public int? CustomerId { get; set; }

        [Column("customer_internal_id")]
        [StringLength(100)]
        public string? CustomerInternalId { get; set; }

        [Column("phone_number")]
        [StringLength(50)]
        public string? PhoneNumber { get; set; }

        [Column("amount")]
        public decimal Amount { get; set; }

        [Column("description")]
        public string? Description { get; set; }

        [Column("status")]
        [StringLength(50)]
        public string? Status { get; set; }

        [Column("loan_balance_before")]
        public decimal? LoanBalanceBefore { get; set; }

        [Column("loan_balance_after")]
        public decimal? LoanBalanceAfter { get; set; }

        [Column("arrears_before")]
        public decimal? ArrearsBefore { get; set; }

        [Column("arrears_after")]
        public decimal? ArrearsAfter { get; set; }

        [Column("payment_method")]
        [StringLength(50)]
        public string? PaymentMethod { get; set; }

        [Column("mpesa_receipt_number")]
        [StringLength(100)]
        public string? MpesaReceiptNumber { get; set; }

        [Column("pin_attempts")]
        public int? PinAttempts { get; set; }

        [Column("error_message")]
        public string? ErrorMessage { get; set; }

        [Column("failure_reason")]
        [StringLength(50)]
        public string? FailureReason { get; set; }

        [Column("stk_push_sent_at")]
        public DateTime? StkPushSentAt { get; set; }

        [Column("processed_at")]
        public DateTime? ProcessedAt { get; set; }

        [Column("initiated_by")]
        [StringLength(100)]
        public string? InitiatedBy { get; set; }

        [Column("initiated_by_user_id")]
        public int? InitiatedByUserId { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }

        [ForeignKey("CustomerId")]
        public virtual Customer? Customer { get; set; }

        [ForeignKey("InitiatedByUserId")]
        public virtual User? InitiatedByUser { get; set; }
    }
}