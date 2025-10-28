# CSV Export Feature - User Guide

Your Finance API now supports CSV export! You can easily export your data to CSV files that open in Excel, Google Sheets, or any spreadsheet application.

## What Was Added

### Backend API Endpoints

**Stocks Export:**
- `GET /api/stocks/export/csv` - Export all stocks with prices and data age

**Dividends Export:**
- `GET /api/dividends/export/csv` - Export all dividend analyses with safety ratings
- `GET /api/dividends/export/payments/csv` - Export all dividend payment history
- `GET /api/dividends/export/payments/csv?symbol=AAPL` - Export payments for specific stock

**API Usage Export:**
- `GET /api/dividends/export/usage/csv` - Export API usage history

### Frontend UI Buttons

**Stock Dashboard:**
- "Export CSV" button in the header (next to "Refresh All")
- Exports all stocks with current prices

**Dividend Analysis:**
- "Export All" button - Exports all analyzed dividends
- "Export Payments" button - Exports detailed payment history

**API Usage Tab:**
- "Export CSV" button - Exports usage history

## How to Use

### From the Frontend (Easiest Way)

1. **Export Stocks:**
   - Go to the "Stocks" tab
   - Click the "Export CSV" button in the header
   - CSV file downloads automatically with filename: `stocks_export_YYYYMMDD_HHMMSS.csv`

2. **Export Dividends:**
   - Go to the "Dividend Analysis" tab
   - Scroll to "Recently Analyzed" section
   - Click "Export All" for dividend summaries
   - Click "Export Payments" for detailed payment history

3. **Export API Usage:**
   - Go to the "API Usage" tab
   - Click the "Export CSV" button in the header
   - View your historical API usage in Excel/Sheets

### From the API Directly

You can also download CSVs directly by visiting these URLs in your browser:

```
http://localhost:5000/api/stocks/export/csv
http://localhost:5000/api/dividends/export/csv
http://localhost:5000/api/dividends/export/payments/csv
http://localhost:5000/api/dividends/export/usage/csv
```

## CSV File Contents

### Stocks Export (`stocks_export_*.csv`)
```
Symbol, Company Name, Price, Dividend Yield (%), Last Updated, Data Age (minutes)
AAPL, "Apple Inc.", 175.23, 0.52, 2025-10-24 14:30:00, 15
MSFT, "Microsoft Corporation", 385.67, 0.78, 2025-10-24 14:30:00, 15
```

**Columns:**
- Symbol
- Company Name
- Current Price
- Dividend Yield (%)
- Last Updated (timestamp)
- Data Age (minutes since last update)

### Dividends Export (`dividends_export_*.csv`)
```
Symbol, Company Name, Sector, Industry, Dividend Yield (%), Payout Ratio (%), Safety Score, Safety Rating, Consecutive Years, Growth Rate (%), Last Updated, Days Old
AAPL, "Apple Inc.", "Technology", "Consumer Electronics", 0.52, 15.23, 4.20, "Very Good", 12, 7.5, 2025-10-24 10:00:00, 0.2
```

**Columns:**
- Symbol
- Company Name
- Sector & Industry
- Dividend Yield (%)
- Payout Ratio (%)
- Safety Score (0-5 scale)
- Safety Rating (Excellent/Good/Fair/Poor)
- Consecutive Years of Payments
- Growth Rate (%)
- Last Updated
- Days Old

### Dividend Payments Export (`dividend_payments_*.csv`)
```
Symbol, Payment Date, Amount, Year, Quarter
AAPL, 2025-08-15, 0.24, 2025, Q3
AAPL, 2025-05-15, 0.24, 2025, Q2
AAPL, 2025-02-15, 0.24, 2025, Q1
```

**Columns:**
- Symbol
- Payment Date
- Dividend Amount
- Year
- Quarter (Q1/Q2/Q3/Q4)

