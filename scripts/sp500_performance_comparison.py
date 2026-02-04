"""
S&P 500 Performance Comparison Tool
Compares your portfolio performance against the S&P 500 index
Calculates key metrics: Returns, Alpha, Beta, Sharpe Ratio, Drawdowns
"""

import requests
import pandas as pd
import numpy as np
from datetime import datetime, timedelta
import json
from typing import Dict, List, Any
import warnings
import sys
import argparse
warnings.filterwarnings('ignore')

# Set UTF-8 encoding for Windows console
if sys.platform == "win32":
    try:
        sys.stdout.reconfigure(encoding='utf-8')
    except:
        pass  # Fallback to default encoding

# Configuration
API_BASE_URL = "http://localhost:5000/api"
BENCHMARK_SYMBOL = "SPY"  # S&P 500 ETF

class PerformanceComparator:
    def __init__(self, period_years=1):
        self.period_years = period_years
        self.end_date = datetime.now()
        self.start_date = self.end_date - timedelta(days=period_years * 365)

    def fetch_portfolio_stocks(self) -> List[Dict[str, Any]]:
        """Fetch stocks from Finance API"""
        try:
            print(f"📊 Fetching portfolio from Finance API...")
            response = requests.get(f"{API_BASE_URL}/stocks", timeout=10)
            response.raise_for_status()
            stocks = response.json()
            print(f"✅ Found {len(stocks)} stocks in portfolio")
            return stocks
        except requests.exceptions.RequestException as e:
            print(f"❌ Error fetching portfolio: {e}")
            return []

    def fetch_historical_data(self, symbol: str) -> pd.DataFrame:
        """Fetch historical price data using yfinance"""
        try:
            import yfinance as yf

            # Clean symbol (remove .TO for Yahoo Finance format)
            clean_symbol = symbol
            if symbol.endswith('.TO'):
                clean_symbol = symbol  # Keep .TO for Canadian stocks

            print(f"  Fetching data for {symbol}...", end=" ")
            ticker = yf.Ticker(clean_symbol)
            df = ticker.history(start=self.start_date, end=self.end_date)

            if df.empty:
                print("❌ No data")
                return pd.DataFrame()

            print(f"✅ {len(df)} days")
            return df[['Close']]
        except Exception as e:
            print(f"❌ Error: {e}")
            return pd.DataFrame()

    def calculate_returns(self, prices: pd.DataFrame) -> pd.Series:
        """Calculate daily returns"""
        return prices['Close'].pct_change().dropna()

    def calculate_cumulative_returns(self, returns: pd.Series) -> pd.Series:
        """Calculate cumulative returns (growth of $1)"""
        return (1 + returns).cumprod()

    def calculate_annualized_return(self, returns: pd.Series) -> float:
        """Calculate annualized return"""
        cumulative_return = (1 + returns).prod() - 1
        years = len(returns) / 252  # 252 trading days per year
        if years > 0:
            return (1 + cumulative_return) ** (1 / years) - 1
        return 0

    def calculate_volatility(self, returns: pd.Series) -> float:
        """Calculate annualized volatility (standard deviation)"""
        return returns.std() * np.sqrt(252)

    def calculate_sharpe_ratio(self, returns: pd.Series, risk_free_rate=0.04) -> float:
        """Calculate Sharpe Ratio (assuming 4% risk-free rate)"""
        annualized_return = self.calculate_annualized_return(returns)
        volatility = self.calculate_volatility(returns)

        if volatility > 0:
            return (annualized_return - risk_free_rate) / volatility
        return 0

    def calculate_beta(self, stock_returns: pd.Series, market_returns: pd.Series) -> float:
        """Calculate beta (systematic risk relative to market)"""
        # Align the series
        aligned = pd.DataFrame({
            'stock': stock_returns,
            'market': market_returns
        }).dropna()

        if len(aligned) < 20:  # Need sufficient data
            return np.nan

        covariance = aligned['stock'].cov(aligned['market'])
        market_variance = aligned['market'].var()

        if market_variance > 0:
            return covariance / market_variance
        return np.nan

    def calculate_alpha(self, stock_returns: pd.Series, market_returns: pd.Series,
                       beta: float, risk_free_rate=0.04) -> float:
        """Calculate alpha (excess return over expected return)"""
        stock_annual_return = self.calculate_annualized_return(stock_returns)
        market_annual_return = self.calculate_annualized_return(market_returns)

        if np.isnan(beta):
            return np.nan

        expected_return = risk_free_rate + beta * (market_annual_return - risk_free_rate)
        return stock_annual_return - expected_return

    def calculate_max_drawdown(self, returns: pd.Series) -> Dict[str, float]:
        """Calculate maximum drawdown"""
        cumulative = self.calculate_cumulative_returns(returns)
        running_max = cumulative.cummax()
        drawdown = (cumulative - running_max) / running_max

        max_dd = drawdown.min()
        max_dd_date = drawdown.idxmin() if not drawdown.empty else None

        return {
            'max_drawdown': max_dd,
            'max_drawdown_date': str(max_dd_date) if max_dd_date else None,
            'max_drawdown_pct': max_dd * 100
        }

    def calculate_correlation(self, stock_returns: pd.Series, market_returns: pd.Series) -> float:
        """Calculate correlation with market"""
        aligned = pd.DataFrame({
            'stock': stock_returns,
            'market': market_returns
        }).dropna()

        if len(aligned) < 20:
            return np.nan

        return aligned['stock'].corr(aligned['market'])

    def analyze_stock_performance(self, symbol: str, benchmark_returns: pd.Series) -> Dict[str, Any]:
        """Analyze individual stock performance vs benchmark"""
        stock_data = self.fetch_historical_data(symbol)

        if stock_data.empty:
            return None

        stock_returns = self.calculate_returns(stock_data)

        # Align stock and benchmark returns
        aligned = pd.DataFrame({
            'stock': stock_returns,
            'benchmark': benchmark_returns
        }).dropna()

        if len(aligned) < 20:
            return None

        stock_returns_aligned = aligned['stock']
        benchmark_returns_aligned = aligned['benchmark']

        # Calculate metrics
        beta = self.calculate_beta(stock_returns_aligned, benchmark_returns_aligned)
        alpha = self.calculate_alpha(stock_returns_aligned, benchmark_returns_aligned, beta)

        total_return = (1 + stock_returns_aligned).prod() - 1
        benchmark_total_return = (1 + benchmark_returns_aligned).prod() - 1

        drawdown_info = self.calculate_max_drawdown(stock_returns_aligned)

        return {
            'symbol': symbol,
            'total_return': total_return * 100,
            'annualized_return': self.calculate_annualized_return(stock_returns_aligned) * 100,
            'volatility': self.calculate_volatility(stock_returns_aligned) * 100,
            'sharpe_ratio': self.calculate_sharpe_ratio(stock_returns_aligned),
            'beta': beta,
            'alpha': alpha * 100 if not np.isnan(alpha) else None,
            'correlation': self.calculate_correlation(stock_returns_aligned, benchmark_returns_aligned),
            'max_drawdown_pct': drawdown_info['max_drawdown_pct'],
            'vs_benchmark': (total_return - benchmark_total_return) * 100,
            'days_analyzed': len(stock_returns_aligned)
        }

    def run_comparison(self):
        """Run full portfolio vs S&P 500 comparison"""
        print("\n" + "="*60, file=sys.stderr)
        print("📈 S&P 500 PERFORMANCE COMPARISON", file=sys.stderr)
        print("="*60, file=sys.stderr)
        print(f"Period: {self.start_date.strftime('%Y-%m-%d')} to {self.end_date.strftime('%Y-%m-%d')}", file=sys.stderr)
        print(f"Benchmark: {BENCHMARK_SYMBOL} (S&P 500)", file=sys.stderr)
        print("="*60 + "\n", file=sys.stderr)

        # Fetch benchmark data
        print(f"📊 Fetching benchmark data ({BENCHMARK_SYMBOL})...", file=sys.stderr)
        benchmark_data = self.fetch_historical_data(BENCHMARK_SYMBOL)

        if benchmark_data.empty:
            print("❌ Failed to fetch benchmark data. Exiting.", file=sys.stderr)
            return None

        benchmark_returns = self.calculate_returns(benchmark_data)
        benchmark_metrics = {
            'total_return': ((1 + benchmark_returns).prod() - 1) * 100,
            'annualized_return': self.calculate_annualized_return(benchmark_returns) * 100,
            'volatility': self.calculate_volatility(benchmark_returns) * 100,
            'sharpe_ratio': self.calculate_sharpe_ratio(benchmark_returns),
            'max_drawdown_pct': self.calculate_max_drawdown(benchmark_returns)['max_drawdown_pct']
        }

        print(f"\n📊 {BENCHMARK_SYMBOL} Benchmark Performance:", file=sys.stderr)
        print(f"  Total Return: {benchmark_metrics['total_return']:.2f}%", file=sys.stderr)
        print(f"  Annualized Return: {benchmark_metrics['annualized_return']:.2f}%", file=sys.stderr)
        print(f"  Volatility: {benchmark_metrics['volatility']:.2f}%", file=sys.stderr)
        print(f"  Sharpe Ratio: {benchmark_metrics['sharpe_ratio']:.2f}", file=sys.stderr)
        print(f"  Max Drawdown: {benchmark_metrics['max_drawdown_pct']:.2f}%", file=sys.stderr)

        # Fetch and analyze portfolio
        stocks = self.fetch_portfolio_stocks()

        if not stocks:
            print("❌ No stocks to analyze", file=sys.stderr)
            return None

        print(f"\n🔍 Analyzing {len(stocks)} stocks...\n", file=sys.stderr)

        results = []
        for stock in stocks:
            symbol = stock.get('symbol', '')
            if not symbol:
                continue

            analysis = self.analyze_stock_performance(symbol, benchmark_returns)
            if analysis:
                results.append(analysis)

        # Sort by total return
        results.sort(key=lambda x: x['total_return'], reverse=True)

        # Display results
        print("\n" + "="*60, file=sys.stderr)
        print("📊 PORTFOLIO PERFORMANCE SUMMARY", file=sys.stderr)
        print("="*60, file=sys.stderr)

        print(f"\n{'Symbol':<10} {'Return %':<12} {'vs S&P500':<12} {'Beta':<8} {'Alpha %':<10} {'Sharpe':<8}", file=sys.stderr)
        print("-" * 60, file=sys.stderr)

        for r in results:
            alpha_str = f"{r['alpha']:.2f}" if r['alpha'] is not None else "N/A"
            beta_str = f"{r['beta']:.2f}" if not np.isnan(r['beta']) else "N/A"

            print(f"{r['symbol']:<10} {r['total_return']:>10.2f}% {r['vs_benchmark']:>10.2f}% "
                  f"{beta_str:>6} {alpha_str:>8} {r['sharpe_ratio']:>6.2f}", file=sys.stderr)

        # Portfolio summary
        if results:
            avg_return = np.mean([r['total_return'] for r in results])
            avg_alpha = np.mean([r['alpha'] for r in results if r['alpha'] is not None])
            avg_beta = np.mean([r['beta'] for r in results if not np.isnan(r['beta'])])

            print("\n" + "="*60, file=sys.stderr)
            print("📈 PORTFOLIO AVERAGES", file=sys.stderr)
            print("="*60, file=sys.stderr)
            print(f"Average Return: {avg_return:.2f}%", file=sys.stderr)
            print(f"Average Alpha: {avg_alpha:.2f}%", file=sys.stderr)
            print(f"Average Beta: {avg_beta:.2f}", file=sys.stderr)
            print(f"Stocks Outperforming S&P 500: {len([r for r in results if r['vs_benchmark'] > 0])}/{len(results)}", file=sys.stderr)

        # Return results (will be output as JSON to stdout)
        return {
            'benchmark': benchmark_metrics,
            'stocks': results,
            'period': {
                'start': self.start_date.strftime('%Y-%m-%d'),
                'end': self.end_date.strftime('%Y-%m-%d'),
                'years': self.period_years
            }
        }

    def export_results(self, results: List[Dict], benchmark: Dict):
        """Export results to JSON and CSV"""
        timestamp = datetime.now().strftime('%Y%m%d_%H%M%S')

        # JSON export
        json_filename = f"performance_comparison_{timestamp}.json"
        with open(json_filename, 'w') as f:
            json.dump({
                'benchmark': benchmark,
                'stocks': results,
                'period': {
                    'start': self.start_date.strftime('%Y-%m-%d'),
                    'end': self.end_date.strftime('%Y-%m-%d'),
                    'years': self.period_years
                },
                'generated_at': datetime.now().isoformat()
            }, f, indent=2)

        print(f"\n✅ Exported JSON: {json_filename}")

        # CSV export
        if results:
            df = pd.DataFrame(results)
            csv_filename = f"performance_comparison_{timestamp}.csv"
            df.to_csv(csv_filename, index=False)
            print(f"✅ Exported CSV: {csv_filename}")


