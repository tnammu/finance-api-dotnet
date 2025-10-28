🇨🇦 Canadian Dividend Analysis API - Complete Guide
🎯 What This Does
Based on your business requirements, this API:
✅ Fetches 5-year dividend history for any stock/ETF
✅ Calculates dividend metrics (growth rate, payout ratio, etc.)
✅ Scores stocks 1-5 based on your quality checklist
✅ Screens Canadian stocks (.TO suffix)
✅ Ranks by safety for your dividend portfolio
✅ Follows your strategy (Dividend Fortress approach)

🚀 Quick Setup (3 Minutes)
Step 1: Add These Files to Your Project
Services folder:

DividendAnalysisService.cs

Controllers folder:

DividendsController.cs

Root:

Program.cs (replace existing)

Step 2: Keep Your API Key
Your appsettings.json stays the same:
json{
  "AlphaVantage": {
    "ApiKey": "YOUR_API_KEY_HERE"
  }
}
Step 3: Run
bashdotnet run
Step 4: Open Swagger
http://localhost:5199 (or your port)

📊 Available Endpoints
1. Analyze Single Stock (Detailed)
Endpoint:
GET /api/dividends/analyze/ENB.TO
What you get:
json{
  "symbol": "ENB.TO",
  "companyName": "Enbridge Inc.",
  "sector": "Energy",
  "industry": "Oil & Gas Midstream",
  
  "currentMetrics": {
    "dividendYield": 6.8,
    "dividendPerShare": 3.44,
    "payoutRatio": 72.5,
    "eps": 4.75,
    "profitMargin": 8.5,
    "beta": 0.75
  },
  
  "historicalAnalysis": {
    "consecutiveYearsOfPayments": 28,
    "dividendGrowthRate": 3.2,
    "yearlyDividends": {
      "2020": 3.24,
      "2021": 3.31,
      "2022": 3.38,
      "2023": 3.44,
      "2024": 3.55
    },
    "totalPaymentsLast5Years": 60
  },
  
  "safetyAnalysis": {
    "score": 4.2,
    "rating": "Very Good",
    "recommendation": "Strong dividend aristocrat candidate; ✓ Yield in optimal range; ✓ Sustainable payout ratio; ✓ Strong dividend growth"
  },
  
  "dividendHistory": [
    { "date": "2020-01-15", "amount": 0.81 },
    { "date": "2020-04-15", "amount": 0.81 },
    // ... all payments
  ]
}

2. Screen Canadian Stocks
Endpoint:
POST /api/dividends/screen/canadian
Request Body:
json[
  "TD", "RY", "ENB", "FTS", "BCE", 
  "TRP", "BNS", "BMO", "CM", "T"
]
(Symbols without .TO will automatically add it)
Response:
json{
  "totalScreened": 10,
  "successCount": 9,
  "failCount": 1,
  "topDividendStocks": [
    {
      "symbol": "FTS.TO",
      "companyName": "Fortis Inc.",
      "sector": "Utilities",
      "dividendYield": 4.2,
      "payoutRatio": 72.0,
      "safetyScore": 4.8,
      "safetyRating": "Excellent",
      "consecutiveYears": 50,
      "dividendGrowthRate": 6.0,
      "recommendation": "Strong dividend aristocrat..."
    },
    {
      "symbol": "ENB.TO",
      "companyName": "Enbridge Inc.",
      "safetyScore": 4.2,
      // ...
    }
    // Ranked by safety score
  ]
}

3. Get Canadian Recommendations
Endpoint:
GET /api/dividends/canadian/recommendations
What it does:

Analyzes 20+ pre-selected Canadian dividend stocks
Groups by sector
Shows top picks (score >= 4.0)

Pre-selected stocks include:

Banks: TD, RY, BNS, BMO, CM
Utilities: FTS, EMA, CU
Energy: ENB, TRP, PPL
Telecom: BCE, T, RCI.B
REITs: CAR.UN, REI.UN
ETFs: XDV, CDZ, VDY

Response:
json{
  "totalAnalyzed": 20,
  "topPicks": [
    {
      "symbol": "FTS.TO",
      "companyName": "Fortis Inc.",
      "dividendYield": 4.2,
      "safetyScore": 4.8,
      "payoutRatio": 72.0,
      "consecutiveYears": 50
    }
    // Only stocks with score >= 4.0
  ],
  "bySector": [
    {
      "sector": "Utilities",
      "stocks": [
        // All utility stocks, ranked by score
      ]
    },
    {
      "sector": "Financials",
      "stocks": [
        // All bank stocks, ranked by score
      ]
    }
  ]
}

