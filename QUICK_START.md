# RekovaBE-CSharp - Quick Start Testing Guide

## ⚡ Get Running in 5 Minutes

### Step 1: Set Environment Variables (1 minute)

**Windows Command Prompt (run as Administrator):**
```cmd
setx DB_SERVER localhost
setx DB_PORT 3306
setx DB_NAME RekovaDB
setx DB_USER root
setx DB_PASSWORD password_here
setx JWT_KEY "abcdefghijklmnopqrstuvwxyz1234567890"
setx JWT_ISSUER "RekovaAPI"
setx JWT_AUDIENCE "RekovaClient"
setx CORS_ORIGINS "http://localhost:5173,http://localhost:3000"
```

**Or add to .env file in project root:**
```
DB_SERVER=localhost
DB_PORT=3306
DB_NAME=RekovaDB
DB_USER=root
DB_PASSWORD=your_password
JWT_KEY=abcdefghijklmnopqrstuvwxyz1234567890
```

### Step 2: Create Database (1 minute)

Open MySQL:
```bash
mysql -u root -p
```

Create database:
```sql
CREATE DATABASE RekovaDB;
EXIT;
```

### Step 3: Run Backend (1 minute)

```bash
cd RekovaBE-CSharp
dotnet watch run
```

Expected output:
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
```

### Step 4: Test API (2 minutes)

**Option A: Swagger UI (Recommended)**
1. Open browser: http://localhost:5000/swagger
2. Click on any endpoint
3. Click "Try it out"
4. Click "Execute"

**Option B: cURL Commands**

```bash
# Health check
curl http://localhost:5000/api/health

# Login (to get token)
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"admin123"}'

# Save the token from response
# Use in other requests:
TOKEN="your_token_here"

# Get current user
curl -X GET http://localhost:5000/api/auth/me \
  -H "Authorization: Bearer $TOKEN"

# List customers
curl -X GET http://localhost:5000/api/customers \
  -H "Authorization: Bearer $TOKEN"
```

**Option C: Postman**
1. Create new request
2. Set method to GET or POST
3. Set URL: http://localhost:5000/api/auth/login
4. Set body (JSON):
   ```json
   {
     "username": "admin",
     "password": "admin123"
   }
   ```
5. Send and get token

---

## 🧪 Test Scenarios

### Test 1: Authentication
```bash
# 1. Login with default admin
POST http://localhost:5000/api/auth/login
{
  "username": "admin",
  "password": "admin123"
}

# Expected response:
{
  "success": true,
  "message": "Login successful",
  "data": {
    "token": "eyJhbGciOiJIUzI1NiIs...",
    "id": 1,
    "username": "admin",
    "role": "admin"
  }
}

# 2. Copy token and use in Authorization header
Authorization: Bearer <your_token_here>
```

### Test 2: User Management
```bash
# 1. Create new user
POST http://localhost:5000/api/auth/register
Headers: Authorization: Bearer <token>
{
  "username": "officer1",
  "email": "officer1@rekova.local",
  "password": "password123",
  "firstName": "John",
  "lastName": "Doe",
  "role": "officer"
}

# 2. Get all users
GET http://localhost:5000/api/auth/users
Headers: Authorization: Bearer <token>

# Expected: List of all users
```

### Test 3: Customer Management
```bash
# 1. Create customer
POST http://localhost:5000/api/customers
Headers: Authorization: Bearer <token>
{
  "name": "Jane Smith",
  "phoneNumber": "+254712345678",
  "customerId": "CST001",
  "accountNumber": "ACC001",
  "loanBalance": 50000,
  "arrears": 10000,
  "loanType": "Personal Loan"
}

# 2. List customers
GET http://localhost:5000/api/customers
Headers: Authorization: Bearer <token>

# 3. Find by phone
GET http://localhost:5000/api/customers/phone/%2B254712345678
Headers: Authorization: Bearer <token>

# 4. Update customer
PUT http://localhost:5000/api/customers/1
Headers: Authorization: Bearer <token>
{
  "loanBalance": 40000,
  "arrears": 8000
}
```

### Test 4: Comments
```bash
# 1. Add comment
POST http://localhost:5000/api/comments/customer/1
Headers: Authorization: Bearer <token>
{
  "text": "Customer paid partial amount today"
}

