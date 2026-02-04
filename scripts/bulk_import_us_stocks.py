"""
Bulk import all NASDAQ and other US exchange stocks
Fetches listings from NASDAQ FTP server and imports via API
Implements rate limiting to avoid hitting Yahoo Finance API limits
"""

import sys
import io
# Set UTF-8 encoding for Windows console
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding='utf-8', errors='replace')

import requests
import time
import json
from datetime import datetime
import urllib.request

# Configuration
API_BASE_URL = "http://localhost:5000"
NASDAQ_FTP_URL = "ftp://ftp.nasdaqtrader.com/symboldirectory/nasdaqlisted.txt"
OTHER_FTP_URL = "ftp://ftp.nasdaqtrader.com/symboldirectory/otherlisted.txt"

# Rate limiting: 2 seconds between requests = 1800 requests/hour (safe for Yahoo Finance)
DELAY_BETWEEN_REQUESTS = 2.0  # seconds

# Batch progress reporting
PROGRESS_REPORT_INTERVAL = 50  # Report progress every 50 stocks

def fetch_nasdaq_listings():
    """Fetch NASDAQ listed stocks from FTP"""
    print(f"📥 Fetching NASDAQ listings from FTP...")

    try:
        response = urllib.request.urlopen(NASDAQ_FTP_URL)
        content = response.read().decode('utf-8')

        stocks = []
        lines = content.strip().split('\n')

        # Skip header and footer
        for line in lines[1:-1]:  # First line is header, last line is footer
            if not line.strip():
                continue

            parts = line.split('|')
            if len(parts) < 8:
                continue

            symbol = parts[0].strip()
            name = parts[1].strip()
            market_category = parts[2].strip()
            test_issue = parts[3].strip()
            financial_status = parts[4].strip()

            # Skip test issues and invalid symbols
            if test_issue == 'Y':
                continue
            if '$' in symbol or '.' in symbol:  # Skip special symbols
                continue
            if len(symbol) > 5:  # Skip very long symbols (usually test/special)
                continue

            stocks.append({
                'symbol': symbol,
                'name': name,
                'exchange': 'NASDAQ',
                'market_category': market_category,
                'financial_status': financial_status
            })

        print(f"✓ Found {len(stocks)} NASDAQ stocks")
        return stocks

    except Exception as e:
        print(f"❌ Error fetching NASDAQ listings: {e}")
        return []


def fetch_other_listings():
    """Fetch NYSE, AMEX and other exchange stocks from FTP"""
    print(f"📥 Fetching other US exchange listings from FTP...")

    try:
        response = urllib.request.urlopen(OTHER_FTP_URL)
        content = response.read().decode('utf-8')

        stocks = []
        lines = content.strip().split('\n')

        # Skip header and footer
        for line in lines[1:-1]:
            if not line.strip():
                continue

            parts = line.split('|')
            if len(parts) < 7:
                continue

            symbol = parts[0].strip()
            name = parts[1].strip()
            exchange = parts[2].strip()  # A=NYSE MKT, N=NYSE, P=NYSE ARCA, Z=BATS, etc.
            test_issue = parts[4].strip()

            # Map exchange codes
            exchange_map = {
                'A': 'NYSE MKT',
                'N': 'NYSE',
                'P': 'NYSE ARCA',
                'Z': 'BATS',
                'V': 'IEX'
            }
            exchange_name = exchange_map.get(exchange, f'Other-{exchange}')

            # Skip test issues and invalid symbols
            if test_issue == 'Y':
                continue
            if '$' in symbol or '^' in symbol:  # Skip special symbols
                continue
            if len(symbol) > 5:
                continue

            stocks.append({
                'symbol': symbol,
                'name': name,
                'exchange': exchange_name,
                'exchange_code': exchange
            })

        print(f"✓ Found {len(stocks)} stocks from other exchanges")
        return stocks

    except Exception as e:
        print(f"❌ Error fetching other listings: {e}")
        return []


def import_stock(stock, index, total):
    """Import a single stock via API"""
    symbol = stock['symbol']

    try:
        url = f"{API_BASE_URL}/api/dividends/analyze/{symbol}"
        response = requests.get(url, timeout=60)

        if response.status_code == 200:
            return {'success': True, 'stock': stock}
        else:
            error_msg = f"HTTP {response.status_code}"
            try:
                error_data = response.json()
                if 'error' in error_data:
                    error_msg = error_data['error']
            except:
                pass
            return {'success': False, 'stock': stock, 'error': error_msg}

    except requests.exceptions.Timeout:
        return {'success': False, 'stock': stock, 'error': 'Timeout after 60s'}
    except Exception as e:
        return {'success': False, 'stock': stock, 'error': str(e)}


