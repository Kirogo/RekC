#!/bin/bash

# Database connection details
DB_HOST="localhost"
DB_PORT="3306"
DB_USER="root"
DB_PASS="Root@123456"
DB_NAME="rekovadb"

echo "=== CHECKING DATABASE SCHEMA ==="
echo ""

# Check transactions table
echo "TRANSACTIONS TABLE - Expected vs Actual Columns:"
echo "---"
mysql -h $DB_HOST -u $DB_USER -p$DB_PASS $DB_NAME << 'EOSQL' 2>/dev/null
SELECT COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE, COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME='transactions' 
ORDER BY ORDINAL_POSITION;
EOSQL

echo ""
echo "CUSTOMERS TABLE - Expected vs Actual Columns:"
echo "---"
mysql -h $DB_HOST -u $DB_USER -p$DB_PASS $DB_NAME << 'EOSQL' 2>/dev/null
SELECT COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE, COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME='customers'
ORDER BY ORDINAL_POSITION;
EOSQL

echo ""
echo "PROMISES TABLE - Expected vs Actual Columns:"
echo "---"
mysql -h $DB_HOST -u $DB_USER -p$DB_PASS $DB_NAME << 'EOSQL' 2>/dev/null
SELECT COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE, COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME='promises'
ORDER BY ORDINAL_POSITION;
EOSQL

echo ""
echo "=== MISSING COLUMNS ANALYSIS ==="
echo ""
echo "TRANSACTIONS - Should have:"
echo "  - loan_balance_before (DECIMAL 15,2)"
echo "  - loan_balance_after (DECIMAL 15,2)"
echo "  - customer_internal_id (VARCHAR 50)"
echo "  - transaction_internal_id (VARCHAR 100)"
echo "  - initiated_by_user_id (INT)"
echo "  - initiated_by (LONGTEXT)"
echo "  - mpesa_receipt_number (VARCHAR 50)"
echo "  - payment_method (VARCHAR 50)"
echo "  - processed_at (DATETIME)"
echo "  - arrears_before (DECIMAL 18,2)"
echo "  - arrears_after (DECIMAL 18,2)"
echo "  - description (LONGTEXT)"
echo ""
echo "CUSTOMERS - Should have:"
echo "  - account_number (VARCHAR 50)"
echo "  - customer_internal_id (VARCHAR 50)"
echo "  - assigned_to_user_id (INT)"
echo "  - created_by_user_id (INT)"
echo ""
echo "PROMISES - Should have:"
echo "  - promise_id (VARCHAR 50)"
echo "  - customer_id (INT)"
echo "  - customer_name (VARCHAR 255)"
echo "  - phone_number (VARCHAR 20)"
echo "  - promise_amount (DECIMAL 15,2)"
echo "  - promise_date (DATETIME)"
echo "  - promise_type (VARCHAR 50)"
echo "  - fulfillment_amount (DECIMAL 15,2)"
echo "  - fulfillment_date (DATETIME)"
echo "  - notes (VARCHAR 500)"
echo "  - created_by_user_id (INT)"
echo "  - created_by_name (VARCHAR 255)"
echo "  - reminder_sent (BOOLEAN)"
echo "  - next_follow_up_date (DATETIME)"
echo "  - status (VARCHAR 50)"
