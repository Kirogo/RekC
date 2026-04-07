// Models/Customer.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RekovaBE_CSharp.Models
{
    [Table("customers")]
    public class Customer
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("customer_internal_id")]
        [StringLength(100)]
        public string? CustomerInternalId { get; set; }

        [Column("customer_id")]
        [StringLength(100)]
        public string? CustomerId { get; set; }

        [Column("phone_number")]
        [StringLength(20)]
        public string? PhoneNumber { get; set; }

        [Column("name")]
        [StringLength(255)]
        public string? Name { get; set; }

        [Column("account_number")]
        [StringLength(100)]
        public string? AccountNumber { get; set; }

        [Column("loan_balance")]
        public decimal LoanBalance { get; set; }

        [Column("arrears")]
        public decimal Arrears { get; set; }

        [Column("total_repayments")]
        public decimal TotalRepayments { get; set; }

        [Column("email")]
        [StringLength(100)]
        public string? Email { get; set; }

        [Column("national_id")]
        [StringLength(20)]
        public string? NationalId { get; set; }

        [Column("last_payment_date")]
        public DateTime? LastPaymentDate { get; set; }

        [Column("last_contact_date")]
        public DateTime? LastContactDate { get; set; }

        [Column("status")]
        [StringLength(50)]
        public string? Status { get; set; }

        [Column("loan_type")]
        [StringLength(100)]
        public string? LoanType { get; set; }

        [Column("assigned_to_user_id")]
        public int? AssignedToUserId { get; set; }

        [Column("created_by")]
        [StringLength(100)]
        public string? CreatedBy { get; set; }

        [Column("created_by_user_id")]
        public int? CreatedByUserId { get; set; }

        [Column("address")]
        public string? Address { get; set; }

        [Column("has_outstanding_promise")]
        public bool? HasOutstandingPromise { get; set; }

        [Column("last_promise_date")]
        public DateTime? LastPromiseDate { get; set; }

        [Column("promise_count")]
        public int? PromiseCount { get; set; }

        [Column("fulfilled_promise_count")]
        public int? FulfilledPromiseCount { get; set; }

        [Column("promise_fulfillment_rate")]  // FIXED: This is decimal? (nullable decimal)
        public decimal? PromiseFulfillmentRate { get; set; }

        [Column("is_active")]
        public bool? IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        [ForeignKey("AssignedToUserId")]
        public virtual User? AssignedToUser { get; set; }

        public virtual ICollection<Transaction>? Transactions { get; set; }
        public virtual ICollection<Promise>? Promises { get; set; }
        public virtual ICollection<Comment>? Comments { get; set; }
        public virtual ICollection<Activity>? Activities { get; set; }
    }
}