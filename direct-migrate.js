// direct-migrate.js
const mysql = require('mysql2/promise');
const { MongoClient } = require('mongodb');

// Configuration
const MONGO_URL = 'mongodb://localhost:27017';
const MONGO_DB = 'STKPUSH';  // Your MongoDB database name

const MYSQL_CONFIG = {
    host: '127.0.0.1',
    user: 'root',
    password: 'IbraKonate@5',
    database: 'rekovadb'
};

// Helper: Convert MongoDB ObjectId to integer for MySQL
function convertObjectIdToInt(objId) {
    if (!objId) return null;
    const idStr = objId.toString();
    // Use last 8 chars as hex
    const hex = idStr.slice(-8);
    return parseInt(hex, 16);
}

// Helper: Format date
function formatDate(date) {
    if (!date) return null;
    if (date instanceof Date) return date;
    return new Date(date);
}

// Clear existing tables
async function clearTables(connection) {
    console.log('🗑️  Clearing existing tables...');
    await connection.execute('SET FOREIGN_KEY_CHECKS = 0');
    
    const tables = ['comments', 'activities', 'promises', 'transactions', 'customers', 'users'];
    for (const table of tables) {
        try {
            await connection.execute(`TRUNCATE TABLE ${table}`);
            console.log(`  ✓ Cleared ${table}`);
        } catch (err) {
            console.log(`  ⚠️ Could not clear ${table}: ${err.message}`);
        }
    }
    
    await connection.execute('SET FOREIGN_KEY_CHECKS = 1');
    console.log('✅ Tables cleared\n');
}

// Migrate users
async function migrateUsers(mongoDb, mysqlConn) {
    console.log('📥 Migrating users...');
    const collection = mongoDb.collection('users');
    const users = await collection.find({}).toArray();
    
    console.log(`  Found ${users.length} users in MongoDB`);
    
    const sql = `INSERT INTO users (
        id, username, email, password_hash, first_name, last_name,
        phone, role, is_active, created_at, updated_at
    ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
    ON DUPLICATE KEY UPDATE
    username = VALUES(username), email = VALUES(email)`;
    
    let count = 0;
    for (const user of users) {
        try {
            const userId = convertObjectIdToInt(user._id);
            const values = [
                userId,
                user.username || '',
                user.email || '',
                user.password || '',
                user.firstName || '',
                user.lastName || '',
                user.phone || '',
                user.role || 'officer',
                user.isActive === false ? 0 : 1,
                formatDate(user.createdAt) || new Date(),
                formatDate(user.updatedAt) || new Date()
            ];
            await mysqlConn.execute(sql, values);
            count++;
        } catch (err) {
            console.error(`  ✗ Error migrating user ${user.username}:`, err.message);
        }
    }
    console.log(`  ✅ Migrated ${count} users\n`);
}

// Migrate customers
async function migrateCustomers(mongoDb, mysqlConn) {
    console.log('📥 Migrating customers...');
    const collection = mongoDb.collection('customers');
    const customers = await collection.find({}).toArray();
    
    console.log(`  Found ${customers.length} customers in MongoDB`);
    
    const sql = `INSERT INTO customers (
        id, customer_internal_id, customer_id, phone_number, name,
        account_number, loan_balance, arrears, total_repayments, email,
        national_id, last_payment_date, last_contact_date, status, loan_type,
        assigned_to_user_id, created_by, is_active, created_at, updated_at,
        address, has_outstanding_promise, last_promise_date, promise_count,
        fulfilled_promise_count, promise_fulfillment_rate
    ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
    ON DUPLICATE KEY UPDATE
    name = VALUES(name), phone_number = VALUES(phone_number), loan_balance = VALUES(loan_balance)`;
    
    let count = 0;
    for (const customer of customers) {
        try {
            const customerId = convertObjectIdToInt(customer._id);
            const assignedTo = customer.assignedTo ? convertObjectIdToInt(customer.assignedTo) : null;
            
            const values = [
                customerId,
                customer.customerInternalId || '',
                customer.customerId || '',
                customer.phoneNumber || '',
                customer.name || '',
                customer.accountNumber || '',
                customer.loanBalance || 0,
                customer.arrears || 0,
                customer.totalRepayments || 0,
                customer.email || '',
                customer.nationalId || '',
                formatDate(customer.lastPaymentDate),
                formatDate(customer.lastContactDate),
                customer.status || 'ACTIVE',
                customer.loanType || 'Standard',
                assignedTo,
                customer.createdBy || '',
                customer.isActive === false ? 0 : 1,
                formatDate(customer.createdAt) || new Date(),
                formatDate(customer.updatedAt) || new Date(),
                customer.address || '',
                customer.hasOutstandingPromise || false,
                formatDate(customer.lastPromiseDate),
                customer.promiseCount || 0,
                customer.fulfilledPromiseCount || 0,
                customer.promiseFulfillmentRate || 0
            ];
            await mysqlConn.execute(sql, values);
            count++;
            
            if (count % 10 === 0) {
                console.log(`  📊 Processed ${count} customers...`);
            }
        } catch (err) {
            console.error(`  ✗ Error migrating customer ${customer.name}:`, err.message);
        }
    }
    console.log(`  ✅ Migrated ${count} customers\n`);
}

