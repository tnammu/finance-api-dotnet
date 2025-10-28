# Database Guide - How to View and Manage Your Data

Your Finance API uses SQLite databases to store all data permanently with no expiration.

## Database Files

You have two SQLite database files in your project root:

1. **finance.db** - Stores stock data
2. **dividends.db** - Stores dividend analysis data and API usage logs

## Quick View - Using Command Line

### Option 1: SQLite CLI (if installed)

```bash
# View stocks database
sqlite3 finance.db

# View dividends database
sqlite3 dividends.db
```

**Useful SQLite commands:**
```sql
.tables                     # List all tables
.schema TableName          # Show table structure
SELECT * FROM Stocks;      # View all stocks
SELECT * FROM DividendModels;  # View all dividends
.quit                      # Exit
```

### Option 2: Using VS Code Extension (Recommended)

1. Install the **SQLite Viewer** extension in VS Code:
   - Press `Ctrl+Shift+X`
   - Search for "SQLite Viewer" by Florian Klampfer
   - Install it

2. View database:
   - Right-click on `finance.db` or `dividends.db`
   - Select "Open Database"
   - Browse tables visually

## Database Structure

### finance.db

**Stocks Table:**
```sql
CREATE TABLE Stocks (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Symbol TEXT NOT NULL,
    CompanyName TEXT,
    Price REAL NOT NULL,
    DividendYield REAL,
    LastUpdated TEXT NOT NULL
);
```

**Example queries:**
```sql
-- View all stocks
SELECT * FROM Stocks;

-- View stocks with prices
SELECT Symbol, CompanyName, Price, LastUpdated FROM Stocks;

-- Count total stocks
SELECT COUNT(*) as TotalStocks FROM Stocks;
```

### dividends.db

**DividendModels Table** - Main dividend data:
```sql
CREATE TABLE DividendModels (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Symbol TEXT NOT NULL,
    CompanyName TEXT,
    Sector TEXT,
    Industry TEXT,
    DividendYield REAL,
    DividendPerShare REAL,
    PayoutRatio REAL,
    EPS REAL,
    ProfitMargin REAL,
    Beta REAL,
    ConsecutiveYearsOfPayments INTEGER,
    DividendGrowthRate REAL,
    SafetyScore REAL,
    SafetyRating TEXT,
    Recommendation TEXT,
    FetchedAt TEXT,
    LastUpdated TEXT,
    ApiCallsUsed INTEGER
);
```

**DividendPayments Table** - Individual payment records:
```sql
CREATE TABLE DividendPayments (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    DividendModelId INTEGER,
    Symbol TEXT NOT NULL,
    PaymentDate TEXT,
    Amount REAL,
    FOREIGN KEY (DividendModelId) REFERENCES DividendModels(Id)
);
```

**YearlyDividends Table** - Yearly summaries:
```sql
CREATE TABLE YearlyDividends (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    DividendModelId INTEGER,
    Symbol TEXT NOT NULL,
    Year INTEGER,
    TotalDividend REAL,
    PaymentCount INTEGER,
    FOREIGN KEY (DividendModelId) REFERENCES DividendModels(Id)
);
```

**ApiUsageLogs Table** - Track API usage:
```sql
CREATE TABLE ApiUsageLogs (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Date TEXT,
    CallsUsed INTEGER,
    DailyLimit INTEGER,
    Notes TEXT
);
```

## Useful SQL Queries

### Stocks Queries

```sql
-- All stocks with their current prices
SELECT Symbol, CompanyName, Price, DividendYield, LastUpdated
FROM Stocks
ORDER BY Symbol;

-- Stocks ordered by price
SELECT Symbol, CompanyName, Price
FROM Stocks
ORDER BY Price DESC;

-- Total portfolio value (if you own 1 of each)
SELECT SUM(Price) as TotalValue FROM Stocks;
```

### Dividend Queries

