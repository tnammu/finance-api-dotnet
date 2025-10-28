# Finance Dashboard - React Frontend

A clean, minimal React frontend for the Finance API that provides stock tracking, dividend analysis, and API usage monitoring.

## Features

- **Stock Dashboard**: View, add, refresh, and delete stocks with real-time price data
- **Dividend Analysis**: Analyze dividend stocks with historical data, safety ratings, and interactive charts
- **API Usage Tracking**: Monitor your Alpha Vantage API usage with daily limits and history
- **Interactive Charts**: Visualize dividend history and API usage trends using Recharts
- **Responsive Design**: Clean, minimal UI that works on all screen sizes

## Prerequisites

- Node.js (v14 or higher)
- npm or yarn
- Running Finance API backend (on http://localhost:5000)

## Installation

1. Navigate to the frontend directory:
```bash
cd frontend
```

2. Install dependencies:
```bash
npm install
```

## Running the Application

1. Make sure your Finance API backend is running on port 5000

2. Start the React development server:
```bash
npm start
```

3. Open your browser and navigate to:
```
http://localhost:3000
```

## Project Structure

```
frontend/
├── public/
│   └── index.html
├── src/
│   ├── components/
│   │   ├── Dashboard.js          # Stock portfolio dashboard
│   │   ├── Dashboard.css
│   │   ├── StockCard.js          # Individual stock card
│   │   ├── StockCard.css
│   │   ├── AddStockModal.js      # Modal for adding new stocks
│   │   ├── DividendAnalysis.js   # Dividend analysis view
│   │   ├── DividendAnalysis.css
│   │   ├── ApiUsage.js           # API usage tracking
│   │   └── ApiUsage.css
│   ├── services/
│   │   └── api.js                # API service layer
│   ├── App.js                    # Main app component
│   ├── App.css                   # Global styles
│   ├── index.js                  # Entry point
│   └── index.css                 # Base styles
├── package.json
└── README.md
```

## Available Scripts

### `npm start`
Runs the app in development mode on http://localhost:3000

### `npm run build`
Builds the app for production to the `build` folder

### `npm test`
Launches the test runner

## API Configuration

The frontend is configured to connect to the backend API at:
```
http://localhost:5000/api
```

To change this, edit the `API_BASE_URL` in `src/services/api.js`.

## Features Guide

### Stock Dashboard
- View all tracked stocks with current prices
- Add new stocks by symbol (e.g., AAPL, GOOGL, MSFT)
- Refresh individual stocks or all stocks at once
- Delete stocks from your portfolio
- See data freshness indicators (Fresh/Stale)

### Dividend Analysis
- Analyze any stock for dividend safety and history
- View cached analyses to save API calls
- See top-rated dividend stocks
- Interactive charts showing:
  - Annual dividend trends
  - Payment history over time
- Safety ratings and scores
- Consecutive years of payments
- Dividend growth rates

### API Usage Tracking
- Real-time usage percentage indicator
- Daily API call limits and remaining calls
- 30-day usage history with charts
- Daily breakdown table
- Usage tips and best practices

## Technologies Used

- **React 18**: UI framework
- **React Router**: Navigation
- **Axios**: HTTP client
- **Recharts**: Data visualization
- **CSS3**: Styling (no heavy frameworks - minimal design)

## Browser Support

- Chrome (latest)
- Firefox (latest)
- Safari (latest)
- Edge (latest)

## Development Tips

1. **Hot Reload**: Changes to React components will automatically reload in the browser
2. **API Proxy**: Configured in package.json to proxy API requests to avoid CORS issues
3. **Error Handling**: All API calls include error handling with user-friendly messages
4. **Loading States**: Components show loading indicators during API calls

## Troubleshooting

### Cannot connect to backend
- Ensure the .NET API is running on http://localhost:5000
- Check that CORS is enabled in the backend (Program.cs)

### Stocks not loading
- Verify your Alpha Vantage API key is configured in the backend
- Check browser console for error messages

### Charts not displaying
- Ensure data is available (analyze stocks first)
- Check browser compatibility

## Building for Production

1. Create production build:
```bash
npm run build
```

2. The optimized production build will be in the `build` folder

3. Serve with a static server:
```bash
npx serve -s build
```

## Future Enhancements

- User authentication
- Portfolio value tracking over time
- Stock comparison views
- Alerts and notifications
- Dark mode
- Export data to CSV/PDF
- Mobile app version

## License

This project is part of the Finance API solution.
