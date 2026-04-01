using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RekovaBE_CSharp.Models
{
    [Table("activities")]
    public class Activity
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("user_id")]
        public int? UserId { get; set; }

        [Column("action")]
        [StringLength(100)]
        public string? Action { get; set; }

        [Column("description")]
        public string? Description { get; set; }

        [Column("resource_type")]
        [StringLength(50)]
        public string? ResourceType { get; set; }

        [Column("resource_id")]
        public int? ResourceId { get; set; }

        [Column("customer_id")]
        public int? CustomerId { get; set; }

        [Column("amount")]
        public decimal? Amount { get; set; }

        [Column("ip_address")]
        [StringLength(45)]
        public string? IpAddress { get; set; }

        [Column("user_agent")]
        public string? UserAgent { get; set; }

        [Column("payment_method")]
        [StringLength(50)]
        public string? PaymentMethod { get; set; }

        [Column("transaction_status")]
        [StringLength(50)]
        public string? TransactionStatus { get; set; }

        [Column("created_at")]  // FIXED: Matches database column name
        public DateTime CreatedAt { get; set; }

        // Navigation properties
        [ForeignKey("UserId")]
        public virtual User? User { get; set; }

        [ForeignKey("CustomerId")]
        public virtual Customer? Customer { get; set; }
    }
}