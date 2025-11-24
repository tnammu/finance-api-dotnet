# Stock Data Scripts

Python scripts for fetching, updating, and analyzing stock data in the Finance API database.

## Available Scripts

### 1. `fetch_index_data.py` - Fetch Market Index Data (NEW)
Fetches market index data (S&P 500, Dow Jones, NASDAQ, TSX) for benchmark comparison.

### 2. `fetch_sp500_stocks.py` - Fetch S&P 500 Stock Data
Fetches all S&P 500 companies and their stock data, then populates the database.

### 3. `update_stocks_from_yahoo.py` - Update Existing Stocks
Updates stock data for existing stocks already in the database.

### 4. `sp500_validator.py` - S&P 500 Stock Validator
Verifies if your portfolio stocks are in the S&P 500 index.

## Installation

1. **Install Python** (if not already installed):
   - Download from https://www.python.org/downloads/
   - Make sure to check "Add Python to PATH" during installation

2. **Install dependencies:**

```cmd
cd c:\Users\Charan\source\repos\FinanceApi\scripts
pip install -r requirements.txt
```

---

## Script Details

### 1. fetch_index_data.py (NEW)

**Purpose:** Fetch market index data for benchmark performance comparison

**What it does:**
- Fetches data for major market indices:
  - S&P 500 (^GSPC)
  - Dow Jones Industrial Average (^DJI)
  - NASDAQ Composite (^IXIC)
  - S&P/TSX Composite (^GSPTSE)
- Downloads 5 years of historical price data
- Calculates performance metrics (1D, 1W, 1M, 3M, 6M, 1Y, 3Y, 5Y, YTD)
- Calculates annualized returns (1Y, 3Y, 5Y)
- Calculates volatility
- Stores data in `IndexData` and `IndexHistory` tables

**Data stored:**
- Current price, open, high, low, volume
- Performance metrics (day, week, month, quarter, year changes)
- Annualized returns
- Historical OHLCV data (5 years)
- Volatility metrics

**Usage:**
```cmd
cd c:\Users\Charan\source\repos\FinanceApi
python scripts\fetch_index_data.py
```

**When to use:**
- Initial setup to populate benchmark data
- Weekly/monthly to update index performance
- Before comparing stock performance against benchmarks

**Note:** This creates the IndexData and IndexHistory tables automatically if they don't exist.

---

### 2. fetch_sp500_stocks.py

**Purpose:** Initial database population with S&P 500 stocks

**What it does:**
- Downloads the current list of S&P 500 companies from Wikipedia
- Fetches stock data from Yahoo Finance for each company
- Stores data in the `DividendModels` table
- Updates existing stocks or inserts new ones

**Data fetched:**
- Company Name, Sector, Industry
- Current Price
- Dividend Yield, Dividend Per Share, Payout Ratio
- EPS, Profit Margin, Beta

**Usage:**
```cmd
cd c:\Users\Charan\source\repos\FinanceApi
python scripts\fetch_sp500_stocks.py
```

**Note:** This script takes 15-30 minutes to complete as it fetches data for 500+ stocks with rate limiting.

---

### 3. update_stocks_from_yahoo.py

**Purpose:** Quick updates of existing stock prices and dividend yields

**What it does:**
- Reads all symbols from the `DividendModels` table
- Fetches updated data from Yahoo Finance
- Updates `CurrentPrice`, `CompanyName`, `DividendYield`, and `LastUpdated` fields

**Usage:**
```cmd
cd c:\Users\Charan\source\repos\FinanceApi
python scripts\update_stocks_from_yahoo.py
```

**When to use:**
- Daily/weekly updates to refresh price and dividend data
- After adding new stocks manually to the database

---

### 4. sp500_validator.py

**Purpose:** Verify if your portfolio stocks are in the S&P 500 index

**Prerequisites:** Make sure your Finance API backend is running:

```cmd
cd c:\Users\Charan\source\repos\FinanceApi
dotnet run --launch-profile http
```

**Usage:**
```cmd
cd c:\Users\Charan\source\repos\FinanceApi\scripts
python sp500_validator.py
```

### Output

The script will:

1. **Fetch S&P 500 list** from Wikipedia
2. **Get your portfolio stocks** from the Finance API
3. **Validate each stock** and display results:
   ```
   AAPL       | ✅ S&P 500      |  $ 268.81
   TD.TO      | ❌ Not S&P 500  |  $ 113.35
   ```
4. **Print summary statistics**
5. **Export reports** to JSON and CSV files
6. **Show recommendations** for S&P 500 dividend stocks you don't own

### Output Files

- `sp500_validation_YYYYMMDD_HHMMSS.json` - Full analysis in JSON format
- `sp500_validation_YYYYMMDD_HHMMSS.csv` - Portfolio validation in CSV format

## Example Output

