#!/usr/bin/env python3
"""
Fetch Market Index Data
Downloads market index data (S&P 500, TSX60, etc.) from Yahoo Finance
and stores it in the dividends.db database for benchmark comparison.
"""

import sqlite3
import yfinance as yf
from datetime import datetime, timedelta
import time
import sys
import os
import pandas as pd

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

# Market Indices Configuration
MARKET_INDICES = [
    {
        'symbol': '^GSPC',
        'name': 'S&P 500',
        'market': 'US',
        'currency': 'USD'
    },
    {
        'symbol': '^DJI',
        'name': 'Dow Jones Industrial Average',
        'market': 'US',
        'currency': 'USD'
    },
    {
        'symbol': '^IXIC',
        'name': 'NASDAQ Composite',
        'market': 'US',
        'currency': 'USD'
    },
    {
        'symbol': '^GSPTSE',
        'name': 'S&P/TSX Composite',
        'market': 'Canada',
        'currency': 'CAD'
    }
]


class IndexDataFetcher:
    def __init__(self, db_path):
        self.db_path = db_path
        self.conn = None
        self.cursor = None
        self.updated_count = 0
        self.inserted_count = 0
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

    def create_tables_if_not_exist(self):
        """Create Index tables if they don't exist"""
        try:
            # Create IndexData table
            self.cursor.execute("""
                CREATE TABLE IF NOT EXISTS IndexData (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Symbol TEXT NOT NULL UNIQUE,
                    Name TEXT NOT NULL,
                    Market TEXT,
                    Currency TEXT,
                    CurrentPrice REAL NOT NULL,
                    PreviousClose REAL,
                    Open REAL,
                    DayHigh REAL,
                    DayLow REAL,
                    Volume INTEGER,
                    DayChange REAL,
                    WeekChange REAL,
                    MonthChange REAL,
                    ThreeMonthChange REAL,
                    SixMonthChange REAL,
                    YearChange REAL,
                    YTDChange REAL,
                    ThreeYearChange REAL,
                    FiveYearChange REAL,
                    AnnualizedReturn1Y REAL,
                    AnnualizedReturn3Y REAL,
                    AnnualizedReturn5Y REAL,
                    Beta REAL,
                    Volatility REAL,
                    LastUpdated TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    IsActive INTEGER DEFAULT 1
                )
            """)

            # Create IndexHistory table
            self.cursor.execute("""
                CREATE TABLE IF NOT EXISTS IndexHistory (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    IndexDataId INTEGER NOT NULL,
                    Symbol TEXT NOT NULL,
                    Date TEXT NOT NULL,
                    Open REAL NOT NULL,
                    High REAL NOT NULL,
                    Low REAL NOT NULL,
                    Close REAL NOT NULL,
                    AdjustedClose REAL NOT NULL,
                    Volume INTEGER NOT NULL,
                    DayChange REAL,
                    DayChangeAmount REAL,
                    FOREIGN KEY (IndexDataId) REFERENCES IndexData(Id) ON DELETE CASCADE,
                    UNIQUE(Symbol, Date)
                )
            """)

            # Create IndexApiUsageLog table
            self.cursor.execute("""
                CREATE TABLE IF NOT EXISTS IndexApiUsageLogs (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Symbol TEXT NOT NULL,
                    ApiSource TEXT NOT NULL,
                    RequestTime TEXT NOT NULL,
                    Success INTEGER NOT NULL,
                    ErrorMessage TEXT,
                    RecordsFetched INTEGER NOT NULL
                )
            """)

            # Create indexes
            self.cursor.execute("CREATE INDEX IF NOT EXISTS idx_indexdata_symbol ON IndexData(Symbol)")
            self.cursor.execute("CREATE INDEX IF NOT EXISTS idx_indexdata_market ON IndexData(Market)")
            self.cursor.execute("CREATE INDEX IF NOT EXISTS idx_indexhistory_symbol ON IndexHistory(Symbol)")
            self.cursor.execute("CREATE INDEX IF NOT EXISTS idx_indexhistory_date ON IndexHistory(Date)")

            self.conn.commit()
            print("✓ Database tables verified/created")
            return True
        except Exception as e:
            print(f"✗ Failed to create tables: {e}")
            return False

    def index_exists(self, symbol):
        """Check if index already exists in database"""
        try:
            self.cursor.execute("SELECT Id FROM IndexData WHERE Symbol = ?", (symbol,))
            result = self.cursor.fetchone()
            return result[0] if result else None
        except Exception as e:
            print(f"  ✗ Error checking if index exists: {e}")
            return None

    def calculate_performance(self, hist_data, current_price):
        """Calculate performance metrics from historical data"""
        try:
            performance = {}

            # Get dates for different periods
            today = datetime.now()
            one_week_ago = today - timedelta(days=7)
            one_month_ago = today - timedelta(days=30)
            three_months_ago = today - timedelta(days=90)
            six_months_ago = today - timedelta(days=180)
            one_year_ago = today - timedelta(days=365)
            three_years_ago = today - timedelta(days=365*3)
            five_years_ago = today - timedelta(days=365*5)
            ytd_start = datetime(today.year, 1, 1)

            # Helper function to find closest price
            def get_closest_price(target_date):
                try:
                    filtered = hist_data[hist_data.index >= target_date]
                    if len(filtered) > 0:
                        return filtered.iloc[0]['Close']
                    return None
                except:
                    return None

            # Calculate percentage changes
            week_price = get_closest_price(one_week_ago)
            month_price = get_closest_price(one_month_ago)
            three_month_price = get_closest_price(three_months_ago)
            six_month_price = get_closest_price(six_months_ago)
            year_price = get_closest_price(one_year_ago)
            three_year_price = get_closest_price(three_years_ago)
            five_year_price = get_closest_price(five_years_ago)
            ytd_price = get_closest_price(ytd_start)

            if week_price:
                performance['week_change'] = ((current_price - week_price) / week_price) * 100
            if month_price:
                performance['month_change'] = ((current_price - month_price) / month_price) * 100
            if three_month_price:
                performance['three_month_change'] = ((current_price - three_month_price) / three_month_price) * 100
            if six_month_price:
                performance['six_month_change'] = ((current_price - six_month_price) / six_month_price) * 100
            if year_price:
                performance['year_change'] = ((current_price - year_price) / year_price) * 100
                performance['annualized_1y'] = performance['year_change']
            if three_year_price:
                performance['three_year_change'] = ((current_price - three_year_price) / three_year_price) * 100
                performance['annualized_3y'] = (((current_price / three_year_price) ** (1/3)) - 1) * 100
            if five_year_price:
                performance['five_year_change'] = ((current_price - five_year_price) / five_year_price) * 100
                performance['annualized_5y'] = (((current_price / five_year_price) ** (1/5)) - 1) * 100
            if ytd_price:
                performance['ytd_change'] = ((current_price - ytd_price) / ytd_price) * 100

            # Calculate volatility (standard deviation of daily returns)
            if len(hist_data) > 1:
                returns = hist_data['Close'].pct_change().dropna()
                performance['volatility'] = returns.std() * (252 ** 0.5) * 100  # Annualized volatility

            return performance
        except Exception as e:
            print(f"  ⚠️  Error calculating performance: {e}")
            return {}

    def fetch_index_data(self, index_config):
        """Fetch index data from Yahoo Finance"""
        symbol = index_config['symbol']
        try:
            print(f"\nFetching data for {index_config['name']} ({symbol})...")

            # Create ticker object
            ticker = yf.Ticker(symbol)

            # Get current info
            info = ticker.info

            # Get current price
            current_price = info.get('regularMarketPrice') or info.get('previousClose')

            if current_price is None:
                print(f"  ⚠️  {symbol}: No price data available")
                return None

            # Get historical data (5 years for comprehensive analysis)
            print(f"  Fetching 5 years of historical data...")
            hist = ticker.history(period="5y")

            if hist.empty:
                print(f"  ⚠️  {symbol}: No historical data available")
                return None

            # Get latest data
            latest = hist.iloc[-1]
            previous = hist.iloc[-2] if len(hist) > 1 else latest

            # Calculate day change
            day_change = ((latest['Close'] - previous['Close']) / previous['Close']) * 100 if len(hist) > 1 else 0

            # Calculate performance metrics
            performance = self.calculate_performance(hist, current_price)

            print(f"  ✓ Fetched {len(hist)} historical records")
            print(f"  Current Price: ${current_price:.2f}")
            print(f"  YTD Change: {performance.get('ytd_change', 0):.2f}%")
            print(f"  1Y Change: {performance.get('year_change', 0):.2f}%")

            return {
                'symbol': symbol,
                'name': index_config['name'],
                'market': index_config['market'],
                'currency': index_config['currency'],
                'current_price': float(current_price),
                'previous_close': float(previous['Close']),
                'open': float(latest['Open']),
                'day_high': float(latest['High']),
                'day_low': float(latest['Low']),
                'volume': int(latest['Volume']),
                'day_change': day_change,
                'performance': performance,
                'historical_data': hist,
                'last_updated': datetime.now().strftime('%Y-%m-%d %H:%M:%S')
            }
        except Exception as e:
            print(f"  ✗ {symbol}: Error fetching data - {str(e)}")
            self.log_api_usage(symbol, "Yahoo Finance", False, str(e), 0)
            return None

    def insert_index(self, data):
        """Insert new index into database"""
        try:
            query = """
                INSERT INTO IndexData (
                    Symbol, Name, Market, Currency,
                    CurrentPrice, PreviousClose, Open, DayHigh, DayLow, Volume,
                    DayChange, WeekChange, MonthChange, ThreeMonthChange, SixMonthChange,
                    YearChange, YTDChange, ThreeYearChange, FiveYearChange,
                    AnnualizedReturn1Y, AnnualizedReturn3Y, AnnualizedReturn5Y,
                    Beta, Volatility, LastUpdated, CreatedAt, IsActive
                ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            """
            perf = data['performance']
            self.cursor.execute(query, (
                data['symbol'],
                data['name'],
                data['market'],
                data['currency'],
                data['current_price'],
                data['previous_close'],
                data['open'],
                data['day_high'],
                data['day_low'],
                data['volume'],
                data['day_change'],
                perf.get('week_change'),
                perf.get('month_change'),
                perf.get('three_month_change'),
                perf.get('six_month_change'),
                perf.get('year_change'),
                perf.get('ytd_change'),
                perf.get('three_year_change'),
                perf.get('five_year_change'),
                perf.get('annualized_1y'),
                perf.get('annualized_3y'),
                perf.get('annualized_5y'),
                None,  # Beta
                perf.get('volatility'),
                data['last_updated'],
                data['last_updated'],
                1  # IsActive
            ))
            self.conn.commit()

            # Get the inserted ID
            index_id = self.cursor.lastrowid
            return index_id
        except Exception as e:
            print(f"  ✗ Failed to insert index: {e}")
            return None

    def update_index(self, index_id, data):
        """Update existing index in database"""
        try:
            query = """
                UPDATE IndexData
                SET Name = ?, Market = ?, Currency = ?,
                    CurrentPrice = ?, PreviousClose = ?, Open = ?, DayHigh = ?, DayLow = ?, Volume = ?,
                    DayChange = ?, WeekChange = ?, MonthChange = ?, ThreeMonthChange = ?, SixMonthChange = ?,
                    YearChange = ?, YTDChange = ?, ThreeYearChange = ?, FiveYearChange = ?,
                    AnnualizedReturn1Y = ?, AnnualizedReturn3Y = ?, AnnualizedReturn5Y = ?,
                    Beta = ?, Volatility = ?, LastUpdated = ?
                WHERE Id = ?
            """
            perf = data['performance']
            self.cursor.execute(query, (
                data['name'],
                data['market'],
                data['currency'],
                data['current_price'],
                data['previous_close'],
                data['open'],
                data['day_high'],
                data['day_low'],
                data['volume'],
                data['day_change'],
                perf.get('week_change'),
                perf.get('month_change'),
                perf.get('three_month_change'),
                perf.get('six_month_change'),
                perf.get('year_change'),
                perf.get('ytd_change'),
                perf.get('three_year_change'),
                perf.get('five_year_change'),
                perf.get('annualized_1y'),
                perf.get('annualized_3y'),
                perf.get('annualized_5y'),
                None,  # Beta
                perf.get('volatility'),
                data['last_updated'],
                index_id
            ))
            self.conn.commit()
            return True
        except Exception as e:
            print(f"  ✗ Failed to update index: {e}")
            return False

    def insert_historical_data(self, index_id, symbol, hist_data):
        """Insert historical data for an index"""
        try:
            print(f"  Storing historical data...")

            # Delete old historical data for this symbol to avoid duplicates
            self.cursor.execute("DELETE FROM IndexHistory WHERE Symbol = ?", (symbol,))

            records_inserted = 0
            for date, row in hist_data.iterrows():
                # Calculate day change
                idx = hist_data.index.get_loc(date)
                if idx > 0:
                    prev_close = hist_data.iloc[idx - 1]['Close']
                    day_change = ((row['Close'] - prev_close) / prev_close) * 100
                    day_change_amount = row['Close'] - prev_close
                else:
                    day_change = 0
                    day_change_amount = 0

                query = """
                    INSERT OR REPLACE INTO IndexHistory (
                        IndexDataId, Symbol, Date,
                        Open, High, Low, Close, AdjustedClose, Volume,
                        DayChange, DayChangeAmount
                    ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                """
                self.cursor.execute(query, (
                    index_id,
                    symbol,
                    date.strftime('%Y-%m-%d'),
                    float(row['Open']),
                    float(row['High']),
                    float(row['Low']),
                    float(row['Close']),
                    float(row['Close']),  # Adjusted close (same as close for indices)
                    int(row['Volume']),
                    day_change,
                    day_change_amount
                ))
                records_inserted += 1

            self.conn.commit()
            print(f"  ✓ Stored {records_inserted} historical records")
            return records_inserted
        except Exception as e:
            print(f"  ✗ Failed to insert historical data: {e}")
            return 0

    def log_api_usage(self, symbol, api_source, success, error_msg, records_fetched):
        """Log API usage"""
        try:
            query = """
                INSERT INTO IndexApiUsageLogs (Symbol, ApiSource, RequestTime, Success, ErrorMessage, RecordsFetched)
                VALUES (?, ?, ?, ?, ?, ?)
            """
            self.cursor.execute(query, (
                symbol,
                api_source,
                datetime.now().strftime('%Y-%m-%d %H:%M:%S'),
                1 if success else 0,
                error_msg,
                records_fetched
            ))
            self.conn.commit()
        except Exception as e:
            print(f"  ⚠️  Failed to log API usage: {e}")

    def process_all_indices(self):
        """Main function to process all market indices"""
        print(f"\n{'='*70}")
        print(f"Starting market index data fetch for {len(MARKET_INDICES)} indices...")
        print(f"{'='*70}")

        for idx, index_config in enumerate(MARKET_INDICES, 1):
            symbol = index_config['symbol']
            print(f"\n[{idx}/{len(MARKET_INDICES)}] Processing {index_config['name']}...")

            # Check if index exists
            index_id = self.index_exists(symbol)

            # Fetch data from Yahoo Finance
            data = self.fetch_index_data(index_config)

            if data:
                if index_id:
                    # Update existing index
                    if self.update_index(index_id, data):
                        print(f"  ✓ Updated index data")
                        self.updated_count += 1
                    else:
                        self.failed_count += 1
                        self.failed_symbols.append(symbol)
                else:
                    # Insert new index
                    index_id = self.insert_index(data)
                    if index_id:
                        print(f"  ✓ Inserted new index")
                        self.inserted_count += 1
                    else:
                        self.failed_count += 1
                        self.failed_symbols.append(symbol)
                        continue

                # Insert historical data
                hist_records = self.insert_historical_data(index_id, symbol, data['historical_data'])

                # Log API usage
                self.log_api_usage(symbol, "Yahoo Finance", True, None, len(data['historical_data']))
            else:
                self.failed_count += 1
                self.failed_symbols.append(symbol)

            # Rate limiting
            if idx < len(MARKET_INDICES):
                time.sleep(1)

        self.print_summary()

    def print_summary(self):
        """Print processing summary"""
        print(f"\n{'='*70}")
        print(f"MARKET INDEX DATA FETCH SUMMARY")
        print(f"{'='*70}")
        print(f"✓ Newly inserted:       {self.inserted_count} indices")
        print(f"✓ Updated existing:     {self.updated_count} indices")
        print(f"✗ Failed to process:    {self.failed_count} indices")
        print(f"  Total processed:      {self.inserted_count + self.updated_count + self.failed_count}")

        if self.failed_symbols:
            print(f"\nFailed symbols: {', '.join(self.failed_symbols)}")

        print(f"{'='*70}\n")

    def close(self):
        """Close database connection"""
        if self.conn:
            self.conn.close()
            print("✓ Database connection closed")


def main():
    print("\n" + "="*70)
    print("MARKET INDEX DATA FETCHER")
    print("Fetches S&P 500, Dow Jones, NASDAQ, and TSX data")
    print("="*70 + "\n")

    # Check if database exists
    if not os.path.exists(DB_PATH):
        print(f"⚠️  Database not found: {DB_PATH}")
        print("  Creating new database...")

    # Create fetcher
    fetcher = IndexDataFetcher(DB_PATH)

    # Connect to database
    if not fetcher.connect_db():
        return

    # Create tables if they don't exist
    if not fetcher.create_tables_if_not_exist():
        return

    try:
        # Process all indices
        fetcher.process_all_indices()
    except KeyboardInterrupt:
        print("\n\n⚠️  Process interrupted by user")
    except Exception as e:
        print(f"\n✗ Unexpected error: {e}")
        import traceback
        traceback.print_exc()
    finally:
        fetcher.close()


if __name__ == "__main__":
    main()
