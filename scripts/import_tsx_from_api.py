#!/usr/bin/env python3
"""
Import all real TSX stocks from the TMX company directory API.
Filters out ETFs, CDRs, preferred shares, debentures, warrants,
split corps, funds, and leveraged products.
Imports using the existing StockDataUpdater from update_stocks_from_yahoo.py.
"""

import sys
import os
import json
import time
import re

# Add scripts directory to path
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from update_stocks_from_yahoo import StockDataUpdater

try:
    import requests
except ImportError:
    print("Installing requests...")
    os.system(f"{sys.executable} -m pip install requests")
    import requests


TMX_API_URL = "https://www.tsx.com/json/company-directory/search/tsx/%5E*"

# Keywords in name that indicate non-stock instruments
EXCLUDE_NAME_PATTERNS = [
    r'\bETF\b',
    r'\bCDR\b',
    r'\bCAD Hedged\b',
    r'\bSplit Corp',
    r'\bIncome Fund\b',
    r'\bBond (Trust|Fund|ETF|Index)',
    r'\bMoney Market\b',
    r'\bBetaPro\b',
    r'\bLeveraged\b',
    r'\b(Bull|Bear) (ETF|Alternative)',
    r'\bIndex ETF\b',
    r'\bIndex Fund\b',
    r'\bCovered Call\b',
    r'\bMutual Fund\b',
    r'\bAlternative (Fund|ETF|Multi)',
    r'\bArbitrage Fund\b',
    r'\bLong.?Short\b',
    r'\bTarget \d{4}\b',         # Target date funds
    r'\bBullion\b',
    r'\bVIX\b',
    r'\bFutures ETF\b',
    r'\bPreferred Share (ETF|Index)\b',
    r'\bBond.*ETF\b',
    r'\bEquity.*ETF\b',
    r'\bDividend.*ETF\b',
    r'\bGrowth.*ETF\b',
    r'\bBalanced.*ETF\b',
    r'\bConservative.*ETF\b',
    r'\bVolatility.*ETF\b',
    r'\bInfrastructure.*ETF\b',
    r'\bAgriculture.*ETF\b',
    r'\bClean Energy.*ETF\b',
    r'\bBlockchain.*ETF\b',
    r'\bCash Management\b',
    r'\bCash Flow Kings\b',
    r'\bBitcoin (Fund|ETF)\b',
    r'\bEther.*(Fund|ETF)\b',
    r'\bSolana.*(Fund|ETF)\b',
    r'\bXRP.*(Fund|ETF)\b',
    r'\bCrypto.*(Fund|ETF)\b',
    r'\bESG.*ETF\b',
    r'\bMSCI.*ETF\b',
    r'\bS&P.*ETF\b',
    r'\bNasdaq.*ETF\b',
    r'\bRussell.*ETF\b',
    r'\bDow Jones.*ETF\b',
    r'\bTSX.*ETF\b',
    r'\bSPDR.*ETF\b',
    r'\bTactical.*ETF\b',
    r'\bPut Write\b',
    r'\bEnhanced.*ETF\b',
    r'\bSelection.*ETF\b',
    r'\bAll.Equity.*ETF\b',
    r'\bMulti.Asset\b',
    r'\bCLO ETF\b',
    r'\bSIA Focused\b',
    r'\bPremium Yield\b',
    r'\bMonthly Income ETF\b',
    r'\bUltra Short\b',
    r'\bDiscount Bond\b',
    r'\bAggregate Bond\b',
    r'\bCorporate Bond\b',
    r'\bGovernment Bond\b',
    r'\bProvincial Bond\b',
    r'\bFederal Bond\b',
    r'\bFloating Rate\b',
    r'\bHigh Yield.*ETF\b',
    r'\bReal Return\b',
    r'\bAnti.Beta\b',
    r'\bMarket Neutral\b',
]