```
================================================================================
🏦 S&P 500 Stock Validator
================================================================================
📥 Fetching S&P 500 list from Wikipedia...
✅ Loaded 503 S&P 500 stocks

📊 Fetching stocks from Finance API...
✅ Found 15 stocks in portfolio

🔍 Validating 15 stocks against S&P 500...
================================================================================
AAPL       | ✅ S&P 500      |  $ 268.81
MSFT       | ✅ S&P 500      |  $ 420.50
TD.TO      | ❌ Not S&P 500  |  $ 113.35
ENB        | ❌ Not S&P 500  |  $  51.20
================================================================================

📊 Portfolio Analysis Summary:
   Total Stocks: 15
   S&P 500 Stocks: 8 (53.3%)
   Non-S&P 500 Stocks: 7

📄 JSON report saved: sp500_validation_20251028_210530.json
📊 CSV report saved: sp500_validation_20251028_210530.csv

💡 S&P 500 Dividend Stock Recommendations:
   1. JNJ    - Johnson & Johnson                        (Health Care)
   2. PG     - Procter & Gamble Company                 (Consumer Staples)
   3. KO     - Coca-Cola Company                        (Consumer Staples)
   4. PEP    - PepsiCo Inc.                            (Consumer Staples)
   5. MCD    - McDonald's Corporation                   (Consumer Discretionary)

✅ Analysis complete!
```

## Use Cases

### 1. Portfolio Diversification Check
See what percentage of your portfolio is in S&P 500 stocks vs international/small-cap stocks.

### 2. Index Tracking
Verify if your holdings match S&P 500 composition.

### 3. Discover New Stocks
Get recommendations for S&P 500 dividend stocks you don't currently own.

### 4. Sector Analysis
See which S&P 500 sectors you're invested in.

## Configuration

Edit the script to change:

- `API_BASE_URL` - Your Finance API URL (default: http://localhost:5000/api)
- `dividend_aristocrats` list - Customize recommended stocks

## Troubleshooting

**Error: "Could not connect to API"**
- Make sure backend is running on http://localhost:5000
- Check if firewall is blocking connections

**Error: "No stocks found"**
- Add stocks to your portfolio first via the web interface
- Go to http://localhost:3000 and add some stocks

**Error: "Failed to fetch S&P 500 list"**
- Check internet connection
- Wikipedia might be temporarily unavailable

## Integration with Finance API

The script automatically:
- Fetches all stocks from `/api/stocks`
- Compares symbols with S&P 500 list
- Works with both US stocks (AAPL) and Canadian stocks (TD.TO)

---

## Recommended Workflow

### First Time Setup
1. Run `fetch_index_data.py` to populate benchmark index data (S&P 500, etc.)
2. Run `fetch_sp500_stocks.py` to populate database with all S&P 500 companies
3. Wait for completion (~15-30 minutes)
4. Use the C# API to perform full analysis: `GET /api/dividends/analyze/{symbol}`

### Regular Maintenance
1. Run `fetch_index_data.py` weekly to update benchmark performance
2. Run `update_stocks_from_yahoo.py` daily/weekly to refresh stock prices
3. Run `sp500_validator.py` to check portfolio composition
4. Use C# API for detailed dividend analysis and stock vs. benchmark comparison

---

## Database Schema

### DividendModels Table
Stores individual stock data:

| Field | Type | Description |
|-------|------|-------------|
| Symbol | String | Stock ticker symbol |
| CompanyName | String | Full company name |
| Sector | String | GICS Sector |
| Industry | String | GICS Sub-Industry |
| CurrentPrice | Decimal | Current stock price |
| DividendYield | Decimal? | Dividend yield % |
| DividendPerShare | Decimal? | Annual dividend per share |
| PayoutRatio | Decimal? | Payout ratio % |
| EPS | Decimal? | Earnings per share |
| ProfitMargin | Decimal? | Profit margin % |
| Beta | Decimal? | Stock beta |
| LastUpdated | DateTime | Last update timestamp |

### IndexData Table (NEW)
Stores market index benchmark data:

| Field | Type | Description |
|-------|------|-------------|
| Symbol | String | Index symbol (^GSPC, ^DJI, etc.) |
| Name | String | Index name (S&P 500, Dow Jones, etc.) |
| Market | String | Market (US, Canada, etc.) |
| Currency | String | Currency (USD, CAD, etc.) |
| CurrentPrice | Decimal | Current index price |
| DayChange | Decimal? | 1-day change % |
| WeekChange | Decimal? | 1-week change % |
| MonthChange | Decimal? | 1-month change % |
| YearChange | Decimal? | 1-year change % |
| YTDChange | Decimal? | Year-to-date change % |
| AnnualizedReturn1Y | Decimal? | 1-year annualized return % |
| AnnualizedReturn3Y | Decimal? | 3-year annualized return % |
| AnnualizedReturn5Y | Decimal? | 5-year annualized return % |
| Volatility | Decimal? | Annualized volatility % |
| LastUpdated | DateTime | Last update timestamp |

### IndexHistory Table (NEW)
Stores historical index price data:

| Field | Type | Description |
|-------|------|-------------|
| Symbol | String | Index symbol |
| Date | DateTime | Trading date |
| Open | Decimal | Opening price |
| High | Decimal | Day high |
| Low | Decimal | Day low |
| Close | Decimal | Closing price |
| Volume | Long | Trading volume |
| DayChange | Decimal? | Daily change % |

---

## Future Enhancements

- [ ] Add endpoint to Finance API to save S&P 500 validation status
- [ ] Create scheduled task to run validation daily
- [ ] Compare portfolio performance vs S&P 500 index
- [ ] Track stocks that enter/exit S&P 500
- [ ] Add other indices (TSX, NASDAQ-100, Dow Jones)
- [ ] Automated dividend payment history fetching

## License

Part of Finance API project.
