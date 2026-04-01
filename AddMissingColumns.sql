-- Add missing columns to transactions table if they don't exist
ALTER TABLE `transactions`
ADD COLUMN IF NOT EXISTS `arrears_before` DECIMAL(18,2) NULL,
ADD COLUMN IF NOT EXISTS `arrears_after` DECIMAL(18,2) NULL,
ADD COLUMN IF NOT EXISTS `description` LONGTEXT NULL;

-- Add missing description column to activities table if it doesn't exist
ALTER TABLE `activities`  
ADD COLUMN IF NOT EXISTS `description` LONGTEXT NULL;

-- Add missing columns to promises table if they don't exist
ALTER TABLE `promises`
ADD COLUMN IF NOT EXISTS `created_by_user_id` INT NULL,
ADD COLUMN IF NOT EXISTS `created_by_name` VARCHAR(255) NULL,
ADD COLUMN IF NOT EXISTS `next_follow_up_date` DATETIME NULL,
ADD COLUMN IF NOT EXISTS `reminder_sent` BOOLEAN NULL DEFAULT FALSE;

-- Verify all columns were added
SELECT COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'RekovaDB' 
  AND TABLE_NAME IN ('transactions', 'activities', 'promises')
  AND COLUMN_NAME IN ('arrears_before', 'arrears_after', 'description', 'created_by_user_id', 'created_by_name', 'next_follow_up_date', 'reminder_sent')
ORDER BY TABLE_NAME, COLUMN_NAME;