```sql
-- All dividend stocks with safety ratings
SELECT Symbol, CompanyName, SafetyScore, SafetyRating, DividendYield
FROM DividendModels
ORDER BY SafetyScore DESC;

-- Best dividend stocks (safety score >= 4.0)
SELECT Symbol, CompanyName, SafetyScore, DividendYield, ConsecutiveYearsOfPayments
FROM DividendModels
WHERE SafetyScore >= 4.0
ORDER BY SafetyScore DESC;

-- Dividend payment history for a specific stock
SELECT Symbol, PaymentDate, Amount
FROM DividendPayments
WHERE Symbol = 'AAPL'
ORDER BY PaymentDate DESC;

-- Yearly dividend summaries
SELECT Symbol, Year, TotalDividend, PaymentCount
FROM YearlyDividends
WHERE Symbol = 'AAPL'
ORDER BY Year DESC;

-- API usage today
SELECT Date, CallsUsed, DailyLimit, (DailyLimit - CallsUsed) as Remaining
FROM ApiUsageLogs
ORDER BY Date DESC
LIMIT 1;

-- API usage history (last 30 days)
SELECT Date, CallsUsed, DailyLimit
FROM ApiUsageLogs
ORDER BY Date DESC
LIMIT 30;
```

### Combined Queries

```sql
-- Stocks with their dividend analysis
SELECT
    s.Symbol,
    s.CompanyName,
    s.Price,
    d.SafetyScore,
    d.SafetyRating,
    d.DividendYield,
    d.ConsecutiveYearsOfPayments
FROM Stocks s
LEFT JOIN DividendModels d ON s.Symbol = d.Symbol;
```

## Database Management

### Backup Your Databases

```bash
# Copy databases to backup folder
copy finance.db finance_backup_2025-10-24.db
copy dividends.db dividends_backup_2025-10-24.db
```

### Reset/Clear Data

**Clear all stocks:**
```sql
DELETE FROM Stocks;
```

**Clear all dividend data:**
```sql
DELETE FROM DividendPayments;
DELETE FROM YearlyDividends;
DELETE FROM DividendModels;
```

**Clear API usage logs:**
```sql
DELETE FROM ApiUsageLogs;
```

### Delete and Recreate

If you want to start fresh:
```bash
# Stop the API first, then delete databases
del finance.db
del dividends.db

# Run the API again - databases will be recreated automatically
dotnet run
```

## Recommended Tools

### Free SQLite Viewers:

1. **DB Browser for SQLite** (Recommended)
   - Download: https://sqlitebrowser.org/
   - Full GUI application
   - View, edit, query, and export data

2. **SQLite Viewer (VS Code Extension)**
   - Search in VS Code extensions
   - View databases directly in VS Code

3. **SQLiteStudio**
   - Download: https://sqlitestudio.pl/
   - Cross-platform
   - Advanced features

## How the Caching Works Now

### Previous Behavior (REMOVED):
- Dividends: 7-day expiration
- Stocks: 15-minute expiration
- Data would be refetched automatically after expiration

### New Behavior (CURRENT):
- **No expiration** - Data is stored permanently
- **Manual refresh only** - Use the refresh button in the frontend or API endpoint
- **Saves API calls** - Only fetch new data when explicitly requested
- **Historical data preserved** - View data from any time

### When Data is Updated:

**Stocks:**
- When you click "Refresh" on a stock in the frontend
- When you call `POST /api/stocks/refresh/{id}`
- When you call `POST /api/stocks/refresh` (refresh all)

**Dividends:**
- When you analyze a stock with `?refresh=true` parameter
- When you explicitly refresh from the frontend

## API Endpoints for Data Management

```bash
# Stocks
GET    /api/stocks                    # Get all stocks from DB
POST   /api/stocks/refresh/{id}       # Refresh single stock
POST   /api/stocks/refresh            # Refresh all stocks
DELETE /api/stocks/{id}               # Delete a stock

# Dividends
GET    /api/dividends/cached          # Get all cached analyses
GET    /api/dividends/analyze/{symbol}?refresh=true   # Force refresh
DELETE /api/dividends/cached/{symbol} # Delete cached analysis

# API Usage
GET    /api/dividends/usage/today     # Today's usage
GET    /api/dividends/usage/history   # 30-day history
GET    /api/dividends/stats           # Database statistics
```

## Tips

1. **Backup regularly** - Copy your .db files before major changes
2. **Check data age** - Use `LastUpdated` field to see when data was fetched
3. **Monitor API usage** - Keep track to avoid hitting the 25 calls/day limit
4. **Manual refresh** - Only refresh data when you need up-to-date information
5. **Export data** - Use DB Browser to export tables to CSV/JSON if needed

## Troubleshooting

**Database locked error:**
- Close any SQLite viewers
- Stop the API
- Restart the API

**Database not found:**
- Make sure you're in the correct directory
- Run the API at least once to create the databases

**Old data showing:**
- Data is now permanent - manually refresh if you need updated information
- Check the `LastUpdated` field to see data age
