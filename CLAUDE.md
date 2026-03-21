# Claude Code Context - Finance API

## Project Overview
A .NET 10 dividend analysis and swing trading platform with React frontend. Uses Python/yfinance to fetch stock data from Yahoo Finance. Sends alerts via ntfy.sh.

## Key Architecture Points

### Backend (.NET 10)

**Controllers:**
- `DividendsController.cs` — Dividend analysis endpoints
- `PortfolioController.cs` — Holdings CRUD (add/edit/delete/view)
- `AlertController.cs` — Trigger SMS alerts (`POST /api/alerts/send`)
- `StrategyController.cs` — Trading strategy analysis
- `StocksController.cs` — Stock data endpoints
- `EtfController.cs` — ETF data
- `PerformanceController.cs` — S&P 500 comparison
- `SP500Controller.cs` — S&P 500 specific endpoints
- `RedditController.cs` — Reddit sentiment
- `SectorController.cs` — Sector performance
- `GrowthController.cs` — Growth stock analysis
- `OilController.cs` — Oil market sentiment (`GET /api/oil/sentiment`, `POST /api/oil/send-sms`)

**Services:**
- `StockAlertService.cs` — Core swing analysis: runs `top_canadian_picks.py`, parses JSON, formats 4 SMS messages. Has 4h in-memory cache (`_cachedPicks`).
- `StockAlertSchedulerService.cs` — Background scheduler for periodic alerts
- `SmsNotificationService.cs` — ntfy.sh push notification wrapper (supports `topic:` and `title:` params)
- `OilSentimentService.cs` — Runs `oil_sentiment.py`, 30-min in-memory cache, formats plain-English SMS/Telegram message
- `PortfolioService.cs` — Holdings enrichment with live price/dividend data
- `DividendAnalysisService.cs` — Dividend safety scores, Python script execution
- `StrategyService.cs` — Trading strategy calculations
- `PerformanceComparisonService.cs` — Portfolio vs S&P 500
- `EtfService.cs`, `SP500Service.cs`, `GrowthStockService.cs`, `StocksService.cs` — Domain services
- `RedditSentimentService.cs` — Reddit mention tracking
- `SectorAnalysisService.cs` — Sector performance comparisons

### Database
- SQLite: `dividends.db` (main app data)
- SQLite: `price_cache.db` (swing trading price cache — 5-year OHLCV data)
- Main tables: `DividendModels`, `Holdings`, `DividendPayments`, `YearlyDividendSummary`
- Cache tables: `PriceCache`, `PriceCacheMeta`, `SymbolListCache`, `MacroSentimentCache`

### Python Scripts (scripts/)
**Swing Trading Analysis:**
- `top_canadian_picks.py` — Main swing analyzer. Fetches all TSX symbols, computes RSI, ATR, Bollinger Bands, divergence, entry/exit zones. Outputs JSON to stdout. See details below.
- `print_swing.py` — Display `picks_result.json` in formatted table (dev tool)

**Oil Sentiment Analysis:**
- `oil_sentiment.py` — WTI (CL=F) and Brent (BZ=F) analyzer. Fetches RSS news (Reuters, BBC, Yahoo, CBC), scores with VADER + keyword weights, computes RSI/ATR/BB technicals, generates directional signal (BUY/SELL/NEUTRAL), suggested order levels (aggressive/moderate/patient entries, SL, TP1, TP2), and 1d/3d/5d forecasts. `--json` flag for machine-readable output.

**Data Management:**
- `update_stocks_from_yahoo.py` — Fetch/update dividend data for individual stocks
- `refresh_all_stocks.py` — Bulk refresh all stocks in database
- `import_wealthsimple.py` — Import holdings from Wealthsimple export
- `import_holdings_report.py` — Import from CSV holdings report
- `fetch_holding_dividends.py` — Fetch dividend data for current holdings
- `bulk_import_tsx_stocks.py` / `bulk_import_us_stocks.py` — Bulk import stock lists
- `fetch_commodity_data.py`, `fetch_etf_history.py`, `fetch_etf_holdings.py` — Commodity/ETF data fetchers
- `fetch_index_data.py`, `fetch_sp500_monthly.py` — Index and S&P 500 data
- `growth_stock_analyzer.py`, `multi_strategy_analyzer.py`, `trading_strategy_analysis.py` — Strategy tools
- `sp500_performance_comparison.py` — Portfolio vs S&P 500 comparison script
- `update_reddit_sentiment.py` — Reddit sentiment refresh

