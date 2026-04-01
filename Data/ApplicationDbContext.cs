using Microsoft.EntityFrameworkCore;
using RekovaBE_CSharp.Models;

namespace RekovaBE_CSharp.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<Promise> Promises { get; set; }
        public DbSet<Activity> Activities { get; set; }
        public DbSet<Comment> Comments { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ==================== USER CONFIGURATION ====================
            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("users");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
                entity.Property(e => e.Username).HasColumnName("username").HasMaxLength(50).IsRequired();
                entity.Property(e => e.Email).HasColumnName("email").HasMaxLength(100);
                entity.Property(e => e.PasswordHash).HasColumnName("password_hash").HasMaxLength(255).IsRequired();
                entity.Property(e => e.FirstName).HasColumnName("first_name").HasMaxLength(100);
                entity.Property(e => e.LastName).HasColumnName("last_name").HasMaxLength(100);
                entity.Property(e => e.Phone).HasColumnName("phone").HasMaxLength(20);
                entity.Property(e => e.Role).HasColumnName("role").HasMaxLength(50).HasDefaultValue("officer");
                entity.Property(e => e.LoanType).HasColumnName("loan_type").HasMaxLength(50);
                entity.Property(e => e.Department).HasColumnName("department").HasMaxLength(100).HasDefaultValue("Collections");
                entity.Property(e => e.EmployeeId).HasColumnName("employee_id").HasMaxLength(50);
                entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
                entity.Property(e => e.LastLogin).HasColumnName("last_login");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP");
                entity.HasMany(e => e.Comments)
                    .WithOne(c => c.User)
                    .HasForeignKey(c => c.UserId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(e => e.Username).IsUnique();
                entity.HasIndex(e => e.Email).IsUnique(); // ADDED: Email should be unique
                entity.HasIndex(e => e.Role);
                entity.HasIndex(e => e.EmployeeId).IsUnique(); // ADDED: EmployeeId should be unique
            });

            // ==================== CUSTOMER CONFIGURATION ====================
            // Customer configuration
            modelBuilder.Entity<Customer>(entity =>
            {
                entity.ToTable("customers");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
                entity.Property(e => e.CustomerInternalId).HasColumnName("customer_internal_id").HasMaxLength(100);
                entity.Property(e => e.CustomerId).HasColumnName("customer_id").HasMaxLength(100);
                entity.Property(e => e.PhoneNumber).HasColumnName("phone_number").HasMaxLength(20).IsRequired();
                entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
                entity.Property(e => e.AccountNumber).HasColumnName("account_number").HasMaxLength(100);
                entity.Property(e => e.LoanBalance).HasColumnName("loan_balance").HasPrecision(15, 2).HasDefaultValue(0);
                entity.Property(e => e.Arrears).HasColumnName("arrears").HasPrecision(15, 2).HasDefaultValue(0);
                entity.Property(e => e.TotalRepayments).HasColumnName("total_repayments").HasPrecision(15, 2).HasDefaultValue(0);
                entity.Property(e => e.Email).HasColumnName("email").HasMaxLength(100);
                entity.Property(e => e.NationalId).HasColumnName("national_id").HasMaxLength(20);
                entity.Property(e => e.LastPaymentDate).HasColumnName("last_payment_date");
                entity.Property(e => e.LastContactDate).HasColumnName("last_contact_date");
                entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(50).HasDefaultValue("ACTIVE");
                entity.Property(e => e.LoanType).HasColumnName("loan_type").HasMaxLength(100);
                entity.Property(e => e.AssignedToUserId).HasColumnName("assigned_to_user_id");
                entity.Property(e => e.CreatedBy).HasColumnName("created_by").HasMaxLength(100);
                entity.Property(e => e.CreatedByUserId).HasColumnName("created_by_user_id");
                entity.Property(e => e.Address).HasColumnName("address");
                entity.Property(e => e.HasOutstandingPromise).HasColumnName("has_outstanding_promise").HasDefaultValue(false);
                entity.Property(e => e.LastPromiseDate).HasColumnName("last_promise_date");
                entity.Property(e => e.PromiseCount).HasColumnName("promise_count").HasDefaultValue(0);
                entity.Property(e => e.FulfilledPromiseCount).HasColumnName("fulfilled_promise_count").HasDefaultValue(0);

                // FIXED: Use decimal literal (0m) for nullable decimal property
                entity.Property(e => e.PromiseFulfillmentRate)
                    .HasColumnName("promise_fulfillment_rate")
                    .HasPrecision(5, 2)
                    .HasDefaultValue(0m);  // Changed from 0 to 0m (decimal literal)

                entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
                entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP");

                // Indexes
                entity.HasIndex(e => e.PhoneNumber).IsUnique();
                entity.HasIndex(e => e.CustomerId).IsUnique();
                entity.HasIndex(e => e.CustomerInternalId).IsUnique();
                entity.HasIndex(e => e.AccountNumber).IsUnique();
                entity.HasIndex(e => e.NationalId).IsUnique();
                entity.HasIndex(e => e.AssignedToUserId);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.LoanType);
                entity.HasIndex(e => e.IsActive);

                // Relationships
                entity.HasOne(e => e.AssignedToUser)
                    .WithMany(u => u.AssignedCustomers)
                    .HasForeignKey(e => e.AssignedToUserId)
                    .OnDelete(DeleteBehavior.SetNull);
            });
            // ==================== TRANSACTION CONFIGURATION ====================
            modelBuilder.Entity<Transaction>(entity =>
            {
                entity.ToTable("transactions");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
                entity.Property(e => e.TransactionInternalId).HasColumnName("transaction_internal_id").HasMaxLength(100);
                entity.Property(e => e.TransactionId).HasColumnName("transaction_id").HasMaxLength(100);
                entity.Property(e => e.CustomerId).HasColumnName("customer_id");
                entity.Property(e => e.CustomerInternalId).HasColumnName("customer_internal_id").HasMaxLength(100);
                entity.Property(e => e.PhoneNumber).HasColumnName("phone_number").HasMaxLength(20);
                entity.Property(e => e.Amount).HasColumnName("amount").HasPrecision(15, 2).IsRequired();
                entity.Property(e => e.Description).HasColumnName("description");
                entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(50).HasDefaultValue("PENDING");
                entity.Property(e => e.LoanBalanceBefore).HasColumnName("loan_balance_before").HasPrecision(15, 2);
                entity.Property(e => e.LoanBalanceAfter).HasColumnName("loan_balance_after").HasPrecision(15, 2);
                entity.Property(e => e.ArrearsBefore).HasColumnName("arrears_before").HasPrecision(15, 2);
                entity.Property(e => e.ArrearsAfter).HasColumnName("arrears_after").HasPrecision(15, 2);
                entity.Property(e => e.PaymentMethod).HasColumnName("payment_method").HasMaxLength(50).HasDefaultValue("MPESA");
                entity.Property(e => e.MpesaReceiptNumber).HasColumnName("mpesa_receipt_number").HasMaxLength(100);
                entity.Property(e => e.ProcessedAt).HasColumnName("processed_at");
                entity.Property(e => e.InitiatedBy).HasColumnName("initiated_by").HasMaxLength(100);
                entity.Property(e => e.InitiatedByUserId).HasColumnName("initiated_by_user_id");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP");

                // Indexes
                entity.HasIndex(e => e.TransactionId).IsUnique();
                entity.HasIndex(e => e.TransactionInternalId).IsUnique();
                entity.HasIndex(e => e.CustomerId);
                entity.HasIndex(e => e.InitiatedByUserId);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.CreatedAt);
                entity.HasIndex(e => e.MpesaReceiptNumber);

                // Relationships
                entity.HasOne(e => e.Customer)
                    .WithMany(c => c.Transactions)
                    .HasForeignKey(e => e.CustomerId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.InitiatedByUser)
                    .WithMany(u => u.Transactions)
                    .HasForeignKey(e => e.InitiatedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // ==================== PROMISE CONFIGURATION ====================
            modelBuilder.Entity<Promise>(entity =>
            {
                entity.ToTable("promises");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
                entity.Property(e => e.PromiseId).HasColumnName("promise_id").HasMaxLength(100);
                entity.Property(e => e.CustomerId).HasColumnName("customer_id").IsRequired();
                entity.Property(e => e.CustomerName).HasColumnName("customer_name").HasMaxLength(255);
                entity.Property(e => e.PhoneNumber).HasColumnName("phone_number").HasMaxLength(20);
                entity.Property(e => e.PromiseAmount).HasColumnName("promise_amount").HasPrecision(15, 2).IsRequired();
                entity.Property(e => e.PromiseDate).HasColumnName("promise_date").IsRequired();
                entity.Property(e => e.PromiseType).HasColumnName("promise_type").HasMaxLength(50).HasDefaultValue("FULL_PAYMENT");
                entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(50).HasDefaultValue("PENDING");
                entity.Property(e => e.FulfillmentAmount).HasColumnName("fulfillment_amount").HasPrecision(15, 2);
                entity.Property(e => e.FulfillmentDate).HasColumnName("fulfillment_date");
                entity.Property(e => e.Notes).HasColumnName("notes");
                entity.Property(e => e.CreatedByUserId).HasColumnName("created_by_user_id");
                entity.Property(e => e.CreatedByName).HasColumnName("created_by_name").HasMaxLength(255);
                entity.Property(e => e.NextFollowUpDate).HasColumnName("next_follow_up_date");
                entity.Property(e => e.ReminderSent).HasColumnName("reminder_sent").HasDefaultValue(false);
                entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP");

                // Indexes
                entity.HasIndex(e => e.PromiseId).IsUnique();
                entity.HasIndex(e => e.CustomerId);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.PromiseDate);
                entity.HasIndex(e => e.NextFollowUpDate);

                // Relationships
                entity.HasOne(e => e.Customer)
                    .WithMany(c => c.Promises)
                    .HasForeignKey(e => e.CustomerId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.CreatedByUser)
                    .WithMany(u => u.CreatedPromises)
                    .HasForeignKey(e => e.CreatedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // ==================== ACTIVITY CONFIGURATION ====================
            modelBuilder.Entity<Activity>(entity =>
            {
                entity.ToTable("activities");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
                entity.Property(e => e.UserId).HasColumnName("user_id");
                entity.Property(e => e.Action).HasColumnName("action").HasMaxLength(100).IsRequired();
                entity.Property(e => e.Description).HasColumnName("description");
                entity.Property(e => e.ResourceType).HasColumnName("resource_type").HasMaxLength(50);
                entity.Property(e => e.ResourceId).HasColumnName("resource_id");
                entity.Property(e => e.CustomerId).HasColumnName("customer_id");
                entity.Property(e => e.Amount).HasColumnName("amount").HasPrecision(15, 2);
                entity.Property(e => e.IpAddress).HasColumnName("ip_address").HasMaxLength(45);
                entity.Property(e => e.UserAgent).HasColumnName("user_agent");
                entity.Property(e => e.PaymentMethod).HasColumnName("payment_method").HasMaxLength(50);
                entity.Property(e => e.TransactionStatus).HasColumnName("transaction_status").HasMaxLength(50);
                entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

                // Indexes
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.CustomerId);
                entity.HasIndex(e => e.Action);
                entity.HasIndex(e => e.CreatedAt);
                entity.HasIndex(e => e.ResourceType);
                entity.HasIndex(e => e.ResourceId);

                // Relationships
                entity.HasOne(e => e.User)
                    .WithMany(u => u.Activities)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.Customer)
                    .WithMany(c => c.Activities)
                    .HasForeignKey(e => e.CustomerId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // ==================== COMMENT CONFIGURATION ====================
            modelBuilder.Entity<Comment>(entity =>
            {
                entity.ToTable("comments");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
                entity.Property(e => e.CustomerId).HasColumnName("customer_id").IsRequired();
                entity.Property(e => e.UserId).HasColumnName("user_id");
                entity.Property(e => e.CommentText).HasColumnName("comment_text").IsRequired();
                entity.Property(e => e.CommentType).HasColumnName("comment_type").HasMaxLength(50).HasDefaultValue("GENERAL");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP");

                // Indexes
                entity.HasIndex(e => e.CustomerId);
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.CreatedAt);
                entity.HasIndex(e => e.CommentType);

                // Relationships
                entity.HasOne(e => e.Customer)
                    .WithMany(c => c.Comments)
                    .HasForeignKey(e => e.CustomerId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.User)
                    .WithMany(u => u.Comments)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.SetNull);
            });
        }
    }
}