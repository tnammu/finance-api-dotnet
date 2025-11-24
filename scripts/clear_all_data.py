#!/usr/bin/env python3
"""
Clear all dividend data from database
"""
import sqlite3
import os

DB_PATH = os.path.join(os.path.dirname(__file__), '..', 'dividends.db')

def clear_all_data():
    print("Clearing all dividend data from database...")

    try:
        conn = sqlite3.connect(DB_PATH)
        cursor = conn.cursor()

        # Delete in correct order (due to foreign keys)
        cursor.execute('DELETE FROM DividendPayments')
        payments_deleted = cursor.rowcount

        cursor.execute('DELETE FROM YearlyDividends')
        yearly_deleted = cursor.rowcount

        cursor.execute('DELETE FROM DividendModels')
        stocks_deleted = cursor.rowcount

        cursor.execute('DELETE FROM ApiUsageLogs')
        logs_deleted = cursor.rowcount

        conn.commit()

        print(f"✓ Deleted {stocks_deleted} stocks")
        print(f"✓ Deleted {payments_deleted} dividend payments")
        print(f"✓ Deleted {yearly_deleted} yearly summaries")
        print(f"✓ Deleted {logs_deleted} API usage logs")
        print("\nAll dividend data cleared successfully!")

        conn.close()
        return True

    except Exception as e:
        print(f"ERROR: {e}")
        return False

if __name__ == "__main__":
    import sys

    response = input("Are you sure you want to delete ALL dividend data? (yes/no): ")
    if response.lower() == 'yes':
        success = clear_all_data()
        sys.exit(0 if success else 1)
    else:
        print("Cancelled.")
        sys.exit(0)