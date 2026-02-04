"""
Fetch all stock listings from TSX exchange
Uses official TSX company directory API
"""

import requests
import json
import sys
from datetime import datetime

def fetch_tsx_listings():
    """Fetch all TSX listed companies from official TSX API"""
    try:
        print("Fetching TSX listings from official API...", file=sys.stderr)

        # TSX official company directory API
        url = "https://www.tsx.com/json/company-directory/search/tsx/%5E*"

        headers = {
            'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36',
            'Accept': 'application/json'
        }

        response = requests.get(url, headers=headers, timeout=30)
        response.raise_for_status()

        data = response.json()

        # Extract stock symbols
        stocks = []
        if 'results' in data:
            for company in data['results']:
                symbol = company.get('symbol', '')
                name = company.get('name', '')
                sector = company.get('sector', '')

                if symbol:
                    # Add .TO suffix for TSX stocks (Yahoo Finance format)
                    yahoo_symbol = f"{symbol}.TO"
                    stocks.append({
                        'symbol': yahoo_symbol,
                        'name': name,
                        'sector': sector,
                        'exchange': 'TSX',
                        'originalSymbol': symbol
                    })

        print(f"Found {len(stocks)} TSX stocks", file=sys.stderr)

        result = {
            'success': True,
            'exchange': 'TSX',
            'stocks': stocks,
            'count': len(stocks),
            'fetchedAt': datetime.utcnow().isoformat()
        }

        # Output JSON to stdout for C# to consume
        print(json.dumps(result))
        return 0

    except requests.exceptions.RequestException as e:
        error_result = {
            'success': False,
            'error': f'Request error: {str(e)}',
            'fetchedAt': datetime.utcnow().isoformat()
        }
        print(json.dumps(error_result))
        return 1

    except Exception as e:
        error_result = {
            'success': False,
            'error': str(e),
            'fetchedAt': datetime.utcnow().isoformat()
        }
        print(json.dumps(error_result))
        return 1

if __name__ == "__main__":
    sys.exit(fetch_tsx_listings())
