-- Add AnnualEPS column to YearlyDividends table for accurate payout ratio calculation
-- Run this migration before using the updated Python script

-- Check if column already exists first (SQLite doesn't have IF NOT EXISTS for ALTER TABLE)
PRAGMA table_info(YearlyDividends);

-- Add the column (run this if AnnualEPS doesn't exist in the above output)
ALTER TABLE YearlyDividends ADD COLUMN AnnualEPS REAL;

-- Verify the column was added
PRAGMA table_info(YearlyDividends);

SELECT 'Migration complete! AnnualEPS column added to YearlyDividends table.' AS Status;
