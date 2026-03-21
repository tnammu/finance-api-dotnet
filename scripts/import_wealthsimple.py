#!/usr/bin/env python3
"""
Import Wealthsimple CSV export into Holdings table.
Usage: python import_wealthsimple.py <csv_path> <db_path>
"""

import sys
import csv
import sqlite3
from datetime import datetime
from collections import defaultdict


def parse_wealthsimple_csv(csv_path):
    """Parse Wealthsimple activity CSV and compute net holdings."""
    # symbol -> {total_shares, total_cost (for avg price), first_buy_date}
    holdings = defaultdict(lambda: {
        'shares': 0.0,
        'total_cost': 0.0,
        'total_buy_shares': 0.0,
        'first_buy_date': None,
        'name': ''
    })

    with open(csv_path, newline='', encoding='utf-8-sig') as f:
        reader = csv.DictReader(f)
        for row in reader:
            if not row or row.get('activity_type') is None:
                continue
            activity_type = row.get('activity_type', '').strip()
            sub_type = row.get('activity_sub_type', '').strip()
            symbol = row.get('symbol', '').strip()
            name = row.get('name', '').strip()
            quantity_str = row.get('quantity', '').strip()
            unit_price_str = row.get('unit_price', '').strip()
            date_str = row.get('transaction_date', '').strip()

            # Only process trades with a symbol
            if activity_type != 'Trade' or not symbol or not quantity_str:
                continue

            try:
                quantity = float(quantity_str)
                unit_price = float(unit_price_str) if unit_price_str else 0.0
            except ValueError:
                continue

            h = holdings[symbol]
            h['name'] = name or symbol

            qty = abs(quantity)  # always positive; direction determined by sub_type

            if sub_type == 'BUY' and qty > 0:
                h['shares'] += qty
                h['total_cost'] += qty * unit_price
                h['total_buy_shares'] += qty
                # Track earliest buy date
                if date_str:
                    try:
                        d = datetime.strptime(date_str, '%Y-%m-%d')
                        if h['first_buy_date'] is None or d < h['first_buy_date']:
                            h['first_buy_date'] = d
                    except ValueError:
                        pass

            elif sub_type == 'SELL' and qty > 0:
                h['shares'] -= qty  # subtract sold shares

    # Filter: only keep positive net positions
    result = []
    for symbol, h in holdings.items():
        net_shares = round(h['shares'], 6)
        if net_shares <= 0.0001:
            continue

        avg_buy_price = (h['total_cost'] / h['total_buy_shares']
                         if h['total_buy_shares'] > 0 else 0.0)
        buy_date = h['first_buy_date'] or datetime.now()

        result.append({
            'symbol': symbol,
            'shares': net_shares,
            'buy_price': round(avg_buy_price, 4),
            'buy_date': buy_date.strftime('%Y-%m-%dT%H:%M:%S'),
            'notes': f"Imported from Wealthsimple — {h['name']}"
        })

    result.sort(key=lambda x: x['symbol'])
    return result


def import_to_db(holdings, db_path):
    conn = sqlite3.connect(db_path)
    try:
        # Clear existing holdings to avoid duplicates
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
                """INSERT INTO Holdings (Symbol, Shares, BuyPrice, BuyDate, Notes, AddedAt)
                   VALUES (?, ?, ?, ?, ?, ?)""",
                (h['symbol'], str(h['shares']), str(h['buy_price']),
                 h['buy_date'], h['notes'], datetime.utcnow().strftime('%Y-%m-%dT%H:%M:%S'))
            )
            inserted += 1
            print(f"  + {h['symbol']:<12} {h['shares']:.4f} shares @ ${h['buy_price']:.2f}")

        conn.commit()
        print(f"\n✓ Imported {inserted} holdings into portfolio.")
    finally:
        conn.close()


def main():
    if len(sys.argv) < 3:
        print("Usage: python import_wealthsimple.py <csv_path> <db_path>")
        sys.exit(1)

    csv_path = sys.argv[1]
    db_path = sys.argv[2]

    print(f"Parsing {csv_path}...")
    holdings = parse_wealthsimple_csv(csv_path)
    print(f"Found {len(holdings)} active positions:\n")
    for h in holdings:
        print(f"  {h['symbol']:<12} {h['shares']:.4f} shares @ ${h['buy_price']:.2f}  ({h['buy_date'][:10]})")

    print()
    import_to_db(holdings, db_path)


if __name__ == '__main__':
    main()
