#!/usr/bin/env python3
"""
Direct database schema fixer - bypasses the migration service
Adds all missing columns to transactions, customers, and promises tables
"""

import mysql.connector
import sys

# Database connection details
DB_CONFIG = {
    'host': 'localhost',
    'user': 'root',
    'password': 'IbraKonate@5',
    'database': 'rekovadb'
}

# Define all missing columns by table
MIGRATIONS = {
    'transactions': [
        ('transaction_internal_id', 'VARCHAR(100) NULL'),
        ('customer_internal_id', 'VARCHAR(50) NULL'),
        ('loan_balance_before', 'DECIMAL(15,2) NULL'),
        ('loan_balance_after', 'DECIMAL(15,2) NULL'),
        ('arrears_before', 'DECIMAL(18,2) NULL'),
        ('arrears_after', 'DECIMAL(18,2) NULL'),
        ('description', 'LONGTEXT NULL'),
        ('payment_method', 'VARCHAR(50) NULL'),
        ('mpesa_receipt_number', 'VARCHAR(100) NULL'),
        ('processed_at', 'DATETIME NULL'),
        ('initiated_by', 'VARCHAR(255) NULL'),
        ('initiated_by_user_id', 'INT NULL'),
    ],
    'customers': [
        ('customer_internal_id', 'VARCHAR(50) NULL'),
        ('account_number', 'VARCHAR(50) NULL'),
        ('assigned_to_user_id', 'INT NULL'),
        ('created_by_user_id', 'INT NULL'),
    ],
    'promises': [
        ('promise_id', 'VARCHAR(50) NULL'),
        ('customer_id', 'INT NULL'),
        ('customer_name', 'VARCHAR(255) NULL'),
        ('phone_number', 'VARCHAR(20) NULL'),
        ('promise_amount', 'DECIMAL(15,2) NULL'),
        ('promise_date', 'DATETIME NULL'),
        ('promise_type', 'VARCHAR(50) NULL'),
        ('status', "VARCHAR(50) NULL DEFAULT 'PENDING'"),
        ('fulfillment_amount', 'DECIMAL(15,2) NULL'),
        ('fulfillment_date', 'DATETIME NULL'),
        ('notes', 'VARCHAR(500) NULL'),
        ('created_by_user_id', 'INT NULL'),
        ('created_by_name', 'VARCHAR(255) NULL'),
        ('reminder_sent', 'BOOLEAN NULL DEFAULT FALSE'),
        ('next_follow_up_date', 'DATETIME NULL'),
    ]
}

def get_existing_columns(cursor, table_name):
    """Get list of existing columns in a table"""
    cursor.execute(f"""
        SELECT COLUMN_NAME 
        FROM INFORMATION_SCHEMA.COLUMNS 
        WHERE TABLE_SCHEMA = 'rekovadb' AND TABLE_NAME = %s
    """, (table_name,))
    return {row[0] for row in cursor.fetchall()}

def add_column_if_missing(cursor, table_name, column_name, column_type):
    """Add a column to a table if it doesn't exist"""
    existing = get_existing_columns(cursor, table_name)
    if column_name in existing:
        print(f"✓ Column {table_name}.{column_name} already exists")
        return False
    
    try:
        query = f"ALTER TABLE `{table_name}` ADD COLUMN `{column_name}` {column_type}"
        cursor.execute(query)
        print(f"✓ Added column {table_name}.{column_name}")
        return True
    except Exception as e:
        print(f"✗ Failed to add {table_name}.{column_name}: {e}")
        return False

def main():
    print("=" * 60)
    print("DATABASE SCHEMA FIXER - Adding Missing Columns")
    print("=" * 60)
    
    try:
        # Connect to MySQL
        conn = mysql.connector.connect(**DB_CONFIG)
        cursor = conn.cursor()
        print("✓ Connected to MySQL database\n")
        
        # Process each table
        for table_name, columns in MIGRATIONS.items():
            print(f"\nProcessing table: {table_name}")
            print("-" * 40)
            
            added_count = 0
            for column_name, column_type in columns:
                if add_column_if_missing(cursor, table_name, column_name, column_type):
                    added_count += 1
            
            print(f"Added {added_count} new columns to {table_name}")
        
        # Commit all changes
        conn.commit()
        print("\n" + "=" * 60)
        print("✓ All migrations completed successfully!")
        print("=" * 60)
        cursor.close()
        conn.close()
        
    except Exception as e:
        print(f"\n✗ Error: {e}")
        sys.exit(1)

if __name__ == "__main__":
    main()
