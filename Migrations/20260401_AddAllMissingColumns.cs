using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RekovaBE_CSharp.Migrations
{
    /// <inheritdoc />
    public partial class AddAllMissingColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add all missing columns to transactions table with direct SQL
            migrationBuilder.Sql(@"
                ALTER TABLE `transactions` 
                ADD COLUMN IF NOT EXISTS `customer_internal_id` VARCHAR(50) NULL,
                ADD COLUMN IF NOT EXISTS `transaction_internal_id` VARCHAR(100) NULL,
                ADD COLUMN IF NOT EXISTS `initiated_by_user_id` INT NULL,
                ADD COLUMN IF NOT EXISTS `initiated_by` LONGTEXT NULL,
                ADD COLUMN IF NOT EXISTS `mpesa_receipt_number` VARCHAR(50) NULL,
                ADD COLUMN IF NOT EXISTS `payment_method` VARCHAR(50) NULL,
                ADD COLUMN IF NOT EXISTS `processed_at` DATETIME NULL;
            ");

            // Add missing columns to other tables
            migrationBuilder.Sql(@"
                ALTER TABLE `promises` 
                ADD COLUMN IF NOT EXISTS `customer_name` VARCHAR(255) NULL,
                ADD COLUMN IF NOT EXISTS `phone_number` VARCHAR(20) NULL,
                ADD COLUMN IF NOT EXISTS `promise_amount` DECIMAL(15,2) NULL,
                ADD COLUMN IF NOT EXISTS `promise_date` DATETIME NULL,
                ADD COLUMN IF NOT EXISTS `promise_type` VARCHAR(50) NULL,
                ADD COLUMN IF NOT EXISTS `fulfillment_amount` DECIMAL(15,2) NULL,
                ADD COLUMN IF NOT EXISTS `fulfillment_date` DATETIME NULL,
                ADD COLUMN IF NOT EXISTS `notes` VARCHAR(500) NULL,
                ADD COLUMN IF NOT EXISTS `created_by_user_id` INT NULL,
                ADD COLUMN IF NOT EXISTS `created_by_name` VARCHAR(255) NULL,
                ADD COLUMN IF NOT EXISTS `reminder_sent` BOOLEAN DEFAULT FALSE,
                ADD COLUMN IF NOT EXISTS `next_follow_up_date` DATETIME NULL;
            ");

            // Add missing columns to customers table
            migrationBuilder.Sql(@"
                ALTER TABLE `customers` 
                ADD COLUMN IF NOT EXISTS `customer_internal_id` VARCHAR(50) NULL,
                ADD COLUMN IF NOT EXISTS `assigned_to_user_id` INT NULL,
                ADD COLUMN IF NOT EXISTS `created_by_user_id` INT NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // This is a data migration - down migration is intentionally left empty
            // as dropping columns is a destructive operation
        }
    }
}
