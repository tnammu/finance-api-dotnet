#!/usr/bin/env python3
"""
Apply commodity trading tables to the SQLite database
"""

import sqlite3
import os
import sys

def main():
    # Database path
    db_path = os.path.join(os.path.dirname(__file__), '..', 'dividends.db')
    sql_file = os.path.join(os.path.dirname(__file__), 'create_commodity_tables.sql')

    if not os.path.exists(db_path):
        print(f"Error: Database not found at {db_path}")
        sys.exit(1)

    if not os.path.exists(sql_file):
        print(f"Error: SQL file not found at {sql_file}")
        sys.exit(1)

    print(f"Connecting to database: {db_path}")

    try:
        # Connect to database
        conn = sqlite3.connect(db_path)
        cursor = conn.cursor()

        # Read SQL file
        with open(sql_file, 'r') as f:
            sql_script = f.read()

        # Execute SQL script
        print("Executing SQL script...")
        cursor.executescript(sql_script)

        # Commit changes
        conn.commit()

        print("[SUCCESS] Successfully created commodity trading tables!")
        print("[SUCCESS] Seeded default CME cost profiles")

        # Verify tables were created
        cursor.execute("SELECT name FROM sqlite_master WHERE type='table' AND name LIKE '%Commodit%' OR name LIKE '%Backtest%' OR name LIKE '%Cme%';")
        tables = cursor.fetchall()

        print(f"\nCreated tables:")
        for table in tables:
            print(f"  - {table[0]}")

        conn.close()

    except sqlite3.Error as e:
        print(f"[ERROR] SQLite error: {e}")
        sys.exit(1)
    except Exception as e:
        print(f"[ERROR] Error: {e}")
        sys.exit(1)

if __name__ == "__main__":
    main()