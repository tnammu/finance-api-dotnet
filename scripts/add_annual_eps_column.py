#!/usr/bin/env python3
"""
Simple script to add AnnualEPS column to YearlyDividends table
"""
import sqlite3
import sys
import os

DB_PATH = os.path.join(os.path.dirname(__file__), '..', 'dividends.db')

def add_column():
    print("Adding AnnualEPS column to YearlyDividends table...")

    try:
        conn = sqlite3.connect(DB_PATH)
        cursor = conn.cursor()

        # Check if column already exists
        cursor.execute('PRAGMA table_info(YearlyDividends)')
        columns = cursor.fetchall()
        column_names = [col[1] for col in columns]

        if 'AnnualEPS' in column_names:
            print("Column AnnualEPS already exists!")
            conn.close()
            return True

        # Add the column
        cursor.execute('ALTER TABLE YearlyDividends ADD COLUMN AnnualEPS REAL')
        conn.commit()

        print("SUCCESS: AnnualEPS column added successfully!")

        # Verify
        cursor.execute('PRAGMA table_info(YearlyDividends)')
        columns = cursor.fetchall()
        print("\nCurrent columns in YearlyDividends:")
        for col in columns:
            print(f"  - {col[1]} ({col[2]})")

        conn.close()
        return True

    except sqlite3.OperationalError as e:
        if 'database is locked' in str(e):
            print("\nERROR: Database is locked!")
            print("Please STOP your backend server first, then run this script again.")
            return False
        else:
            print(f"ERROR: {e}")
            return False
    except Exception as e:
        print(f"ERROR: {e}")
        return False

if __name__ == "__main__":
    success = add_column()
    sys.exit(0 if success else 1)
