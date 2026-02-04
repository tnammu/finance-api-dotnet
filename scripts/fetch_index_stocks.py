#!/usr/bin/env python3
"""
Fetch stocks from major indices (NASDAQ, TSX)
Uses popular index ETFs to get constituent stocks
"""

import yfinance as yf
import pandas as pd
import json
from datetime import datetime

def log(message):
    """Print log message"""
    print(f"[{datetime.now().strftime('%H:%M:%S')}] {message}")

def fetch_sp500_stocks():
    """Fetch S&P 500 stock list from Wikipedia"""
    log("Fetching S&P 500 stocks...")
    try:
        url = 'https://en.wikipedia.org/wiki/List_of_S%26P_500_companies'
        tables = pd.read_html(url)
        df = tables[0]

        stocks = []
        for _, row in df.iterrows():
            stocks.append({
                'symbol': row['Symbol'],
                'name': row['Security'],
                'sector': row['GICS Sector'],
                'industry': row['GICS Sub-Industry'],
                'exchange': 'NASDAQ/NYSE'
            })

        log(f"✓ Found {len(stocks)} S&P 500 stocks")
        return stocks
    except Exception as e:
        log(f"✗ Error fetching S&P 500: {e}")
        return []

def fetch_nasdaq100_stocks():
    """Fetch NASDAQ-100 stock list from Wikipedia"""
    log("Fetching NASDAQ-100 stocks...")
    try:
        url = 'https://en.wikipedia.org/wiki/Nasdaq-100'
        tables = pd.read_html(url)
        df = tables[4]  # The constituent table

        stocks = []
        for _, row in df.iterrows():
            stocks.append({
                'symbol': row['Ticker'],
                'name': row['Company'],
                'sector': row['GICS Sector'] if 'GICS Sector' in row else 'Technology',
                'industry': row['GICS Sub-Industry'] if 'GICS Sub-Industry' in row else 'Technology',
                'exchange': 'NASDAQ'
            })

        log(f"✓ Found {len(stocks)} NASDAQ-100 stocks")
        return stocks
    except Exception as e:
        log(f"✗ Error fetching NASDAQ-100: {e}")
        return []

