#!/usr/bin/env python3
"""
Update Stock Data from Yahoo Finance
Fetches current stock prices, dividend yields, and company info from Yahoo Finance
and updates the dividends.db database (DividendModels table).
"""

import sqlite3
import yfinance as yf
from datetime import datetime
import time
import sys
import os
import math

# Set UTF-8 encoding for Windows console
if sys.platform == "win32":
    try:
        sys.stdout.reconfigure(encoding='utf-8')
    except:
        pass  # Fallback to default encoding

# Get the database path (relative to script location)
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
PROJECT_ROOT = os.path.dirname(SCRIPT_DIR)
DB_PATH = os.path.join(PROJECT_ROOT, "dividends.db")


class StockDataUpdater:
    def __init__(self, db_path):
        self.db_path = db_path
        self.conn = None
        self.cursor = None
        self.updated_count = 0
        self.failed_count = 0
        self.failed_symbols = []

    def connect_db(self):
        """Connect to SQLite database"""
        try:
            self.conn = sqlite3.connect(self.db_path)
            self.cursor = self.conn.cursor()
            print(f"✓ Connected to database: {self.db_path}")
            return True
        except Exception as e:
            print(f"✗ Failed to connect to database: {e}")
            return False

    def get_all_stocks(self):
        """Retrieve all stock symbols from database"""
        try:
            self.cursor.execute("SELECT Id, Symbol FROM DividendModels ORDER BY Symbol")
            stocks = self.cursor.fetchall()
            print(f"✓ Found {len(stocks)} stocks in database")
            return stocks
        except Exception as e:
            print(f"✗ Failed to retrieve stocks: {e}")
            return []

    def calculate_payout_policy(self, payout_ratio, dividend_yield):
        """Determine payout policy based on payout ratio and dividend yield"""
        if payout_ratio is None or payout_ratio == 0:
            if dividend_yield and dividend_yield > 0:
                # Has dividend but no payout ratio (ETF or missing data)
                return "Dividend Only", 100.0, 0.0
            else:
                # No dividends at all
                return "Reinvestment Only", 0.0, 100.0

        # Calculate allocation percentages
        dividend_allocation = min(payout_ratio, 100.0)  # Cap at 100%
        reinvestment_allocation = max(100.0 - payout_ratio, 0.0)  # Ensure non-negative

        # Determine policy category
        if payout_ratio >= 95:
            policy = "Dividend Only"
        elif payout_ratio <= 5:
            policy = "Reinvestment Only"
        else:
            policy = "Mixed"

        return policy, dividend_allocation, reinvestment_allocation

    def calculate_safety_score(self, dividend_yield, payout_ratio, dividend_growth_rate, consecutive_years, beta):
        """Calculate safety score based on dividend metrics (0-5 scale)"""
        score = 0.0

        # Dividend Yield Score (0-1 points) - Optimal range: 2-6%
        if dividend_yield:
            if 2.0 <= dividend_yield <= 6.0:
                score += 1.0
            elif 1.0 <= dividend_yield < 2.0 or 6.0 < dividend_yield <= 8.0:
                score += 0.7
            elif dividend_yield > 0:
                score += 0.3

        # Payout Ratio Score (0-1 points) - Lower is safer (< 60% is ideal)
        if payout_ratio:
            if payout_ratio < 50:
                score += 1.0
            elif payout_ratio < 60:
                score += 0.8
            elif payout_ratio < 75:
                score += 0.5
            elif payout_ratio < 90:
                score += 0.3
            # Over 90% gets 0 points
        elif dividend_yield and dividend_yield > 0:
            # If no payout ratio but has dividend, give partial credit
            score += 0.5

        # Dividend Growth Rate Score (0-1 points)
        if dividend_growth_rate:
            if dividend_growth_rate >= 10:
                score += 1.0
            elif dividend_growth_rate >= 5:
                score += 0.8
            elif dividend_growth_rate >= 0:
                score += 0.5
            # Negative growth gets 0 points

        # Consecutive Years Score (0-1 points)
        if consecutive_years >= 25:
            score += 1.0
        elif consecutive_years >= 10:
            score += 0.8
        elif consecutive_years >= 5:
            score += 0.5
        elif consecutive_years > 0:
            score += 0.3

        # Beta Score (0-1 points) - Lower volatility is better
        if beta:
            if beta < 0.8:
                score += 1.0
            elif beta < 1.0:
                score += 0.8
            elif beta < 1.2:
                score += 0.5
            elif beta < 1.5:
                score += 0.3
            # High beta gets 0 points

        # Determine rating
        if score >= 4.5:
            rating = "Excellent"
        elif score >= 3.5:
            rating = "Very Good"
        elif score >= 2.5:
            rating = "Good"
        elif score >= 1.5:
            rating = "Fair"
        elif score >= 0.5:
            rating = "Below Average"
        else:
            rating = "Poor"

        # Generate recommendation
        if score >= 4.0:
            recommendation = "Strong dividend stock with excellent safety metrics. Suitable for income-focused portfolios."
        elif score >= 3.0:
            recommendation = "Good dividend stock with solid fundamentals. Consider for balanced income portfolios."
        elif score >= 2.0:
            recommendation = "Acceptable dividend stock but monitor closely. Suitable for moderate risk tolerance."
        elif score >= 1.0:
            recommendation = "Below average dividend metrics. Higher risk - consider alternatives."
        else:
            recommendation = "Poor dividend safety. High risk - not recommended for income investors."

        return round(score, 2), rating, recommendation

    def fetch_yahoo_data(self, symbol):
        """Fetch stock data from Yahoo Finance with advanced metrics"""
        def safe_float(value, default=None):
            if value is None:
                return default
            try:
                f = float(value)
                if math.isinf(f) or math.isnan(f):
                    return default
                return f
            except (ValueError, TypeError):
                return default
        try:
            # Create ticker object
            ticker = yf.Ticker(symbol)

            # Get current price and info
            info = ticker.info

            # Extract basic data
            current_price = info.get('currentPrice') or info.get('regularMarketPrice') or info.get('previousClose')
            company_name = info.get('longName') or info.get('shortName') or symbol

            if current_price is None:
                print(f"  ⚠️  {symbol}: No price data available")
                return None

            # Get dividend data
            dividend_yield = info.get('dividendYield')
            # Prefer dividendRate (most current) over trailingAnnualDividendRate
            dividend_rate = info.get('dividendRate')
            trailing_annual_dividend = info.get('trailingAnnualDividendRate')

            # Get EPS for payout ratio calculation
            eps = info.get('trailingEps')

            # Detect if this is an ETF
            quote_type = info.get('quoteType', '')
            is_etf = quote_type == 'ETF'

            # Get sector and industry (ETFs won't have these)
            if is_etf:
                sector = 'ETF'
                industry = info.get('category', 'Exchange Traded Fund')
            else:
                sector = info.get('sector', 'Unknown')
                industry = info.get('industry', 'Unknown')

            # Get beta
            beta = safe_float(info.get('beta'))

            # Get valuation metrics  
            pe_ratio = safe_float(info.get('trailingPE')) or safe_float(info.get('forwardPE'))
            pb_ratio = safe_float(info.get('priceToBook'))
            market_cap = safe_float(info.get('marketCap'))
            earnings_growth = safe_float(info.get('earningsGrowth'))

            # Calculate dividend per share (prefer most current source)
            dividend_per_share = None
            if dividend_rate:
                # Most current - based on latest quarterly payment annualized
                dividend_per_share = float(dividend_rate)
            elif trailing_annual_dividend:
                # Trailing annual rate
                dividend_per_share = float(trailing_annual_dividend)
            elif dividend_yield and current_price:
                # Calculate from yield - yield is already in percentage, so divide by 100
                dividend_per_share = (float(dividend_yield) / 100) * float(current_price)

            # Calculate payout ratio (not applicable for ETFs)
            payout_ratio = None
            if is_etf:
                print(f"    📦 ETF: {industry} - Payout ratio N/A for ETFs")
            elif eps and eps > 0 and dividend_per_share and dividend_per_share > 0:
                payout_ratio = (dividend_per_share / eps) * 100
                print(f"    💰 Payout Ratio: {payout_ratio:.2f}% (DPS: ${dividend_per_share:.2f}, EPS: ${eps:.2f})")
            elif dividend_per_share and dividend_per_share > 0:
                print(f"    💰 Dividend: ${dividend_per_share:.2f} (EPS data not available)")

            # Fetch dividend history for growth rate calculation
            dividend_growth_rate = None
            consecutive_years = 0
            yearly_dividends_data = {}  # Store {year: dividend_amount}
            try:
                dividends = ticker.dividends
                if len(dividends) > 0:
                    # Group by year and calculate growth
                    yearly_dividends = dividends.groupby(dividends.index.year).sum()
                    yearly_dividends_data = yearly_dividends.to_dict()

                    if len(yearly_dividends) >= 2:
                        # Calculate year-over-year growth rates (last 10 years only)
                        growth_rates = []
                        years = list(yearly_dividends.index)

                        # Use last 10 years for more relevant growth rate
                        recent_years = yearly_dividends.tail(11)  # 11 years gives us 10 growth calculations

                        for i in range(1, len(recent_years)):
                            prev_year_div = recent_years.iloc[i-1]
                            curr_year_div = recent_years.iloc[i]

                            if prev_year_div > 0:
                                growth = ((curr_year_div - prev_year_div) / prev_year_div) * 100
                                growth_rates.append(growth)

                        if growth_rates:
                            dividend_growth_rate = sum(growth_rates) / len(growth_rates)
                            years_counted = len(growth_rates)
                            print(f"    📈 Dividend Growth: {dividend_growth_rate:.2f}% avg over last {years_counted} years")

                        # Count consecutive years of payments
                        current_year = datetime.now().year
                        for year in sorted(years, reverse=True):
                            # Check if this year is the expected next consecutive year
                            expected_year = current_year - consecutive_years
                            if year >= expected_year:  # Allow for current year in progress
                                consecutive_years += 1
                            else:
                                break

                        print(f"    📅 Consecutive years: {consecutive_years}")
            except Exception as e:
                print(f"    ⚠️  Could not fetch dividend history: {e}")

            # Fetch historical EPS for accurate payout ratio calculation
            annual_eps_data = {}  # Store {year: eps}
            try:
                # Get annual income statement
                income_stmt = ticker.income_stmt
                if income_stmt is not None and not income_stmt.empty and 'Net Income' in income_stmt.index:
                    net_income_series = income_stmt.loc['Net Income']
                    shares_outstanding = info.get('sharesOutstanding')

                    if shares_outstanding and shares_outstanding > 0:
                        # Calculate EPS for each year
                        for date, net_income in net_income_series.items():
                            if net_income and net_income == net_income:  # Check not NaN (NaN != NaN)
                                year = date.year
                                calculated_eps = float(net_income) / shares_outstanding
                                # Only store if EPS is reasonable (avoid tiny values that create huge payout ratios)
                                if calculated_eps >= 0.10:  # Minimum threshold of $0.10
                                    annual_eps_data[year] = round(calculated_eps, 2)

                        if annual_eps_data:
                            print(f"    💹 Historical EPS: {len(annual_eps_data)} years")
            except Exception as e:
                print(f"    ⚠️  Could not fetch historical EPS: {e}")

            # Calculate Safety Score
            safety_score, safety_rating, recommendation = self.calculate_safety_score(
                float(dividend_yield) if dividend_yield else None,
                payout_ratio,
                dividend_growth_rate,
                consecutive_years,
                float(beta) if beta else None
            )

            if safety_score > 0:
                print(f"    ⭐ Safety Score: {safety_score:.1f}/5.0 ({safety_rating})")

            # Calculate Payout Policy
            payout_policy, dividend_allocation, reinvestment_allocation = self.calculate_payout_policy(
                payout_ratio,
                float(dividend_yield) if dividend_yield else None
            )
            print(f"    📊 Payout Policy: {payout_policy} (Dividend: {dividend_allocation:.1f}%, Reinvestment: {reinvestment_allocation:.1f}%)")

            return {
                'price': float(current_price),
                'company_name': company_name,
                'dividend_yield': float(dividend_yield) if dividend_yield else None,  # Yahoo returns percentage already
                'dividend_per_share': dividend_per_share,
                'eps': float(eps) if eps else None,
                'payout_ratio': payout_ratio,
                'dividend_growth_rate': dividend_growth_rate,
                'consecutive_years': consecutive_years,
                'sector': sector,
                'industry': industry,
                'beta': beta,
                # Valuation metrics
                'pe_ratio': pe_ratio if pe_ratio else 0.0,
                'pb_ratio': pb_ratio if pb_ratio else 0.0,
                'market_cap': market_cap if market_cap else 0.0,
                'earnings_growth': (earnings_growth * 100) if earnings_growth else 0.0,  # Convert to percentage
                # Payout policy
                'payout_policy': payout_policy,
                'dividend_allocation_pct': dividend_allocation,
                'reinvestment_allocation_pct': reinvestment_allocation,
                # Safety
                'safety_score': safety_score,
                'safety_rating': safety_rating,
                'recommendation': recommendation,
                'yearly_dividends': yearly_dividends_data,  # {year: dividend_amount}
                'annual_eps': annual_eps_data,  # {year: eps}
                'last_updated': datetime.utcnow().strftime('%Y-%m-%d %H:%M:%S')
            }
        except Exception as e:
            print(f"  ✗ {symbol}: Error fetching data - {str(e)}")
            return None

    def update_stock(self, stock_id, data):
        """Update stock record in database"""
        try:
            # Get symbol for yearly data update
            self.cursor.execute("SELECT Symbol FROM DividendModels WHERE Id = ?", (stock_id,))
            result = self.cursor.fetchone()
            symbol = result[0] if result else None

            query = """
                UPDATE DividendModels
                SET CurrentPrice = ?,
                    CompanyName = ?,
                    DividendYield = ?,
                    DividendPerShare = ?,
                    EPS = ?,
                    PayoutRatio = ?,
                    DividendGrowthRate = ?,
                    ConsecutiveYearsOfPayments = ?,
                    Sector = ?,
                    Industry = ?,
                    Beta = ?,
                    PE = ?,
                    PB = ?,
                    MarketCap = ?,
                    EarningsGrowth = ?,
                    PayoutPolicy = ?,
                    DividendAllocationPct = ?,
                    ReinvestmentAllocationPct = ?,
                    SafetyScore = ?,
                    SafetyRating = ?,
                    Recommendation = ?,
                    LastUpdated = ?
                WHERE Id = ?
            """
            self.cursor.execute(query, (
                data['price'],
                data['company_name'],
                data['dividend_yield'],
                data.get('dividend_per_share'),
                data.get('eps'),
                data.get('payout_ratio'),
                data.get('dividend_growth_rate'),
                data.get('consecutive_years', 0),
                data.get('sector', 'Unknown'),
                data.get('industry', 'Unknown'),
                data.get('beta'),
                data.get('pe_ratio', 0.0),
                data.get('pb_ratio', 0.0),
                data.get('market_cap', 0.0),
                data.get('earnings_growth', 0.0),
                data.get('payout_policy', 'Unknown'),
                data.get('dividend_allocation_pct'),
                data.get('reinvestment_allocation_pct'),
                data.get('safety_score', 0.0),
                data.get('safety_rating', 'N/A'),
                data.get('recommendation', 'No data available'),
                data['last_updated'],
                stock_id
            ))

            # Update yearly dividends and EPS data
            if symbol:
                self.save_yearly_data(stock_id, symbol, data)

            self.conn.commit()
            return True
        except Exception as e:
            print(f"  ✗ Failed to update database: {e}")
            return False

    def save_yearly_data(self, dividend_model_id, symbol, data):
        """Save or update yearly dividends and EPS data"""
        try:
            # Delete existing yearly data for this stock
            self.cursor.execute("DELETE FROM YearlyDividends WHERE DividendModelId = ?", (dividend_model_id,))

            # Get yearly dividends and annual EPS from data
            yearly_dividends = data.get('yearly_dividends', {})
            annual_eps = data.get('annual_eps', {})

            # Get all years from both datasets
            all_years = set(yearly_dividends.keys()) | set(annual_eps.keys())

            # Insert new records
            for year in sorted(all_years):
                dividend_amount = yearly_dividends.get(year, 0)
                eps_amount = annual_eps.get(year)

                # Count payments for this year (if available from dividend history)
                payment_count = 1 if dividend_amount > 0 else 0

                # Only insert if there's dividend data or EPS data
                if dividend_amount > 0 or eps_amount:
                    query = """
                        INSERT INTO YearlyDividends (
                            DividendModelId, Symbol, Year, TotalDividend, PaymentCount, AnnualEPS
                        ) VALUES (?, ?, ?, ?, ?, ?)
                    """
                    self.cursor.execute(query, (
                        dividend_model_id,
                        symbol,
                        year,
                        float(dividend_amount) if dividend_amount else 0,
                        payment_count,
                        float(eps_amount) if eps_amount else None
                    ))

        except Exception as e:
            print(f"    ⚠️  Could not save yearly data: {e}")

    def add_or_update_single_stock(self, symbol):
        """Add or update a single stock by symbol"""
        print(f"\n{'='*60}")
        print(f"Analyzing: {symbol}")
        print(f"{'='*60}\n")

        # Check if stock exists
        self.cursor.execute("SELECT Id FROM DividendModels WHERE Symbol = ?", (symbol,))
        existing = self.cursor.fetchone()

        # Fetch data from Yahoo Finance
        print(f"Fetching {symbol}...", end=" ")
        sys.stdout.flush()

        data = self.fetch_yahoo_data(symbol)

        if data:
            if existing:
                # Update existing
                if self.update_stock(existing[0], data):
                    price_str = f"${data['price']:.2f}"
                    div_str = f"{data['dividend_yield']:.2f}%" if data['dividend_yield'] else "N/A"
                    print(f"✓ Updated: {price_str}, Div: {div_str}")
                    return True
            else:
                # Insert new
                if self.insert_stock(symbol, data):
                    price_str = f"${data['price']:.2f}"
                    div_str = f"{data['dividend_yield']:.2f}%" if data['dividend_yield'] else "N/A"
                    print(f"✓ Added: {price_str}, Div: {div_str}")
                    return True
        else:
            print(f"✗ Failed to fetch data")
            return False

    def insert_stock(self, symbol, data):
        """Insert new stock into database"""
        try:
            query = """
                INSERT INTO DividendModels (
                    Symbol, CompanyName, CurrentPrice, DividendYield, DividendPerShare,
                    PayoutRatio, EPS, ProfitMargin, Beta,
                    PE, PB, MarketCap, EarningsGrowth,
                    PayoutPolicy, DividendAllocationPct, ReinvestmentAllocationPct,
                    Sector, Industry, ConsecutiveYearsOfPayments, DividendGrowthRate,
                    GrowthScore, GrowthRating,
                    SafetyScore, SafetyRating, Recommendation,
                    FetchedAt, LastUpdated, ApiCallsUsed
                ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            """
            self.cursor.execute(query, (
                symbol,
                data['company_name'],
                data['price'],
                data['dividend_yield'],
                data.get('dividend_per_share'),
                data.get('payout_ratio'),
                data.get('eps'),
                None,       # ProfitMargin - not available from basic Yahoo data
                data.get('beta'),
                data.get('pe_ratio', 0.0),
                data.get('pb_ratio', 0.0),
                data.get('market_cap', 0.0),
                data.get('earnings_growth', 0.0),
                data.get('payout_policy', 'Unknown'),
                data.get('dividend_allocation_pct'),
                data.get('reinvestment_allocation_pct'),
                data.get('sector', 'Unknown'),
                data.get('industry', 'Unknown'),
                data.get('consecutive_years', 0),
                data.get('dividend_growth_rate'),
                0.0,        # GrowthScore - not calculated for dividend stocks
                'N/A',      # GrowthRating - not calculated for dividend stocks
                data.get('safety_score', 0.0),
                data.get('safety_rating', 'N/A'),
                data.get('recommendation', 'No data available'),
                data['last_updated'],
                data['last_updated'],
                0
            ))

            # Get the ID of the newly inserted record
            dividend_model_id = self.cursor.lastrowid

            # Insert yearly dividends and EPS data
            self.save_yearly_data(dividend_model_id, symbol, data)

            self.conn.commit()
            return True
        except Exception as e:
            print(f"  ✗ Failed to insert: {e}")
            return False

    def update_all_stocks(self):
        """Main function to update all stocks"""
        stocks = self.get_all_stocks()

        if not stocks:
            print("No stocks found to update")
            return

        print(f"\n{'='*60}")
        print(f"Starting update for {len(stocks)} stocks...")
        print(f"{'='*60}\n")

        for stock_id, symbol in stocks:
            print(f"Fetching {symbol}...", end=" ")
            sys.stdout.flush()

            # Fetch data from Yahoo Finance
            data = self.fetch_yahoo_data(symbol)

            if data:
                # Update database
                if self.update_stock(stock_id, data):
                    price_str = f"${data['price']:.2f}"
                    div_str = f"{data['dividend_yield']:.2f}%" if data['dividend_yield'] else "N/A"
                    print(f"✓ Updated: {price_str}, Div: {div_str}")
                    self.updated_count += 1
                else:
                    self.failed_count += 1
                    self.failed_symbols.append(symbol)
            else:
                self.failed_count += 1
                self.failed_symbols.append(symbol)

            # Rate limiting - be respectful to Yahoo Finance
            time.sleep(0.5)

        self.print_summary()

    def print_summary(self):
        """Print update summary"""
        print(f"\n{'='*60}")
        print(f"UPDATE SUMMARY")
        print(f"{'='*60}")
        print(f"✓ Successfully updated: {self.updated_count} stocks")
        print(f"✗ Failed to update:     {self.failed_count} stocks")

        if self.failed_symbols:
            print(f"\nFailed symbols: {', '.join(self.failed_symbols)}")

        print(f"{'='*60}\n")

    def close(self):
        """Close database connection"""
        if self.conn:
            self.conn.close()
            print("✓ Database connection closed")


def main():
    print("\n" + "="*60)
    print("STOCK DATA UPDATER - Yahoo Finance")
    print("="*60 + "\n")

    # Check if database exists
    if not os.path.exists(DB_PATH):
        print(f"✗ Database not found: {DB_PATH}")
        print("  Make sure the backend has been run at least once to create the database.")
        return

    # Create updater
    updater = StockDataUpdater(DB_PATH)

    # Connect to database
    if not updater.connect_db():
        return

    try:
        # Check if a symbol was provided as argument
        if len(sys.argv) > 1:
            symbol = sys.argv[1].upper()
            success = updater.add_or_update_single_stock(symbol)
            if success:
                print(f"\n✓ Successfully processed {symbol}")
            else:
                print(f"\n✗ Failed to process {symbol}")
                sys.exit(1)
        else:
            # Update all stocks (original behavior)
            updater.update_all_stocks()
    except KeyboardInterrupt:
        print("\n\n⚠️  Update interrupted by user")
    except Exception as e:
        print(f"\n✗ Unexpected error: {e}")
        import traceback
        traceback.print_exc()
        sys.exit(1)
    finally:
        updater.close()


if __name__ == "__main__":
    main()