def main():
    """Main entry point"""
    parser = argparse.ArgumentParser(
        description='S&P 500 Performance Comparison Tool',
        formatter_class=argparse.RawDescriptionHelpFormatter
    )
    parser.add_argument(
        '-p', '--period',
        type=float,
        default=1,
        help='Analysis period in years (default: 1)'
    )
    parser.add_argument(
        '-i', '--interactive',
        action='store_true',
        help='Run in interactive mode with prompts'
    )

    args = parser.parse_args()

    try:
        # Interactive mode
        if args.interactive:
            print("Select analysis period:")
            print("1. 1 Year")
            print("2. 3 Years")
            print("3. 5 Years")
            print("4. Custom")

            choice = input("\nEnter choice (1-4) [default: 1]: ").strip() or "1"

            if choice == "1":
                period = 1
            elif choice == "2":
                period = 3
            elif choice == "3":
                period = 5
            elif choice == "4":
                period = float(input("Enter number of years: "))
            else:
                period = 1
        else:
            # Non-interactive mode (use command-line argument)
            period = args.period

        print(f"\n🚀 Starting {period}-year performance comparison...", file=sys.stderr)

        comparator = PerformanceComparator(period_years=period)
        results = comparator.run_comparison()

        if results:
            # Output JSON to stdout for the API to consume
            print(json.dumps(results))

        print("\n" + "="*60, file=sys.stderr)
        print("✅ Analysis complete!", file=sys.stderr)
        print("="*60, file=sys.stderr)

    except KeyboardInterrupt:
        print("\n\n❌ Analysis cancelled by user")
    except Exception as e:
        print(f"\n❌ Error: {e}")


if __name__ == "__main__":
    main()