def fetch_tsx_composite_stocks():
    """Fetch TSX Composite top holdings"""
    log("Fetching TSX top stocks...")

    # Major Canadian stocks by sector
    tsx_stocks = [
        # Banks
        {'symbol': 'RY.TO', 'name': 'Royal Bank of Canada', 'sector': 'Financials', 'industry': 'Banks', 'exchange': 'TSX'},
        {'symbol': 'TD.TO', 'name': 'Toronto-Dominion Bank', 'sector': 'Financials', 'industry': 'Banks', 'exchange': 'TSX'},
        {'symbol': 'BNS.TO', 'name': 'Bank of Nova Scotia', 'sector': 'Financials', 'industry': 'Banks', 'exchange': 'TSX'},
        {'symbol': 'BMO.TO', 'name': 'Bank of Montreal', 'sector': 'Financials', 'industry': 'Banks', 'exchange': 'TSX'},
        {'symbol': 'CM.TO', 'name': 'Canadian Imperial Bank of Commerce', 'sector': 'Financials', 'industry': 'Banks', 'exchange': 'TSX'},
        {'symbol': 'NA.TO', 'name': 'National Bank of Canada', 'sector': 'Financials', 'industry': 'Banks', 'exchange': 'TSX'},

        # Energy
        {'symbol': 'ENB.TO', 'name': 'Enbridge Inc.', 'sector': 'Energy', 'industry': 'Oil & Gas Midstream', 'exchange': 'TSX'},
        {'symbol': 'CNQ.TO', 'name': 'Canadian Natural Resources', 'sector': 'Energy', 'industry': 'Oil & Gas E&P', 'exchange': 'TSX'},
        {'symbol': 'SU.TO', 'name': 'Suncor Energy', 'sector': 'Energy', 'industry': 'Integrated Oil & Gas', 'exchange': 'TSX'},
        {'symbol': 'TRP.TO', 'name': 'TC Energy Corporation', 'sector': 'Energy', 'industry': 'Oil & Gas Midstream', 'exchange': 'TSX'},
        {'symbol': 'CVE.TO', 'name': 'Cenovus Energy', 'sector': 'Energy', 'industry': 'Integrated Oil & Gas', 'exchange': 'TSX'},
        {'symbol': 'IMO.TO', 'name': 'Imperial Oil', 'sector': 'Energy', 'industry': 'Integrated Oil & Gas', 'exchange': 'TSX'},

        # Telecommunications
        {'symbol': 'BCE.TO', 'name': 'BCE Inc.', 'sector': 'Communication Services', 'industry': 'Telecom', 'exchange': 'TSX'},
        {'symbol': 'T.TO', 'name': 'TELUS Corporation', 'sector': 'Communication Services', 'industry': 'Telecom', 'exchange': 'TSX'},
        {'symbol': 'RCI-B.TO', 'name': 'Rogers Communications', 'sector': 'Communication Services', 'industry': 'Telecom', 'exchange': 'TSX'},

        # Utilities
        {'symbol': 'FTS.TO', 'name': 'Fortis Inc.', 'sector': 'Utilities', 'industry': 'Electric Utilities', 'exchange': 'TSX'},
        {'symbol': 'EMA.TO', 'name': 'Emera Inc.', 'sector': 'Utilities', 'industry': 'Electric Utilities', 'exchange': 'TSX'},
        {'symbol': 'AQN.TO', 'name': 'Algonquin Power & Utilities', 'sector': 'Utilities', 'industry': 'Electric Utilities', 'exchange': 'TSX'},

        # Financials (Insurance)
        {'symbol': 'MFC.TO', 'name': 'Manulife Financial', 'sector': 'Financials', 'industry': 'Insurance', 'exchange': 'TSX'},
        {'symbol': 'SLF.TO', 'name': 'Sun Life Financial', 'sector': 'Financials', 'industry': 'Insurance', 'exchange': 'TSX'},
        {'symbol': 'GWO.TO', 'name': 'Great-West Lifeco', 'sector': 'Financials', 'industry': 'Insurance', 'exchange': 'TSX'},

        # Real Estate
        {'symbol': 'BIP-UN.TO', 'name': 'Brookfield Infrastructure Partners', 'sector': 'Real Estate', 'industry': 'Infrastructure', 'exchange': 'TSX'},
        {'symbol': 'REI-UN.TO', 'name': 'RioCan REIT', 'sector': 'Real Estate', 'industry': 'Retail REIT', 'exchange': 'TSX'},

        # Materials
        {'symbol': 'ABX.TO', 'name': 'Barrick Gold', 'sector': 'Materials', 'industry': 'Gold Mining', 'exchange': 'TSX'},
        {'symbol': 'NTR.TO', 'name': 'Nutrien Ltd.', 'sector': 'Materials', 'industry': 'Fertilizers', 'exchange': 'TSX'},
        {'symbol': 'CCL-B.TO', 'name': 'CCL Industries', 'sector': 'Materials', 'industry': 'Packaging', 'exchange': 'TSX'},

        # Industrials
        {'symbol': 'CNR.TO', 'name': 'Canadian National Railway', 'sector': 'Industrials', 'industry': 'Railroads', 'exchange': 'TSX'},
        {'symbol': 'CP.TO', 'name': 'Canadian Pacific Railway', 'sector': 'Industrials', 'industry': 'Railroads', 'exchange': 'TSX'},
        {'symbol': 'CSU.TO', 'name': 'Constellation Software', 'sector': 'Information Technology', 'industry': 'Software', 'exchange': 'TSX'},

        # Consumer
        {'symbol': 'ATD.TO', 'name': 'Alimentation Couche-Tard', 'sector': 'Consumer Discretionary', 'industry': 'Convenience Stores', 'exchange': 'TSX'},
        {'symbol': 'L.TO', 'name': 'Loblaw Companies', 'sector': 'Consumer Staples', 'industry': 'Food Retail', 'exchange': 'TSX'},
        {'symbol': 'MG.TO', 'name': 'Magna International', 'sector': 'Consumer Discretionary', 'industry': 'Auto Parts', 'exchange': 'TSX'},
        {'symbol': 'DOL.TO', 'name': 'Dollarama Inc.', 'sector': 'Consumer Discretionary', 'industry': 'Discount Stores', 'exchange': 'TSX'},

        # Healthcare/Cannabis
        {'symbol': 'WEED.TO', 'name': 'Canopy Growth', 'sector': 'Healthcare', 'industry': 'Cannabis', 'exchange': 'TSX'},
    ]

    log(f"✓ Found {len(tsx_stocks)} major TSX stocks")
    return tsx_stocks

