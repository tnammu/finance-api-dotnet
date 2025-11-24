# Finance API - Dividend Analysis Platform

A .NET 7 Web API with React frontend for analyzing dividend stocks using Yahoo Finance data via Python.

## Overview

This application helps you:
- Analyze dividend stocks with safety scores and ratings
- Track 5-year dividend history and growth rates
- Compare portfolio performance against S&P 500
- Manage a sortable, filterable portfolio of dividend stocks
- Support for both US stocks and Canadian ETFs (.TO suffix)

## Architecture

```
FinanceApi/
├── Controllers/
│   ├── DividendsController.cs      # Main dividend analysis endpoints
│   └── PerformanceController.cs    # S&P 500 comparison metrics
├── Services/
│   ├── DividendAnalysisService.cs  # Dividend analysis + Python script execution
│   └── PerformanceComparisonService.cs  # Portfolio performance calculations
├── Data/
│   └── DividendDbContext.cs        # Entity Framework context
├── Model/
│   ├── DividendModel.cs            # Dividend data models
│   └── IndexData.cs                # S&P 500 comparison models
├── scripts/
│   ├── update_stocks_from_yahoo.py # Fetch stock data from Yahoo Finance
│   └── refresh_all_stocks.py       # Refresh all stocks in database
├── frontend/                       # React application
│   └── src/
│       └── components/
│           ├── Dashboard.js
│           ├── DividendAnalysis.js
│           ├── DividendCharts.js
│           └── PerformanceDashboard.js
├── Program.cs
├── appsettings.json
└── dividends.db                    # SQLite database
```

## Tech Stack

### Backend
- .NET 7 / ASP.NET Core
- Entity Framework Core
- SQLite
- Python 3 + yfinance (Yahoo Finance data)

### Frontend
- React 18
- Recharts (data visualization)
- Axios
- React Router

## Quick Start

### Prerequisites
- .NET 7 SDK
- Node.js 18+
- Python 3.8+ with yfinance: `pip install yfinance`

### Option 1: Use Startup Script
Double-click `start-app.bat` to start both backend and frontend.

### Option 2: Manual Start

**Terminal 1 - Backend:**
```bash
dotnet run
```
API starts at http://localhost:5000

**Terminal 2 - Frontend:**
```bash
cd frontend
npm install
npm start
```
React app opens at http://localhost:3000

## API Endpoints

### Dividend Analysis
```
GET  /api/dividends                    # Get all stocks in portfolio
GET  /api/dividends/analyze/{symbol}   # Analyze a stock (fetches via Python)
GET  /api/dividends/cached             # Get cached analyses
GET  /api/dividends/history/{symbol}   # Get dividend payment history
DELETE /api/dividends/cached/{symbol}  # Remove stock from portfolio
```

### Performance Comparison
```
GET  /api/performance/compare          # Compare portfolio vs S&P 500
GET  /api/performance/portfolio-summary # Portfolio metrics
```

## Features

### Portfolio Table
- Sortable columns (Price, Yield, Payout Ratio, Growth Rate, Safety Score)
- Filter/search by symbol
- Real-time data freshness indicators
- Delete stocks from portfolio

### Dividend Analysis
- Safety scores (1-5) based on yield, payout ratio, growth, consecutive years
- Visual charts for dividend history
- Sector and industry classification
- Recommendations based on your criteria

### Performance Dashboard
- Portfolio total return vs S&P 500
- Alpha and relative performance
- Dividend income tracking

## Safety Score Calculation

Stocks are scored on:
- **Dividend Yield**: 2-6% optimal
- **Payout Ratio**: <60% sustainable
- **Dividend Growth**: Positive 5-year trend
- **Consecutive Years**: 10+ years = Aristocrat
- **Beta**: <1.0 preferred

## Data Source

Stock data is fetched from Yahoo Finance via Python scripts. The system:
1. Uses yfinance library for reliable data
2. Caches data in SQLite database
3. Auto-retries with .TO suffix for Canadian stocks
4. Updates CurrentPrice, PayoutRatio, and DividendGrowthRate fields

### Refreshing Stock Data
```bash
cd scripts
python refresh_all_stocks.py
```

## Troubleshooting

### N/A Values in Portfolio Table
Run the refresh script to update existing stocks with missing data fields.

### Python Script Errors
Ensure yfinance is installed: `pip install yfinance`

### Database Issues
Delete `dividends.db` and restart API to recreate.

## Development

### Backend
```bash
dotnet watch run  # Auto-reload on changes
```

### Frontend
```bash
cd frontend
npm start  # Hot reload enabled
```

## License

Educational and personal use.