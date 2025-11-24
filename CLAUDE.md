# Claude Code Context - Finance API

## Project Overview
A .NET 7 dividend analysis platform with React frontend. Uses Python/yfinance to fetch stock data from Yahoo Finance.

## Key Architecture Points

### Backend (.NET 7)
- **DividendsController.cs**: Main API controller for dividend operations
- **DividendAnalysisService.cs**: Core service containing:
  - Dividend analysis logic
  - Safety score calculations
  - Python script execution (FetchStockDataViaPythonAsync)
- **PerformanceController.cs**: S&P 500 comparison endpoints
- **PerformanceComparisonService.cs**: Portfolio performance calculations

### Database
- SQLite: `dividends.db`
- Main table: `DividendModels`
- Related tables: `DividendPaymentRecord`, `YearlyDividendSummary`, `ApiUsageLog`

### Python Scripts (scripts/)
- `update_stocks_from_yahoo.py`: StockDataUpdater class for fetching from Yahoo Finance
- `refresh_all_stocks.py`: Bulk refresh all stocks in database

### Frontend (React)
- `DividendAnalysis.js`: Main view with sortable portfolio table
- `DividendCharts.js`: Dividend history visualization
- `PerformanceDashboard.js`: S&P 500 comparison charts

## Important Patterns

### Python Script Execution
Python scripts are executed from DividendAnalysisService using System.Diagnostics.Process. The service method `FetchStockDataViaPythonAsync` handles:
- Script path resolution
- Process execution
- Auto-retry with .TO suffix for Canadian stocks

### Data Flow
1. Frontend calls `/api/dividends/analyze/{symbol}`
2. Controller calls DividendAnalysisService
3. Service executes Python script
4. Python fetches from Yahoo Finance via yfinance
5. Data saved to SQLite
6. Returns to frontend

### Safety Score
Calculated based on: Yield (2-6%), Payout Ratio (<60%), Growth Rate, Consecutive Years (10+), Beta (<1.0)

## Common Tasks

### Refresh All Stocks
```bash
cd scripts
python refresh_all_stocks.py
```

### Add New Field to Model
1. Update `Model/DividendModel.cs`
2. Update Python script to populate field
3. Update controller endpoint to return field
4. Update frontend to display field

### Debug N/A Values
Usually means database field is empty. Run refresh script or check:
1. GetAllDividends() in controller returns the field
2. Python script updates the field
3. Frontend maps the field correctly

## Files Modified Recently
- Controllers/DividendsController.cs - Removed Python script methods (moved to service)
- Services/DividendAnalysisService.cs - Added Python script execution methods
- scripts/refresh_all_stocks.py - Created for bulk stock refresh

## Dependencies
- Backend: .NET 7, EF Core, SQLite
- Frontend: React 18, Recharts, Axios
- Python: yfinance

## Ports
- Backend: http://localhost:5000
- Frontend: http://localhost:3000
