-- Rekova System Database Migration Script
-- From MongoDB to MySQL
-- Execute this script in MySQL Workbench or MySQL CLI

-- ============================================
-- CREATE DATABASE
-- ============================================
CREATE DATABASE IF NOT EXISTS `RekovaDB` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE `RekovaDB`;

-- ============================================
-- USERS TABLE
-- ============================================
CREATE TABLE `users` (
  `id` INT AUTO_INCREMENT PRIMARY KEY,
  `username` VARCHAR(100) NOT NULL UNIQUE,
  `email` VARCHAR(255) NOT NULL UNIQUE,
  `password` VARCHAR(255) NOT NULL COMMENT 'bcrypt hashed',
  `first_name` VARCHAR(100),
  `last_name` VARCHAR(100),
  `phone` VARCHAR(20),
  `role` VARCHAR(50) NOT NULL DEFAULT 'officer' COMMENT 'admin, supervisor, officer',
  `loan_type` VARCHAR(100),
  `department` VARCHAR(100) DEFAULT 'Collections',
  `employee_id` VARCHAR(50),
  `assigned_customers_count` INT DEFAULT 0,
  `efficiency` DECIMAL(10, 2) DEFAULT 0,
  `can_manage_users` BOOLEAN DEFAULT FALSE,
  `can_approve_transactions` BOOLEAN DEFAULT FALSE,
  `can_view_all_performance` BOOLEAN DEFAULT FALSE,
  `can_export_data` BOOLEAN DEFAULT FALSE,
  `can_manage_settings` BOOLEAN DEFAULT FALSE,
  `transaction_limit` DECIMAL(15, 2) DEFAULT 50000,
  `is_active` BOOLEAN DEFAULT TRUE,
  `last_login` DATETIME,
  `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  
  INDEX `idx_username` (`username`),
  INDEX `idx_email` (`email`),
  INDEX `idx_is_active` (`is_active`),
  INDEX `idx_role` (`role`),
  INDEX `idx_created_at` (`created_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ============================================
-- CUSTOMERS TABLE
-- ============================================
CREATE TABLE `customers` (
  `id` INT AUTO_INCREMENT PRIMARY KEY,
  `customer_internal_id` VARCHAR(50) NOT NULL UNIQUE,
  `customer_id` VARCHAR(50) NOT NULL UNIQUE,
  `phone_number` VARCHAR(20) NOT NULL UNIQUE,
  `name` VARCHAR(255) NOT NULL,
  `account_number` VARCHAR(50) NOT NULL UNIQUE,
  `loan_balance` DECIMAL(15, 2) NOT NULL DEFAULT 0,
  `arrears` DECIMAL(15, 2) NOT NULL DEFAULT 0,
  `total_repayments` DECIMAL(15, 2) DEFAULT 0,
  `email` VARCHAR(255),
  `national_id` VARCHAR(20),
  `last_payment_date` DATETIME,
  `last_contact_date` DATETIME,
  `status` VARCHAR(50),
  `is_active` BOOLEAN DEFAULT TRUE,
  `created_by` VARCHAR(255),
  `created_by_user_id` INT,
  `assigned_to_user_id` INT,
  `promise_count` INT DEFAULT 0,
  `fulfilled_promise_count` INT DEFAULT 0,
  `last_promise_date` DATETIME,
  `promise_fulfillment_rate` DECIMAL(5, 2) DEFAULT 0,
  `loan_type` VARCHAR(100),
  `address` VARCHAR(255),
  `has_outstanding_promise` BOOLEAN DEFAULT FALSE,
  `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  
  FOREIGN KEY `fk_customer_assigned_to_user` (`assigned_to_user_id`) REFERENCES `users`(`id`) ON DELETE SET NULL,
  FOREIGN KEY `fk_customer_created_by_user` (`created_by_user_id`) REFERENCES `users`(`id`) ON DELETE SET NULL,
  
  INDEX `idx_customer_internal_id` (`customer_internal_id`),
  INDEX `idx_customer_id` (`customer_id`),
  INDEX `idx_phone_number` (`phone_number`),
  INDEX `idx_is_active` (`is_active`),
  INDEX `idx_assigned_to_user_id` (`assigned_to_user_id`),
  INDEX `idx_loan_type` (`loan_type`),
  INDEX `idx_created_at` (`created_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ============================================
-- TRANSACTIONS TABLE
-- ============================================
CREATE TABLE `transactions` (
  `id` INT AUTO_INCREMENT PRIMARY KEY,
  `transaction_internal_id` VARCHAR(50) NOT NULL UNIQUE,
  `transaction_id` VARCHAR(50) NOT NULL UNIQUE,
  `customer_id` INT NOT NULL,
  `customer_internal_id` VARCHAR(50) NOT NULL,
  `phone_number` VARCHAR(20) NOT NULL,
  `amount` DECIMAL(15, 2) NOT NULL,
  `description` VARCHAR(500) DEFAULT 'Loan Repayment',
  `status` VARCHAR(50) NOT NULL DEFAULT 'PENDING' COMMENT 'PENDING, SUCCESS, FAILED, CANCELLED, EXPIRED',
  `loan_balance_before` DECIMAL(15, 2) NOT NULL,
  `loan_balance_after` DECIMAL(15, 2) NOT NULL,
  `arrears_before` DECIMAL(15, 2) NOT NULL,
  `arrears_after` DECIMAL(15, 2) NOT NULL,
  `payment_method` VARCHAR(50) DEFAULT 'MPESA' COMMENT 'MPESA, CASH, BANK_TRANSFER, WHATSAPP',
  `initiated_by` VARCHAR(255),
  `initiated_by_user_id` INT,
  `mpesa_receipt_number` VARCHAR(50),
  `processed_at` DATETIME,
  `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  
  FOREIGN KEY `fk_transaction_customer` (`customer_id`) REFERENCES `customers`(`id`) ON DELETE CASCADE,
  FOREIGN KEY `fk_transaction_initiated_by_user` (`initiated_by_user_id`) REFERENCES `users`(`id`) ON DELETE SET NULL,
  
  INDEX `idx_transaction_internal_id` (`transaction_internal_id`),
  INDEX `idx_transaction_id` (`transaction_id`),
  INDEX `idx_customer_id` (`customer_id`),
  INDEX `idx_status` (`status`),
  INDEX `idx_created_at` (`created_at`),
  INDEX `idx_phone_number` (`phone_number`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ============================================
-- PROMISES TABLE
-- ============================================
CREATE TABLE `promises` (
  `id` INT AUTO_INCREMENT PRIMARY KEY,
  `promise_id` VARCHAR(50) NOT NULL UNIQUE,
  `customer_id` INT NOT NULL,
  `customer_name` VARCHAR(255) NOT NULL,
  `phone_number` VARCHAR(20) NOT NULL,
  `promise_amount` DECIMAL(15, 2) NOT NULL,
  `promise_date` DATETIME NOT NULL,
  `promise_type` VARCHAR(50) DEFAULT 'FULL_PAYMENT' COMMENT 'FULL_PAYMENT, PARTIAL_PAYMENT, SETTLEMENT, PAYMENT_PLAN',
  `status` VARCHAR(50) NOT NULL DEFAULT 'PENDING' COMMENT 'PENDING, FULFILLED, BROKEN, RESCHEDULED, CANCELLED',
  `fulfillment_amount` DECIMAL(15, 2) DEFAULT 0,
  `fulfillment_date` DATETIME,
  `notes` VARCHAR(500),
  `created_by_user_id` INT NOT NULL,
  `created_by_name` VARCHAR(255) NOT NULL,
  `reminder_sent` BOOLEAN DEFAULT FALSE,
  `next_follow_up_date` DATETIME,
  `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  
  FOREIGN KEY `fk_promise_customer` (`customer_id`) REFERENCES `customers`(`id`) ON DELETE CASCADE,
  FOREIGN KEY `fk_promise_created_by_user` (`created_by_user_id`) REFERENCES `users`(`id`) ON DELETE CASCADE,
  
  INDEX `idx_promise_id` (`promise_id`),
  INDEX `idx_customer_id` (`customer_id`),
  INDEX `idx_status` (`status`),
  INDEX `idx_promise_date` (`promise_date`),
  INDEX `idx_next_follow_up_date` (`next_follow_up_date`),
  INDEX `idx_created_at` (`created_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ============================================
-- ACTIVITIES TABLE
-- ============================================
CREATE TABLE `activities` (
  `id` INT AUTO_INCREMENT PRIMARY KEY,
  `user_id` INT NOT NULL,
  `action` VARCHAR(100) NOT NULL,
  `description` VARCHAR(500) NOT NULL,
  `resource_type` VARCHAR(50),
  `resource_id` INT,
  `customer_id` INT,
  `transaction_status` VARCHAR(50),
  `amount` DECIMAL(15, 2),
  `payment_method` VARCHAR(50),
  `ip_address` VARCHAR(50),
  `user_agent` VARCHAR(500),
  `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  
  FOREIGN KEY `fk_activity_user` (`user_id`) REFERENCES `users`(`id`) ON DELETE CASCADE,
  FOREIGN KEY `fk_activity_customer` (`customer_id`) REFERENCES `customers`(`id`) ON DELETE SET NULL,
  
  INDEX `idx_user_id` (`user_id`),
  INDEX `idx_customer_id` (`customer_id`),
  INDEX `idx_action` (`action`),
  INDEX `idx_created_at` (`created_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ============================================
-- COMMENTS TABLE
-- ============================================
CREATE TABLE `comments` (
  `id` INT AUTO_INCREMENT PRIMARY KEY,
  `customer_id` INT NOT NULL,
  `user_id` INT NOT NULL,
  `text` VARCHAR(1000) NOT NULL,
  `comment_type` VARCHAR(50),
  `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  
  FOREIGN KEY `fk_comment_customer` (`customer_id`) REFERENCES `customers`(`id`) ON DELETE CASCADE,
  FOREIGN KEY `fk_comment_user` (`user_id`) REFERENCES `users`(`id`) ON DELETE CASCADE,
  
  INDEX `idx_customer_id` (`customer_id`),
  INDEX `idx_user_id` (`user_id`),
  INDEX `idx_created_at` (`created_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ============================================
-- VERIFICATION QUERIES
-- ============================================
-- Run these after creating the schema and populating data
-- SELECT COUNT(*) as user_count FROM users;
-- SELECT COUNT(*) as customer_count FROM customers;
-- SELECT COUNT(*) as transaction_count FROM transactions;
-- SELECT COUNT(*) as promise_count FROM promises;
-- SELECT COUNT(*) as activity_count FROM activities;
-- SELECT COUNT(*) as comment_count FROM comments;

-- Show table structure
-- SHOW TABLES;
-- DESC users;
-- DESC customers;
-- DESC transactions;
-- DESC promises;
-- DESC activities;
-- DESC comments;
