import mysql.connector
from pymongo import MongoClient
from datetime import datetime
import sys

# Configuration - FIXED: Changed from 'test' to 'rekova'
MONGO_HOST = 'localhost'
MONGO_PORT = 27017
MONGO_DB = 'rekova'  # Changed from 'test' to 'rekova'

MYSQL_HOST = '127.0.0.1'
MYSQL_USER = 'root'
MYSQL_PASSWORD = 'IbraKonate@5'
MYSQL_DB = 'rekovadb'

def get_mongo_connection():
    try:
        client = MongoClient(MONGO_HOST, MONGO_PORT)
        print(f"✓ Connected to MongoDB at {MONGO_HOST}:{MONGO_PORT}")
        return client
    except Exception as e:
        print(f"✗ Failed to connect to MongoDB: {e}")
        return None

def get_mysql_connection():
    try:
        conn = mysql.connector.connect(
            host=MYSQL_HOST,
            user=MYSQL_USER,
            password=MYSQL_PASSWORD,
            database=MYSQL_DB
        )
        print(f"✓ Connected to MySQL database {MYSQL_DB}")
        return conn
    except Exception as e:
        print(f"✗ Failed to connect to MySQL: {e}")
        return None

def migrate_users(mongo_db, mysql_conn):
    print("\n--- Migrating users ---")
    cursor = mysql_conn.cursor()
    collection = mongo_db['users']
    
    users = list(collection.find({}))
    print(f"Found {len(users)} users in MongoDB")
    
    inserted = 0
    updated = 0
    for user in users:
        try:
            # Extract user ID - handle both formats
            if 'id' in user:
                user_id = user['id']
            elif '_id' in user:
                # Try to convert ObjectId to integer
                try:
                    user_id = int(str(user['_id'])[-8:], 16)
                except:
                    user_id = hash(str(user['_id'])) % 1000000
            else:
                user_id = None
            
            sql = """INSERT INTO users (id, username, email, password_hash, first_name, last_name, 
                     phone, role, is_active, created_at, updated_at)
                     VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s)
                     ON DUPLICATE KEY UPDATE 
                     username=VALUES(username), 
                     email=VALUES(email),
                     password_hash=VALUES(password_hash),
                     first_name=VALUES(first_name),
                     last_name=VALUES(last_name),
                     phone=VALUES(phone),
                     role=VALUES(role),
                     is_active=VALUES(is_active),
                     updated_at=VALUES(updated_at)"""
            
            values = (
                user_id,
                user.get('username', ''),
                user.get('email', ''),
                user.get('password', ''),
                user.get('firstName', ''),
                user.get('lastName', ''),
                user.get('phone', ''),
                user.get('role', 'officer'),
                1 if user.get('isActive', True) else 0,
                user.get('createdAt', datetime.now()),
                user.get('updatedAt', datetime.now())
            )
            cursor.execute(sql, values)
            if cursor.rowcount == 1:
                inserted += 1
            else:
                updated += 1
        except Exception as e:
            print(f"  Error migrating user {user.get('username', 'unknown')}: {e}")
    
    mysql_conn.commit()
    cursor.close()
    print(f"✓ Migrated {inserted} new users, updated {updated} existing users")

