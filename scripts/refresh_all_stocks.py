"""
Refresh all stocks in the database to update missing fields
(CurrentPrice, PayoutRatio, DividendGrowthRate)
"""

import sqlite3
import os

# Get the database path
db_path = os.path.join(os.path.dirname(__file__), '..', 'dividends.db')

# Import the updater
from update_stocks_from_yahoo import StockDataUpdater

def refresh_all():
    # Get all symbols from database
    conn = sqlite3.connect(db_path)
    cursor = conn.cursor()

    cursor.execute("SELECT Symbol FROM DividendModels")
    symbols = [row[0] for row in cursor.fetchall()]
    conn.close()

    if not symbols:
        print("No stocks found in database")
        return

    print(f"Found {len(symbols)} stocks to refresh: {', '.join(symbols)}")
    print("-" * 50)

    updater = StockDataUpdater(db_path)
    updater.connect_db()

    success = 0
    failed = 0

    for symbol in symbols:
        print(f"\nRefreshing {symbol}...")
        if updater.add_or_update_single_stock(symbol):
            success += 1
        else:
            failed += 1

    updater.close()

    print("\n" + "=" * 50)
    print(f"Refresh complete: {success} succeeded, {failed} failed")

if __name__ == "__main__":
    refresh_all()
