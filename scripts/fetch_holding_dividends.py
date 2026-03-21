#!/usr/bin/env python3
"""
Fetch annual dividend per share from Yahoo Finance for all holdings
and store it back in the Holdings table.

Usage: python fetch_holding_dividends.py <db_path>
"""

import sys
import sqlite3
import time
from datetime import datetime, timedelta
import yfinance as yf


def yahoo_candidates(symbol):
    """
    Return Yahoo Finance ticker candidates to try.
    - REI.UN  -> ['REI.UN', 'REI-UN.TO']
    - BMO     -> ['BMO', 'BMO.TO']
    - VDY     -> ['VDY', 'VDY.TO']
    - VWOB    -> ['VWOB']  (US ETF, no .TO)
    """
    candidates = [symbol]
    if symbol.endswith('.UN'):
        base = symbol[:-3]
        candidates.append(base + '-UN.TO')
    elif not symbol.endswith('.TO') and '.' not in symbol:
        candidates.append(symbol + '.TO')
    return candidates


def fetch_dividend(symbol):
    """
    Return (annual_dividend_per_share, used_ticker) or (None, symbol).
    Tries:
      1. dividendRate or trailingAnnualDividendRate from info
      2. Sum of last 12 months of actual dividend history
    """
    for ticker_sym in yahoo_candidates(symbol):
        try:
            t = yf.Ticker(ticker_sym)
            info = t.info or {}

            price = info.get('regularMarketPrice') or info.get('currentPrice')
            if not price:
                continue  # ticker returned nothing useful

            # 1. Direct rate fields
            rate = info.get('dividendRate') or info.get('trailingAnnualDividendRate')
            if rate and float(rate) > 0:
                return round(float(rate), 4), ticker_sym

            # 2. Compute from trailing 12-month dividend history
            try:
                divs = t.dividends
                if divs is not None and len(divs) > 0:
                    cutoff = datetime.now() - timedelta(days=365)
                    cutoff_str = cutoff.strftime('%Y-%m-%d')
                    try:
                        recent = divs[divs.index.astype(str) >= cutoff_str]
                    except Exception:
                        recent = divs.tail(12)
                    if len(recent) > 0:
                        annual = float(recent.sum())
                        if annual > 0:
                            return round(annual, 4), ticker_sym
            except Exception:
                pass

        except Exception:
            pass

        time.sleep(0.4)

    return None, symbol


def main():
    if len(sys.argv) < 2:
        print("Usage: python fetch_holding_dividends.py <db_path>")
        sys.exit(1)

    db_path = sys.argv[1]
    conn = sqlite3.connect(db_path)

    try:
        rows = conn.execute("SELECT Id, Symbol FROM Holdings ORDER BY Symbol").fetchall()
        print(f"Fetching dividends for {len(rows)} holdings...\n")

        updated = 0
        no_div = 0

        for holding_id, symbol in rows:
            div, used_sym = fetch_dividend(symbol)
            if div is not None:
                conn.execute(
                    "UPDATE Holdings SET AnnualDividendPerShare = ? WHERE Id = ?",
                    (div, holding_id)
                )
                tag = f"(via {used_sym})" if used_sym != symbol else ""
                print(f"  {symbol:<12} -> ${div:.4f}/share  {tag}")
                updated += 1
            else:
                conn.execute(
                    "UPDATE Holdings SET AnnualDividendPerShare = NULL WHERE Id = ?",
                    (holding_id,)
                )
                print(f"  {symbol:<12} -> no dividend")
                no_div += 1

            time.sleep(0.5)

        conn.commit()
        print(f"\nDone. {updated} with dividends, {no_div} without.")

    finally:
        conn.close()


if __name__ == '__main__':
    main()
