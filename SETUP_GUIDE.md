# Canadian Dividend Analysis API - Setup Guide

## Overview

This API analyzes dividend stocks using Yahoo Finance data via Python. It provides:
- 5-year dividend history for any stock/ETF
- Safety scores (1-5) based on yield, payout ratio, growth
- Support for Canadian stocks (.TO suffix)
- Portfolio performance comparison vs S&P 500

## Quick Setup

### Prerequisites

1. **.NET 7 SDK**: https://dotnet.microsoft.com/download
2. **Node.js 18+**: https://nodejs.org
3. **Python 3.8+** with yfinance:
   ```bash
   pip install yfinance
   ```

### Step 1: Start Backend

```bash
dotnet run
```

Opens Swagger UI at http://localhost:5000

### Step 2: Start Frontend

```bash
cd frontend
npm install
npm start
```

Opens React app at http://localhost:3000

### Alternative: Use Startup Script

Double-click `start-app.bat` to start both.

## API Endpoints

### Main Endpoints

```
GET  /api/dividends                     # Get all portfolio stocks
GET  /api/dividends/analyze/{symbol}    # Analyze stock (fetches via Python)
GET  /api/dividends/cached              # Get cached analyses
GET  /api/dividends/history/{symbol}    # Get dividend payment history
DELETE /api/dividends/cached/{symbol}   # Remove from portfolio
```

### Performance Endpoints

```
GET  /api/performance/compare           # Portfolio vs S&P 500
GET  /api/performance/portfolio-summary # Portfolio metrics
```

## Safety Score Calculation

Stocks are scored 1-5 based on:

| Metric | Excellent | Good | Fair | Poor |
|--------|-----------|------|------|------|
| Dividend Yield | 2-6% | 1-2% or 6-8% | 0.5-1% or 8-10% | <0.5% or >10% |
| Payout Ratio | <60% | 60-75% | 75-90% | >90% |
| Dividend Growth | >5%/yr | 0-5%/yr | -2% to 0% | <-2%/yr |
| Consecutive Years | 10+ | 5-10 | 3-5 | <3 |
| Beta | <0.8 | 0.8-1.0 | 1.0-1.3 | >1.3 |

### Ratings
- 4.5-5.0: Excellent
- 4.0-4.5: Very Good
- 3.5-4.0: Good
- 3.0-3.5: Fair
- <3.0: Below Average

## Example Workflow

### Analyze a Stock

```
GET /api/dividends/analyze/ENB.TO
```

Returns:
- Company info (name, sector, industry)
- Current metrics (yield, payout ratio, EPS)
- Historical analysis (growth rate, consecutive years)
- Safety score and recommendation
- 5-year dividend history

### Build Your Portfolio

1. Analyze stocks: `GET /api/dividends/analyze/{symbol}`
2. View portfolio: `GET /api/dividends`
3. Compare performance: `GET /api/performance/compare`
4. Remove stocks: `DELETE /api/dividends/cached/{symbol}`

## Canadian Stock Symbols

Add `.TO` suffix for Toronto Stock Exchange:
- **Banks**: TD.TO, RY.TO, BNS.TO, BMO.TO, CM.TO
- **Utilities**: FTS.TO, EMA.TO, CU.TO
- **Energy**: ENB.TO, TRP.TO, PPL.TO
- **Telecom**: BCE.TO, T.TO
- **ETFs**: XDV.TO, CDZ.TO, VDY.TO

The API auto-retries with .TO suffix if initial fetch fails.

## Data Management

### Refresh All Stocks

To update existing stocks with current data:

```bash
cd scripts
python refresh_all_stocks.py
```

### Database

- SQLite database: `dividends.db`
- Auto-created on first run
- Delete and restart to reset

## Troubleshooting

### N/A Values in Table
Run the refresh script to populate missing fields (CurrentPrice, PayoutRatio, DividendGrowthRate).

### Python Script Errors
```bash
pip install yfinance
```

### CORS Errors
Ensure backend runs on port 5000 and frontend on port 3000.

### Slow Response
First-time stock analysis fetches from Yahoo Finance and may take 5-10 seconds. Subsequent requests use cached data.

## Project Structure

```
FinanceApi/
├── Controllers/
│   ├── DividendsController.cs      # API endpoints
│   └── PerformanceController.cs    # S&P comparison
├── Services/
│   ├── DividendAnalysisService.cs  # Core analysis logic
│   └── PerformanceComparisonService.cs
├── Data/
│   └── DividendDbContext.cs
├── Model/
│   ├── DividendModel.cs
│   └── IndexData.cs
├── scripts/
│   ├── update_stocks_from_yahoo.py
│   └── refresh_all_stocks.py
└── frontend/
    └── src/components/
        ├── DividendAnalysis.js
        ├── DividendCharts.js
        └── PerformanceDashboard.js
```

## Development

### Backend Watch Mode
```bash
dotnet watch run
```

### Frontend Development
```bash
cd frontend
npm start
```

## Next Steps

1. Add stocks to portfolio via the Dividend Analysis tab
2. Run refresh script to update all stock data
3. Compare portfolio performance against S&P 500
4. Review safety scores to optimize holdings