4. Compare Multiple Stocks
Endpoint:
GET /api/dividends/compare?symbols=TD.TO,RY.TO,ENB.TO,FTS.TO
Response:
json{
  "symbols": ["TD.TO", "RY.TO", "ENB.TO", "FTS.TO"],
  "comparison": [
    {
      "symbol": "FTS.TO",
      "name": "Fortis Inc.",
      "yield": 4.2,
      "payoutRatio": 72.0,
      "safetyScore": 4.8,
      "rating": "Excellent",
      "growth5yr": 6.0,
      "consecutiveYears": 50
    },
    // Sorted by safety score
  ]
}

5. Get Portfolio Allocation Strategy
Endpoint:
GET /api/dividends/portfolio/suggested
Response:
json{
  "strategy": "Dividend Fortress Portfolio",
  "categories": [
    {
      "category": "Dividend Aristocrats/Kings",
      "allocation": "40%",
      "description": "Companies with 10+ years of dividend growth",
      "examples": ["FTS.TO", "ENB.TO", "TD.TO"],
      "targetYield": "3-5%"
    },
    {
      "category": "Defensive Utilities & Telecom",
      "allocation": "20%",
      "examples": ["BCE.TO", "T.TO", "EMA.TO"],
      "targetYield": "4-6%"
    }
    // ... all 6 categories from your strategy
  ],
  "qualityChecklist": {
    "dividendYield": "2-6% (optimal range)",
    "payoutRatio": "<60% (sustainable)",
    "dividendGrowth": "Positive trend over 5-10 years",
    "consecutiveYears": "5+ years (10+ for aristocrats)",
    "beta": "<1.0 (less volatile)",
    "safetyScore": "4.0+ (out of 5)"
  },
  "defensiveStrategy": [
    "Focus on utilities, consumer staples, healthcare",
    "Choose low beta stocks (< 1.0)",
    // ... all your defensive strategies
  ]
}

📈 Safety Score Calculation
Based on your business requirements checklist:
Scoring Criteria (Out of 5):
MetricExcellent (1.0)Good (0.7)Fair (0.4)Poor (0)Dividend Yield2-6%1-2% or 6-8%0.5-1% or 8-10%<0.5% or >10%Payout Ratio<60%60-75%75-90%>90%Dividend Growth>5%/yr0-5%/yr-2% to 0%<-2%/yrConsecutive Years10+ years5-10 years3-5 years<3 yearsBeta (Volatility)<0.80.8-1.01.0-1.3>1.3
Final Score: Average of all criteria × 5
Ratings:

4.5-5.0: Excellent ⭐⭐⭐⭐⭐
4.0-4.5: Very Good ⭐⭐⭐⭐
3.5-4.0: Good ⭐⭐⭐
3.0-3.5: Fair ⭐⭐
2.0-3.0: Below Average ⭐
<2.0: Poor


🎯 Example Workflow
Workflow 1: Find Best Canadian Dividend Stocks
Step 1: Get recommendations
GET /api/dividends/canadian/recommendations

Step 2: Look at "topPicks" array
→ Shows stocks with score >= 4.0

Step 3: Analyze top pick in detail
GET /api/dividends/analyze/FTS.TO

Step 4: Check 5-year dividend history
→ See "dividendHistory" and "yearlyDividends"

Workflow 2: Build Your Portfolio
Step 1: Get allocation strategy
GET /api/dividends/portfolio/suggested
→ See 40% aristocrats, 20% utilities, etc.

Step 2: Screen stocks for each category

Aristocrats:
POST /api/dividends/screen/canadian
Body: ["FTS", "ENB", "TD"]

Utilities:
POST /api/dividends/screen/canadian
Body: ["BCE", "T", "EMA"]

Step 3: Compare top picks
GET /api/dividends/compare?symbols=FTS.TO,ENB.TO,TD.TO,BCE.TO

Step 4: Analyze each in detail
GET /api/dividends/analyze/{symbol}

Workflow 3: Verify Existing Holdings
You own: TD.TO, ENB.TO, BCE.TO

Step 1: Compare them
GET /api/dividends/compare?symbols=TD.TO,ENB.TO,BCE.TO

Step 2: Check each safety score
→ Are they all >= 4.0?

Step 3: Review 5-year dividend growth
GET /api/dividends/analyze/TD.TO
→ Check "dividendGrowthRate"

Step 4: Verify payout ratio is sustainable
→ Check "payoutRatio" < 60%

💡 Dividend Metrics Explained
1. Dividend Yield
Formula: (Annual Dividend / Stock Price) × 100
Example: $3.44 / $50 = 6.88%

