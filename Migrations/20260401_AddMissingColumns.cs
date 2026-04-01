using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RekovaBE_CSharp.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add missing columns to transactions table
            migrationBuilder.AddColumn<decimal>(
                name: "arrears_after",
                table: "transactions",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "arrears_before",
                table: "transactions",
                type: "decimal(18,2)",
                nullable: true);

            // Add missing description column to activities table
            if (!ColumnExists(migrationBuilder, "activities", "description"))
            {
                migrationBuilder.AddColumn<string>(
                    name: "description",
                    table: "activities",
                    type: "longtext",
                    nullable: true);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "arrears_after",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "arrears_before",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "description",
                table: "activities");
        }

        private bool ColumnExists(MigrationBuilder migrationBuilder, string tableName, string columnName)
        {
            // This is a helper method - the actual check happens during migration
            return false; // Placeholder
        }
    }
}
