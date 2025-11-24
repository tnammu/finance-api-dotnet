# Payout Ratio Fix: Accurate Historical Calculations

## Problem Statement

Previously, the payout ratio history was calculated using **current EPS** for all historical years, leading to:

- **Incorrect payout ratios** like 214%, 500%, or even 2934%
- **Misleading analysis** where past years showed inflated ratios
- **Loss of valuable historical context** for dividend sustainability

### Example of the Problem:
```
Stock AAPL:
- 2024: Dividend $1.04 / EPS $7.46 = 13.9% ✓ (correct)
- 2023: Dividend $0.96 / EPS $7.46 = 12.9% ✗ (WRONG! Used 2024 EPS)
- 2022: Dividend $0.90 / EPS $7.46 = 12.1% ✗ (WRONG! Used 2024 EPS)

The correct calculation for 2023 should use 2023's actual EPS, not 2024's!
```

## Solution Overview

✅ **Collect annual EPS data** from Yahoo Finance for each year
✅ **Store EPS per year** in the database
✅ **Calculate payout ratios** using the correct EPS for each year
✅ **Add safeguards** to prevent garbage data from tiny EPS values
✅ **Cap extreme ratios** at 200% with explanatory notes

---

## What Was Changed

### 1. **Database Schema** (`YearlyDividends` Table)
Added `AnnualEPS` column to store EPS for each year:

```sql
ALTER TABLE YearlyDividends ADD COLUMN AnnualEPS REAL;
```

### 2. **Python Script** (`update_stocks_from_yahoo.py`)
- Fetches annual income statements from Yahoo Finance
- Calculates EPS per year from Net Income / Shares Outstanding
- Applies minimum threshold (EPS ≥ $0.10) to avoid unrealistic ratios
- Stores yearly dividends AND yearly EPS together

### 3. **C# Service** (`DividendAnalysisService.cs`)
- Updated payout ratio calculation to use **year-specific EPS**
- Added MIN_EPS_THRESHOLD ($0.10) to skip unreliable data
- Added MAX_PAYOUT_RATIO (200%) cap with explanatory notes
- Provides context when EPS is unavailable

---

## Migration Instructions

### Step 1: Stop the Backend Server
The database must be unlocked before migration.

### Step 2: Run the Migration
```bash
cd scripts
python migrate_add_annual_eps.py
```

You should see:
```
====================================
DATABASE MIGRATION: Add AnnualEPS Column
====================================

Current columns in YearlyDividends:
  - Id (INTEGER)
  - DividendModelId (INTEGER)
  - Symbol (TEXT)
  - Year (INTEGER)
  - TotalDividend (TEXT)
  - PaymentCount (INTEGER)

+ Adding AnnualEPS column...
✓ AnnualEPS column added successfully!

====================================
✓ Migration completed successfully!
====================================
```

### Step 3: Restart the Backend Server
```bash
dotnet run
```

### Step 4: Update Stock Data with Historical EPS
For existing stocks, refresh them to collect historical EPS:

```bash
cd scripts
python update_stocks_from_yahoo.py AAPL
python update_stocks_from_yahoo.py MSFT
python update_stocks_from_yahoo.py TD.TO
```

New stocks added via the Dividend Analysis page will automatically collect historical EPS.

---

## How It Works Now

### Before (Incorrect):
```
2023 Payout Ratio = (2023 Dividend / 2024 EPS) * 100 = 210% ✗ WRONG!
```

### After (Correct):
```
2023 Payout Ratio = (2023 Dividend / 2023 EPS) * 100 = 45% ✓ CORRECT!
```

### Safeguards:
1. **Minimum EPS Threshold**: If EPS < $0.10, skip calculation (prevents 500%, 1000% ratios)
2. **Maximum Payout Cap**: If ratio > 200%, cap it and add a note
3. **Graceful Handling**: If EPS unavailable, show "N/A" with explanation

---

## Example Output

### Chart Data (Payout Ratio Trend):
```json
{
  "payoutRatioTrend": [
    {
      "year": 2020,
      "payoutRatio": 22.15,
      "dividendPerShare": 0.82,
      "eps": 3.70,
      "note": null
    },
    {
      "year": 2021,
      "payoutRatio": 14.97,
      "dividendPerShare": 0.87,
      "eps": 5.81,
      "note": null
    },
    {
      "year": 2022,
      "payoutRatio": 14.77,
      "dividendPerShare": 0.92,
      "eps": 6.23,
      "note": null
    },
    {
      "year": 2023,
      "payoutRatio": 14.36,
      "dividendPerShare": 0.96,
      "eps": 6.69,
      "note": null
    },
    {
      "year": 2024,
      "payoutRatio": 13.94,
      "dividendPerShare": 1.04,
      "eps": 7.46,
      "note": null
    }
  ]
}
```

---

## Benefits

✅ **Accurate Historical Analysis**: See how payout ratios evolved over time
✅ **Better Investment Decisions**: Identify trends in dividend sustainability
✅ **Realistic Numbers**: No more 500% payout ratios from data artifacts
✅ **ETF-Friendly**: Gracefully handles ETFs that don't have EPS
✅ **Transparent**: Shows notes when data is capped or unavailable

---

## Testing

### Test with a real stock:
```bash
# Update AAPL with historical EPS
python scripts/update_stocks_from_yahoo.py AAPL

# Then view in the dividend analysis page
# Check the "Payout Ratio Trend" chart
```

### Expected Results:
- Payout ratios should be realistic (10-60% for most companies)
- REITs may legitimately show 80-100%+ (that's normal)
- ETFs will show "N/A (ETF)" for payout ratio
- Extreme cases are capped at 200% with explanatory notes

---

## Technical Details

### Data Sources:
- **Yahoo Finance Income Statement**: Provides annual Net Income
- **Yahoo Finance Info**: Provides shares outstanding
- **Calculation**: EPS = Net Income / Shares Outstanding

### Threshold Logic:
```python
# Only store EPS if it's reasonable
if calculated_eps >= 0.10:  # $0.10 minimum
    annual_eps_data[year] = round(calculated_eps, 2)
```

### C# Safeguards:
```csharp
const decimal MIN_EPS_THRESHOLD = 0.10m;
const decimal MAX_PAYOUT_RATIO = 200m;

if (yearEps >= MIN_EPS_THRESHOLD) {
    var ratio = (dividend / eps) * 100;
    if (ratio > MAX_PAYOUT_RATIO) {
        note = $"Capped from {ratio}%";
        ratio = MAX_PAYOUT_RATIO;
    }
}
```

---

## Troubleshooting

### "Database is locked"
- Stop the backend server before running migration
- Close any database browser tools

### "AnnualEPS column already exists"
- Migration already completed, you're good to go!

### Still seeing high payout ratios (>200%)?
- This might be legitimate (REITs, special dividends, loss years)
- Check the `note` field for explanations
- Verify the stock is using fresh data (refresh it)

---

## Files Modified

1. **Model/DividendModel.cs**: Added `AnnualEPS` property to `YearlyDividendSummary`
2. **scripts/update_stocks_from_yahoo.py**:
   - Added historical EPS collection
   - Added `save_yearly_data()` method
   - Added yearly EPS storage
3. **Services/DividendAnalysisService.cs**: Updated payout ratio calculation with safeguards
4. **scripts/migrate_add_annual_eps.py**: Database migration script

---

## Summary

This fix ensures that **payout ratios are calculated correctly** using the appropriate EPS for each year, providing **accurate historical analysis** while protecting against **unrealistic values** from data artifacts.

Your dividend analysis is now much more reliable! 🎉
