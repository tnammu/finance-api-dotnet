🚀 Alpha Vantage Setup Guide
Why Alpha Vantage?
✅ Free tier: 25 API calls per day (perfect for learning)
✅ Official API: No web scraping, reliable
✅ Real-time data: Current stock prices
✅ Canadian stocks: Supports .TO suffix
✅ No credit card: Just need email for API key

📋 Step-by-Step Setup (5 Minutes)
Step 1: Get Your FREE API Key

Go to: https://www.alphavantage.co/support/#api-key
Enter your email address
Click "GET FREE API KEY"
Copy the API key (looks like: ABC123XYZ456)

Example API Key: DEMO (for testing, but limited)

Step 2: Update Your Files
A) Replace StockService.cs
Location: Services/StockService.cs
Replace your entire file with: StockService.cs (from AlphaVantage folder)
Key changes:

Uses Alpha Vantage REST API instead of web scraping
No more HTML parsing
JSON responses (clean and reliable)
Better error handling for API limits


B) Update appsettings.json
Location: appsettings.json (root of project)
Add the AlphaVantage section with YOUR API key:
json{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "FinanceApi": "Information"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=finance.db"
  },
  "AlphaVantage": {
    "ApiKey": "PUT_YOUR_API_KEY_HERE"
  }
}
IMPORTANT: Replace PUT_YOUR_API_KEY_HERE with your actual API key!

Step 3: Clean and Rebuild
bash# Delete old database for fresh start
del finance.db

# Restore packages (if needed)
dotnet restore

# Build
dotnet build

Step 4: Run the API
bashdotnet run
Expected console output:
Starting database seeding...
Fetching data for AAPL...
✓ Added AAPL: Apple Inc. at $178.50
Fetching data for MSFT...
✓ Added MSFT: Microsoft Corporation at $420.30
...
Seeding complete: 10 added, 0 skipped, 0 failed
Now listening on: http://localhost:5199

Step 5: Test in Swagger
Open: http://localhost:5199 (or your port from console)
Test 1: Get All Stocks
GET /api/stocks
Should show 10 stocks with real prices!
Test 2: Get Live Data
GET /api/stocks/live/AAPL
Should return current Apple stock price from Alpha Vantage
Test 3: Add New Stock
POST /api/stocks
Body: {"symbol": "NVDA"}
Should fetch NVIDIA data and add to database

📊 What Alpha Vantage Returns
Global Quote Response (Current Price):
json{
  "Global Quote": {
    "01. symbol": "AAPL",
    "02. open": "178.50",
    "03. high": "179.75",
    "04. low": "177.25",
    "05. price": "178.75",
    "06. volume": "50234567",
    "07. latest trading day": "2025-10-08",
    "08. previous close": "178.50",
    "09. change": "0.25",
    "10. change percent": "0.14%"
  }
}
Company Overview Response (Name & Dividend):
json{
  "Symbol": "AAPL",
  "Name": "Apple Inc.",
  "Description": "Apple Inc. designs, manufactures...",
  "Sector": "Technology",
  "Industry": "Consumer Electronics",
  "DividendYield": "0.0052",
  "MarketCapitalization": "2800000000000"
}

⚠️ Important Notes
API Rate Limits (Free Tier):

25 API calls per day
5 API calls per minute

What this means:

Each stock fetch uses 2-3 API calls (quote + company info + dividend)
You can fetch data for ~8-10 stocks per day
Perfect for learning and testing!

How My Code Handles Limits:

5-minute caching: Same stock requested twice? Uses cache (no API call)
Rate limit detection: If API returns rate limit message, falls back to cache
Error handling: Graceful failures, won't crash your app


🎯 API Call Usage
Seeding 10 Stocks at Startup:
10 stocks × 3 calls each = 30 calls
Result: Will hit rate limit during seeding!
Solution: Reduce Initial Seeds
Update Program.cs to seed fewer stocks:
csharp// Start with just 3-5 stocks to stay within limits
var symbols = new List<string> 
{ 
    "AAPL",   // Apple
    "MSFT",   // Microsoft
    "GOOGL",  // Google
    "TSLA",   // Tesla
    "AMZN"    // Amazon
};
Then add more stocks throughout the day using:
POST /api/stocks
Body: {"symbol": "NVDA"}

🔍 Testing Tips
Test with DEMO Key First:
Alpha Vantage provides a DEMO key for testing (very limited):
json"AlphaVantage": {
  "ApiKey": "DEMO"
}
But get your own key for real testing!

Check Your API Usage:
Alpha Vantage doesn't provide a usage dashboard on free tier, but you can track it by:

Watching console logs (each call is logged)
Keeping count manually
Using cache effectively


🐛 Troubleshooting
Error: "Alpha Vantage API key not configured"
Solution:

Check appsettings.json has the AlphaVantage section
Verify API key is not "YOUR_API_KEY_HERE"
Restart the application


Error: "Note: Thank you for using Alpha Vantage! Our standard API call frequency is 5 calls per minute..."
This means: Rate limit reached (5 calls/minute)
Solution:

Wait 1 minute
Use cached data (automatic)
Reduce number of stocks being seeded


Error: "No data returned for AAPL"
Possible causes:

Invalid API key
Invalid stock symbol
API is down (rare)

Solution:

Verify API key is correct
Test with known symbols: AAPL, MSFT, GOOGL
Check if symbol exists on Alpha Vantage


Canadian ETFs Not Working?
For Toronto Stock Exchange, use .TO suffix:
XEQT.TO  ✅
XEQT     ❌
Alpha Vantage supports Canadian markets!

📈 Upgrade Options
When 25 calls/day isn't enough:
Alpha Vantage Paid Plans:

$49.99/month: 75 calls/minute, extended history
$149.99/month: 150 calls/minute, real-time data
$499.99/month: 600 calls/minute, premium support

Or switch to Finnhub (free tier: 60 calls/minute!)

🎓 What You're Learning
With this implementation, you'll understand:

✅ REST API consumption
✅ API key authentication
✅ Rate limiting and caching strategies
✅ JSON parsing with JObject
✅ Error handling with external APIs
✅ Configuration management in .NET


🚀 Next Steps After Testing
Once Alpha Vantage works:

✅ Test all endpoints
✅ Add your favorite stocks
✅ Monitor API usage
🎯 Consider adding background service for auto-refresh
🎯 Build a frontend dashboard
🎯 Upgrade to Finnhub for more API calls


📞 Need Help?
If you get stuck:

Check console logs - detailed error messages
Verify API key - make sure it's valid
Test with DEMO key - ensure code works
Check API limits - might need to wait

Share with me:

Console error messages
Which endpoint failed
Your API key status (valid/invalid)

Let's get your API working with real data! 🎉