### Frontend (React, port 3000)
- `Dashboard.js` — Main dashboard
- `Portfolio.js` — Holdings view with gain/loss, dividend income
- `DividendAnalysis.js` — Sortable dividend portfolio table
- `DividendCharts.js` — Dividend history charts
- `PerformanceDashboard.js` — S&P 500 comparison charts
- `OilSentiment.js` — Oil market signal dashboard (WTI + Brent, forecast bars, order levels)
- `StockAlerts.js` — Swing trade alerts view
- `StockCard.js` — Reusable stock card component
- `AddStockModal.js` — Modal for adding stocks to portfolio
- `EtfAnalysis.js` — ETF analysis view
- `GrowthAnalysis.js` — Growth stock analysis view
- `SP500Analysis.js` — S&P 500 analysis view
- `StrategyAnalysis.js` — Trading strategy analysis view

## Swing Trading Analysis (top_canadian_picks.py)

### What it does
Analyzes all TSX stocks/ETFs and ranks them by 4 strategies:
1. **RSI Oversold** — RSI < 40, excludes confirmed Downtrend stocks (falling knives)
2. **Swing Score** — Composite: ATR%, Bollinger Band position, volume ratio, RSI, 52w proximity, SMA200, RSI divergence bonus (+15pts), news sentiment (±15pts)
3. **Seasonal** — Stocks with >60% win rate in current month over 3+ years
4. **News Sentiment** — VADER-scored yfinance headlines (top positive)

### Caching layers
- **Price data** (`price_cache.db`): Full 5y download if >7 days old, incremental 15-day if stale >2 days, otherwise reads from SQLite — fast
- **TSX symbol list** (`SymbolListCache` table): 24h cache, avoids TSX API calls
- **Macro RSS sentiment** (`MacroSentimentCache` table): 1h cache
- **C# result cache** (`StockAlertService._cachedPicks`): 4h in-memory cache, returns immediately without running Python

### Entry/Exit Zone Calculation
- `L` = min(lows[-15 bars]) — swing support using actual intraday lows
- `H` = max(highs[-5 bars]) — recent signal bar high
- `E` = L + 0.4*(H-L) — entry at lower 40% of zone
- `SL` = E - 2×ATR
- `TP1` = E + 2×ATR (R:R = 2.0)
- `TP2` = E + 4×ATR (R:R = 4.0)

### Quality Filters
- `MIN_PRICE = $5.00`
- `MIN_AVG_VOLUME = 25,000`
- `MIN_DATA_DAYS = 200`
- RSI picks: excludes stocks in confirmed Downtrend (below both SMA50 and SMA200)

### Notification Output (4 messages via ntfy.sh)
- `[1/4]` RSI Oversold picks
- `[2/4]` Swing Trade picks (with entry zone, SL, TP1, TP2, R:R)
- `[3/4]` Seasonal picks
- `[4/4]` News Sentiment picks + macro sector arrows

## Oil Sentiment Analysis (oil_sentiment.py)

### What it does
Analyzes WTI (CL=F) and Brent (BZ=F) crude oil and produces a directional signal for each.

### News scoring
- Fetches 7 RSS feeds in parallel (Reuters, Yahoo Finance WTI/Brent, CBC, Global News, BBC, CBC World)
- Also pulls yfinance news headlines for CL=F and BZ=F
- Scores each headline: VADER sentiment (35% weight) + oil-specific keyword weights (65% weight)
- Keywords: geopolitical (war, sanctions, iran, houthi, red sea = bullish), supply/demand (ceasefire, surplus, opec increase, china slowdown = bearish)
- `avg_weighted > 0.08` → Bullish, `< -0.08` → Bearish, else Neutral

