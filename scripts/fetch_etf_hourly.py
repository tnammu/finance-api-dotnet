#!/usr/bin/env python3
"""
Fetch hourly data for ETFs and calculate average price/volume by hour of day
Usage: python fetch_etf_hourly.py SYMBOL [DAYS]
Example: python fetch_etf_hourly.py XEG.TO 730
Note: yfinance limits hourly data to ~730 days (2 years max)
"""

import sys
import json
import yfinance as yf
from datetime import datetime, timedelta
from collections import defaultdict

def log(message):
    """Print to stderr for logging"""
    print(message, file=sys.stderr)

def fetch_etf_hourly(symbol, days=730):
    """Fetch hourly price data and calculate averages by hour of day"""
    try:
        log(f"\n{'='*60}")
        log(f"FETCHING HOURLY DATA: {symbol}")
        log(f"{'='*60}\n")

        # Create ticker object
        ticker = yf.Ticker(symbol)

        # Calculate date range (max 730 days for hourly data)
        if days > 730:
            log(f"⚠ Warning: yfinance limits hourly data to 730 days. Using 730 days.")
            days = 730

        end_date = datetime.now()
        start_date = end_date - timedelta(days=days)

        log(f"Date Range: {start_date.strftime('%Y-%m-%d')} to {end_date.strftime('%Y-%m-%d')}")
        log(f"Fetching hourly (1h interval) data...")

        # Fetch hourly data
        hist = ticker.history(start=start_date, end=end_date, interval='1h')

        if hist.empty:
            log(f"✗ No hourly data found for {symbol}")
            return None

        # Get basic info
        info = ticker.info
        name = info.get('longName', symbol)
        currency = info.get('currency', 'USD')

        log(f"✓ Found {len(hist)} hourly data points")
        log(f"✓ Name: {name}")
        log(f"✓ Currency: {currency}")

        # Group data by hour of day
        hourly_groups = defaultdict(lambda: {'prices': [], 'volumes': []})

        for date, row in hist.iterrows():
            hour = date.hour
            price = float(row['Close'])
            volume = int(row['Volume'])

            hourly_groups[hour]['prices'].append(price)
            hourly_groups[hour]['volumes'].append(volume)

        # Calculate averages for each hour
        hourly_analysis = []
        for hour in range(24):
            if hour in hourly_groups and len(hourly_groups[hour]['prices']) > 0:
                prices = hourly_groups[hour]['prices']
                volumes = hourly_groups[hour]['volumes']

                avg_price = sum(prices) / len(prices)
                avg_volume = sum(volumes) / len(volumes)
                min_price = min(prices)
                max_price = max(prices)
                occurrences = len(prices)

                # Calculate price volatility (standard deviation)
                mean_price = avg_price
                variance = sum((p - mean_price) ** 2 for p in prices) / len(prices)
                std_dev = variance ** 0.5

                hourly_analysis.append({
                    'hour': hour,
                    'timeLabel': f"{hour:02d}:00",
                    'avgPrice': round(avg_price, 2),
                    'avgVolume': round(avg_volume, 0),
                    'minPrice': round(min_price, 2),
                    'maxPrice': round(max_price, 2),
                    'priceStdDev': round(std_dev, 2),
                    'occurrences': occurrences
                })

        # Sort by hour
        hourly_analysis.sort(key=lambda x: x['hour'])

        # Find peak hours
        if hourly_analysis:
            highest_price_hour = max(hourly_analysis, key=lambda x: x['avgPrice'])
            lowest_price_hour = min(hourly_analysis, key=lambda x: x['avgPrice'])
            highest_volume_hour = max(hourly_analysis, key=lambda x: x['avgVolume'])
            lowest_volume_hour = min(hourly_analysis, key=lambda x: x['avgVolume'])
            most_volatile_hour = max(hourly_analysis, key=lambda x: x['priceStdDev'])

            log(f"\nHourly Analysis Summary:")
            log(f"  Highest Avg Price: {highest_price_hour['timeLabel']} (${highest_price_hour['avgPrice']})")
            log(f"  Lowest Avg Price: {lowest_price_hour['timeLabel']} (${lowest_price_hour['avgPrice']})")
            log(f"  Highest Avg Volume: {highest_volume_hour['timeLabel']} ({highest_volume_hour['avgVolume']:,.0f})")
            log(f"  Most Volatile: {most_volatile_hour['timeLabel']} (σ=${most_volatile_hour['priceStdDev']})")

        result = {
            'success': True,
            'symbol': symbol,
            'name': name,
            'currency': currency,
            'days': days,
            'dataPoints': len(hist),
            'startDate': hist.index[0].strftime('%Y-%m-%d %H:%M:%S'),
            'endDate': hist.index[-1].strftime('%Y-%m-%d %H:%M:%S'),
            'hourlyAnalysis': hourly_analysis,
            'summary': {
                'highestPriceHour': highest_price_hour if hourly_analysis else None,
                'lowestPriceHour': lowest_price_hour if hourly_analysis else None,
                'highestVolumeHour': highest_volume_hour if hourly_analysis else None,
                'lowestVolumeHour': lowest_volume_hour if hourly_analysis else None,
                'mostVolatileHour': most_volatile_hour if hourly_analysis else None
            },
            'fetched_at': datetime.now().isoformat()
        }

        log(f"\n{'='*60}")
        log(f"✓ Successfully analyzed hourly data for {symbol}")
        log(f"{'='*60}\n")

        return result

    except Exception as e:
        log(f"\n✗ Error fetching hourly data for {symbol}: {str(e)}")
        return {
            'success': False,
            'error': str(e),
            'symbol': symbol
        }

if __name__ == '__main__':
    if len(sys.argv) < 2:
        log("Usage: python fetch_etf_hourly.py SYMBOL [DAYS]")
        log("Example: python fetch_etf_hourly.py XEG.TO 730")
        log("Note: Maximum 730 days for hourly data")
        sys.exit(1)

    symbol = sys.argv[1].upper()
    days = int(sys.argv[2]) if len(sys.argv) > 2 else 730

    result = fetch_etf_hourly(symbol, days)

    if result:
        # Output JSON to stdout
        print(json.dumps(result, indent=2))
    else:
        sys.exit(1)