def fetch_top_etfs():
    """Fetch popular dividend and sector ETFs"""
    log("Adding popular ETFs...")

    etfs = [
        # Dividend ETFs
        {'symbol': 'VYM', 'name': 'Vanguard High Dividend Yield ETF', 'sector': 'ETF', 'industry': 'Dividend ETF', 'exchange': 'NYSE'},
        {'symbol': 'SCHD', 'name': 'Schwab US Dividend Equity ETF', 'sector': 'ETF', 'industry': 'Dividend ETF', 'exchange': 'NYSE'},
        {'symbol': 'VIG', 'name': 'Vanguard Dividend Appreciation ETF', 'sector': 'ETF', 'industry': 'Dividend Growth ETF', 'exchange': 'NYSE'},
        {'symbol': 'DVY', 'name': 'iShares Select Dividend ETF', 'sector': 'ETF', 'industry': 'Dividend ETF', 'exchange': 'NYSE'},
        {'symbol': 'DGRO', 'name': 'iShares Core Dividend Growth ETF', 'sector': 'ETF', 'industry': 'Dividend Growth ETF', 'exchange': 'NASDAQ'},

        # Sector ETFs
        {'symbol': 'XLK', 'name': 'Technology Select Sector SPDR', 'sector': 'ETF', 'industry': 'Technology ETF', 'exchange': 'NYSE'},
        {'symbol': 'XLF', 'name': 'Financial Select Sector SPDR', 'sector': 'ETF', 'industry': 'Financials ETF', 'exchange': 'NYSE'},
        {'symbol': 'XLE', 'name': 'Energy Select Sector SPDR', 'sector': 'ETF', 'industry': 'Energy ETF', 'exchange': 'NYSE'},
        {'symbol': 'XLV', 'name': 'Health Care Select Sector SPDR', 'sector': 'ETF', 'industry': 'Healthcare ETF', 'exchange': 'NYSE'},
        {'symbol': 'XLI', 'name': 'Industrial Select Sector SPDR', 'sector': 'ETF', 'industry': 'Industrials ETF', 'exchange': 'NYSE'},

        # Canadian ETFs
        {'symbol': 'XIU.TO', 'name': 'iShares S&P/TSX 60 Index ETF', 'sector': 'ETF', 'industry': 'Canadian Index ETF', 'exchange': 'TSX'},
        {'symbol': 'XEG.TO', 'name': 'iShares S&P/TSX Capped Energy Index ETF', 'sector': 'ETF', 'industry': 'Energy ETF', 'exchange': 'TSX'},
        {'symbol': 'XFN.TO', 'name': 'iShares S&P/TSX Capped Financials Index ETF', 'sector': 'ETF', 'industry': 'Financials ETF', 'exchange': 'TSX'},
        {'symbol': 'XRE.TO', 'name': 'iShares S&P/TSX Capped REIT Index ETF', 'sector': 'ETF', 'industry': 'Real Estate ETF', 'exchange': 'TSX'},
        {'symbol': 'CDZ.TO', 'name': 'iShares S&P/TSX Canadian Dividend Aristocrats Index ETF', 'sector': 'ETF', 'industry': 'Dividend ETF', 'exchange': 'TSX'},
    ]

    log(f"✓ Added {len(etfs)} popular ETFs")
    return etfs

def main():
    log("="*60)
    log("FETCHING STOCKS FROM MAJOR INDICES")
    log("="*60)

    all_stocks = []

    # Fetch from different sources
    all_stocks.extend(fetch_sp500_stocks())
    all_stocks.extend(fetch_nasdaq100_stocks())
    all_stocks.extend(fetch_tsx_composite_stocks())
    all_stocks.extend(fetch_top_etfs())

    # Remove duplicates
    unique_stocks = {}
    for stock in all_stocks:
        symbol = stock['symbol']
        if symbol not in unique_stocks:
            unique_stocks[symbol] = stock

    stocks_list = list(unique_stocks.values())

    log("="*60)
    log(f"TOTAL UNIQUE STOCKS: {len(stocks_list)}")
    log("="*60)

    # Group by exchange
    by_exchange = {}
    for stock in stocks_list:
        exchange = stock.get('exchange', 'Unknown')
        if exchange not in by_exchange:
            by_exchange[exchange] = []
        by_exchange[exchange].append(stock)

    log("\nBreakdown by Exchange:")
    for exchange, stocks in sorted(by_exchange.items()):
        log(f"  {exchange}: {len(stocks)} stocks")

    # Save to JSON
    output_file = 'stocks_list.json'
    with open(output_file, 'w') as f:
        json.dump({
            'fetched_at': datetime.now().isoformat(),
            'total_stocks': len(stocks_list),
            'stocks': stocks_list
        }, f, indent=2)

    log(f"\n✓ Saved to {output_file}")

    # Also save just symbols for easy import
    symbols_file = 'symbols_list.txt'
    with open(symbols_file, 'w') as f:
        for stock in stocks_list:
            f.write(f"{stock['symbol']}\n")

    log(f"✓ Saved symbols to {symbols_file}")

    return stocks_list

if __name__ == '__main__':
    stocks = main()
    print(f"\n✓ Ready to import {len(stocks)} stocks!")
