#!/usr/bin/env python3
"""
Bulk Import Stocks into Database
Imports stocks from JSON file with progress tracking
"""

import json
import time
from update_stocks_from_yahoo import StockDataUpdater
from datetime import datetime

def log(message):
    """Print log message"""
    print(f"[{datetime.now().strftime('%H:%M:%S')}] {message}")

def bulk_import(json_file='stocks_list.json', limit=None, skip_existing=True):
    """
    Import stocks from JSON file

    Args:
        json_file: Path to JSON file with stock list
        limit: Maximum number of stocks to import (None = all)
        skip_existing: Skip stocks that already exist in database
    """
    log("="*60)
    log("BULK STOCK IMPORT")
    log("="*60)

    # Load stock list
    try:
        with open(json_file, 'r') as f:
            data = json.load(f)
            stocks = data['stocks']
        log(f"✓ Loaded {len(stocks)} stocks from {json_file}")
    except Exception as e:
        log(f"✗ Error loading {json_file}: {e}")
        return

    # Apply limit if specified
    if limit:
        stocks = stocks[:limit]
        log(f"  Limited to {limit} stocks")

    # Initialize updater
    updater = StockDataUpdater("../dividends.db")
    if not updater.connect_db():
        log("✗ Failed to connect to database")
        return

    # Get existing symbols if skip_existing is True
    existing_symbols = set()
    if skip_existing:
        try:
            updater.cursor.execute("SELECT Symbol FROM DividendModels")
            existing_symbols = {row[0] for row in updater.cursor.fetchall()}
            log(f"  Found {len(existing_symbols)} existing stocks in database")
        except Exception as e:
            log(f"  Warning: Could not fetch existing symbols: {e}")

    # Import statistics
    stats = {
        'total': len(stocks),
        'skipped_existing': 0,
        'succeeded': 0,
        'failed': 0,
        'failed_symbols': []
    }

    log("\n" + "="*60)
    log("STARTING IMPORT")
    log("="*60 + "\n")

    start_time = time.time()

    # Process each stock
    for idx, stock_info in enumerate(stocks, 1):
        symbol = stock_info['symbol']

        # Progress indicator
        progress = f"[{idx}/{stats['total']}]"

        # Skip if exists
        if skip_existing and symbol in existing_symbols:
            log(f"{progress} ⊘ SKIP {symbol} (already exists)")
            stats['skipped_existing'] += 1
            continue

        log(f"{progress} → Processing {symbol} ({stock_info.get('name', 'Unknown')})...")

        # Process the stock
        success = updater.process_stock(symbol)

        if success:
            stats['succeeded'] += 1
            log(f"{progress} ✓ SUCCESS {symbol}")
        else:
            stats['failed'] += 1
            stats['failed_symbols'].append(symbol)
            log(f"{progress} ✗ FAILED {symbol}")

        # Rate limiting: Wait 0.5 seconds between requests
        if idx < stats['total']:
            time.sleep(0.5)

        # Progress summary every 10 stocks
        if idx % 10 == 0:
            elapsed = time.time() - start_time
            avg_time = elapsed / idx
            remaining = (stats['total'] - idx) * avg_time
            log(f"\n--- Progress: {idx}/{stats['total']} | "
                f"Success: {stats['succeeded']} | "
                f"Failed: {stats['failed']} | "
                f"Skipped: {stats['skipped_existing']} | "
                f"ETA: {remaining/60:.1f} min ---\n")

    # Close database
    updater.close_db()

    # Final summary
    elapsed = time.time() - start_time
    log("\n" + "="*60)
    log("IMPORT COMPLETE")
    log("="*60)
    log(f"Total Processed: {stats['total']}")
    log(f"✓ Succeeded: {stats['succeeded']}")
    log(f"✗ Failed: {stats['failed']}")
    log(f"⊘ Skipped (existing): {stats['skipped_existing']}")
    log(f"⏱  Time Elapsed: {elapsed/60:.1f} minutes")
    log(f"⚡ Avg per stock: {elapsed/stats['total']:.1f} seconds")

    if stats['failed_symbols']:
        log(f"\n❌ Failed symbols ({len(stats['failed_symbols'])}):")
        for symbol in stats['failed_symbols'][:20]:  # Show first 20
            log(f"  - {symbol}")
        if len(stats['failed_symbols']) > 20:
            log(f"  ... and {len(stats['failed_symbols']) - 20} more")

    # Save failed symbols for retry
    if stats['failed_symbols']:
        failed_file = f"failed_imports_{datetime.now().strftime('%Y%m%d_%H%M%S')}.txt"
        with open(failed_file, 'w') as f:
            for symbol in stats['failed_symbols']:
                f.write(f"{symbol}\n")
        log(f"\n💾 Saved failed symbols to {failed_file}")

    return stats

def import_from_symbols_file(symbols_file='symbols_list.txt', limit=None):
    """Import stocks from a simple text file with one symbol per line"""
    log("="*60)
    log(f"IMPORTING FROM {symbols_file}")
    log("="*60)

    try:
        with open(symbols_file, 'r') as f:
            symbols = [line.strip() for line in f if line.strip()]

        # Convert to JSON format
        stocks = [{'symbol': symbol, 'name': symbol, 'sector': 'Unknown', 'industry': 'Unknown', 'exchange': 'Unknown'}
                  for symbol in symbols]

        # Save to temporary JSON
        temp_json = 'temp_import.json'
        with open(temp_json, 'w') as f:
            json.dump({'stocks': stocks}, f)

        # Import
        return bulk_import(temp_json, limit=limit)

    except Exception as e:
        log(f"✗ Error: {e}")
        return None

if __name__ == '__main__':
    import sys

    # Check command line arguments
    if len(sys.argv) > 1:
        if sys.argv[1] == '--help':
            print("""
Usage:
  python bulk_import_stocks.py [json_file] [limit]

Arguments:
  json_file  - Path to JSON file with stocks (default: stocks_list.json)
  limit      - Maximum number of stocks to import (default: all)

Examples:
  python bulk_import_stocks.py                    # Import all from stocks_list.json
  python bulk_import_stocks.py stocks_list.json 50  # Import first 50
  python bulk_import_stocks.py --test             # Import only 10 for testing
            """)
            sys.exit(0)
        elif sys.argv[1] == '--test':
            # Test mode: Import only 10 stocks
            bulk_import('stocks_list.json', limit=10, skip_existing=False)
        else:
            # Custom file and/or limit
            json_file = sys.argv[1]
            limit = int(sys.argv[2]) if len(sys.argv) > 2 else None
            bulk_import(json_file, limit=limit)
    else:
        # Default: Import all stocks, skipping existing
        print("\n⚠️  This will import ALL stocks from stocks_list.json")
        print("   Existing stocks will be skipped.")
        print("   This may take several minutes.")
        print("\n   Use --test to import only 10 stocks for testing")
        print("   Use --help for more options\n")

        response = input("Continue? (y/N): ")
        if response.lower() == 'y':
            bulk_import('stocks_list.json', skip_existing=True)
        else:
            print("Cancelled.")