// Migrate transactions
async function migrateTransactions(mongoDb, mysqlConn) {
    console.log('📥 Migrating transactions...');
    const collection = mongoDb.collection('transactions');
    const transactions = await collection.find({}).toArray();
    
    console.log(`  Found ${transactions.length} transactions in MongoDB`);
    
    const sql = `INSERT INTO transactions (
        id, transaction_internal_id, transaction_id, customer_id,
        phone_number, amount, description, status, payment_method,
        mpesa_receipt_number, initiated_by, initiated_by_user_id,
        created_at, updated_at
    ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
    ON DUPLICATE KEY UPDATE
    status = VALUES(status), amount = VALUES(amount)`;
    
    let count = 0;
    for (const tx of transactions) {
        try {
            const txId = convertObjectIdToInt(tx._id);
            const customerId = tx.customerId ? convertObjectIdToInt(tx.customerId) : null;
            const initiatedByUserId = tx.initiatedBy ? convertObjectIdToInt(tx.initiatedBy) : null;
            
            const values = [
                txId,
                tx.transactionInternalId || '',
                tx.transactionId || '',
                customerId,
                tx.phoneNumber || '',
                tx.amount || 0,
                tx.description || '',
                tx.status || 'PENDING',
                tx.paymentMethod || 'MPESA',
                tx.mpesaReceiptNumber || '',
                tx.initiatedByName || '',
                initiatedByUserId,
                formatDate(tx.createdAt) || new Date(),
                formatDate(tx.updatedAt) || new Date()
            ];
            await mysqlConn.execute(sql, values);
            count++;
        } catch (err) {
            console.error(`  ✗ Error migrating transaction ${tx.transactionId}:`, err.message);
        }
    }
    console.log(`  ✅ Migrated ${count} transactions\n`);
}

// Migrate promises
async function migratePromises(mongoDb, mysqlConn) {
    console.log('📥 Migrating promises...');
    const collection = mongoDb.collection('promises');
    const promises = await collection.find({}).toArray();
    
    console.log(`  Found ${promises.length} promises in MongoDB`);
    
    const sql = `INSERT INTO promises (
        id, promise_id, customer_id, customer_name, phone_number,
        promise_amount, promise_date, promise_type, status,
        fulfillment_amount, fulfillment_date, notes, created_by_user_id,
        created_by_name, created_at, updated_at
    ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
    ON DUPLICATE KEY UPDATE
    status = VALUES(status), fulfillment_amount = VALUES(fulfillment_amount)`;
    
    let count = 0;
    for (const promise of promises) {
        try {
            const promiseId = convertObjectIdToInt(promise._id);
            const customerId = promise.customerId ? convertObjectIdToInt(promise.customerId) : null;
            const createdByUserId = promise.createdBy ? convertObjectIdToInt(promise.createdBy) : null;
            
            const values = [
                promiseId,
                promise.promiseId || '',
                customerId,
                promise.customerName || '',
                promise.phoneNumber || '',
                promise.promiseAmount || 0,
                formatDate(promise.promiseDate) || new Date(),
                promise.promiseType || 'FULL_PAYMENT',
                promise.status || 'PENDING',
                promise.fulfillmentAmount || null,
                formatDate(promise.fulfillmentDate),
                promise.notes || '',
                createdByUserId,
                promise.createdByName || 'System',
                formatDate(promise.createdAt) || new Date(),
                formatDate(promise.updatedAt) || new Date()
            ];
            await mysqlConn.execute(sql, values);
            count++;
        } catch (err) {
            console.error(`  ✗ Error migrating promise ${promise.promiseId}:`, err.message);
        }
    }
    console.log(`  ✅ Migrated ${count} promises\n`);
}

// Main function
async function main() {
    let mongoClient;
    let mysqlConn;
    
    try {
        console.log('🚀 Starting MongoDB to MySQL Migration\n');
        console.log('=' .repeat(50));
        
        // Connect to MongoDB
        console.log('📡 Connecting to MongoDB...');
        mongoClient = new MongoClient(MONGO_URL);
        await mongoClient.connect();
        const mongoDb = mongoClient.db(MONGO_DB);
        console.log(`✅ Connected to MongoDB database: ${MONGO_DB}\n`);
        
        // Connect to MySQL
        console.log('📡 Connecting to MySQL...');
        mysqlConn = await mysql.createConnection(MYSQL_CONFIG);
        console.log(`✅ Connected to MySQL database: ${MYSQL_CONFIG.database}\n`);
        
        // Clear existing tables
        await clearTables(mysqlConn);
        
        // Migrate data
        await migrateUsers(mongoDb, mysqlConn);
        await migrateCustomers(mongoDb, mysqlConn);
        await migrateTransactions(mongoDb, mysqlConn);
        await migratePromises(mongoDb, mysqlConn);
        
        // Show final counts
        console.log('=' .repeat(50));
        console.log('📊 Final MySQL Counts:');
        const [rows] = await mysqlConn.execute(`
            SELECT 'users' as table_name, COUNT(*) as count FROM users
            UNION ALL
            SELECT 'customers', COUNT(*) FROM customers
            UNION ALL
            SELECT 'transactions', COUNT(*) FROM transactions
            UNION ALL
            SELECT 'promises', COUNT(*) FROM promises
        `);
        
        rows.forEach(row => {
            console.log(`  ${row.table_name}: ${row.count}`);
        });
        
        console.log('\n✅ Migration completed successfully!');
        
    } catch (error) {
        console.error('❌ Migration failed:', error);
    } finally {
        if (mongoClient) await mongoClient.close();
        if (mysqlConn) await mysqlConn.end();
    }
}

// Run the migration
main();