# 2. Get comments
GET http://localhost:5000/api/comments/customer/1
Headers: Authorization: Bearer <token>

# Expected: List of comments with timestamps
```

### Test 5: Reports
```bash
# 1. Summary report
GET http://localhost:5000/api/reports/summary
Headers: Authorization: Bearer <token>

# 2. Customer report
GET http://localhost:5000/api/reports/customers
Headers: Authorization: Bearer <token>

# 3. Transaction report
GET http://localhost:5000/api/reports/transactions
Headers: Authorization: Bearer <token>

# 4. Performance report (admin/supervisor only)
GET http://localhost:5000/api/reports/performance
Headers: Authorization: Bearer <token>
```

---

## 🐛 Troubleshooting

### Issue: "Database connection failed"
```
Error: Unable to connect to database at localhost:3306
```

**Solution:**
1. Check MySQL running: `mysql -u root -p`
2. Verify database exists: `SHOW DATABASES;`
3. Check connection string in environment variables

### Issue: "JWT Key must be at least 32 characters"
```
Error: JWT Key must be at least 32 characters long
```

**Solution:**
Generate a long key and set it:
```bash
setx JWT_KEY "abcdefghijklmnopqrstuvwxyz1234567890abcdefghijklmnop"
```

### Issue: "Port 5000 already in use"
```
Error: Address [::]:5000 already in use
```

**Solution:**
```bash
# Find process on port
netstat -ano | findstr :5000

# Kill process (replace PID)
taskkill /PID 1234 /F
```

### Issue: "Access denied" when creating users/customers
```
Error: 403 Forbidden
```

**Solution:**
Make sure your token's role is "admin" or "supervisor". Check token in https://jwt.io

---

## 📊 Expected API Response Structure

All successful responses return:
```json
{
  "success": true,
  "message": "Operation successful",
  "data": {
    // Response data here
  }
}
```

All error responses return:
```json
{
  "success": false,
  "message": "Error description here"
}
```

---

## ✅ Verification Checklist

Mark these off as you test:

- [ ] Backend starts without errors
- [ ] Health check returns 200 OK
- [ ] Login works with default admin credentials
- [ ] Can create new user
- [ ] Can create customer
- [ ] Can create comment
- [ ] Can list customers
- [ ] Can view reports
- [ ] Can update customer
- [ ] Can delete user
- [ ] Swagger UI accessible
- [ ] All endpoints return valid JSON

---

## 🚀 Next Steps

### After Verification:
1. ✅ Stop backend (Ctrl+C)
2. ✅ Review SETUP_GUIDE.md for production config
3. ✅ Connect your frontend to http://localhost:5000
4. ✅ Test end-to-end flows
5. ✅ Deploy to production when ready

### Additional Configuration:
- Set up M-Pesa sandbox credentials for testing payments
- Configure email notifications (if needed)
- Set up SSL certificate for HTTPS
- Configure production database server
- Set up log rotation and monitoring

---

## 📝 Quick API Reference

| Method | Endpoint | Purpose |
|--------|----------|---------|
| POST | /api/auth/login | User login |
| POST | /api/auth/register | Create user |
| GET | /api/auth/me | Get current user |
| GET | /api/customers | List customers |
| POST | /api/customers | Create customer |
| PUT | /api/customers/{id} | Update customer |
| GET | /api/comments/customer/{id} | Get comments |
| POST | /api/comments/customer/{id} | Add comment |
| GET | /api/reports/summary | Summary statistics |
| GET | /api/health | API health check |

---

## 💡 Tips

1. **Keep terminal open** - Your API runs in the foreground with `dotnet watch run`
2. **Save the token** - Same token works for multiple requests in testing
3. **Test Swagger first** - Swagger UI is the easiest way to test
4. **Check logs** - Look at console output for errors and debug information
5. **Use Postman** - Better for complex testing and saving requests

---

## 🎯 Success Indicators

You know everything works when:
- ✅ Swagger UI displays all endpoints
- ✅ Login endpoint returns a valid JWT token
- ✅ You can create customers with that token
- ✅ You can create comments
- ✅ Reports return data
- ✅ No 500 errors in responses
- ✅ All responses are valid JSON

---

**Status**: Ready to test immediately! 🚀

If you encounter any issues, check the logs in the project's `logs/` folder or ask for help with specific error messages.
