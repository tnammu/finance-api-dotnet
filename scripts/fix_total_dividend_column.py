#!/usr/bin/env python3
"""
Fix TotalDividend column type from TEXT to REAL
"""
import sqlite3
import os

DB_PATH = os.path.join(os.path.dirname(__file__), '..', 'dividends.db')

def fix_column_type():
    print("Fixing TotalDividend column type...")

    try:
        conn = sqlite3.connect(DB_PATH)
        cursor = conn.cursor()

        # SQLite doesn't support ALTER COLUMN TYPE directly
        # We need to recreate the table

        print("  1. Creating new table with correct schema...")
        cursor.execute('''
            CREATE TABLE YearlyDividends_new (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                DividendModelId INTEGER NOT NULL,
                Symbol TEXT NOT NULL,
                Year INTEGER NOT NULL,
                TotalDividend REAL NOT NULL DEFAULT 0,
                PaymentCount INTEGER NOT NULL DEFAULT 0,
                AnnualEPS REAL,
                FOREIGN KEY (DividendModelId) REFERENCES DividendModels(Id) ON DELETE CASCADE
            )
        ''')

        print("  2. Copying data to new table...")
        cursor.execute('''
            INSERT INTO YearlyDividends_new (Id, DividendModelId, Symbol, Year, TotalDividend, PaymentCount, AnnualEPS)
            SELECT Id, DividendModelId, Symbol, Year,
                   CAST(CASE WHEN TotalDividend = '' THEN '0' ELSE TotalDividend END AS REAL),
                   PaymentCount, AnnualEPS
            FROM YearlyDividends
        ''')

        print("  3. Dropping old table...")
        cursor.execute('DROP TABLE YearlyDividends')

        print("  4. Renaming new table...")
        cursor.execute('ALTER TABLE YearlyDividends_new RENAME TO YearlyDividends')

        print("  5. Creating indexes...")
        cursor.execute('CREATE UNIQUE INDEX idx_yearly_symbol_year ON YearlyDividends(Symbol, Year)')

        conn.commit()

        # Verify
        cursor.execute('PRAGMA table_info(YearlyDividends)')
        columns = cursor.fetchall()

        print("\nSUCCESS: YearlyDividends table schema updated:")
        for col in columns:
            print(f"  - {col[1]}: {col[2]}")

        conn.close()
        print("\nSUCCESS: TotalDividend column type fixed!")
        return True

    except Exception as e:
        print(f"\nERROR: {e}")
        return False

if __name__ == "__main__":
    import sys
    print("This will fix the TotalDividend column type from TEXT to REAL")
    print("Make sure your backend is STOPPED before running this.\n")

    response = input("Continue? (yes/no): ")
    if response.lower() == 'yes':
        success = fix_column_type()
        sys.exit(0 if success else 1)
    else:
        print("Cancelled.")
        sys.exit(0)
