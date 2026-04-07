using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RekovaBE_CSharp.Models
{
    [Table("users")]
    public class User
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("username")]
        [StringLength(100)]
        public string? Username { get; set; }

        [Column("email")]
        [StringLength(200)]
        public string? Email { get; set; }

        [Column("password_hash")]
        [StringLength(255)]
        public string? PasswordHash { get; set; }

        [Column("first_name")]
        [StringLength(100)]
        public string? FirstName { get; set; }

        [Column("last_name")]
        [StringLength(100)]
        public string? LastName { get; set; }

        [Column("phone")]
        [StringLength(50)]
        public string? Phone { get; set; }

        [Column("role")]
        [StringLength(50)]
        public string? Role { get; set; }

        [Column("loan_type")]
        [StringLength(50)]
        public string? LoanType { get; set; }

        [Column("department")]
        [StringLength(100)]
        public string? Department { get; set; }

        [Column("is_active")]
        public bool? IsActive { get; set; }

        [Column("last_login")]
        public DateTime? LastLogin { get; set; }

        [Column("created_by")]
        [StringLength(100)]
        public string? CreatedBy { get; set; }

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        [InverseProperty("AssignedToUser")]
        public virtual ICollection<Customer>? AssignedCustomers { get; set; }

        [InverseProperty("InitiatedByUser")]
        public virtual ICollection<Transaction>? Transactions { get; set; }

        [InverseProperty("CreatedByUser")]
        public virtual ICollection<Promise>? CreatedPromises { get; set; }

        [InverseProperty("User")]
        public virtual ICollection<Activity>? Activities { get; set; }

        [InverseProperty("User")]
        public virtual ICollection<Comment>? Comments { get; set; }
    }
}