### Signal engine (composite score)
| Factor | Points |
|---|---|
| Trend (above/below SMA20+SMA50) | ±20 |
| RSI (<35 oversold / >65 overbought) | ±15 |
| Bollinger Band position (<20% / >80%) | ±15 |
| Weekly momentum (>5% / <-5%) | ±10 |
| News sentiment | ±25 (biggest factor) |
| Volume confirmation | ±5 |

- Score ≥ +20 → BUY, ≤ -20 → SELL, else NEUTRAL / WAIT
- Confidence = `min(95, 50 + abs(score))`

### Order level suggestions (ATR-based)
- **Buy entries:** Aggressive = price - 0.5×ATR, Moderate = price - 1×ATR, Patient = price - 2×ATR
- **Buy SL/TP:** SL = price - 3×ATR, TP1 = price + 2×ATR (R:R 1:2), TP2 = price + 4×ATR (R:R 1:4)
- **Sell entries:** Aggressive = price + 0.5×ATR, Moderate = price + 1×ATR, Patient = price + 2×ATR
- **Sell SL/TP:** SL = price + 3×ATR, TP1 = price - 2×ATR, TP2 = price - 4×ATR

### Forecast
- RSI-conditional historical analysis: finds past instances with similar RSI (±10 pts), computes actual 1d/3d/5d returns
- Combines with ATR-based range; takes wider of the two for safety
- Mean-reversion target = SMA20

### Caching
- `OilSentimentService` caches result 30 minutes in-memory

### Run manually
```bash
cd scripts
python oil_sentiment.py              # human-readable report
python oil_sentiment.py --json       # JSON output
```

### Send via API
```
POST http://localhost:5000/api/oil/send-sms
```

## Important Patterns

### Python Script Execution
`StockAlertService.GetTopPicksAsync()` runs `top_canadian_picks.py` via `System.Diagnostics.Process`. Script writes JSON to stdout, logs to stderr. Cache check happens BEFORE creating `ProcessStartInfo`.

### Portfolio Holdings
`HoldingModel` stores: Symbol, Shares, BuyPrice, BuyDate, Notes, MarketPrice (imported), AnnualDividendPerShare. `PortfolioService.GetHoldingsAsync()` enriches with live price from `DividendModels` table (tries exact symbol then `.TO` suffix).

### Canadian Stock Symbols
All TSX stocks use `.TO` suffix (e.g., `ENB.TO`). The portfolio service handles both bare symbols and `.TO` suffix automatically.

### Safety Score (Dividends)
Calculated based on: Yield (2-6%), Payout Ratio (<60%), Growth Rate, Consecutive Years (10+), Beta (<1.0)

## Common Tasks

### Run Swing Analysis Manually
```bash
cd scripts
python top_canadian_picks.py > picks_result.json
python print_swing.py   # pretty-print the result
```

### Trigger Swing Alert via API
```
POST http://localhost:5000/api/alerts/send-sms
```

### Get Oil Signal via API
```
GET  http://localhost:5000/api/oil/sentiment
POST http://localhost:5000/api/oil/send-sms
```

### Refresh Dividend Data
```bash
cd scripts
python refresh_all_stocks.py
```

### Import Holdings
```bash
cd scripts
python import_wealthsimple.py path/to/export.csv
```

### Add New Field to Model
1. Update `Model/DividendModel.cs`
2. Update Python script to populate field
3. Update controller endpoint to return field
4. Update frontend to display field

### Debug N/A Values
Usually means database field is empty. Check:
1. Controller returns the field
2. Python script updates the field
3. Frontend maps the field correctly

## Dependencies
- Backend: .NET 10, EF Core, SQLite, ntfy.sh
- Frontend: React 18, Recharts, Axios
- Python: yfinance, pandas, numpy, vaderSentiment

## Ports
- Backend: http://localhost:5000
- Frontend: http://localhost:3000
