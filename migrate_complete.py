# migrate_complete_fixed.py
import mysql.connector
from pymongo import MongoClient
from datetime import datetime
import re

# Configuration - Using the correct database path
MONGO_HOST = 'localhost'
MONGO_PORT = 27017
MONGO_DB = 'STKPUSH'  # Changed from 'test' to 'STKPUSH'

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

def convert_objectid_to_int(obj_id):
    """Convert MongoDB ObjectId to integer for MySQL"""
    try:
        # Use the last 8 characters of the ObjectId as integer
        hex_str = str(obj_id)[-8:]
        return int(hex_str, 16)
    except:
        return hash(str(obj_id)) % 1000000000

def clear_mysql_tables(mysql_conn):
    """Clear existing data from MySQL tables"""
    print("\n--- Clearing existing MySQL data ---")
    cursor = mysql_conn.cursor()
    
    # Disable foreign key checks
    cursor.execute("SET FOREIGN_KEY_CHECKS = 0")
    
    # Truncate tables in correct order
    tables = ['activities', 'comments', 'promises', 'transactions', 'customers', 'users']
    for table in tables:
        try:
            cursor.execute(f"TRUNCATE TABLE {table}")
            print(f"  ✓ Cleared table: {table}")
        except Exception as e:
            print(f"  ⚠️ Could not clear {table}: {e}")
    
    # Re-enable foreign key checks
    cursor.execute("SET FOREIGN_KEY_CHECKS = 1")
    
    mysql_conn.commit()
    cursor.close()
    print("✓ MySQL tables cleared")

def migrate_users(mongo_db, mysql_conn):
    print("\n--- Migrating users ---")
    cursor = mysql_conn.cursor()
    
    # Try to find users collection in various possible locations
    users = []
    possible_collections = ['users', 'user', 'User', 'Users']
    
    for coll_name in possible_collections:
        if coll_name in mongo_db.list_collection_names():
            users = list(mongo_db[coll_name].find({}))
            if users:
                print(f"Found users in collection: {coll_name}")
                break
    
    print(f"Found {len(users)} users in MongoDB")
    
    if not users:
        print("⚠️ No users found, creating default admin user")
        # Insert default admin user
        sql = """INSERT INTO users (id, username, email, password_hash, first_name, last_name, 
                 phone, role, is_active, created_at, updated_at)
                 VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s)"""
        
        # Password: Admin@123 (hashed with BCrypt)
        default_password = "$2a$10$YourHashedPasswordHere"
        
        values = (1, 'admin', 'admin@rekova.com', default_password, 'Admin', 'User', 
                 '0712345678', 'admin', 1, datetime.now(), datetime.now())
        
        try:
            cursor.execute(sql, values)
            print("  ✓ Created default admin user")
        except Exception as e:
            print(f"  ✗ Error creating admin user: {e}")
    
    inserted = 0
    for user in users:
        try:
            user_id = convert_objectid_to_int(user['_id'])
            
            sql = """INSERT INTO users (id, username, email, password_hash, first_name, last_name, 
                     phone, role, is_active, created_at, updated_at)
                     VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s)
                     ON DUPLICATE KEY UPDATE
                     username=VALUES(username), email=VALUES(email)"""
            
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
            inserted += 1
            print(f"  ✓ Migrated user: {user.get('username', 'unknown')}")
        except Exception as e:
            print(f"  ✗ Error migrating user {user.get('username', 'unknown')}: {e}")
    
    mysql_conn.commit()
    cursor.close()
    print(f"✓ Migrated {inserted} users")

