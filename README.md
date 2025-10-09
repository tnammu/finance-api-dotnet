# Canadian Finance API - Updated Version

## What Changed? ✨

### Key Improvements:
1. **5-minute caching** - Reduces Yahoo Finance requests
2. **User-Agent headers** - Prevents blocking
3. **Better error handling** - Graceful failures with logging
4. **Separate endpoints** - Fast DB reads vs live updates
5. **Rate limiting protection** - 500ms delay between requests
6. **Multiple XPath selectors** - More reliable scraping
7. **Comprehensive logging** - See what's happening

---

## API Endpoints 🚀

### 1. **GET /api/stocks**
Get all stocks from database (FAST - no live fetching)

**Response includes:**
- Current price (from DB)
- How old the data is (in minutes)
- Whether data is stale (>15 minutes old)

```json
[
  {
    "id": 1,
    "symbol": "AAPL",
    "companyName": "Apple Inc.",
    "price": 178.50,
    "dividendYield": 0.52,
    "lastUpdated": "2025-10-08T10:30:00Z",
    "minutesOld": 5,
    "isStale": false
  }
]
```

---

### 2. **GET /api/stocks/{id}**
Get a single stock by ID (from database)

**Example:** `GET /api/stocks/1`

---

### 3. **GET /api/stocks/live/{symbol}** ⭐ NEW
Fetch live data from Yahoo Finance for a specific stock

**Example:** `GET /api/stocks/live/AAPL`

**What it does:**
- Fetches live data from Yahoo Finance
- Updates the stock in database if it exists
- Returns live price, company name, dividend yield
- Uses cache if data fetched recently

```json
{
  "symbol": "AAPL",
  "companyName": "Apple Inc.",
  "price": 178.75,
  "dividendYield": 0.52,
  "fetchedAt": "2025-10-08T10:35:00Z",
  "isLive": true,
  "source": "Yahoo Finance (Live)"
}
```

---

### 4. **POST /api/stocks/refresh** ⭐ UPDATED
Refresh ALL stocks in database (use sparingly!)

**What it does:**
- Loops through all stocks in DB
- Fetches live data for each
- Updates database
- Has 500ms delay between requests to avoid rate limiting

**Response:**
```json
{
  "totalStocks": 10,
  "successCount": 9,
  "failCount": 1,
  "updatedAt": "2025-10-08T10:40:00Z",
  "results": [
    { "symbol": "AAPL", "status": "Updated", "price": 178.75 },
    { "symbol": "MSFT", "status": "Updated", "price": 420.30 }
  ]
}
```

---

### 5. **POST /api/stocks/refresh/{id}** ⭐ NEW
Refresh a single stock by ID

**Example:** `POST /api/stocks/refresh/1`

---

### 6. **POST /api/stocks** ⭐ NEW
Add a new stock by symbol

**Request Body:**
```json
{
  "symbol": "GOOGL"
}
```

**What it does:**
- Fetches live data from Yahoo
- Adds to database if successful
- Returns error if symbol invalid

---

### 7. **DELETE /api/stocks/{id}** ⭐ NEW
Delete a stock by ID

**Example:** `DELETE /api/stocks/1`

---

## How to Use 📖

### Scenario 1: Quick Check (Use Database)
```
GET /api/stocks
```
**Fast response, shows all stocks with age indicators**

---

### Scenario 2: Get Latest Price for One Stock
```
GET /api/stocks/live/AAPL
```
**Fetches from Yahoo, updates DB, returns live data**

---

### Scenario 3: Refresh All Prices (Morning routine)
```
POST /api/stocks/refresh
```
**Updates all stocks, takes ~5-10 seconds depending on count**

---

### Scenario 4: Add New Stock
```
POST /api/stocks
Body: { "symbol": "TSLA" }
```
**Fetches live data and adds to database**

---

## Important Notes ⚠️

### Canadian ETFs
For Toronto Stock Exchange symbols, add `.TO` suffix:
- ✅ `XEQT.TO` (correct)
- ❌ `XEQT` (wrong - will search US markets)

### Rate Limiting
- **Built-in protection**: 500ms delay between requests
- **Caching**: 5-minute cache reduces Yahoo requests
- **Recommendation**: Don't call `/refresh` more than once per 5 minutes

### Yahoo Finance Scraping
- **Unofficial API**: Yahoo can change HTML anytime
- **Multiple selectors**: Code tries several XPath patterns
- **Fallback**: Returns cached data if scraping fails

---

## Testing Steps 🧪

### Step 1: Start the API
```bash
dotnet run
```

### Step 2: Open Swagger
Navigate to: `http://localhost:5000` or `https://localhost:5001`

### Step 3: Test Endpoints

1. **GET /api/stocks** - See seed data
2. **GET /api/stocks/live/AAPL** - Fetch live Apple stock
3. **POST /api/stocks** with `{"symbol": "GOOGL"}` - Add Google
4. **POST /api/stocks/refresh** - Update all prices
5. **GET /api/stocks** again - See updated prices

---

## Troubleshooting 🔧

### Problem: Getting seed data only, no live updates
**Solution:** Use the `/live/{symbol}` or `/refresh` endpoints

### Problem: "Could not fetch price"
**Possible causes:**
- Yahoo changed their HTML structure
- Rate limited (wait 5 minutes)
- Invalid symbol
- Network issue

**Check logs** in console for detailed error messages

### Problem: Slow response
**Cause:** Multiple Yahoo requests
**Solution:** 
- Use `/api/stocks` for quick reads (from DB)
- Use `/live/{symbol}` for single stock updates
- Avoid calling `/refresh` frequently

---

## Code Structure 📁

```
FinanceApi/
├── Controllers/
│   └── StocksController.cs    ← Updated with new endpoints
├── Services/
│   ├── StockService.cs        ← Updated with caching & error handling
│   └── LiveStockSeeder.cs     ← Updated seeding logic
├── Data/
│   └── FinanceContext.cs      ← Your existing DbContext
├── Model/
│   └── Stock.cs               ← Your existing model
├── Program.cs                 ← Updated startup logic
└── appsettings.json           ← Configuration
```

---

## What's Next? 🚀

### Suggested Improvements:
1. **Background Service** - Auto-refresh every 15 minutes
2. **Redis Cache** - Better caching for production
3. **Retry Logic** - Using Polly library
4. **API Key Support** - Switch to official APIs
5. **Historical Data** - Store price history

---

## Questions? 💡

Test the API and let me know:
1. Which endpoints work?
2. Which symbols fail?
3. What features you want next?

**Remember:** The `/live/{symbol}` endpoint is your friend for getting real-time data!