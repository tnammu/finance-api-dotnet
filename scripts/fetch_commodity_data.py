#!/usr/bin/env python3
"""
Fetch commodity futures data from Yahoo Finance

Usage: python fetch_commodity_data.py <symbol> <years>
Example: python fetch_commodity_data.py GC=F 5
"""

import sys
import json
import yfinance as yf
import pandas as pd
import numpy as np
from datetime import datetime, timedelta

def calculate_atr(df, period=14):
    """Calculate Average True Range (ATR)"""
    high_low = df['High'] - df['Low']
    high_close = np.abs(df['High'] - df['Close'].shift())
    low_close = np.abs(df['Low'] - df['Close'].shift())

    ranges = pd.concat([high_low, high_close, low_close], axis=1)
    true_range = np.max(ranges, axis=1)
    atr = true_range.rolling(period).mean()

    return atr

def calculate_volatility(df, period=20):
    """Calculate historical volatility (annualized)"""
    log_returns = np.log(df['Close'] / df['Close'].shift(1))
    volatility = log_returns.rolling(period).std() * np.sqrt(252) * 100  # Annualized %
    return volatility

def get_commodity_specs(symbol):
    """Get commodity contract specifications"""
    specs = {
        'GC=F': {
            'name': 'Gold Futures',
            'category': 'Metals',
            'contractSize': 100,  # 100 troy ounces
            'tickSize': 0.10,
            'tickValue': 10.00,  # $10 per tick
            'marginRequirement': 8000
        },
        'SI=F': {
            'name': 'Silver Futures',
            'category': 'Metals',
            'contractSize': 5000,  # 5,000 troy ounces
            'tickSize': 0.005,
            'tickValue': 25.00,
            'marginRequirement': 6000
        },
        'HG=F': {
            'name': 'Copper Futures',
            'category': 'Metals',
            'contractSize': 25000,  # 25,000 pounds
            'tickSize': 0.0005,
            'tickValue': 12.50,
            'marginRequirement': 5500
        },
        'PL=F': {
            'name': 'Platinum Futures',
            'category': 'Metals',
            'contractSize': 50,  # 50 troy ounces
            'tickSize': 0.10,
            'tickValue': 5.00,
            'marginRequirement': 5000
        },
        'CL=F': {
            'name': 'Crude Oil Futures',
            'category': 'Energy',
            'contractSize': 1000,  # 1,000 barrels
            'tickSize': 0.01,
            'tickValue': 10.00,
            'marginRequirement': 5000
        },
        'NG=F': {
            'name': 'Natural Gas Futures',
            'category': 'Energy',
            'contractSize': 10000,  # 10,000 MMBtu
            'tickSize': 0.001,
            'tickValue': 10.00,
            'marginRequirement': 3000
        },
        'HO=F': {
            'name': 'Heating Oil Futures',
            'category': 'Energy',
            'contractSize': 42000,  # 42,000 gallons
            'tickSize': 0.0001,
            'tickValue': 4.20,
            'marginRequirement': 4500
        }
    }

    return specs.get(symbol, {
        'name': f'{symbol} Futures',
        'category': 'Unknown',
        'contractSize': 1,
        'tickSize': 0.01,
        'tickValue': 1.00,
        'marginRequirement': 5000
    })

def fetch_commodity_data(symbol, years=5):
    """
    Fetch commodity futures data from Yahoo Finance

    Args:
        symbol: Commodity symbol (e.g., 'GC=F' for Gold)
        years: Number of years of historical data

    Returns:
        dict: JSON response with commodity data
    """
    try:
        # Calculate date range
        end_date = datetime.now()
        start_date = end_date - timedelta(days=years*365)

        sys.stderr.write(f"Fetching {symbol} data from {start_date.date()} to {end_date.date()}...\n")

        # Fetch data from Yahoo Finance
        ticker = yf.Ticker(symbol)
        df = ticker.history(start=start_date, end=end_date)

        if df.empty:
            return {
                'success': False,
                'error': f'No data found for {symbol}'
            }

        sys.stderr.write(f"Fetched {len(df)} data points\n")

        # Calculate technical indicators
        sys.stderr.write("Calculating technical indicators...\n")
        df['ATR14'] = calculate_atr(df, period=14)
        df['Volatility20'] = calculate_volatility(df, period=20)

        # Get commodity specifications
        specs = get_commodity_specs(symbol)

        # Get current price (most recent close)
        current_price = float(df['Close'].iloc[-1])

        # Prepare history data
        history = []
        for date, row in df.iterrows():
            history.append({
                'date': date.strftime('%Y-%m-%d'),
                'open': float(row['Open']),
                'high': float(row['High']),
                'low': float(row['Low']),
                'close': float(row['Close']),
                'volume': int(row['Volume']) if not pd.isna(row['Volume']) else 0,
                'atr14': float(row['ATR14']) if not pd.isna(row['ATR14']) else None,
                'volatility20': float(row['Volatility20']) if not pd.isna(row['Volatility20']) else None
            })

        # Build response
        response = {
            'success': True,
            'symbol': symbol,
            'name': specs['name'],
            'category': specs['category'],
            'currentPrice': current_price,
            'contractSize': specs['contractSize'],
            'tickSize': specs['tickSize'],
            'tickValue': specs['tickValue'],
            'marginRequirement': specs['marginRequirement'],
            'period': f'{years} years',
            'dataPoints': len(df),
            'startDate': df.index[0].strftime('%Y-%m-%d'),
            'endDate': df.index[-1].strftime('%Y-%m-%d'),
            'history': history,
            'fetchedAt': datetime.now().isoformat()
        }

        sys.stderr.write(f"Successfully fetched data for {symbol}\n")
        sys.stderr.write(f"Price range: ${df['Close'].min():.2f} - ${df['Close'].max():.2f}\n")
        sys.stderr.write(f"Current price: ${current_price:.2f}\n")
        sys.stderr.write(f"Average ATR(14): ${df['ATR14'].mean():.2f}\n")
        sys.stderr.write(f"Average Volatility(20): {df['Volatility20'].mean():.2f}%\n")

        return response

    except Exception as e:
        sys.stderr.write(f"Error fetching data: {str(e)}\n")
        return {
            'success': False,
            'error': str(e)
        }

def main():
    if len(sys.argv) < 2:
        print(json.dumps({
            'success': False,
            'error': 'Usage: python fetch_commodity_data.py <symbol> [years]'
        }), indent=2)
        sys.exit(1)

    symbol = sys.argv[1].upper()
    years = int(sys.argv[2]) if len(sys.argv) > 2 else 5

    # Validate years
    if years < 1 or years > 20:
        print(json.dumps({
            'success': False,
            'error': 'Years must be between 1 and 20'
        }), indent=2)
        sys.exit(1)

    # Fetch data
    result = fetch_commodity_data(symbol, years)

    # Output JSON to stdout
    print(json.dumps(result, indent=2))

    # Exit with appropriate code
    sys.exit(0 if result['success'] else 1)

if __name__ == '__main__':
    main()