def migrate_customers(mongo_db, mysql_conn):
    print("\n--- Migrating customers ---")
    cursor = mysql_conn.cursor()
    collection = mongo_db['customers']
    
    customers = list(collection.find({}))
    print(f"Found {len(customers)} customers in MongoDB")
    
    inserted = 0
    updated = 0
    for customer in customers:
        try:
            # Extract customer ID
            if 'id' in customer:
                customer_id = customer['id']
            elif '_id' in customer:
                try:
                    customer_id = int(str(customer['_id'])[-8:], 16)
                except:
                    customer_id = hash(str(customer['_id'])) % 1000000
            else:
                customer_id = None
            
            sql = """INSERT INTO customers (id, name, phone_number, loan_balance, arrears, 
                     loan_type, assigned_to_user_id, status, total_repayments, is_active, 
                     created_at, updated_at, email, national_id, account_number,
                     customer_internal_id, customer_id, created_by, address,
                     has_outstanding_promise, last_promise_date, promise_count, 
                     fulfilled_promise_count, promise_fulfillment_rate)
                     VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s)
                     ON DUPLICATE KEY UPDATE
                     name=VALUES(name), 
                     phone_number=VALUES(phone_number),
                     loan_balance=VALUES(loan_balance),
                     arrears=VALUES(arrears),
                     assigned_to_user_id=VALUES(assigned_to_user_id),
                     status=VALUES(status),
                     updated_at=VALUES(updated_at)"""
            
            values = (
                customer_id,
                customer.get('name', ''),
                customer.get('phoneNumber', ''),
                customer.get('loanBalance', 0),
                customer.get('arrears', 0),
                customer.get('loanType', 'Standard'),
                customer.get('assignedTo', None),
                customer.get('status', 'ACTIVE'),
                customer.get('totalRepayments', 0),
                1 if customer.get('isActive', True) else 0,
                customer.get('createdAt', datetime.now()),
                customer.get('updatedAt', datetime.now()),
                customer.get('email', ''),
                customer.get('nationalId', ''),
                customer.get('accountNumber', ''),
                customer.get('customerInternalId', ''),
                customer.get('customerId', ''),
                customer.get('createdBy', ''),
                customer.get('address', ''),
                customer.get('hasOutstandingPromise', False),
                customer.get('lastPromiseDate', None),
                customer.get('promiseCount', 0),
                customer.get('fulfilledPromiseCount', 0),
                customer.get('promiseFulfillmentRate', 0)
            )
            cursor.execute(sql, values)
            if cursor.rowcount == 1:
                inserted += 1
            else:
                updated += 1
        except Exception as e:
            print(f"  Error migrating customer {customer.get('name', 'unknown')}: {e}")
    
    mysql_conn.commit()
    cursor.close()
    print(f"✓ Migrated {inserted} new customers, updated {updated} existing customers")

def migrate_transactions(mongo_db, mysql_conn):
    print("\n--- Migrating transactions ---")
    cursor = mysql_conn.cursor()
    collection = mongo_db['transactions']
    
    transactions = list(collection.find({}))
    print(f"Found {len(transactions)} transactions in MongoDB")
    
    inserted = 0
    updated = 0
    for transaction in transactions:
        try:
            # Extract transaction ID
            if 'id' in transaction:
                trans_id = transaction['id']
            elif '_id' in transaction:
                try:
                    trans_id = int(str(transaction['_id'])[-8:], 16)
                except:
                    trans_id = hash(str(transaction['_id'])) % 1000000
            else:
                trans_id = None
            
            sql = """INSERT INTO transactions (id, transaction_id, customer_id, phone_number, amount, 
                     status, payment_method, mpesa_receipt_number, initiated_by_user_id, 
                     created_at, updated_at, description, initiated_by)
                     VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s)
                     ON DUPLICATE KEY UPDATE
                     status=VALUES(status),
                     amount=VALUES(amount),
                     updated_at=VALUES(updated_at)"""
            
            values = (
                trans_id,
                transaction.get('transactionId', ''),
                transaction.get('customerId', None),
                transaction.get('phoneNumber', ''),
                transaction.get('amount', 0),
                transaction.get('status', 'PENDING'),
                transaction.get('paymentMethod', 'MPESA'),
                transaction.get('mpesaReceiptNumber', ''),
                transaction.get('initiatedBy', None),
                transaction.get('createdAt', datetime.now()),
                transaction.get('updatedAt', datetime.now()),
                transaction.get('description', ''),
                transaction.get('initiatedByName', '')
            )
            cursor.execute(sql, values)
            if cursor.rowcount == 1:
                inserted += 1
            else:
                updated += 1
        except Exception as e:
            print(f"  Error migrating transaction {transaction.get('transactionId', 'unknown')}: {e}")
    
    mysql_conn.commit()
    cursor.close()
    print(f"✓ Migrated {inserted} new transactions, updated {updated} existing transactions")

