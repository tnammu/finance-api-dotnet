#!/usr/bin/env python3
"""
Force add AnnualEPS column with WAL checkpoint
"""
import sqlite3
import sys
import os
import time

DB_PATH = os.path.join(os.path.dirname(__file__), '..', 'dividends.db')

def add_column():
    print("Adding AnnualEPS column to YearlyDividends table...")

    max_retries = 3
    for attempt in range(max_retries):
        try:
            # Use a shorter timeout and isolation level
            conn = sqlite3.connect(DB_PATH, timeout=1.0, isolation_level=None)
            cursor = conn.cursor()

            # Try to checkpoint WAL
            try:
                cursor.execute('PRAGMA wal_checkpoint(TRUNCATE)')
                print("  Checkpointed WAL file")
            except:
                pass

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
            if 'database is locked' in str(e) or 'locked' in str(e):
                if attempt < max_retries - 1:
                    print(f"  Database locked, retrying in 2 seconds... (attempt {attempt + 1}/{max_retries})")
                    time.sleep(2)
                    continue
                else:
                    print("\nERROR: Database is still locked after multiple attempts!")
                    print("\nPlease ensure:")
                    print("  1. Backend server is STOPPED")
                    print("  2. Frontend is CLOSED (browser tabs)")
                    print("  3. No other processes are accessing dividends.db")
                    return False
            else:
                print(f"ERROR: {e}")
                return False
        except Exception as e:
            print(f"ERROR: {e}")
            return False

    return False

if __name__ == "__main__":
    success = add_column()
    sys.exit(0 if success else 1)