### API Usage Export (`api_usage_export_*.csv`)
```
Date, Calls Used, Daily Limit, Remaining, Percentage Used (%), Notes
2025-10-24, 15, 25, 10, 60.0, ""
2025-10-23, 8, 25, 17, 32.0, ""
```

**Columns:**
- Date
- API Calls Used
- Daily Limit (25 for free tier)
- Remaining Calls
- Percentage Used (%)
- Notes (optional)

## Opening CSV Files

### In Excel
1. Download the CSV file
2. Right-click → "Open with" → Microsoft Excel
3. Or open Excel → File → Open → select the CSV file

### In Google Sheets
1. Download the CSV file
2. Go to Google Sheets (sheets.google.com)
3. File → Import → Upload → select the CSV file
4. Click "Import data"

### In VS Code
1. Install "Excel Viewer" extension
2. Open the CSV file in VS Code
3. View as a formatted table

## Use Cases

### Financial Analysis
- Export dividend data to Excel
- Create pivot tables and charts
- Compare safety scores across sectors
- Analyze dividend growth trends

### Portfolio Tracking
- Export stocks to track portfolio value over time
- Calculate total dividends earned
- Monitor price changes

### API Budget Management
- Export usage history
- Identify high-usage days
- Plan when to fetch new data
- Stay within free tier limits (25 calls/day)

### Sharing & Backup
- Share CSV files with collaborators
- Backup your data in spreadsheet format
- Import into other tools/applications
- Create presentations with your data

## Tips

1. **Regular Exports**: Export your data weekly to track changes over time
2. **Compare Files**: Keep multiple exports to see how safety scores and prices change
3. **Analyze in Sheets**: Use Google Sheets for quick charts and sharing
4. **Backup Before Refresh**: Export before doing bulk refreshes in case something goes wrong
5. **Filter & Sort**: Use Excel/Sheets filtering to find top dividend stocks

## Advanced: Programmatic Access

You can also fetch CSVs programmatically:

**JavaScript/Node.js:**
```javascript
const response = await fetch('http://localhost:5000/api/stocks/export/csv');
const csvText = await response.text();
// Process CSV
```

**Python:**
```python
import requests
import pandas as pd
from io import StringIO

response = requests.get('http://localhost:5000/api/stocks/export/csv')
df = pd.read_csv(StringIO(response.text))
print(df)
```

**cURL:**
```bash
curl http://localhost:5000/api/stocks/export/csv -o stocks.csv
```

## Why CSV Instead of Database Tools?

**CSV Advantages:**
- Opens in Excel, Google Sheets (familiar tools)
- Easy to share with non-technical users
- No special software needed
- Import into other applications
- Quick analysis with formulas and charts

**Database Advantages:**
- Faster queries for large datasets
- Better for app performance
- Relationships between tables
- Better for the backend

**Best of Both Worlds:**
- App uses fast SQLite database
- Export to CSV when you want to analyze/share
- You get performance + convenience!

## Troubleshooting

**CSV doesn't download:**
- Make sure backend API is running on http://localhost:5000
- Check browser console for errors
- Try the direct URL in a new browser tab

**CSV shows weird characters:**
- The CSV is UTF-8 encoded
- Most modern tools handle this automatically
- If issues in Excel: Data → From Text/CSV → UTF-8 encoding

**Empty CSV:**
- Make sure you have data in the database
- Check that stocks/dividends have been added/analyzed
- View the data in the frontend first to confirm

**Need different columns:**
- The backend controllers can be customized
- Edit the CSV generation code in Controllers/
- Add or remove columns as needed

## Summary

The CSV export feature gives you the **best of both worlds**:
- ✅ Fast SQLite database for the app
- ✅ Easy CSV export for viewing/sharing
- ✅ No API rate limits (unlike Google Sheets API)
- ✅ Works offline
- ✅ Familiar tools (Excel/Google Sheets)

Enjoy your new export feature! 🎉
