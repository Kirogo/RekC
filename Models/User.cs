using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace RekovaBE_CSharp.Models
{
    [Table("users")]
    public class User
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("username")]
        [StringLength(50)]
        public string Username { get; set; } = string.Empty;

        [Column("email")]
        [StringLength(100)]
        public string? Email { get; set; }

        [Required]
        [Column("password_hash")]
        [StringLength(255)]
        [JsonIgnore]
        public string PasswordHash { get; set; } = string.Empty;

        [Column("first_name")]
        [StringLength(100)]
        public string? FirstName { get; set; }

        [Column("last_name")]
        [StringLength(100)]
        public string? LastName { get; set; }

        [Column("phone")]
        [StringLength(20)]
        public string? Phone { get; set; }

        [Column("role")]
        [StringLength(50)]
        public string? Role { get; set; } = "officer";

        [Column("loan_type")]
        [StringLength(50)]
        public string? LoanType { get; set; }

        [Column("department")]
        [StringLength(100)]
        public string? Department { get; set; } = "Collections";

        [Column("employee_id")]
        [StringLength(50)]
        public string? EmployeeId { get; set; }

        [Column("is_active")]
        public bool? IsActive { get; set; } = true;

        [Column("last_login")]
        public DateTime? LastLogin { get; set; }

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        [JsonIgnore]
        public virtual ICollection<Customer>? AssignedCustomers { get; set; }

        [JsonIgnore]
        public virtual ICollection<Transaction>? Transactions { get; set; }

        [JsonIgnore]
        public virtual ICollection<Promise>? CreatedPromises { get; set; }

        [JsonIgnore]
        public virtual ICollection<Activity>? Activities { get; set; }

        [JsonIgnore]
        public virtual ICollection<Comment>? Comments { get; set; }  // ADDED: Missing navigation property
    }
}