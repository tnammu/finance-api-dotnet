"""
S&P 500 Stock Validator
Verifies if stocks in your portfolio are part of the S&P 500 index
Links with your Finance API to sync data
"""

import requests
import pandas as pd
import json
from datetime import datetime
from typing import List, Dict, Any

# Configuration
API_BASE_URL = "http://localhost:5000/api"
SP500_URL = "https://en.wikipedia.org/wiki/List_of_S%26P_500_companies"

class SP500Validator:
    def __init__(self):
        self.sp500_tickers = []
        self.sp500_data = None

    def fetch_sp500_list(self) -> bool:
        """Fetch current S&P 500 constituents from Wikipedia"""
        try:
            print("📥 Fetching S&P 500 list from Wikipedia...")

            # Read S&P 500 data from Wikipedia
            tables = pd.read_html(SP500_URL)
            self.sp500_data = tables[0]

            # Extract tickers
            self.sp500_tickers = self.sp500_data['Symbol'].str.replace('.', '-').tolist()

            print(f"✅ Loaded {len(self.sp500_tickers)} S&P 500 stocks")
            return True

        except Exception as e:
            print(f"❌ Error fetching S&P 500 list: {e}")
            return False

    def get_portfolio_stocks(self) -> List[Dict[str, Any]]:
        """Fetch stocks from Finance API"""
        try:
            print(f"\n📊 Fetching stocks from Finance API...")
            response = requests.get(f"{API_BASE_URL}/stocks", timeout=10)
            response.raise_for_status()

            stocks = response.json()
            print(f"✅ Found {len(stocks)} stocks in portfolio")
            return stocks

        except requests.exceptions.RequestException as e:
            print(f"❌ Error fetching portfolio: {e}")
            return []

    def validate_stock(self, symbol: str) -> Dict[str, Any]:
        """Check if a stock is in S&P 500"""
        # Remove exchange suffix for comparison
        clean_symbol = symbol.replace('.TO', '').replace('.', '-')

        is_sp500 = clean_symbol in self.sp500_tickers

        result = {
            'symbol': symbol,
            'is_sp500': is_sp500,
            'index': 'S&P 500' if is_sp500 else 'Not in S&P 500'
        }

        # Get additional info if in S&P 500
        if is_sp500:
            stock_info = self.sp500_data[self.sp500_data['Symbol'].str.replace('.', '-') == clean_symbol]
            if not stock_info.empty:
                result['company_name'] = stock_info.iloc[0]['Security']
                result['sector'] = stock_info.iloc[0]['GICS Sector']
                result['industry'] = stock_info.iloc[0]['GICS Sub-Industry']
                result['headquarters'] = stock_info.iloc[0]['Headquarters Location']

        return result

    def analyze_portfolio(self) -> Dict[str, Any]:
        """Analyze entire portfolio against S&P 500"""
        stocks = self.get_portfolio_stocks()

        if not stocks:
            return {'error': 'No stocks found in portfolio'}

        results = []
        sp500_count = 0
        non_sp500_count = 0

        print(f"\n🔍 Validating {len(stocks)} stocks against S&P 500...")
        print("=" * 80)

        for stock in stocks:
            symbol = stock['symbol']
            validation = self.validate_stock(symbol)

            # Add current stock data
            validation['current_price'] = stock.get('price', 0)
            validation['dividend_yield'] = stock.get('dividendYield', 0)

            results.append(validation)

            if validation['is_sp500']:
                sp500_count += 1
                status = "✅ S&P 500"
            else:
                non_sp500_count += 1
                status = "❌ Not S&P 500"

            print(f"{symbol:10} | {status:15} | ${validation['current_price']:8.2f}")

        print("=" * 80)

        return {
            'total_stocks': len(stocks),
            'sp500_stocks': sp500_count,
            'non_sp500_stocks': non_sp500_count,
            'sp500_percentage': (sp500_count / len(stocks) * 100) if stocks else 0,
            'details': results,
            'analyzed_at': datetime.now().isoformat()
        }

    def export_report(self, analysis: Dict[str, Any], filename: str = None):
        """Export analysis report to JSON and CSV"""
        if filename is None:
            filename = f"sp500_validation_{datetime.now().strftime('%Y%m%d_%H%M%S')}"

        # Export JSON
        json_file = f"{filename}.json"
        with open(json_file, 'w') as f:
            json.dump(analysis, f, indent=2)
        print(f"\n📄 JSON report saved: {json_file}")

        # Export CSV
        if analysis.get('details'):
            df = pd.DataFrame(analysis['details'])
            csv_file = f"{filename}.csv"
            df.to_csv(csv_file, index=False)
            print(f"📊 CSV report saved: {csv_file}")

    def get_sp500_recommendations(self) -> List[Dict[str, Any]]:
        """Get recommended S&P 500 dividend stocks not in portfolio"""
        try:
            # Get current portfolio
            portfolio_stocks = [s['symbol'].replace('.TO', '').replace('.', '-')
                              for s in self.get_portfolio_stocks()]

            # Filter S&P 500 stocks not in portfolio
            # Focus on known dividend aristocrats
            dividend_aristocrats = [
                'AAPL', 'JNJ', 'PG', 'KO', 'PEP', 'MCD', 'WMT', 'V', 'MA', 'HD',
                'LOW', 'TGT', 'COST', 'ABT', 'UNH', 'CVX', 'XOM', 'JPM', 'BAC'
            ]

            recommendations = []
            for ticker in dividend_aristocrats:
                if ticker in self.sp500_tickers and ticker not in portfolio_stocks:
                    stock_info = self.sp500_data[self.sp500_data['Symbol'] == ticker]
                    if not stock_info.empty:
                        recommendations.append({
                            'symbol': ticker,
                            'company': stock_info.iloc[0]['Security'],
                            'sector': stock_info.iloc[0]['GICS Sector'],
                            'reason': 'Dividend Aristocrat'
                        })

            return recommendations

        except Exception as e:
            print(f"❌ Error getting recommendations: {e}")
            return []

def main():
    print("=" * 80)
    print("🏦 S&P 500 Stock Validator")
    print("=" * 80)

    validator = SP500Validator()

    # Step 1: Fetch S&P 500 list
    if not validator.fetch_sp500_list():
        print("Failed to fetch S&P 500 data")
        return

    # Step 2: Analyze portfolio
    analysis = validator.analyze_portfolio()

    if 'error' in analysis:
        print(f"\n❌ {analysis['error']}")
        return

    # Step 3: Print summary
    print(f"\n📊 Portfolio Analysis Summary:")
    print(f"   Total Stocks: {analysis['total_stocks']}")
    print(f"   S&P 500 Stocks: {analysis['sp500_stocks']} ({analysis['sp500_percentage']:.1f}%)")
    print(f"   Non-S&P 500 Stocks: {analysis['non_sp500_stocks']}")

    # Step 4: Export report
    validator.export_report(analysis)

    # Step 5: Get recommendations
    print(f"\n💡 S&P 500 Dividend Stock Recommendations:")
    recommendations = validator.get_sp500_recommendations()
    if recommendations:
        for i, rec in enumerate(recommendations[:10], 1):
            print(f"   {i}. {rec['symbol']:6} - {rec['company']:40} ({rec['sector']})")
    else:
        print("   No recommendations available (or already own them all!)")

    print("\n✅ Analysis complete!")

if __name__ == "__main__":
    main()