def bulk_import_us_stocks():
    """Main function to bulk import all US stocks"""
    print("=" * 80)
    print("🇺🇸 US STOCK BULK IMPORT")
    print("=" * 80)
    print(f"Started at: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    print(f"Rate limit: {DELAY_BETWEEN_REQUESTS}s between requests")
    print()

    # Step 1: Fetch all listings
    nasdaq_stocks = fetch_nasdaq_listings()
    other_stocks = fetch_other_listings()

    all_stocks = nasdaq_stocks + other_stocks
    total_stocks = len(all_stocks)

    print()
    print(f"📊 Total stocks to import: {total_stocks}")
    print(f"   - NASDAQ: {len(nasdaq_stocks)}")
    print(f"   - Other exchanges: {len(other_stocks)}")
    print()

    estimated_time = (total_stocks * DELAY_BETWEEN_REQUESTS) / 3600
    print(f"⏱️  Estimated time: {estimated_time:.1f} hours")
    print()
    print("=" * 80)
    print()

    # Step 2: Import each stock with rate limiting
    successful = []
    failed = []
    start_time = time.time()

    for index, stock in enumerate(all_stocks, 1):
        symbol = stock['symbol']
        exchange = stock['exchange']

        # Progress indicator
        progress = (index / total_stocks) * 100
        print(f"[{index}/{total_stocks}] ({progress:.1f}%) {symbol:6s} | {exchange:12s}", end='', flush=True)

        # Import the stock
        result = import_stock(stock, index, total_stocks)

        if result['success']:
            successful.append(result['stock'])
            print(" ✓ Success")
        else:
            failed.append({'symbol': symbol, 'error': result['error']})
            print(f" ✗ Failed: {result['error']}")

        # Progress report every N stocks
        if index % PROGRESS_REPORT_INTERVAL == 0:
            elapsed = time.time() - start_time
            rate = index / elapsed if elapsed > 0 else 0
            remaining = (total_stocks - index) / rate if rate > 0 else 0
            print()
            print(f"   📈 Progress: {index}/{total_stocks} | Success: {len(successful)} | Failed: {len(failed)}")
            print(f"   ⏱️  Elapsed: {elapsed/60:.1f}m | Remaining: {remaining/60:.1f}m | Rate: {rate*60:.1f}/min")
            print()

        # Rate limiting (except for last stock)
        if index < total_stocks:
            time.sleep(DELAY_BETWEEN_REQUESTS)

    # Step 3: Generate final report
    total_time = time.time() - start_time
    success_rate = (len(successful) / total_stocks * 100) if total_stocks > 0 else 0

    report = {
        'timestamp': datetime.now().isoformat(),
        'summary': {
            'total_stocks': total_stocks,
            'successful': len(successful),
            'failed': len(failed),
            'success_rate': round(success_rate, 2),
            'total_time_seconds': round(total_time, 2),
            'total_time_minutes': round(total_time / 60, 2),
            'total_time_hours': round(total_time / 3600, 2),
            'rate_per_minute': round(total_stocks / (total_time / 60), 2) if total_time > 0 else 0
        },
        'exchange_breakdown': {
            'nasdaq': len([s for s in successful if s['exchange'] == 'NASDAQ']),
            'nyse': len([s for s in successful if s.get('exchange_code') == 'N']),
            'nyse_mkt': len([s for s in successful if s.get('exchange_code') == 'A']),
            'nyse_arca': len([s for s in successful if s.get('exchange_code') == 'P']),
            'other': len([s for s in successful if s['exchange'] not in ['NASDAQ'] and s.get('exchange_code') not in ['N', 'A', 'P']])
        },
        'successful_stocks': successful,
        'failed_stocks': failed
    }

    # Save report
    report_filename = f"us_import_report_{datetime.now().strftime('%Y%m%d_%H%M%S')}.json"
    with open(report_filename, 'w') as f:
        json.dump(report, f, indent=2)

    # Print final summary
    print()
    print("=" * 80)
    print("📊 FINAL REPORT")
    print("=" * 80)
    print(f"Total stocks:     {total_stocks}")
    print(f"✓ Successful:     {len(successful)} ({success_rate:.1f}%)")
    print(f"✗ Failed:         {len(failed)} ({100-success_rate:.1f}%)")
    print()
    print(f"⏱️  Total time:     {total_time/3600:.2f} hours ({total_time/60:.1f} minutes)")
    print(f"📈 Import rate:    {report['summary']['rate_per_minute']:.1f} stocks/minute")
    print()
    print("Exchange breakdown (successful):")
    for exchange, count in report['exchange_breakdown'].items():
        print(f"  - {exchange.upper():12s}: {count}")
    print()
    print(f"📄 Full report saved to: {report_filename}")
    print("=" * 80)

    # Print sample failures (first 10)
    if failed:
        print()
        print("Sample failures (first 10):")
        for fail in failed[:10]:
            print(f"  - {fail['symbol']:6s}: {fail['error']}")
        if len(failed) > 10:
            print(f"  ... and {len(failed) - 10} more (see report file)")

    print()
    print("✅ Bulk import complete!")

    return report


if __name__ == "__main__":
    try:
        report = bulk_import_us_stocks()
    except KeyboardInterrupt:
        print()
        print()
        print("⚠️  Import interrupted by user")
    except Exception as e:
        print()
        print(f"❌ Fatal error: {e}")
        import traceback
        traceback.print_exc()
