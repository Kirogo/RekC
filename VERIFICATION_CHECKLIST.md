# Rekova Migration - Verification Checklist

## 🎯 Pre-Migration Verification

### MongoDB Check
- [ ] MongoDB server running: `mongosh --eval "db.version()"`
- [ ] Database exists: `use stk_push_loans; show collections`
- [ ] Collections present: users, customers, transactions, promises, activities, comments
- [ ] Data count verified:
  ```javascript
  db.users.countDocuments()
  db.customers.countDocuments()
  db.transactions.countDocuments()
  db.promises.countDocuments()
  db.activities.countDocumentsDocuments()
  db.comments.countDocuments()
  ```

### Passwords Check
- [ ] Users exist and have bcrypt hashed passwords
- [ ] No plain-text passwords in database
- [ ] Sample password hash format: `$2a$...` or `$2b$...`

---

## 🗄️ Database Migration Verification

### MySQL Database Creation
- [ ] MySQL running: `mysql --version`
- [ ] Database created: `SHOW DATABASES;`
- [ ] Tables created: `SHOW TABLES;` (6 tables expected)

### Table Structure Verification

```sql
USE RekovaDB;

-- Verify users table
--  [ ] DESCRIBE users;
-- Expected columns: id, username, email, password, role, first_name, last_name, phone, is_active, created_at

-- Verify customers table
-- [ ] DESCRIBE customers;
-- Expected columns: id, customer_id, phone_number, name, loan_balance, arrears, assigned_to_user_id

-- Verify transactions table
-- [ ] DESCRIBE transactions;
-- Expected columns: id, transaction_id, customer_id, amount, status, created_at

-- Verify promises table
-- [ ] DESCRIBE promises;
-- Expected columns: id, promise_id, customer_id, status, promise_date

-- Verify relationships
-- [ ] SHOW CREATE TABLE customers;
-- Should show FOREIGN KEY for assigned_to_user_id

-- Check indexes
-- [ ] SHOW INDEX FROM customers;
-- Should include indexes on: customer_id, phone_number, is_active
```

### Data Migration Verification

```sql
USE RekovaDB;

-- Count verification
-- [ ] SELECT COUNT(*) FROM users;
-- [ ] SELECT COUNT(*) FROM customers;
-- [ ] SELECT COUNT(*) FROM transactions;
-- [ ] SELECT COUNT(*) FROM promises;
-- [ ] SELECT COUNT(*) FROM activities;
-- [ ] SELECT COUNT(*) FROM comments;

-- Sample data check
-- [ ] SELECT id, username, email, role FROM users LIMIT 5;
-- [ ] SELECT id, name, phone_number, loan_balance FROM customers LIMIT 5;
-- [ ] SELECT id, transaction_id, amount, status FROM transactions LIMIT 5;

-- Foreign key validation
-- [ ] SELECT COUNT(*) FROM customers WHERE assigned_to_user_id IS NOT NULL;
-- [ ] SELECT c.name, u.username FROM customers c LEFT JOIN users u ON c.assigned_to_user_id = u.id LIMIT 5;

-- Data integrity
-- [ ] SELECT COUNT(*) FROM transactions WHERE customer_id NOT IN (SELECT id FROM customers);
-- Result should be: 0 (no orphaned transactions)

-- [ ] SELECT COUNT(*) FROM promises WHERE customer_id NOT IN (SELECT id FROM customers);
-- Result should be: 0 (no orphaned promises)
```

---

## 🔧 C# .NET Application Verification

### Project Build
- [ ] Project builds without errors: `dotnet build`
- [ ] No warnings in critical areas
- [ ] All NuGet packages restore: `dotnet restore`

### Configuration
- [ ] `appsettings.json` updated with MySQL connection string
- [ ] JWT Key is at least 32 characters
- [ ] CORS origins configured for frontend
- [ ] Database connection string format correct:
  ```
  Server=localhost;Port=3306;Database=RekovaDB;User=root;Password=***;
  ```

### Entity Framework Migrations
- [ ] Migrations created: `dotnet ef migrations list`
- [ ] Migrations applied: `dotnet ef database update`
- [ ] No migration errors

### Initial Startup
- [ ] Application starts without errors: `dotnet run`
- [ ] Console shows:
  ```
  Now listening on: http://0.0.0.0:5000
  ```
- [ ] Health endpoint responds: GET `http://localhost:5000/api/health`

---

## 🔐 API Authentication Verification

### JWT Token Generation
- [ ] Login endpoint works:
  ```bash
  POST /api/auth/login
  Body: {"username": "admin", "password": "password123"}
  ```
- [ ] Response includes valid JWT token
- [ ] Token format: `Header.Payload.Signature`

### Token Validation
- [ ] Token can be decoded at jwt.io
- [ ] Payload includes: id, name, email, role, exp
- [ ] Token expiration: 24 hours from issuance

### Protected Endpoints
- [ ] GET `/api/customers` without token → 401 Unauthorized
- [ ] GET `/api/customers` with valid token → 200 OK
- [ ] GET `/api/customers` with expired token → 401 Unauthorized
- [ ] GET `/api/customers` with malformed token → 401 Unauthorized

---

## 📊 API Endpoint Verification

### Authentication Endpoints
- [ ] POST `/api/auth/login` → 200, returns token
- [ ] GET `/api/auth/me` → 200, returns current user
- [ ] PUT `/api/auth/change-password` → 200, password changed
- [ ] POST `/api/auth/logout` → 200, logout successful