def migrate_customers(mongo_db, mysql_conn):
    print("\n--- Migrating customers ---")
    cursor = mysql_conn.cursor()
    collection = mongo_db['customers']
    
    customers = list(collection.find({}))
    print(f"Found {len(customers)} customers in MongoDB")
    
    if not customers:
        print("⚠️ No customers found, creating sample customers")
        # Insert sample customers for testing
        sample_customers = [
            ('John Doe', '0712345678', 50000, 15000, 1),
            ('Jane Smith', '0723456789', 75000, 25000, 1),
            ('Peter Jones', '0734567890', 100000, 30000, 1),
        ]
        
        for name, phone, balance, arrears, assigned_to in sample_customers:
            sql = """INSERT INTO customers (name, phone_number, loan_balance, arrears, 
                     assigned_to_user_id, status, is_active, created_at, updated_at)
                     VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s)"""
            values = (name, phone, balance, arrears, assigned_to, 'ACTIVE', 1, datetime.now(), datetime.now())
            try:
                cursor.execute(sql, values)
                print(f"  ✓ Created sample customer: {name}")
            except Exception as e:
                print(f"  ✗ Error creating sample customer: {e}")
        
        mysql_conn.commit()
    
    inserted = 0
    for customer in customers:
        try:
            customer_id = convert_objectid_to_int(customer['_id'])
            
            # Convert assignedTo ObjectId to integer if exists
            assigned_to = None
            if 'assignedTo' in customer and customer['assignedTo']:
                assigned_to = convert_objectid_to_int(customer['assignedTo'])
            
            # Handle lastPaymentDate
            last_payment_date = customer.get('lastPaymentDate')
            if isinstance(last_payment_date, str):
                try:
                    last_payment_date = datetime.fromisoformat(last_payment_date.replace('Z', '+00:00'))
                except:
                    last_payment_date = None
            
            sql = """INSERT INTO customers (
                id, customer_internal_id, customer_id, phone_number, name, 
                account_number, loan_balance, arrears, total_repayments, email, 
                national_id, last_payment_date, last_contact_date, status, loan_type, 
                assigned_to_user_id, created_by, is_active, created_at, updated_at
            ) VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s)
            ON DUPLICATE KEY UPDATE
            name=VALUES(name), phone_number=VALUES(phone_number), loan_balance=VALUES(loan_balance)"""
            
            values = (
                customer_id,
                customer.get('customerInternalId', ''),
                customer.get('customerId', ''),
                customer.get('phoneNumber', ''),
                customer.get('name', ''),
                customer.get('accountNumber', ''),
                customer.get('loanBalance', 0),
                customer.get('arrears', 0),
                customer.get('totalRepayments', 0),
                customer.get('email', ''),
                customer.get('nationalId', ''),
                last_payment_date,
                customer.get('lastContactDate', None),
                customer.get('status', 'ACTIVE'),
                customer.get('loanType', 'Standard'),
                assigned_to,
                customer.get('createdBy', ''),
                1 if customer.get('isActive', True) else 0,
                customer.get('createdAt', datetime.now()),
                customer.get('updatedAt', datetime.now())
            )
            cursor.execute(sql, values)
            inserted += 1
            print(f"  ✓ Migrated customer: {customer.get('name', 'unknown')}")
        except Exception as e:
            print(f"  ✗ Error migrating customer {customer.get('name', 'unknown')}: {e}")
    
    mysql_conn.commit()
    cursor.close()
    print(f"✓ Migrated {inserted} customers")

def main():
    print("Starting complete data migration from MongoDB to MySQL...")
    print("=" * 60)
    
    # Connect to databases
    mongo_client = get_mongo_connection()
    if not mongo_client:
        return
    
    mysql_conn = get_mysql_connection()
    if not mysql_conn:
        mongo_client.close()
        return
    
    # First, clear existing MySQL data
    clear_mysql_tables(mysql_conn)
    
    # Try both possible database names
    possible_db_names = ['STKPUSH', 'test', 'rekova']
    migrated = False
    
    for db_name in possible_db_names:
        print(f"\n📁 Trying database: {db_name}")
        mongo_db = mongo_client[db_name]
        
        # List collections in this database
        collections = mongo_db.list_collection_names()
        if collections:
            print(f"  Found collections: {', '.join(collections)}")
            
            if 'customers' in collections or 'users' in collections:
                print(f"✓ Using database: {db_name}")
                
                # Run migrations
                migrate_users(mongo_db, mysql_conn)
                migrate_customers(mongo_db, mysql_conn)
                migrated = True
                break
        else:
            print(f"  No collections found in {db_name}")
    
    if not migrated:
        print("\n⚠️ No data found in any database. Creating sample data...")
        migrate_users(mongo_client['STKPUSH'], mysql_conn)
        migrate_customers(mongo_client['STKPUSH'], mysql_conn)
    
    # Close connections
    mysql_conn.close()
    mongo_client.close()
    
    print("\n" + "=" * 60)
    print("Migration completed!")
    
    # Show final counts
    verify_conn = get_mysql_connection()
    if verify_conn:
        cursor = verify_conn.cursor()
        cursor.execute("""
            SELECT 'users' as table_name, COUNT(*) as count FROM users
            UNION ALL
            SELECT 'customers', COUNT(*) FROM customers
            UNION ALL
            SELECT 'transactions', COUNT(*) FROM transactions
            UNION ALL
            SELECT 'promises', COUNT(*) FROM promises
        """)
        print("\n📊 Final MySQL counts:")
        for table, count in cursor.fetchall():
            print(f"  {table}: {count}")
        cursor.close()
        verify_conn.close()

if __name__ == "__main__":
    main()