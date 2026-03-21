#!/usr/bin/env python3
"""
Import Wealthsimple Holdings Report CSV into Holdings table.
This report has exact current share counts and cost basis (book value).

Usage: python import_holdings_report.py <csv_path> <db_path>

CSV columns:
  Account Name, Account Type, Account Classification, Account Number,
  Symbol, Exchange, MIC, Name, Security Type, Quantity, Position Direction,
  Market Price, Market Price Currency, Book Value (CAD), ...
"""

import sys
import csv
import sqlite3
from datetime import datetime
from collections import defaultdict


def parse_holdings_report(csv_path):
    """Parse Wealthsimple holdings report and aggregate by symbol."""
    # symbol -> {total_shares, total_book_value_cad, name, market_price}
    holdings = defaultdict(lambda: {
        'shares': 0.0,
        'total_book_value': 0.0,
        'name': '',
        'market_price': None
    })

    with open(csv_path, newline='', encoding='utf-8-sig') as f:
        reader = csv.DictReader(f)
        for row in reader:
            if not row or row.get('Symbol') is None:
                continue

            symbol = row.get('Symbol', '').strip().strip('"')
            name = row.get('Name', '').strip().strip('"')
            quantity_str = row.get('Quantity', '').strip().strip('"')
            book_value_str = row.get('Book Value (CAD)', '').strip().strip('"')
            market_price_str = row.get('Market Price', '').strip().strip('"')
            market_price_currency = row.get('Market Price Currency', '').strip().strip('"')

            if not symbol or not quantity_str:
                continue

            try:
                quantity = float(quantity_str)
                book_value = float(book_value_str) if book_value_str else 0.0
                # Only store CAD-denominated prices (skip USD to avoid FX confusion)
                market_price = float(market_price_str) if market_price_str and market_price_currency == 'CAD' else None
            except ValueError:
                continue

            if quantity <= 0:
                continue

            h = holdings[symbol]
            h['shares'] += quantity
            h['total_book_value'] += book_value
            if name:
                h['name'] = name
            # Use last seen CAD market price (or accumulate weighted; simple: last wins for price)
            if market_price is not None:
                h['market_price'] = market_price

    result = []
    for symbol, h in holdings.items():
        total_shares = round(h['shares'], 6)
        if total_shares <= 0:
            continue

        avg_buy_price = (h['total_book_value'] / total_shares
                         if total_shares > 0 else 0.0)

        result.append({
            'symbol': symbol,
            'shares': total_shares,
            'buy_price': round(avg_buy_price, 4),
            'market_price': round(h['market_price'], 4) if h['market_price'] is not None else None,
            'buy_date': datetime.now().strftime('%Y-%m-%dT%H:%M:%S'),
            'notes': f"Imported from Wealthsimple holdings report — {h['name']}"
        })

    result.sort(key=lambda x: x['symbol'])
    return result


def import_to_db(holdings, db_path):
    conn = sqlite3.connect(db_path)
    try:
        existing = conn.execute("SELECT COUNT(*) FROM Holdings").fetchone()[0]
        if existing > 0:
            confirm = input(f"\n{existing} existing holding(s) found. Replace all? (y/n): ").strip().lower()
            if confirm != 'y':
                print("Import cancelled.")
                return

        conn.execute("DELETE FROM Holdings")

        inserted = 0
        for h in holdings:
            conn.execute(
                """INSERT INTO Holdings (Symbol, Shares, BuyPrice, BuyDate, Notes, AddedAt, MarketPrice)
                   VALUES (?, ?, ?, ?, ?, ?, ?)""",
                (h['symbol'], str(h['shares']), str(h['buy_price']),
                 h['buy_date'], h['notes'], datetime.now().strftime('%Y-%m-%dT%H:%M:%S'),
                 str(h['market_price']) if h['market_price'] is not None else None)
            )
            inserted += 1
            print(f"  + {h['symbol']:<12} {h['shares']:.4f} shares @ ${h['buy_price']:.2f}")

        conn.commit()
        print(f"\n✓ Imported {inserted} holdings into portfolio.")
    finally:
        conn.close()


def main():
    if len(sys.argv) < 3:
        print("Usage: python import_holdings_report.py <csv_path> <db_path>")
        sys.exit(1)

    csv_path = sys.argv[1]
    db_path = sys.argv[2]

    print(f"Parsing {csv_path}...")
    holdings = parse_holdings_report(csv_path)
    print(f"Found {len(holdings)} positions:\n")
    for h in holdings:
        print(f"  {h['symbol']:<12} {h['shares']:.4f} shares @ ${h['buy_price']:.2f}")

    print()
    import_to_db(holdings, db_path)


if __name__ == '__main__':
    main()
