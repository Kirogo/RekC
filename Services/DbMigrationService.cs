using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using Serilog;

namespace RekovaBE_CSharp.Services
{
    public interface IDbMigrationService
    {
        Task MigrateAsync();
    }

    public class DbMigrationService : IDbMigrationService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<DbMigrationService> _logger;

        public DbMigrationService(IConfiguration configuration, ILogger<DbMigrationService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task MigrateAsync()
        {
            try
            {
                var connectionString = BuildConnectionString();
                using (var connection = new MySqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    _logger.LogInformation("✓ Database connection established");

                    // ========== TRANSACTIONS TABLE ==========
                    await AddColumnIfNotExistsAsync(connection, "transactions", "transaction_internal_id",
                        "ALTER TABLE `transactions` ADD COLUMN `transaction_internal_id` VARCHAR(100) NULL");

                    await AddColumnIfNotExistsAsync(connection, "transactions", "customer_internal_id",
                        "ALTER TABLE `transactions` ADD COLUMN `customer_internal_id` VARCHAR(50) NULL");

                    await AddColumnIfNotExistsAsync(connection, "transactions", "loan_balance_before",
                        "ALTER TABLE `transactions` ADD COLUMN `loan_balance_before` DECIMAL(15,2) NULL");

                    await AddColumnIfNotExistsAsync(connection, "transactions", "loan_balance_after",
                        "ALTER TABLE `transactions` ADD COLUMN `loan_balance_after` DECIMAL(15,2) NULL");

                    await AddColumnIfNotExistsAsync(connection, "transactions", "arrears_before",
                        "ALTER TABLE `transactions` ADD COLUMN `arrears_before` DECIMAL(18,2) NULL");

                    await AddColumnIfNotExistsAsync(connection, "transactions", "arrears_after",
                        "ALTER TABLE `transactions` ADD COLUMN `arrears_after` DECIMAL(18,2) NULL");

                    await AddColumnIfNotExistsAsync(connection, "transactions", "description",
                        "ALTER TABLE `transactions` ADD COLUMN `description` LONGTEXT NULL");

                    await AddColumnIfNotExistsAsync(connection, "transactions", "payment_method",
                        "ALTER TABLE `transactions` ADD COLUMN `payment_method` VARCHAR(50) NULL");

                    await AddColumnIfNotExistsAsync(connection, "transactions", "mpesa_receipt_number",
                        "ALTER TABLE `transactions` ADD COLUMN `mpesa_receipt_number` VARCHAR(100) NULL");

                    await AddColumnIfNotExistsAsync(connection, "transactions", "processed_at",
                        "ALTER TABLE `transactions` ADD COLUMN `processed_at` DATETIME NULL");

                    await AddColumnIfNotExistsAsync(connection, "transactions", "initiated_by",
                        "ALTER TABLE `transactions` ADD COLUMN `initiated_by` VARCHAR(255) NULL");

                    await AddColumnIfNotExistsAsync(connection, "transactions", "initiated_by_user_id",
                        "ALTER TABLE `transactions` ADD COLUMN `initiated_by_user_id` INT NULL");

                    // ========== CUSTOMERS TABLE ==========
                    await AddColumnIfNotExistsAsync(connection, "customers", "customer_internal_id",
                        "ALTER TABLE `customers` ADD COLUMN `customer_internal_id` VARCHAR(50) NULL");

                    await AddColumnIfNotExistsAsync(connection, "customers", "account_number",
                        "ALTER TABLE `customers` ADD COLUMN `account_number` VARCHAR(50) NULL");

                    await AddColumnIfNotExistsAsync(connection, "customers", "assigned_to_user_id",
                        "ALTER TABLE `customers` ADD COLUMN `assigned_to_user_id` INT NULL");

                    await AddColumnIfNotExistsAsync(connection, "customers", "created_by_user_id",
                        "ALTER TABLE `customers` ADD COLUMN `created_by_user_id` INT NULL");

                    // ========== PROMISES TABLE ==========
                    await AddColumnIfNotExistsAsync(connection, "promises", "promise_id",
                        "ALTER TABLE `promises` ADD COLUMN `promise_id` VARCHAR(50) NULL");

                    await AddColumnIfNotExistsAsync(connection, "promises", "customer_id",
                        "ALTER TABLE `promises` ADD COLUMN `customer_id` INT NULL");

                    await AddColumnIfNotExistsAsync(connection, "promises", "customer_name",
                        "ALTER TABLE `promises` ADD COLUMN `customer_name` VARCHAR(255) NULL");

                    await AddColumnIfNotExistsAsync(connection, "promises", "phone_number",
                        "ALTER TABLE `promises` ADD COLUMN `phone_number` VARCHAR(20) NULL");

                    await AddColumnIfNotExistsAsync(connection, "promises", "promise_amount",
                        "ALTER TABLE `promises` ADD COLUMN `promise_amount` DECIMAL(15,2) NULL");

                    await AddColumnIfNotExistsAsync(connection, "promises", "promise_date",
                        "ALTER TABLE `promises` ADD COLUMN `promise_date` DATETIME NULL");

                    await AddColumnIfNotExistsAsync(connection, "promises", "promise_type",
                        "ALTER TABLE `promises` ADD COLUMN `promise_type` VARCHAR(50) NULL");

                    await AddColumnIfNotExistsAsync(connection, "promises", "status",
                        "ALTER TABLE `promises` ADD COLUMN `status` VARCHAR(50) NULL DEFAULT 'PENDING'");

                    await AddColumnIfNotExistsAsync(connection, "promises", "fulfillment_amount",
                        "ALTER TABLE `promises` ADD COLUMN `fulfillment_amount` DECIMAL(15,2) NULL");

                    await AddColumnIfNotExistsAsync(connection, "promises", "fulfillment_date",
                        "ALTER TABLE `promises` ADD COLUMN `fulfillment_date` DATETIME NULL");

                    await AddColumnIfNotExistsAsync(connection, "promises", "notes",
                        "ALTER TABLE `promises` ADD COLUMN `notes` VARCHAR(500) NULL");

                    await AddColumnIfNotExistsAsync(connection, "promises", "created_by_user_id",
                        "ALTER TABLE `promises` ADD COLUMN `created_by_user_id` INT NULL");

                    await AddColumnIfNotExistsAsync(connection, "promises", "created_by_name",
                        "ALTER TABLE `promises` ADD COLUMN `created_by_name` VARCHAR(255) NULL");

                    await AddColumnIfNotExistsAsync(connection, "promises", "reminder_sent",
                        "ALTER TABLE `promises` ADD COLUMN `reminder_sent` BOOLEAN NULL DEFAULT FALSE");

                    await AddColumnIfNotExistsAsync(connection, "promises", "next_follow_up_date",
                        "ALTER TABLE `promises` ADD COLUMN `next_follow_up_date` DATETIME NULL");

                    // ========== ACTIVITIES TABLE ==========
                    await AddColumnIfNotExistsAsync(connection, "activities", "description",
                        "ALTER TABLE `activities` ADD COLUMN `description` LONGTEXT NULL");

                    _logger.LogInformation("✓ All database schema migrations completed successfully");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠ Database migration warning (non-fatal): {Message}. The app will continue but some features may not work properly. Make sure DB_PASSWORD and other DB_* environment variables are set.", ex.Message);
                // Don't throw - allow app to continue even if migration fails
            }
        }

        private async Task AddColumnIfNotExistsAsync(MySqlConnection connection, string tableName, string columnName, string alterStatement)
        {
            try
            {
                // Check if column exists
                using (var cmd = new MySqlCommand(
                    $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '{tableName}' AND COLUMN_NAME = '{columnName}'",
                    connection))
                {
                    var result = (long?)await cmd.ExecuteScalarAsync() ?? 0;
                    
                    if (result == 0)
                    {
                        using (var addCmd = new MySqlCommand(alterStatement, connection))
                        {
                            await addCmd.ExecuteNonQueryAsync();
                            _logger.LogInformation($"✓ Added column `{columnName}` to table `{tableName}`");
                        }
                    }
                    else
                    {
                        _logger.LogInformation($"✓ Column `{columnName}` already exists in table `{tableName}`");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to add column {columnName} to {tableName}: {ex.Message}");
                throw;
            }
        }

        private string BuildConnectionString()
        {
            var dbServer = Environment.GetEnvironmentVariable("DB_SERVER") ?? "127.0.0.1";
            var dbPort = Environment.GetEnvironmentVariable("DB_PORT") ?? "3306";
            var dbName = Environment.GetEnvironmentVariable("DB_NAME") ?? "rekovadb";
            var dbUser = Environment.GetEnvironmentVariable("DB_USER") ?? "root";
            var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "IbraKonate@5";

            // Check for explicit connection string first
            var explicitConnStr = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");
            if (!string.IsNullOrEmpty(explicitConnStr))
            {
                return explicitConnStr;
            }

            var connectionString = $"Server={dbServer};Port={dbPort};Database={dbName};User={dbUser};Password={dbPassword};SslMode=None;AllowPublicKeyRetrieval=True;";
            return connectionString;
        }
    }
}
