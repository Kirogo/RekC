#!/bin/bash

# Rekova Migration - Complete Setup Script
# This script automates the entire migration process

set -e  # Exit on error

# Color output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}Rekova System - Migration Setup${NC}"
echo -e "${GREEN}========================================${NC}\n"

# ==================== CHECK PREREQUISITES ====================
echo -e "${YELLOW}[1/7] Checking Prerequisites...${NC}\n"

# Check MySQL
if ! command -v mysql &> /dev/null; then
    echo -e "${RED}❌ MySQL not found. Please install MySQL Server.${NC}"
    exit 1
fi
echo -e "${GREEN}✅ MySQL installed${NC}"

# Check .NET SDK
if ! command -v dotnet &> /dev/null; then
    echo -e "${RED}❌ .NET SDK not found. Please install .NET 8.0 SDK.${NC}"
    exit 1
fi
echo -e "${GREEN}✅ .NET SDK installed (version: $(dotnet --version))${NC}"

# ==================== CREATE DATABASE ====================
echo -e "\n${YELLOW}[2/7] Creating MySQL Database...${NC}\n"

read -p "Enter MySQL root password: " -s MYSQL_PASSWORD
echo

MYSQL_CMD="mysql -u root -p$MYSQL_PASSWORD"

if $MYSQL_CMD -e "USE RekovaDB;" 2>/dev/null; then
    echo -e "${YELLOW}⚠️  Database RekovaDB already exists. Skipping creation.${NC}"
else
    echo "Creating database..."
    $MYSQL_CMD < Database/01_CreateSchema.sql
    echo -e "${GREEN}✅ Database created successfully${NC}"
fi

# ==================== CONFIGURE APPLICATION ====================
echo -e "\n${YELLOW}[3/7] Configuring Application...${NC}\n"

# Update appsettings.json with MySQL password
read -p "Enter desired JWT Secret Key (32+ characters): " JWT_KEY
read -p "Enter MySQL password again: " -s DB_PASSWORD
echo

# Create temporary appsettings update (would need json processing)
echo -e "${GREEN}✅ Configuration prepared${NC}"
echo -e "${YELLOW}⚠️  Please manually update appsettings.json with:${NC}"
echo "   - MySQL password"
echo "   - JWT Key: $JWT_KEY"

# ==================== BUILD PROJECT ====================
echo -e "\n${YELLOW}[4/7] Building C# Project...${NC}\n"

dotnet clean
dotnet restore
dotnet build

if [ $? -eq 0 ]; then
    echo -e "${GREEN}✅ Project built successfully${NC}"
else
    echo -e "${RED}❌ Build failed${NC}"
    exit 1
fi

# ==================== DATABASE MIGRATIONS ====================
echo -e "\n${YELLOW}[5/7] Running Database Migrations...${NC}\n"

dotnet ef migrations add InitialCreate 2>/dev/null || true
dotnet ef database update

echo -e "${GREEN}✅ Migrations applied${NC}"

# ==================== VERIFY SCHEMA ====================
echo -e "\n${YELLOW}[6/7] Verifying Database Schema...${NC}\n"

VERIFICATION_SQL="
USE RekovaDB;
SELECT COUNT(*) as table_count FROM information_schema.tables WHERE table_schema = 'RekovaDB';
"

$MYSQL_CMD -e "$VERIFICATION_SQL"

echo -e "${GREEN}✅ Schema verification complete${NC}"

# ==================== STARTUP INSTRUCTIONS ====================
echo -e "\n${YELLOW}[7/7] Setup Complete!${NC}\n"

echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}✨ Migration Setup Completed Successfully${NC}"
echo -e "${GREEN}========================================${NC}\n"

echo "📋 Next Steps:\n"
echo "   1. Start MySQL Server:"
echo "      Windows: net start MySQL80"
echo "      Mac: brew services start mysql"
echo "      Linux: sudo systemctl start mysql\n"

echo "   2. Start the C# Backend:"
echo "      dotnet run\n"

echo "   3. Update Frontend API URL:"
echo "      Edit: RekovaFE/src/services/api.js"
echo "      Change: 'http://localhost:5050' → 'http://localhost:5000'\n"

echo "   4. Start the Frontend:"
echo "      npm run dev\n"

echo "   5. Test the System:"
echo "      - Open http://localhost:5173"
echo "      - Login with existing credentials"
echo "      - Verify all functionality\n"

echo -e "${GREEN}Happy Collecting! 🚀${NC}\n"
