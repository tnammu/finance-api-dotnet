#!/usr/bin/env python3
"""
Fetch intraday/historical data for ETFs with multiple time frames
Usage: python fetch_etf_intraday.py SYMBOL [PERIOD]
Example: python fetch_etf_intraday.py XEG.TO 1d
Supported periods: 1d, 5d, 1wk, 1mo, ytd, 1y, 5y
"""

import sys
import json
import yfinance as yf
from datetime import datetime

def log(message):
    """Print to stderr for logging"""
    print(message, file=sys.stderr)

def fetch_etf_intraday(symbol, period='1d'):
    """Fetch intraday/historical data based on time frame"""
    try:
        # Determine interval based on period
        period_config = {
            '1d': {'period': '1d', 'interval': '5m', 'label': '1 Day'},
            '5d': {'period': '5d', 'interval': '15m', 'label': '5 Days'},
            '1wk': {'period': '1mo', 'interval': '30m', 'label': '1 Week'},
            '1mo': {'period': '1mo', 'interval': '1h', 'label': '1 Month'},
            'ytd': {'period': 'ytd', 'interval': '1d', 'label': 'Year to Date'},
            '1y': {'period': '1y', 'interval': '1d', 'label': '1 Year'},
            '5y': {'period': '5y', 'interval': '1d', 'label': '5 Years'}
        }

        if period not in period_config:
            log(f"✗ Invalid period: {period}. Using 1d as default.")
            period = '1d'

        config = period_config[period]

        log(f"\n{'='*60}")
        log(f"FETCHING {config['label'].upper()} DATA: {symbol}")
        log(f"Period: {config['period']}, Interval: {config['interval']}")
        log(f"{'='*60}\n")

        # Create ticker object
        ticker = yf.Ticker(symbol)

        # Fetch data with appropriate interval
        hist = ticker.history(period=config['period'], interval=config['interval'])

        if hist.empty:
            log(f"✗ No intraday data found for {symbol}")
            return None

        # Get basic info
        info = ticker.info
        name = info.get('longName', symbol)
        currency = info.get('currency', 'USD')

        log(f"✓ Found {len(hist)} hourly data points for today")
        log(f"✓ Name: {name}")
        log(f"✓ Currency: {currency}")

        # Group data by hour for hourly averages
        from collections import defaultdict
        hourly_groups = defaultdict(lambda: {'prices': [], 'volumes': [], 'price_changes': []})

        opening_price = None
        first_price = None
        last_price = None
        total_volume = 0
        high_of_period = float('-inf')
        low_of_period = float('inf')

        for date, row in hist.iterrows():
            hour = date.hour
            minute = date.minute

            open_price = float(row['Open'])
            high_price = float(row['High'])
            low_price = float(row['Low'])
            close_price = float(row['Close'])
            volume = int(row['Volume'])

            # Track period stats
            if first_price is None:
                first_price = open_price
                opening_price = open_price

            last_price = close_price
            total_volume += volume
            high_of_period = max(high_of_period, high_price)
            low_of_period = min(low_of_period, low_price)

            # Group by hour
            hourly_groups[hour]['prices'].append(close_price)
            hourly_groups[hour]['volumes'].append(volume)

            # Calculate interval price change
            interval_change_pct = ((close_price - open_price) / open_price * 100) if open_price else 0
            hourly_groups[hour]['price_changes'].append(interval_change_pct)

        # Calculate hourly averages
        intraday_data = []
        for hour in range(24):
            if hour in hourly_groups and len(hourly_groups[hour]['prices']) > 0:
                prices = hourly_groups[hour]['prices']
                volumes = hourly_groups[hour]['volumes']
                price_changes = hourly_groups[hour]['price_changes']

                avg_price = sum(prices) / len(prices)
                avg_volume = sum(volumes) / len(volumes)
                avg_price_change = sum(price_changes) / len(price_changes)
                min_price = min(prices)
                max_price = max(prices)
                occurrences = len(prices)

                # Calculate price volatility
                mean_price = avg_price
                variance = sum((p - mean_price) ** 2 for p in prices) / len(prices)
                std_dev = variance ** 0.5

                intraday_data.append({
                    'time': f"{hour:02d}:00",
                    'hour': hour,
                    'avgPrice': round(avg_price, 2),
                    'avgVolume': round(avg_volume, 0),
                    'avgPriceChange': round(avg_price_change, 2),
                    'minPrice': round(min_price, 2),
                    'maxPrice': round(max_price, 2),
                    'priceStdDev': round(std_dev, 2),
                    'occurrences': occurrences,
                    'volume': round(avg_volume, 0),
                    'hourlyChangePercent': round(avg_price_change, 2),
                    'priceChangePercent': round(avg_price_change, 2)
                })

        # Sort by hour
        intraday_data.sort(key=lambda x: x['hour'])

        if not intraday_data:
            log(f"✗ No valid intraday data points")
            return None

        # Calculate summary statistics
        current_price = last_price
        day_change = current_price - opening_price
        day_change_percent = ((current_price - opening_price) / opening_price * 100) if opening_price else 0

        avg_volume = total_volume / len(intraday_data) if intraday_data else 0
        max_volume_hour = max(intraday_data, key=lambda x: x['avgVolume']) if intraday_data else None

        high_of_day = high_of_period
        low_of_day = low_of_period

        log(f"\nIntraday Summary:")
        log(f"  Opening Price: ${opening_price:.2f}")
        log(f"  Current Price: ${current_price:.2f}")
        log(f"  Day Change: ${day_change:+.2f} ({day_change_percent:+.2f}%)")
        log(f"  High: ${high_of_day:.2f}")
        log(f"  Low: ${low_of_day:.2f}")
        log(f"  Total Volume: {total_volume:,}")

        result = {
            'success': True,
            'symbol': symbol,
            'name': name,
            'currency': currency,
            'period': period,
            'periodLabel': config['label'],
            'interval': config['interval'],
            'date': datetime.now().strftime('%Y-%m-%d'),
            'dataPoints': len(intraday_data),
            'openingPrice': round(opening_price, 2),
            'currentPrice': round(current_price, 2),
            'dayChange': round(day_change, 2),
            'dayChangePercent': round(day_change_percent, 2),
            'highOfDay': round(high_of_day, 2),
            'lowOfDay': round(low_of_day, 2),
            'totalVolume': total_volume,
            'avgVolume': round(avg_volume, 0),
            'maxVolumeHour': max_volume_hour['time'] if max_volume_hour else None,
            'intradayData': intraday_data,
            'fetched_at': datetime.now().isoformat()
        }

        log(f"\n{'='*60}")
        log(f"✓ Successfully fetched {config['label']} data for {symbol}")
        log(f"{'='*60}\n")

        return result

    except Exception as e:
        log(f"\n✗ Error fetching intraday data for {symbol}: {str(e)}")
        return {
            'success': False,
            'error': str(e),
            'symbol': symbol
        }

if __name__ == '__main__':
    if len(sys.argv) < 2:
        log("Usage: python fetch_etf_intraday.py SYMBOL [PERIOD]")
        log("Example: python fetch_etf_intraday.py XEG.TO 1d")
        log("Supported periods: 1d, 5d, 1wk, 1mo, ytd, 1y, 5y")
        sys.exit(1)

    symbol = sys.argv[1].upper()
    period = sys.argv[2] if len(sys.argv) > 2 else '1d'
    result = fetch_etf_intraday(symbol, period)

    if result:
        # Output JSON to stdout
        print(json.dumps(result, indent=2))
    else:
        sys.exit(1)