# Symbol patterns to exclude
EXCLUDE_SYMBOL_PATTERNS = [
    r'\.PR\.',      # Preferred shares
    r'\.DB',        # Debentures
    r'\.WT',        # Warrants
    r'\.R$',        # Rights
    r'\.U$',        # USD-denominated units
    r'\.F$',        # Hedged versions
    r'\.T$',        # T-class distributions
    r'\.B$',        # B-class shares (keep some)
    r'\.V$',        # V-class
    r'\.L$',        # Accumulating class
]

# Known ETF provider prefixes (symbols that start with these are usually ETFs/funds)
ETF_PREFIXES = [
    'ZA', 'ZB', 'ZC', 'ZD', 'ZE', 'ZF', 'ZG', 'ZH', 'ZI', 'ZJ',
    'ZL', 'ZM', 'ZN', 'ZP', 'ZQ', 'ZR', 'ZS', 'ZT', 'ZU', 'ZV', 'ZW',
    'ZX', 'ZZ',  # BMO ETFs
    'XI', 'XA', 'XB', 'XC', 'XD', 'XE', 'XF', 'XG', 'XH',
    'XM', 'XR', 'XS', 'XT', 'XU',  # iShares ETFs
    'VA', 'VB', 'VC', 'VD', 'VE', 'VF', 'VG', 'VH', 'VI', 'VR', 'VS', 'VU', 'VX',  # Vanguard ETFs
    'HA', 'HB', 'HC', 'HD', 'HE', 'HF', 'HG', 'HH', 'HM', 'HN',
    'HP', 'HQ', 'HR', 'HU', 'HX',  # Horizons/Harvest ETFs
    'FI', 'FL', 'FN', 'FP',  # Fidelity/First Asset ETFs
    'CI',  # CI ETFs
    'TD',  # TD ETFs
    'RQ',  # RBC ETFs
    'DX',  # Dynamic ETFs
    'PD',  # Purpose ETFs
    'EQ',  # Evolve ETFs
    'MN',  # Mackenzie ETFs
]


def fetch_tsx_directory():
    """Fetch the complete TSX company directory from TMX API."""
    print("Fetching TSX company directory from TMX API...")
    try:
        headers = {
            'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36',
            'Accept': 'application/json',
        }
        response = requests.get(TMX_API_URL, headers=headers, timeout=30)
        response.raise_for_status()
        data = response.json()
        print(f"Fetched {data.get('length', '?')} listings from TMX")
        return data
    except Exception as e:
        print(f"Failed to fetch from TMX API: {e}")
        return None


def is_etf_or_fund(name, symbol):
    """Check if the listing is an ETF, fund, or non-stock instrument."""
    name_upper = name.upper()

    # Check name patterns
    for pattern in EXCLUDE_NAME_PATTERNS:
        if re.search(pattern, name, re.IGNORECASE):
            return True

    # If name ends with "Fund" or "ETF", it's almost certainly not a stock
    if re.search(r'\bFund$', name) or re.search(r'\bETF$', name):
        return True

    # If "Fund" is in the name and no company suffix (Inc., Corp., Ltd.), it's a fund
    if 'Fund' in name:
        company_suffixes = ['Inc.', 'Corp.', 'Ltd.', 'Limited', 'Company', 'Co.']
        has_company_suffix = any(s in name for s in company_suffixes)
        if not has_company_suffix:
            return True

    # Split Corps are structured products, not operating companies
    if 'Split Corp' in name or 'Split Banc' in name:
        return True

    # Income Trusts that aren't REITs
    if 'Income Trust' in name and 'Real Estate' not in name and 'REIT' not in name:
        return True

    # Bond Trusts
    if 'Bond Trust' in name:
        return True

    return False


def is_excluded_symbol(symbol):
    """Check if the symbol pattern indicates a non-common-stock instrument."""
    for pattern in EXCLUDE_SYMBOL_PATTERNS:
        if re.search(pattern, symbol):
            return True
    return False


