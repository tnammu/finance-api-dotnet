#!/usr/bin/env python3
"""
Fetch 5-year historical data for ETFs
Usage: python fetch_etf_history.py SYMBOL [YEARS]
Example: python fetch_etf_history.py XEG.TO 5
"""

import sys
import json
import yfinance as yf
from datetime import datetime, timedelta

def log(message):
    """Print to stderr for logging"""
    print(message, file=sys.stderr)

def fetch_etf_history(symbol, years=5):
    """Fetch historical price data for an ETF"""
    try:
        log(f"\n{'='*60}")
        log(f"FETCHING {years}-YEAR HISTORY: {symbol}")
        log(f"{'='*60}\n")

        # Create ticker object
        ticker = yf.Ticker(symbol)

        # Calculate date range
        end_date = datetime.now()
        start_date = end_date - timedelta(days=years * 365)

        log(f"Date Range: {start_date.strftime('%Y-%m-%d')} to {end_date.strftime('%Y-%m-%d')}")

        # Fetch historical data
        hist = ticker.history(start=start_date, end=end_date)

        if hist.empty:
            log(f"✗ No historical data found for {symbol}")
            return None

        # Get basic info
        info = ticker.info
        name = info.get('longName', symbol)
        currency = info.get('currency', 'USD')

        log(f"✓ Found {len(hist)} trading days of data")
        log(f"✓ Name: {name}")
        log(f"✓ Currency: {currency}")

        # Prepare data for JSON output
        history_data = []
        for date, row in hist.iterrows():
            history_data.append({
                'date': date.strftime('%Y-%m-%d'),
                'open': float(row['Open']),
                'high': float(row['High']),
                'low': float(row['Low']),
                'close': float(row['Close']),
                'volume': int(row['Volume']),
                'adjClose': float(row['Close'])  # Using Close as adjusted close
            })

        # Calculate performance metrics
        first_price = history_data[0]['close']
        last_price = history_data[-1]['close']
        total_return = ((last_price - first_price) / first_price) * 100

        # Calculate yearly returns
        yearly_returns = []
        current_year = None
        year_start_price = None
        year_end_price = None

        for record in history_data:
            year = record['date'][:4]
            if current_year != year:
                if current_year is not None and year_start_price is not None:
                    yearly_return = ((year_end_price - year_start_price) / year_start_price) * 100
                    yearly_returns.append({
                        'year': int(current_year),
                        'return': round(yearly_return, 2)
                    })
                current_year = year
                year_start_price = record['close']
            year_end_price = record['close']

        # Add last year
        if current_year is not None and year_start_price is not None:
            yearly_return = ((year_end_price - year_start_price) / year_start_price) * 100
            yearly_returns.append({
                'year': int(current_year),
                'return': round(yearly_return, 2)
            })

        # Calculate consolidated monthly data (average by calendar month)
        from collections import defaultdict
        monthly_groups = defaultdict(list)

        # Calculate monthly returns
        for i in range(1, len(history_data)):
            prev_close = history_data[i-1]['close']
            curr_close = history_data[i]['close']
            monthly_return = ((curr_close - prev_close) / prev_close) * 100

            # Get month name from date
            date_obj = datetime.strptime(history_data[i]['date'], '%Y-%m-%d')
            month_name = date_obj.strftime('%B')  # Full month name (January, February, etc.)

            monthly_groups[month_name].append(monthly_return)

        # Calculate statistics for each month
        month_order = ['January', 'February', 'March', 'April', 'May', 'June',
                      'July', 'August', 'September', 'October', 'November', 'December']

        consolidated_monthly_data = []
        for month_name in month_order:
            if month_name in monthly_groups and len(monthly_groups[month_name]) > 0:
                returns = monthly_groups[month_name]
                avg_growth = sum(returns) / len(returns)
                positive_count = sum(1 for r in returns if r > 0)
                negative_count = sum(1 for r in returns if r < 0)
                occurrences = len(returns)
                positive_percentage = round((positive_count / occurrences) * 100, 1) if occurrences > 0 else 0

                consolidated_monthly_data.append({
                    'month': month_name,
                    'avgGrowth': round(avg_growth, 2),
                    'positiveCount': positive_count,
                    'negativeCount': negative_count,
                    'occurrences': occurrences,
                    'positivePercentage': positive_percentage
                })

        log(f"\nPerformance Summary:")
        log(f"  Total Return ({years}Y): {total_return:.2f}%")
        log(f"  Starting Price: ${first_price:.2f}")
        log(f"  Current Price: ${last_price:.2f}")

        result = {
            'success': True,
            'symbol': symbol,
            'name': name,
            'currency': currency,
            'years': years,
            'dataPoints': len(history_data),
            'startDate': history_data[0]['date'],
            'endDate': history_data[-1]['date'],
            'startPrice': round(first_price, 2),
            'currentPrice': round(last_price, 2),
            'totalReturn': round(total_return, 2),
            'yearlyReturns': yearly_returns,
            'consolidatedMonthlyData': consolidated_monthly_data,
            'history': history_data,
            'fetched_at': datetime.now().isoformat()
        }

        log(f"\n{'='*60}")
        log(f"✓ Successfully fetched {years}-year history for {symbol}")
        log(f"{'='*60}\n")

        return result

    except Exception as e:
        log(f"\n✗ Error fetching history for {symbol}: {str(e)}")
        return {
            'success': False,
            'error': str(e),
            'symbol': symbol
        }

if __name__ == '__main__':
    if len(sys.argv) < 2:
        log("Usage: python fetch_etf_history.py SYMBOL [YEARS]")
        log("Example: python fetch_etf_history.py XEG.TO 5")
        sys.exit(1)

    symbol = sys.argv[1].upper()
    years = int(sys.argv[2]) if len(sys.argv) > 2 else 5

    result = fetch_etf_history(symbol, years)

    if result:
        # Output JSON to stdout
        print(json.dumps(result, indent=2))
    else:
        sys.exit(1)