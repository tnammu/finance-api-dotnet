"""
Bulk import all TSX stocks by fetching listings from TSX API
and calling the existing dividend analysis endpoint
"""

import requests
import time
import json
from datetime import datetime

# Configuration
API_BASE_URL = "http://localhost:5000"
TSX_LISTING_URL = "https://www.tsx.com/json/company-directory/search/tsx/%5E*"
DELAY_BETWEEN_REQUESTS = 0.5  # seconds

def fetch_tsx_listings():
    """Fetch all TSX stock listings from official API"""
    print("📥 Fetching TSX stock listings from official API...")
    print(f"   URL: {TSX_LISTING_URL}\n")

    try:
        headers = {
            'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36',
            'Accept': 'application/json'
        }

        response = requests.get(TSX_LISTING_URL, headers=headers, timeout=30)
        response.raise_for_status()

        data = response.json()

        stocks = []
        if 'results' in data:
            for company in data['results']:
                symbol = company.get('symbol', '')
                name = company.get('name', '')
                sector = company.get('sector', '')

                if symbol:
                    # Add .TO suffix for Yahoo Finance format
                    yahoo_symbol = f"{symbol}.TO"
                    stocks.append({
                        'symbol': yahoo_symbol,
                        'name': name,
                        'sector': sector,
                        'originalSymbol': symbol
                    })

        print(f"✅ Found {len(stocks)} TSX stocks\n")
        return stocks

    except Exception as e:
        print(f"❌ Error fetching TSX listings: {e}")
        return []

def import_stock(symbol, name, index, total):
    """Import a single stock by calling the dividend analysis API"""
    try:
        url = f"{API_BASE_URL}/api/dividends/analyze/{symbol}"

        print(f"[{index}/{total}] Processing {symbol} - {name}...", end=" ")

        response = requests.get(url, timeout=60)

        if response.status_code == 200:
            print("✅ Success")
            return True, None
        else:
            error_msg = f"HTTP {response.status_code}"
            print(f"❌ Failed: {error_msg}")
            return False, error_msg

    except requests.exceptions.Timeout:
        print("❌ Failed: Timeout")
        return False, "Timeout"
    except Exception as e:
        print(f"❌ Failed: {str(e)}")
        return False, str(e)

def bulk_import_tsx_stocks():
    """Main function to bulk import all TSX stocks"""
    print("=" * 80)
    print("🚀 TSX BULK STOCK IMPORT")
    print("=" * 80)
    print()

    start_time = datetime.now()

    # Step 1: Fetch stock listings
    stocks = fetch_tsx_listings()

    if not stocks:
        print("❌ No stocks to import. Exiting.")
        return

    # Step 2: Import each stock
    print(f"📊 Starting import of {len(stocks)} stocks...")
    print(f"⏱️  Estimated time: ~{len(stocks) * DELAY_BETWEEN_REQUESTS / 60:.1f} minutes")
    print("=" * 80)
    print()

    successful = []
    failed = []

    for index, stock in enumerate(stocks, 1):
        success, error = import_stock(
            stock['symbol'],
            stock['name'],
            index,
            len(stocks)
        )

        if success:
            successful.append(stock)
        else:
            failed.append({
                'symbol': stock['symbol'],
                'name': stock['name'],
                'error': error
            })

        # Delay between requests to avoid overwhelming the API
        if index < len(stocks):
            time.sleep(DELAY_BETWEEN_REQUESTS)

    # Step 3: Summary
    end_time = datetime.now()
    duration = (end_time - start_time).total_seconds()

    print()
    print("=" * 80)
    print("📈 IMPORT SUMMARY")
    print("=" * 80)
    print(f"Total stocks:     {len(stocks)}")
    print(f"✅ Successful:    {len(successful)} ({len(successful)/len(stocks)*100:.1f}%)")
    print(f"❌ Failed:        {len(failed)} ({len(failed)/len(stocks)*100:.1f}%)")
    print(f"⏱️  Duration:      {duration/60:.1f} minutes")
    print()

    if failed:
        print("Failed stocks:")
        for item in failed[:10]:  # Show first 10 failures
            print(f"  - {item['symbol']}: {item['error']}")
        if len(failed) > 10:
            print(f"  ... and {len(failed) - 10} more")
        print()

    # Save report
    report = {
        'startTime': start_time.isoformat(),
        'endTime': end_time.isoformat(),
        'durationSeconds': duration,
        'total': len(stocks),
        'successful': len(successful),
        'failed': len(failed),
        'successRate': len(successful)/len(stocks)*100 if stocks else 0,
        'failedStocks': failed
    }

    report_file = f"tsx_import_report_{datetime.now().strftime('%Y%m%d_%H%M%S')}.json"
    with open(report_file, 'w') as f:
        json.dump(report, f, indent=2)

    print(f"📄 Detailed report saved to: {report_file}")
    print("=" * 80)

if __name__ == "__main__":
    try:
        bulk_import_tsx_stocks()
    except KeyboardInterrupt:
        print("\n\n⚠️  Import interrupted by user")
    except Exception as e:
        print(f"\n\n❌ Unexpected error: {e}")
