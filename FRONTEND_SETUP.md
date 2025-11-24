# Frontend Setup Guide

React-based dashboard for the Finance API dividend analysis platform.

## Quick Start

```bash
cd frontend
npm install
npm start
```

Opens at http://localhost:3000

## Features

### Stocks Tab (Dashboard)
- View all stocks in portfolio
- Add new stocks by symbol
- Refresh stock prices
- Delete stocks
- Data freshness indicators

### Dividend Analysis Tab
- **Portfolio Table**: Sortable/filterable table with all dividend stocks
  - Columns: Symbol, Company, Sector, Price, Yield, Payout Ratio, Growth Rate, Safety Score
  - Click column headers to sort
  - Search/filter by symbol

- **Add New Stock**: Enter symbol to analyze and add to portfolio
  - Supports US stocks (AAPL) and Canadian stocks (TD.TO)
  - Auto-tries .TO suffix for Canadian ETFs

- **Dividend Charts**: Visual history of dividend payments

- **Performance Dashboard**: Compare portfolio returns vs S&P 500

## Components

```
src/
├── App.js                           # Main app with tab navigation
├── components/
│   ├── Dashboard.js                 # Stock list and management
│   ├── DividendAnalysis.js          # Main dividend analysis view
│   ├── DividendCharts.js            # Dividend history charts
│   ├── PerformanceDashboard.js      # S&P 500 comparison
│   └── StockCard.js                 # Individual stock display
└── services/
    └── api.js                       # API client
```

## API Integration

The frontend connects to the .NET backend at http://localhost:5000.

Key endpoints used:
- `GET /api/dividends` - Fetch all portfolio stocks
- `GET /api/dividends/analyze/{symbol}` - Analyze and add stock
- `DELETE /api/dividends/cached/{symbol}` - Remove stock
- `GET /api/performance/compare` - Portfolio vs S&P comparison

## Styling

- CSS files per component (e.g., DividendAnalysis.css)
- Responsive design
- Dark theme for charts (Recharts)

## Development

```bash
npm start          # Development server
npm run build      # Production build
npm test           # Run tests
```

## Troubleshooting

### CORS Errors
Ensure backend is running on port 5000 with CORS enabled.

### N/A Values in Table
Backend needs to return all fields. If stocks show N/A, run the Python refresh script on the backend.

### Dependencies
```bash
npm install        # Install all dependencies
```

Required packages:
- react, react-dom
- react-router-dom
- axios
- recharts