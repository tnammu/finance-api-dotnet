# Bulk Import Guide - NASDAQ & TSX Stocks

## Overview
This guide explains how to import stocks from NASDAQ and TSX exchanges into your Finance API database.

## Prerequisites
- Python 3.x installed
- Required packages: `yfinance`, `pandas`
- Backend running to access database

## Step 1: Fetch Stock Lists

Run the stock fetcher to get all available stocks from major indices:

```bash
cd scripts
python fetch_index_stocks.py
```

**What it does:**
- Fetches S&P 500 stocks from Wikipedia (~500 stocks)
- Fetches NASDAQ-100 stocks from Wikipedia (~100 stocks)
- Includes 35 major TSX stocks (Canadian banks, energy, telecoms, utilities)
- Includes 20 popular ETFs (dividend ETFs, sector ETFs, Canadian ETFs)
- Removes duplicates
- Outputs `stocks_list.json` with metadata (symbol, name, sector, industry, exchange)

**Output files:**
- `stocks_list.json` - Full JSON with metadata
- `symbols_list.txt` - Just the ticker symbols (one per line)

**Expected result:**
```
TOTAL UNIQUE STOCKS: 600+

Breakdown by Exchange:
  NASDAQ/NYSE: ~550 stocks
  TSX: ~50 stocks
  ETFs: ~20 funds
```

## Step 2: Test Import (Recommended)

Before importing all stocks, test with a small batch:

```bash
python bulk_import_stocks.py --test
```

**What it does:**
- Imports only the first 10 stocks
- Shows progress and success rate
- Helps identify any issues before full import

**Expected output:**
```
[HH:MM:SS] Processing: AAPL (1/10)
[HH:MM:SS] ✓ Successfully added AAPL
[HH:MM:SS] Processing: MSFT (2/10)
...
Total processed: 10
Successfully added: 9
Already existed: 1
Failed: 0
```

## Step 3: Full Import

Once test succeeds, run the full import:

```bash
python bulk_import_stocks.py
```

**Options:**
- `--skip-existing` - Skip stocks already in database (faster)
- `--test` - Import only first 10 stocks
- No flags - Import all stocks

**Expected duration:**
- With rate limiting (0.5s between requests): ~5-10 minutes for 600 stocks
- Progress bar shows ETA and current stock

**Example output:**
```
[12:00:00] ========================================
[12:00:00] BULK IMPORT STOCKS TO DATABASE
[12:00:00] ========================================
[12:00:00] Reading stocks from stocks_list.json...
[12:00:00] Found 650 stocks to import

[12:00:05] Processing: AAPL (1/650) - ETA: 5m 25s
[12:00:05] ✓ Successfully added AAPL - Safety: 3.4/5.0 (Good)
[12:00:06] Processing: MSFT (2/650) - ETA: 5m 24s
...
```

## Step 4: Review Results

After import completes:

**Success summary:**
```
========================================
IMPORT COMPLETE
========================================
Total processed: 650
Successfully added: 580
Already existed: 50
Failed: 20

Time taken: 6m 32s
```

**Failed stocks:**
If some stocks fail, a retry file is generated:
- `failed_imports_YYYYMMDD_HHMMSS.txt` - List of failed symbols
- Review and retry manually or investigate why they failed

## Common Issues

### Issue: "Database locked"
**Solution:** Make sure backend is NOT running during bulk import
```bash
# Stop backend first
Ctrl+C in backend terminal

# Then run import
python bulk_import_stocks.py
```

### Issue: "NOT NULL constraint failed"
**Solution:** This was fixed in recent updates. Make sure you have latest `update_stocks_from_yahoo.py` with `GrowthScore` and `GrowthRating` fields.

### Issue: Rate limiting / API throttling
**Solution:** The script includes 0.5s delay between requests. If you still get throttled, increase the delay:
```python
# In bulk_import_stocks.py, line ~85
time.sleep(1.0)  # Increase from 0.5 to 1.0
```

### Issue: Symbol not found
**Cause:** Some stocks may have been delisted or symbol changed
**Solution:** Review `failed_imports_*.txt` and manually verify symbols on Yahoo Finance

## Database Schema

Each imported stock includes:

**Basic Info:**
- Symbol, CompanyName, Exchange
- CurrentPrice, MarketCap, Beta

**Dividend Data:**
- DividendYield, PayoutRatio
- ConsecutiveYears, DividendGrowthRate

**Safety Metrics:**
- SafetyScore (0.0 - 5.0)
- SafetyRating (Excellent, Very Good, Good, Fair, Poor)
- Recommendation (Strong Buy, Buy, Hold, Caution, Avoid)

**Growth Metrics:**
- GrowthScore (0.0 for dividend stocks)
- GrowthRating (N/A for dividend stocks)

## Sector Performance (Coming Soon)

After import, you can analyze sector performance:
- Average returns by sector
- Stock performance vs sector average
- Sector rankings and percentiles
- Sector valuation metrics (PE, PB, Yield)

## Advanced Usage

### Import specific symbols only
Edit `stocks_list.json` to include only desired stocks, then run:
```bash
python bulk_import_stocks.py
```

### Re-import all stocks (refresh data)
Remove `--skip-existing` flag to update all stocks:
```bash
python bulk_import_stocks.py
```

### Filter by exchange
Before import, edit `stocks_list.json` to filter by exchange:
```python
# In fetch_index_stocks.py, comment out unwanted sources
# all_stocks.extend(fetch_sp500_stocks())  # Comment to skip S&P 500
all_stocks.extend(fetch_tsx_composite_stocks())  # Keep only TSX
```

## Maintenance

### Refresh all stocks daily
Set up a scheduled task to refresh stock data:

```bash
# Windows Task Scheduler or cron job
cd c:\Users\Charan\source\repos\FinanceApi\scripts
python refresh_all_stocks.py
```

### Monitor database size
SQLite database grows with more stocks:
- ~1 KB per stock
- 600 stocks ≈ 600 KB
- Safe to grow to several MB

## Support

If you encounter issues:
1. Check error messages in console
2. Review `failed_imports_*.txt` for failed symbols
3. Verify database schema matches Python script
4. Test with single stock first: `python update_stocks_from_yahoo.py AAPL`
