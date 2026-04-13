// Models/Promise.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RekovaBE_CSharp.Models
{
    [Table("promises")]
    public class Promise
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("promise_id")]
        [StringLength(100)]
        public string? PromiseId { get; set; }

        [Column("customer_id")]
        public int CustomerId { get; set; }

        [Column("customer_name")]
        [StringLength(200)]
        public string? CustomerName { get; set; }

        [Column("phone_number")]
        [StringLength(50)]
        public string? PhoneNumber { get; set; }

        [Column("promise_amount")]
        public decimal PromiseAmount { get; set; }

        [Column("promise_date")]
        public DateTime PromiseDate { get; set; }

        [Column("promise_type")]
        [StringLength(50)]
        public string? PromiseType { get; set; }

        [Column("status")]
        [StringLength(50)]
        public string? Status { get; set; }

        [Column("fulfillment_amount")]
        public decimal? FulfillmentAmount { get; set; }

        [Column("fulfillment_date")]
        public DateTime? FulfillmentDate { get; set; }

        [Column("notes")]
        public string? Notes { get; set; }

        [Column("created_by_user_id")]
        public int? CreatedByUserId { get; set; }

        [Column("created_by_name")]
        [StringLength(200)]
        public string? CreatedByName { get; set; }

        [Column("reminder_sent")]
        public bool? ReminderSent { get; set; }

        [Column("next_follow_up_date")]
        public DateTime? NextFollowUpDate { get; set; }

        [Column("follow_up_count")]
        public int? FollowUpCount { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }

        // Navigation properties
        [ForeignKey("CustomerId")]
        public virtual Customer? Customer { get; set; }

        [ForeignKey("CreatedByUserId")]
        public virtual User? CreatedByUser { get; set; }
    }
}