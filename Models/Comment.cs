using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RekovaBE_CSharp.Models
{
    [Table("comments")]
    public class Comment
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("customer_id")]
        public int CustomerId { get; set; }

        [Column("user_id")]
        public int? UserId { get; set; }

        [Column("comment_text")]  // FIXED: Property name matches column
        public string? CommentText { get; set; }

        [Column("comment_type")]
        [StringLength(50)]
        public string? CommentType { get; set; } = "GENERAL";

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }

        // Navigation properties
        [ForeignKey("CustomerId")]
        public virtual Customer? Customer { get; set; }

        [ForeignKey("UserId")]
        public virtual User? User { get; set; }
    }
}