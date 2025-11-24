#!/usr/bin/env python3
"""
Database Migration: Add AnnualEPS column to YearlyDividends table
Run this AFTER stopping the backend server
"""

import sqlite3
import os

DB_PATH = os.path.join(os.path.dirname(os.path.dirname(__file__)), 'dividends.db')

def run_migration():
    print("\n" + "="*60)
    print("DATABASE MIGRATION: Add AnnualEPS Column")
    print("="*60 + "\n")

    if not os.path.exists(DB_PATH):
        print(f"✗ Database not found: {DB_PATH}")
        return False

    try:
        conn = sqlite3.connect(DB_PATH)
        cursor = conn.cursor()

        # Check current schema
        cursor.execute('PRAGMA table_info(YearlyDividends)')
        columns = cursor.fetchall()
        print("Current columns in YearlyDividends:")
        for col in columns:
            print(f"  - {col[1]} ({col[2]})")

        # Check if AnnualEPS already exists
        has_annual_eps = any(col[1] == 'AnnualEPS' for col in columns)

        if has_annual_eps:
            print("\n✓ AnnualEPS column already exists! No migration needed.")
            return True
        else:
            print("\n+ Adding AnnualEPS column...")
            cursor.execute('ALTER TABLE YearlyDividends ADD COLUMN AnnualEPS REAL')
            conn.commit()
            print("✓ AnnualEPS column added successfully!")

            # Verify
            cursor.execute('PRAGMA table_info(YearlyDividends)')
            columns = cursor.fetchall()
            print("\nUpdated columns:")
            for col in columns:
                print(f"  - {col[1]} ({col[2]})")

            return True

    except sqlite3.OperationalError as e:
        if "locked" in str(e):
            print("\n✗ Database is locked!")
            print("  Please stop the backend server first, then run this migration.")
        else:
            print(f"\n✗ Database error: {e}")
        return False
    except Exception as e:
        print(f"\n✗ Unexpected error: {e}")
        return False
    finally:
        if 'conn' in locals():
            conn.close()

if __name__ == "__main__":
    success = run_migration()
    if success:
        print("\n" + "="*60)
        print("✓ Migration completed successfully!")
        print("="*60)
        print("\nYou can now:")
        print("  1. Restart your backend server")
        print("  2. Run update_stocks_from_yahoo.py to collect historical EPS")
        print("  3. Use the dividend analysis feature with accurate payout ratios!")
        print()
    else:
        print("\n" + "="*60)
        print("✗ Migration failed")
        print("="*60 + "\n")
        exit(1)