Your target: 2-6% (optimal)
⚠️ >8% = potential yield trap
2. Payout Ratio
Formula: (Dividend / Earnings) × 100
Example: $3.44 / $4.75 = 72.4%

Your target: <60% (sustainable)
⚠️ >90% = dividend at risk
3. Dividend Growth Rate
Formula: Average year-over-year growth
Example: 
  2020: $3.24
  2024: $3.55
  Growth: 2.32%/year average

Your target: Positive trend
⚠️ Negative = red flag
4. Consecutive Years
How many years in a row of dividend payments?
Example: Fortis = 50 years

Your target: 
  - 10+ years = Aristocrat
  - 5+ years = Solid
  - <3 years = Risky
5. Beta (Volatility)
Measures stock volatility vs market
Example: 0.75 = 25% less volatile than market

Your target: <1.0 (less volatile)
⚠️ >1.3 = too volatile

🏦 Top Canadian Dividend Stocks by Category
Dividend Aristocrats (10+ years growth):
FTS.TO  - Fortis (50+ years) ⭐⭐⭐⭐⭐
ENB.TO  - Enbridge (28+ years) ⭐⭐⭐⭐
TD.TO   - TD Bank (20+ years) ⭐⭐⭐⭐
Defensive Utilities:
FTS.TO  - Fortis
EMA.TO  - Emera
CU.TO   - Canadian Utilities
Banks (Big 5):
TD.TO   - TD Bank
RY.TO   - Royal Bank
BNS.TO  - Scotiabank
BMO.TO  - Bank of Montreal
CM.TO   - CIBC
Energy Infrastructure:
ENB.TO  - Enbridge
TRP.TO  - TC Energy
PPL.TO  - Pembina Pipeline
Telecom:
BCE.TO     - BCE Inc
T.TO       - Telus
RCI.B.TO   - Rogers
ETFs:
XDV.TO  - iShares Canadian Dividend
CDZ.TO  - Dividend Aristocrats
VDY.TO  - Vanguard High Dividend Yield

⚠️ Important Notes
API Call Limits (Alpha Vantage Free Tier):
Each analysis uses ~2-3 API calls:

1 call: Company overview
1 call: 5-year dividend history
1 call: Current price data

Daily limit: 25 calls

Can analyze ~8-10 stocks per day
Use compare endpoint to be efficient
Recommendations endpoint analyzes 20+ stocks (takes time!)

Rate limiting:

Built-in 1-second delay between stocks
Respects 5 calls/minute limit


🎓 Based on Your Business Strategy
This API implements YOUR exact requirements:
✅ 2-6% yield target → Scored in safety calculation
✅ <60% payout ratio → Scored in safety calculation
✅ Dividend growth → 5-year history calculated
✅ Free cash flow → Profit margin used as proxy
✅ Low beta <1.0 → Included in safety score
✅ EPS growth → Year-over-year growth included
✅ Defensive sectors → Portfolio allocation follows your %
✅ Diversification → 6 categories as you specified
✅ DRIP strategy → Mentioned in recommendations

🚀 Real-World Example
You want to invest $10,000:
Step 1: Get allocation
GET /api/dividends/portfolio/suggested

Step 2: Allocate:
  $4,000 (40%) → Aristocrats (FTS, ENB, TD)
  $2,000 (20%) → Utilities (BCE, T, EMA)
  $1,500 (15%) → Banks (RY, BNS)
  $1,000 (10%) → Energy (TRP, PPL)
  $1,000 (10%) → Growth (ETFs)
  $500  (5%)  → REITs (CAR.UN)

Step 3: Screen each category
POST /api/dividends/screen/canadian

Step 4: Pick top scorer from each

Step 5: Verify safety
GET /api/dividends/compare?symbols=...
→ All should be score >= 4.0

📝 Summary
You now have:
✅ 5-year dividend history for any Canadian stock
✅ Safety scoring (1-5) based on YOUR criteria
✅ Screening & ranking for Canadian dividend stocks
✅ Portfolio allocation following YOUR strategy
✅ Automated analysis of 20+ recommended stocks
✅ Comparison tools to rank your picks
Test it now in Swagger! 🚀
Try:
GET /api/dividends/analyze/ENB.TO
GET /api/dividends/canadian/recommendations
GET /api/dividends/portfolio/suggested

🎯 Next Steps

Test with your favorite Canadian stocks
Use screening to find hidden gems
Build your diversified portfolio
Review safety scores regularly
Track dividend growth over time

Happy dividend investing! 💰