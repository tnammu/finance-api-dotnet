# Finance Dashboard - Complete Setup Guide

This guide will help you set up and run both the backend API and the React frontend.

## Quick Start

### Option 1: Use the Startup Script (Easiest! ✨)

**Double-click `start-app.bat`** in the project root directory.

This will automatically:
- Start the backend API on http://localhost:5000
- Start the React frontend on http://localhost:3000
- Open both in separate terminal windows

### Option 2: Run Manually in Separate Terminals

**Terminal 1 - Backend API:**
```bash
dotnet run
```
The API will start on http://localhost:5000 (Swagger UI)

**Terminal 2 - Frontend React App:**
```bash
cd frontend
npm install
npm start
```
The React app will open automatically at http://localhost:3000

### First Time Setup (Frontend Dependencies)

Before running the frontend for the first time, install dependencies:
```bash
cd frontend
npm install
```

## What's Included

### Backend API (C# .NET 7)
- **Stocks API**: Track stock prices with live data from Alpha Vantage
- **Dividends API**: Analyze dividend stocks with historical data and safety ratings
- **Database**: SQLite database with caching to minimize API calls
- **Swagger**: Interactive API documentation

### Frontend (React)
- **Stock Dashboard**: View, add, refresh, and delete stocks
- **Dividend Analysis**: Analyze dividend stocks with charts and metrics
- **API Usage Tracker**: Monitor your Alpha Vantage API usage
- **Responsive Design**: Clean, minimal UI

## Project Structure

```
FinanceApi/
├── Controllers/
│   ├── StocksController.cs          # Stock endpoints
│   └── DividendsController.cs       # Dividend endpoints
├── Services/
│   ├── StockService.cs              # Stock data fetching
│   ├── DividendAnalysisService.cs   # Dividend analysis
│   └── LiveStockSeeder.cs           # Bulk stock seeding
├── Data/
│   ├── FinanceDbContext.cs          # Stock database context
│   └── DividendDbContext.cs         # Dividend database context
├── Model/
│   └── DividendModel.cs             # Dividend models
├── Program.cs                        # API configuration
├── appsettings.json                 # Configuration (API key here)
└── frontend/                         # React frontend
    ├── public/
    ├── src/
    │   ├── components/
    │   │   ├── Dashboard.js
    │   │   ├── DividendAnalysis.js
    │   │   └── ApiUsage.js
    │   ├── services/
    │   │   └── api.js
    │   └── App.js
    └── package.json
```

## Configuration

### API Key Setup

Make sure you have your Alpha Vantage API key configured in `appsettings.json`:

```json
{
  "AlphaVantage": {
    "ApiKey": "YOUR_API_KEY_HERE"
  }
}
```

Get a free API key at: https://www.alphavantage.co/support/#api-key

### Database

The SQLite database (`finance.db`) is created automatically when you run the API.

## Usage Guide

### Adding Stocks

1. Go to the **Stocks** tab
2. Click **+ Add Stock**
3. Enter a stock symbol (e.g., AAPL, MSFT, GOOGL)
4. Click **Add Stock**

### Analyzing Dividends

1. Go to the **Dividend Analysis** tab
2. Enter a stock symbol (e.g., AAPL, TD.TO for Canadian stocks)
3. Click **Analyze**
4. View detailed dividend history, safety ratings, and charts

### Monitoring API Usage

1. Go to the **API Usage** tab
2. View today's usage and remaining calls
3. Check 30-day history to track usage patterns

## API Endpoints

### Stocks API

```
GET    /api/stocks              # Get all stocks
GET    /api/stocks/{id}         # Get stock by ID
GET    /api/stocks/live/{symbol}  # Get live stock data
POST   /api/stocks              # Add new stock
DELETE /api/stocks/{id}         # Delete stock
POST   /api/stocks/refresh/{id} # Refresh single stock
POST   /api/stocks/refresh      # Refresh all stocks
```

### Dividends API

```
GET    /api/dividends/analyze/{symbol}     # Analyze dividend stock
GET    /api/dividends/cached               # Get all cached analyses
GET    /api/dividends/history/{symbol}     # Get dividend history
GET    /api/dividends/usage/today          # Today's API usage
GET    /api/dividends/usage/history        # 30-day usage history
GET    /api/dividends/stats                # Database statistics
DELETE /api/dividends/cached/{symbol}      # Delete cached data
```

## Features Showcase

### Stock Dashboard
- Real-time stock prices
- Data freshness indicators
- Bulk refresh capability
- Portfolio statistics

### Dividend Analysis
- Safety scores and ratings
- Historical dividend charts
- Consecutive years tracking
- Growth rate calculations
- Smart caching (7-day expiration)

### API Usage Tracking
- Visual usage indicators
- Daily limit tracking
- Historical usage charts
- Usage tips and recommendations

## Development

### Backend Development

**Watch mode (auto-reload):**
```bash
dotnet watch run
```

**Run tests:**
```bash
dotnet test
```

### Frontend Development

**Start with hot reload:**
```bash
npm start
```

**Build for production:**
```bash
npm run build
```

## Troubleshooting

### Common Issues

**1. CORS Errors**
- Ensure backend is running on port 5000
- Check that CORS is configured in Program.cs
- Verify frontend is running on port 3000

**2. API Key Issues**
- Check appsettings.json has valid API key
- Free tier has 25 calls/day limit
- Use cached data to minimize API calls

**3. Database Issues**
- Delete finance.db and restart API to recreate
- Check write permissions in project directory

**4. Frontend Won't Start**
- Delete node_modules and run `npm install` again
- Clear npm cache: `npm cache clean --force`
- Try different port if 3000 is in use

## Performance Tips

1. **Use Caching**: View cached dividend analyses instead of refreshing
2. **Batch Operations**: Refresh multiple stocks during off-peak hours
3. **Monitor Usage**: Keep an eye on API usage to avoid hitting limits
4. **Data Retention**: Cached data stays fresh for 15 minutes (stocks) or 7 days (dividends)

## Tech Stack

### Backend
- .NET 7
- Entity Framework Core
- SQLite
- Alpha Vantage API
- Swagger/OpenAPI

### Frontend
- React 18
- Axios
- Recharts
- React Router
- CSS3

## Next Steps

1. Add more stocks to your portfolio
2. Analyze dividend stocks for investment opportunities
3. Monitor your API usage to stay within limits
4. Explore the charts and visualizations
5. Use Swagger docs to test API endpoints

## Support

For issues or questions:
1. Check the Swagger documentation at http://localhost:5000
2. Review the frontend README in the `frontend` folder
3. Check browser console for errors
4. Verify API key and configuration

## License

This project is for educational and personal use.