def migrate_promises(mongo_db, mysql_conn):
    print("\n--- Migrating promises ---")
    cursor = mysql_conn.cursor()
    collection = mongo_db['promises']
    
    promises = list(collection.find({}))
    print(f"Found {len(promises)} promises in MongoDB")
    
    inserted = 0
    updated = 0
    for promise in promises:
        try:
            # Extract promise ID
            if 'id' in promise:
                promise_id_val = promise['id']
            elif '_id' in promise:
                try:
                    promise_id_val = int(str(promise['_id'])[-8:], 16)
                except:
                    promise_id_val = hash(str(promise['_id'])) % 1000000
            else:
                promise_id_val = None
            
            sql = """INSERT INTO promises (id, promise_id, customer_id, customer_name, phone_number,
                     promise_amount, promise_date, status, created_by_user_id, created_by_name,
                     notes, created_at, updated_at, fulfillment_amount, fulfillment_date)
                     VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s)
                     ON DUPLICATE KEY UPDATE
                     status=VALUES(status),
                     fulfillment_amount=VALUES(fulfillment_amount),
                     fulfillment_date=VALUES(fulfillment_date),
                     updated_at=VALUES(updated_at)"""
            
            values = (
                promise_id_val,
                promise.get('promiseId', ''),
                promise.get('customerId', 0),
                promise.get('customerName', ''),
                promise.get('phoneNumber', ''),
                promise.get('promiseAmount', 0),
                promise.get('promiseDate', datetime.now()),
                promise.get('status', 'PENDING'),
                promise.get('createdBy', None),
                promise.get('createdByName', 'System'),
                promise.get('notes', ''),
                promise.get('createdAt', datetime.now()),
                promise.get('updatedAt', datetime.now()),
                promise.get('fulfillmentAmount', None),
                promise.get('fulfillmentDate', None)
            )
            cursor.execute(sql, values)
            if cursor.rowcount == 1:
                inserted += 1
            else:
                updated += 1
        except Exception as e:
            print(f"  Error migrating promise {promise.get('promiseId', 'unknown')}: {e}")
    
    mysql_conn.commit()
    cursor.close()
    print(f"✓ Migrated {inserted} new promises, updated {updated} existing promises")

def main():
    print("Starting data migration from MongoDB to MySQL...")
    print("=" * 50)
    
    # Connect to databases
    mongo_client = get_mongo_connection()
    if not mongo_client:
        return
    
    mysql_conn = get_mysql_connection()
    if not mysql_conn:
        mongo_client.close()
        return
    
    # Get MongoDB database
    mongo_db = mongo_client[MONGO_DB]
    
    # List available collections
    print(f"\nAvailable collections in '{MONGO_DB}':")
    for coll in mongo_db.list_collection_names():
        count = mongo_db[coll].count_documents({})
        print(f"  - {coll}: {count} documents")
    
    # Run migrations
    migrate_users(mongo_db, mysql_conn)
    migrate_customers(mongo_db, mysql_conn)
    migrate_transactions(mongo_db, mysql_conn)
    migrate_promises(mongo_db, mysql_conn)
    
    # Close connections
    mysql_conn.close()
    mongo_client.close()
    
    print("\n" + "=" * 50)
    print("Migration completed!")
    
    # Show final counts
    verify_conn = get_mysql_connection()
    if verify_conn:
        cursor = verify_conn.cursor()
        cursor.execute("SELECT 'users' as table_name, COUNT(*) as count FROM users UNION ALL SELECT 'customers', COUNT(*) FROM customers UNION ALL SELECT 'transactions', COUNT(*) FROM transactions UNION ALL SELECT 'promises', COUNT(*) FROM promises")
        print("\nFinal MySQL counts:")
        for table, count in cursor.fetchall():
            print(f"  {table}: {count}")
        cursor.close()
        verify_conn.close()

if __name__ == "__main__":
    main()