def filter_real_stocks(data):
    """Convert all TMX listings to Yahoo Finance symbols."""
    if not data or 'results' not in data:
        return []

    all_stocks = []

    excluded = 0
    for entry in data['results']:
        symbol = entry['symbol']
        name = entry['name']

        # Skip preferred shares, debentures, warrants, etc. by symbol pattern
        if is_excluded_symbol(symbol):
            excluded += 1
            continue

        # The symbol on TMX doesn't have .TO suffix; add it for Yahoo Finance
        yahoo_symbol = f"{symbol}.TO" if '.TO' not in symbol else symbol

        # Replace TMX special characters for Yahoo compatibility
        yahoo_symbol = yahoo_symbol.replace('.UN.TO', '-UN.TO')
        yahoo_symbol = yahoo_symbol.replace('.A.TO', '-A.TO')
        yahoo_symbol = yahoo_symbol.replace('.B.TO', '-B.TO')
        yahoo_symbol = yahoo_symbol.replace('.X.TO', '-X.TO')

        all_stocks.append({
            'symbol': yahoo_symbol,
            'name': name,
            'tmx_symbol': symbol,
        })

    print(f"Total listings: {len(all_stocks) + excluded} | Excluded (preferred/warrants/debentures): {excluded} | Importing: {len(all_stocks)}")

    return all_stocks


def main():
    # Try to fetch from API
    data = fetch_tsx_directory()

    # Fallback: try reading from a local file
    if not data:
        local_file = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'tsx_directory.json')
        if os.path.exists(local_file):
            print(f"Reading from local file: {local_file}")
            with open(local_file, 'r') as f:
                data = json.load(f)
        else:
            print("No data available. Save the JSON to scripts/tsx_directory.json and retry.")
            sys.exit(1)

    # Filter to real stocks
    stocks = filter_real_stocks(data)

    if not stocks:
        print("No stocks to import after filtering!")
        sys.exit(1)

    # Show what we'll import
    print(f"\nWill import {len(stocks)} stocks. First 20:")
    for s in stocks[:20]:
        print(f"  {s['symbol']:15s} {s['name']}")
    print(f"  ... and {len(stocks) - 20} more\n")

    # Connect to database
    script_dir = os.path.dirname(os.path.abspath(__file__))
    project_root = os.path.dirname(script_dir)
    db_path = os.path.join(project_root, 'dividends.db')

    if not os.path.exists(db_path):
        print(f"Database not found: {db_path}")
        sys.exit(1)

    updater = StockDataUpdater(db_path)
    if not updater.connect_db():
        print("Failed to connect to database!")
        sys.exit(1)

    # Check which stocks are already in the database
    updater.cursor.execute("SELECT Symbol FROM DividendModels")
    existing = {row[0] for row in updater.cursor.fetchall()}
    print(f"Already in database: {len(existing)} stocks")

    new_stocks = [s for s in stocks if s['symbol'] not in existing]
    print(f"New stocks to import: {len(new_stocks)}")

    if not new_stocks:
        print("All stocks already imported!")
        return

    # Import
    total = len(new_stocks)
    success = 0
    failed = 0
    skipped = 0

    print(f"\n{'='*60}")
    print(f"Importing {total} new TSX stocks")
    print(f"{'='*60}\n")

    for i, stock in enumerate(new_stocks, 1):
        symbol = stock['symbol']
        print(f"[{i}/{total}] {symbol} ({stock['name'][:40]})", end=" ... ", flush=True)

        try:
            result = updater.add_or_update_single_stock(symbol)
            if result:
                print(f"OK")
                success += 1
            else:
                print(f"skipped (no data)")
                skipped += 1
        except Exception as e:
            print(f"error: {e}")
            failed += 1

        # Rate limit to avoid Yahoo Finance throttling
        if i < total:
            time.sleep(1.2)

        # Progress update every 25 stocks
        if i % 25 == 0:
            elapsed_pct = (i / total) * 100
            print(f"\n--- Progress: {i}/{total} ({elapsed_pct:.0f}%) | OK: {success} | Skip: {skipped} | Fail: {failed} ---\n")

    print(f"\n{'='*60}")
    print(f"Import complete:")
    print(f"  Success:  {success}")
    print(f"  Skipped:  {skipped}")
    print(f"  Failed:   {failed}")
    print(f"  Total DB: {len(existing) + success}")
    print(f"{'='*60}\n")


if __name__ == '__main__':
    main()