### Customer Endpoints
- [ ] GET `/api/customers` → 200, paginated list
- [ ] GET `/api/customers/1` → 200, customer details
- [ ] POST `/api/customers` (Admin/Supervisor) → 201, customer created
- [ ] PUT `/api/customers/1` (Admin/Supervisor) → 200, customer updated
- [ ] GET `/api/customers/assigned-to-me` → 200, assigned customers
- [ ] GET `/api/customers/dashboard/stats` → 200, dashboard stats

### Transaction Endpoints
- [ ] GET `/api/transactions` → 200, transaction list
- [ ] GET `/api/transactions/my-transactions` → 200, user's transactions

### Promise Endpoints
- [ ] GET `/api/promises` → 200, all promises
- [ ] GET `/api/promises/my-promises` → 200, user's promises
- [ ] POST `/api/promises` → 201, promise created
- [ ] PATCH `/api/promises/1/status` → 200, status updated

### Payment Endpoints
- [ ] POST `/api/payments/initiate` → 200, payment initiated

### Supervisor Endpoints (Admin/Supervisor only)
- [ ] GET `/api/supervisor/dashboard` → 200, dashboard data
- [ ] GET `/api/supervisor/officers/performance` → 200, officer stats
- [ ] POST `/api/supervisor/assignments/bulk` → 200, assignments made

### System Endpoints
- [ ] GET `/api/health` → 200, system healthy

---

## 🔄 Data Consistency Verification

### User Data
- [ ] All users migrated correctly
- [ ] Passwords verify correctly (bcrypt unchanged)
- [ ] Roles preserved: admin, supervisor, officer
- [ ] User permissions intact
- [ ] Sample user login works

### Customer Data
- [ ] All customers present
- [ ] Phone numbers unique
- [ ] Account numbers unique
- [ ] Loan balances accurate
- [ ] Arrears amounts correct
- [ ] Customer assignments preserved

### Transaction Data
- [ ] Transaction counts match
- [ ] Amounts preserved
- [ ] Status values valid: PENDING, SUCCESS, FAILED, EXPIRED
- [ ] Customer relationships intact
- [ ] Initiator (User) relationships intact

### Promise Data
- [ ] Promise counts match
- [ ] Dates preserved (UTC)
- [ ] Status values valid: PENDING, FULFILLED, BROKEN
- [ ] Customer references intact
- [ ] Creator (User) references intact

### Activity Data
- [ ] Activities logged correctly
- [ ] Timestamps accurate
- [ ] User references intact
- [ ] Action types preserved

---

## 🔗 Frontend Integration Verification

### API Configuration
- [ ] Frontend API URL updated to `http://localhost:5000/api`
- [ ] CORS errors resolved
- [ ] Requests reaching backend

### Authentication Flow
- [ ] Frontend login form submits correctly
- [ ] Backend returns valid token
- [ ] Token stored in localStorage
- [ ] Token used in subsequent requests

### Data Display
- [ ] Dashboard loads without errors
- [ ] Customer list displays from C# backend
- [ ] Customer details show correct data
- [ ] Transaction history shows data
- [ ] Promises display correctly

### User Interactions
- [ ] Officer can view assigned customers
- [ ] Officer can view transactions
- [ ] Officer can create promises
- [ ] Supervisor can view dashboard
- [ ] Supervisor can view officer performance
- [ ] Admin can manage all resources

---

## ⚡ Performance Verification

### Response Times
- [ ] Login response < 500ms
- [ ] Customer list response < 1s
- [ ] Dashboard stats response < 1s
- [ ] Single customer detail < 500ms

### Database Queries
- [ ] Queries use appropriate indexes
- [ ] No N+1 query problems
- [ ] Pagination working correctly
- [ ] Filtering/searching fast

### Memory & CPU
- [ ] Application memory usage stable
- [ ] CPU usage reasonable
- [ ] No memory leaks after extended use

---

## 🔒 Security Verification

### Authentication
- [ ] Passwords hashed (bcrypt)
- [ ] Tokens validated on each request
- [ ] Token expiration enforced
- [ ] No credentials in logs

### Authorization
- [ ] Role-based access control working
- [ ] Officers can't access admin endpoints
- [ ] Supervisor can't delete users
- [ ] Unauthorized requests return 403

### Data Protection
- [ ] No SQL injection vulnerabilities
- [ ] CORS properly configured
- [ ] Sensitive data not exposed in logs
- [ ] Database password not in code

---

## 📋 Final Checklist

### Pre-Launch
- [ ] All migrations completed successfully
- [ ] Data integrity verified
- [ ] API endpoints tested
- [ ] Frontend connected and working
- [ ] No errors in application logs
- [ ] Performance acceptable
- [ ] Security checks passed
- [ ] User passwords working

### Documentation
- [ ] README.md complete
- [ ] MIGRATION_GUIDE.md complete
- [ ] API documentation up to date
- [ ] Database schema documented
- [ ] Troubleshooting guide updated

### Backup & Recovery
- [ ] MongoDB backup created before migration
- [ ] MySQL database backed up
- [ ] Rollback procedure documented
- [ ] Recovery tested

### Handoff
- [ ] Team trained on new system
- [ ] Documentation shared
- [ ] Support contacts updated
- [ ] Monitoring configured
- [ ] Alerts configured

---

## ✅ Sign-Off

**Verification Completed:** _________________ (Date)
**Verified By:** _________________ (Name)
**System Status:** [ ] Ready for Production [ ] Needs Fixes
**Issues Found:** _________________ (If any)
**Resolution:** _________________ (If applicable)

---

## 📞 Contact & Support

For issues or questions during verification:
- Check MIGRATION_GUIDE.md troubleshooting section
- Review API endpoint documentation
- Check application logs: `logs/rekova_*.txt`
- MySQL error log: `/var/log/mysql/error.log`

