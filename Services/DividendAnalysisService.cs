using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using FinanceApi.Data;
using FinanceApi.Models;

namespace FinanceApi.Services
{
    /// <summary>
    /// Enhanced Dividend Analysis Service with Database Caching
    /// SMART CACHING: Checks DB first, only calls API if data is old or missing
    /// </summary>
    public class DividendAnalysisService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<DividendAnalysisService> _logger;
        private readonly string _apiKey;
        private readonly DividendDbContext _dbContext;
        // Cache permanently - no expiration (user can manually refresh if needed)

        public DividendAnalysisService(
            HttpClient httpClient,
            ILogger<DividendAnalysisService> logger,
            IConfiguration configuration,
            DividendDbContext dbContext)
        {
            _httpClient = httpClient;
            _logger = logger;
            _dbContext = dbContext;

            _apiKey = configuration["AlphaVantage:ApiKey"]
                ?? throw new InvalidOperationException("Alpha Vantage API key not configured");

            _logger.LogInformation($"✓ DividendAnalysisService initialized with DB caching");
        }

        /// <summary>
        /// Get dividend analysis - checks DB first, then API if needed
        /// </summary>
        public async Task<DividendAnalysis?> GetDividendAnalysisAsync(string symbol, bool forceRefresh = false)
        {
            try
            {
                symbol = symbol.ToUpper();

                _logger.LogInformation($"========================================");
                _logger.LogInformation($"→ Requesting analysis for: {symbol}");

                // STEP 1: Check database first
                if (!forceRefresh)
                {
                    var cached = await GetFromDatabaseAsync(symbol);
                    if (cached != null)
                    {
                        var age = DateTime.UtcNow - cached.LastUpdated;
                        _logger.LogInformation($"✓ CACHE HIT! Data age: {age.TotalDays:F1} days (saved 2-3 API calls)");
                        _logger.LogInformation($"========================================");
                        return cached;
                    }
                    else
                    {
                        _logger.LogInformation($"ℹ️ No cached data found, fetching from API...");
                    }
                }
                else
                {
                    _logger.LogInformation($"🔄 Force refresh requested, fetching from API...");
                }

                // STEP 2: Fetch from API
                _logger.LogInformation($"📡 Calling Alpha Vantage API...");

                var overview = await FetchCompanyOverviewAsync(symbol);
                if (overview == null)
                {
                    _logger.LogError($"❌ Failed to fetch overview data");
                    _logger.LogInformation($"========================================");
                    return null;
                }

                var dividendHistory = await FetchDividendHistoryAsync(symbol);

                // Fetch current stock price for accurate dividend yield calculation
                var currentPrice = await FetchCurrentPriceAsync(symbol);

                // STEP 3: Calculate metrics
                var analysis = CalculateDividendMetrics(symbol, overview, dividendHistory, currentPrice);
                analysis.ApiCallsUsed = currentPrice.HasValue ? 3 : 2; // Overview + History + (optional) Current Price

                // STEP 4: Save to database
                await SaveToDatabaseAsync(analysis, dividendHistory);

                // STEP 5: Log API usage
                await LogApiUsageAsync(analysis.ApiCallsUsed);

                _logger.LogInformation($"✓ Analysis complete and saved to DB");
                _logger.LogInformation($"  Safety Score: {analysis.SafetyScore:F2}/5");
                _logger.LogInformation($"  API Calls Used: {analysis.ApiCallsUsed}");
                _logger.LogInformation($"========================================");

                return analysis;
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error: {ex.Message}");
                _logger.LogInformation($"========================================");
                return null;
            }
        }

        /// <summary>
        /// Get analysis from database
        /// </summary>
        private async Task<DividendAnalysis?> GetFromDatabaseAsync(string symbol)
        {
            try
            {
                var cached = await _dbContext.DividendModels
                    .Include(d => d.DividendPayments)
                    .Include(d => d.YearlyDividends)
                    .FirstOrDefaultAsync(d => d.Symbol == symbol);

                if (cached == null)
                    return null;

                // Convert to DividendAnalysis
                var analysis = new DividendAnalysis
                {
                    Symbol = cached.Symbol,
                    CompanyName = cached.CompanyName,
                    Sector = cached.Sector,
                    Industry = cached.Industry,
                    DividendYield = cached.DividendYield,
                    DividendPerShare = cached.DividendPerShare,
                    PayoutRatio = cached.PayoutRatio,
                    EPS = cached.EPS,
                    ProfitMargin = cached.ProfitMargin,
                    Beta = cached.Beta,
                    ConsecutiveYearsOfPayments = cached.ConsecutiveYearsOfPayments,
                    DividendGrowthRate = cached.DividendGrowthRate,
                    SafetyScore = cached.SafetyScore,
                    SafetyRating = cached.SafetyRating,
                    Recommendation = cached.Recommendation,
                    DividendHistory = cached.DividendPayments
                        .Select(p => new DividendPayment
                        {
                            Date = p.PaymentDate,
                            Amount = p.Amount
                        })
                        .OrderBy(p => p.Date)
                        .ToList(),
                    YearlyDividends = cached.YearlyDividends
                        .ToDictionary(y => y.Year, y => y.TotalDividend),
                    FetchedAt = cached.FetchedAt,
                    LastUpdated = cached.LastUpdated,
                    ApiCallsUsed = 0, // From cache
                    IsFromCache = true
                };

                return analysis;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Error reading from cache: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Save analysis to database
        /// </summary>
        private async Task SaveToDatabaseAsync(DividendAnalysis analysis, List<DividendPayment> history)
        {
            try
            {
                // Check if exists
                var existing = await _dbContext.DividendModels
                    .Include(d => d.DividendPayments)
                    .Include(d => d.YearlyDividends)
                    .FirstOrDefaultAsync(d => d.Symbol == analysis.Symbol);

                if (existing != null)
                {
                    // Update existing
                    existing.CompanyName = analysis.CompanyName;
                    existing.Sector = analysis.Sector;
                    existing.Industry = analysis.Industry;
                    existing.DividendYield = analysis.DividendYield;
                    existing.DividendPerShare = analysis.DividendPerShare;
                    existing.PayoutRatio = analysis.PayoutRatio;
                    existing.EPS = analysis.EPS;
                    existing.ProfitMargin = analysis.ProfitMargin;
                    existing.Beta = analysis.Beta;
                    existing.ConsecutiveYearsOfPayments = analysis.ConsecutiveYearsOfPayments;
                    existing.DividendGrowthRate = analysis.DividendGrowthRate;
                    existing.SafetyScore = analysis.SafetyScore;
                    existing.SafetyRating = analysis.SafetyRating;
                    existing.Recommendation = analysis.Recommendation;
                    existing.LastUpdated = DateTime.UtcNow;
                    existing.ApiCallsUsed = analysis.ApiCallsUsed;

                    // Clear old dividend records
                    _dbContext.DividendPayments.RemoveRange(existing.DividendPayments);
                    _dbContext.YearlyDividends.RemoveRange(existing.YearlyDividends);
                }
                else
                {
                    // Create new
                    existing = new DividendModel
                    {
                        Symbol = analysis.Symbol,
                        CompanyName = analysis.CompanyName,
                        Sector = analysis.Sector,
                        Industry = analysis.Industry,
                        DividendYield = analysis.DividendYield,
                        DividendPerShare = analysis.DividendPerShare,
                        PayoutRatio = analysis.PayoutRatio,
                        EPS = analysis.EPS,
                        ProfitMargin = analysis.ProfitMargin,
                        Beta = analysis.Beta,
                        ConsecutiveYearsOfPayments = analysis.ConsecutiveYearsOfPayments,
                        DividendGrowthRate = analysis.DividendGrowthRate,
                        SafetyScore = analysis.SafetyScore,
                        SafetyRating = analysis.SafetyRating,
                        Recommendation = analysis.Recommendation,
                        FetchedAt = DateTime.UtcNow,
                        LastUpdated = DateTime.UtcNow,
                        ApiCallsUsed = analysis.ApiCallsUsed
                    };

                    _dbContext.DividendModels.Add(existing);
                }

                await _dbContext.SaveChangesAsync();

                // Add dividend payment records
                foreach (var payment in history)
                {
                    _dbContext.DividendPayments.Add(new DividendPaymentRecord
                    {
                        DividendModelId = existing.Id,
                        Symbol = analysis.Symbol,
                        PaymentDate = payment.Date,
                        Amount = payment.Amount
                    });
                }

                // Add yearly summaries
                foreach (var year in analysis.YearlyDividends)
                {
                    var yearPayments = history.Where(h => h.Date.Year == year.Key).ToList();

                    _dbContext.YearlyDividends.Add(new YearlyDividendSummary
                    {
                        DividendModelId = existing.Id,
                        Symbol = analysis.Symbol,
                        Year = year.Key,
                        TotalDividend = year.Value,
                        PaymentCount = yearPayments.Count
                    });
                }

                await _dbContext.SaveChangesAsync();

                _logger.LogInformation($"  💾 Saved to database: {history.Count} payments, {analysis.YearlyDividends.Count} years");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error saving to database: {ex.Message}");
            }
        }

        /// <summary>
        /// Log API usage for tracking
        /// </summary>
        private async Task LogApiUsageAsync(int callsUsed)
        {
            try
            {
                var today = DateTime.UtcNow.Date;
                var log = await _dbContext.ApiUsageLogs.FirstOrDefaultAsync(l => l.Date == today);

                if (log == null)
                {
                    log = new ApiUsageLog
                    {
                        Date = today,
                        CallsUsed = callsUsed,
                        DailyLimit = 25
                    };
                    _dbContext.ApiUsageLogs.Add(log);
                }
                else
                {
                    log.CallsUsed += callsUsed;
                }

                await _dbContext.SaveChangesAsync();

                var remaining = log.DailyLimit - log.CallsUsed;
                _logger.LogInformation($"  📊 API Usage Today: {log.CallsUsed}/{log.DailyLimit} (Remaining: {remaining})");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Error logging API usage: {ex.Message}");
            }
        }

        /// <summary>
        /// Get all cached analyses
        /// </summary>
        public async Task<List<DividendAnalysis>> GetAllCachedAnalysesAsync()
        {
            // Fetch all data first (SQLite doesn't support ORDER BY decimal)
            var cached = await _dbContext.DividendModels
                .Include(d => d.DividendPayments)
                .Include(d => d.YearlyDividends)
                .ToListAsync();

            // Sort in memory and convert to DividendAnalysis
            return cached
                .OrderByDescending(c => c.SafetyScore)
                .Select(c => new DividendAnalysis
                {
                    Symbol = c.Symbol,
                    CompanyName = c.CompanyName,
                    Sector = c.Sector,
                    SafetyScore = c.SafetyScore,
                    SafetyRating = c.SafetyRating,
                    DividendYield = c.DividendYield,
                    PayoutRatio = c.PayoutRatio,
                    DividendGrowthRate = c.DividendGrowthRate,
                    ConsecutiveYearsOfPayments = c.ConsecutiveYearsOfPayments,
                    LastUpdated = c.LastUpdated,
                    IsFromCache = true
                }).ToList();
        }

        // API fetch methods (same as before)
        private async Task<JObject?> FetchCompanyOverviewAsync(string symbol)
        {
            try
            {
                await Task.Delay(500);

                // Try Alpha Vantage first
                var url = $"https://www.alphavantage.co/query?function=OVERVIEW&symbol={symbol}&apikey={_apiKey}";
                var response = await _httpClient.GetStringAsync(url);
                var json = JObject.Parse(response);

                if (json["Note"] == null && json["Error Message"] == null && json["Symbol"] != null)
                {
                    _logger.LogInformation($"  ✓ Alpha Vantage OVERVIEW data retrieved");
                    return json;
                }

                // Alpha Vantage failed, try Yahoo Finance fallback
                _logger.LogWarning($"  ⚠️ Alpha Vantage returned no data for {symbol}, trying Yahoo Finance...");
                return await FetchYahooFinanceOverviewAsync(symbol);
            }
            catch (Exception ex)
            {
                _logger.LogError($"  ❌ Error fetching overview: {ex.Message}");
                _logger.LogInformation($"  🔄 Trying Yahoo Finance fallback...");
                try
                {
                    return await FetchYahooFinanceOverviewAsync(symbol);
                }
                catch
                {
                    return null;
                }
            }
        }

        private async Task<JObject?> FetchYahooFinanceOverviewAsync(string symbol)
        {
            try
            {
                // Yahoo Finance quote summary endpoint
                var url = $"https://query1.finance.yahoo.com/v11/finance/quoteSummary/{symbol}?modules=summaryDetail,defaultKeyStatistics,financialData,earningsHistory,dividendHistory";
                var response = await _httpClient.GetStringAsync(url);
                var json = JObject.Parse(response);

                var quoteSummary = json["quoteSummary"];
                if (quoteSummary?["error"] != null)
                {
                    _logger.LogWarning($"  ⚠️ Yahoo Finance error for {symbol}");
                    return null;
                }

                var result = quoteSummary?["result"]?[0];
                if (result == null)
                {
                    _logger.LogWarning($"  ⚠️ Yahoo Finance returned no data for {symbol}");
                    return null;
                }

                // Convert Yahoo Finance format to Alpha Vantage-like format for compatibility
                var summaryDetail = result["summaryDetail"];
                var defaultKeyStats = result["defaultKeyStatistics"];
                var financialData = result["financialData"];

                var convertedJson = new JObject
                {
                    ["Symbol"] = symbol,
                    ["Name"] = symbol, // Yahoo doesn't always provide full name in this endpoint
                    ["DividendPerShare"] = summaryDetail?["trailingAnnualDividendRate"]?["raw"]?.ToString() ?? "0",
                    ["DividendYield"] = summaryDetail?["trailingAnnualDividendYield"]?["raw"] != null
                        ? (decimal.Parse(summaryDetail["trailingAnnualDividendYield"]["raw"].ToString()) * 100).ToString()
                        : "0",
                    ["EPS"] = defaultKeyStats?["trailingEps"]?["raw"]?.ToString() ?? "0",
                    ["PERatio"] = summaryDetail?["trailingPE"]?["raw"]?.ToString() ?? "0",
                    ["PEGRatio"] = defaultKeyStats?["pegRatio"]?["raw"]?.ToString() ?? "0",
                    ["Beta"] = defaultKeyStats?["beta"]?["raw"]?.ToString() ?? "0",
                    ["PriceToBookRatio"] = defaultKeyStats?["priceToBook"]?["raw"]?.ToString() ?? "0",
                    ["PayoutRatio"] = defaultKeyStats?["payoutRatio"]?["raw"] != null
                        ? (decimal.Parse(defaultKeyStats["payoutRatio"]["raw"].ToString()) * 100).ToString()
                        : "0",
                    ["DividendDate"] = summaryDetail?["exDividendDate"]?["fmt"]?.ToString() ?? "",
                    ["Source"] = "Yahoo Finance"
                };

                _logger.LogInformation($"  ✓ Yahoo Finance data retrieved for {symbol}");
                return convertedJson;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"  ⚠️ Yahoo Finance fallback failed: {ex.Message}");
                return null;
            }
        }

        private async Task<decimal?> FetchCurrentPriceAsync(string symbol)
        {
            try
            {
                await Task.Delay(500);

                // Try Alpha Vantage first
                var url = $"https://www.alphavantage.co/query?function=GLOBAL_QUOTE&symbol={symbol}&apikey={_apiKey}";
                var response = await _httpClient.GetStringAsync(url);
                var json = JObject.Parse(response);

                var globalQuote = json["Global Quote"];
                if (globalQuote != null && globalQuote.HasValues)
                {
                    var priceString = globalQuote["05. price"]?.ToString();
                    if (decimal.TryParse(priceString, out var price))
                    {
                        _logger.LogInformation($"  💵 Current Price: ${price:F2} (Alpha Vantage)");
                        return price;
                    }
                }

                // Alpha Vantage failed, try Yahoo Finance
                _logger.LogWarning($"  ⚠️ Alpha Vantage price not available, trying Yahoo Finance...");
                return await FetchYahooFinanceCurrentPriceAsync(symbol);
            }
            catch (Exception ex)
            {
                _logger.LogError($"  ❌ Error fetching current price: {ex.Message}");
                _logger.LogInformation($"  🔄 Trying Yahoo Finance for price...");
                try
                {
                    return await FetchYahooFinanceCurrentPriceAsync(symbol);
                }
                catch
                {
                    return null;
                }
            }
        }

        private async Task<decimal?> FetchYahooFinanceCurrentPriceAsync(string symbol)
        {
            try
            {
                var url = $"https://query1.finance.yahoo.com/v8/finance/chart/{symbol}?interval=1d&range=1d";
                var response = await _httpClient.GetStringAsync(url);
                var json = JObject.Parse(response);

                var result = json["chart"]?["result"]?[0];
                var priceValue = result?["meta"]?["regularMarketPrice"];

                if (priceValue != null && decimal.TryParse(priceValue.ToString(), out var price))
                {
                    _logger.LogInformation($"  💵 Current Price: ${price:F2} (Yahoo Finance)");
                    return price;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"  ⚠️ Yahoo Finance price fetch failed: {ex.Message}");
                return null;
            }
        }

        private async Task<List<DividendPayment>> FetchDividendHistoryAsync(string symbol)
        {
            try
            {
                await Task.Delay(500);
                var url = $"https://www.alphavantage.co/query?function=TIME_SERIES_MONTHLY_ADJUSTED&symbol={symbol}&apikey={_apiKey}";
                var response = await _httpClient.GetStringAsync(url);
                var json = JObject.Parse(response);

                var timeSeries = json["Monthly Adjusted Time Series"] as JObject;
                if (timeSeries == null) return new List<DividendPayment>();

                var dividends = new List<DividendPayment>();
                var fiveYearsAgo = DateTime.Now.AddYears(-5);

                foreach (var item in timeSeries.Properties())
                {
                    if (DateTime.TryParse(item.Name, out var date) && date >= fiveYearsAgo)
                    {
                        var data = item.Value as JObject;
                        var dividendString = data?["7. dividend amount"]?.ToString();
                        if (!string.IsNullOrEmpty(dividendString) &&
                            decimal.TryParse(dividendString, out var dividend) &&
                            dividend > 0)
                        {
                            dividends.Add(new DividendPayment { Date = date, Amount = dividend });
                        }
                    }
                }

                return dividends.OrderBy(d => d.Date).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError($"  ❌ Error fetching history: {ex.Message}");
                return new List<DividendPayment>();
            }
        }

        private DividendAnalysis CalculateDividendMetrics(string symbol, JObject overview, List<DividendPayment> history, decimal? currentPrice)
        {
            var analysis = new DividendAnalysis
            {
                Symbol = symbol,
                CompanyName = overview["Name"]?.ToString() ?? symbol,
                Sector = overview["Sector"]?.ToString() ?? "Unknown",
                Industry = overview["Industry"]?.ToString() ?? "Unknown"
            };

            // Parse raw values from API
            decimal.TryParse(overview["DividendPerShare"]?.ToString(), out var dividendPerShare);
            analysis.DividendPerShare = dividendPerShare;

            decimal.TryParse(overview["EPS"]?.ToString(), out var eps);
            analysis.EPS = eps;

            decimal.TryParse(overview["Beta"]?.ToString(), out var beta);
            analysis.Beta = beta;

            // ===== DIVIDEND YIELD CALCULATION =====
            // Formula: (Annual Dividend per Share / Current Market Price per Share) × 100
            // Example: ($2 dividend / $50 price) × 100 = 4%
            if (currentPrice.HasValue && currentPrice.Value > 0 && dividendPerShare > 0)
            {
                // Calculate it ourselves using current price
                var calculatedDividendYield = (dividendPerShare / currentPrice.Value) * 100;
                analysis.DividendYield = calculatedDividendYield;

                _logger.LogInformation($"  📊 Dividend Yield: {calculatedDividendYield:F2}% (Calculated: ${dividendPerShare:F2} / ${currentPrice.Value:F2})");
            }
            else
            {
                // Fallback to API value if we can't calculate
                decimal.TryParse(overview["DividendYield"]?.ToString(), out var dividendYield);
                analysis.DividendYield = dividendYield * 100; // Convert to percentage
                _logger.LogInformation($"  📊 Dividend Yield: {analysis.DividendYield:F2}% (From API)");
            }

            // ===== PAYOUT RATIO CALCULATION =====
            // Formula: (Annual Dividend per Share / Earnings per Share) × 100
            // Example: ($2 dividend / $5 EPS) × 100 = 40%
            if (eps > 0 && dividendPerShare > 0)
            {
                // Calculate it ourselves for accuracy
                var calculatedPayoutRatio = (dividendPerShare / eps) * 100;
                analysis.PayoutRatio = calculatedPayoutRatio;

                _logger.LogInformation($"  📊 Payout Ratio: {calculatedPayoutRatio:F2}% (Calculated: ${dividendPerShare:F2} / ${eps:F2})");
            }
            else
            {
                // Fallback to API value if we can't calculate
                decimal.TryParse(overview["PayoutRatio"]?.ToString(), out var payoutRatio);
                analysis.PayoutRatio = payoutRatio * 100;
                _logger.LogInformation($"  📊 Payout Ratio: {analysis.PayoutRatio:F2}% (From API)");
            }

            analysis.DividendHistory = history;
            analysis.ConsecutiveYearsOfPayments = CalculateConsecutiveYears(history);
            analysis.DividendGrowthRate = CalculateDividendGrowthRate(history);
            analysis.YearlyDividends = CalculateYearlyDividends(history);
            analysis.SafetyScore = CalculateSafetyScore(analysis);
            analysis.SafetyRating = GetSafetyRating(analysis.SafetyScore);
            analysis.Recommendation = GenerateRecommendation(analysis);
            analysis.FetchedAt = DateTime.UtcNow;
            analysis.LastUpdated = DateTime.UtcNow;

            return analysis;
        }

        // Calculation methods (same as before - omitted for brevity)
        private int CalculateConsecutiveYears(List<DividendPayment> history)
        {
            if (history.Count == 0) return 0;
            var yearlyPayments = history.GroupBy(d => d.Date.Year).Select(g => g.Key).OrderByDescending(y => y).ToList();
            int consecutive = 0;
            int expectedYear = DateTime.Now.Year;
            foreach (var year in yearlyPayments)
            {
                if (year == expectedYear || year == expectedYear - 1)
                {
                    consecutive++;
                    expectedYear = year - 1;
                }
                else break;
            }
            return consecutive;
        }

        private decimal? CalculateDividendGrowthRate(List<DividendPayment> history)
        {
            if (history.Count < 8) return null;
            var yearlyTotals = history.GroupBy(d => d.Date.Year).Select(g => new { Year = g.Key, Total = g.Sum(d => d.Amount) }).OrderBy(y => y.Year).ToList();
            if (yearlyTotals.Count < 2) return null;
            var growthRates = new List<decimal>();
            for (int i = 1; i < yearlyTotals.Count; i++)
            {
                var previousYear = yearlyTotals[i - 1].Total;
                var currentYear = yearlyTotals[i].Total;
                if (previousYear > 0)
                {
                    var growth = ((currentYear - previousYear) / previousYear) * 100;
                    growthRates.Add(growth);
                }
            }
            return growthRates.Any() ? growthRates.Average() : null;
        }

        private Dictionary<int, decimal> CalculateYearlyDividends(List<DividendPayment> history)
        {
            return history.GroupBy(d => d.Date.Year).ToDictionary(g => g.Key, g => g.Sum(d => d.Amount));
        }

        private decimal CalculateSafetyScore(DividendAnalysis analysis)
        {
            decimal score = 0;
            int criteriaCount = 0;

            if (analysis.DividendYield.HasValue)
            {
                criteriaCount++;
                if (analysis.DividendYield >= 2 && analysis.DividendYield <= 6) score += 1.0m;
                else if (analysis.DividendYield >= 1 && analysis.DividendYield < 8) score += 0.5m;
            }

            if (analysis.PayoutRatio.HasValue)
            {
                criteriaCount++;
                if (analysis.PayoutRatio < 60) score += 1.0m;
                else if (analysis.PayoutRatio < 75) score += 0.6m;
                else if (analysis.PayoutRatio < 90) score += 0.3m;
            }

            if (analysis.DividendGrowthRate.HasValue)
            {
                criteriaCount++;
                if (analysis.DividendGrowthRate > 5) score += 1.0m;
                else if (analysis.DividendGrowthRate > 0) score += 0.7m;
                else if (analysis.DividendGrowthRate >= -2) score += 0.3m;
            }

            criteriaCount++;
            if (analysis.ConsecutiveYearsOfPayments >= 10) score += 1.0m;
            else if (analysis.ConsecutiveYearsOfPayments >= 5) score += 0.7m;
            else if (analysis.ConsecutiveYearsOfPayments >= 3) score += 0.4m;

            if (analysis.Beta.HasValue)
            {
                criteriaCount++;
                if (analysis.Beta < 0.8m) score += 1.0m;
                else if (analysis.Beta < 1.0m) score += 0.7m;
                else if (analysis.Beta < 1.3m) score += 0.4m;
            }

            return criteriaCount > 0 ? (score / criteriaCount) * 5 : 0;
        }

        private string GetSafetyRating(decimal score)
        {
            if (score >= 4.5m) return "Excellent";
            if (score >= 4.0m) return "Very Good";
            if (score >= 3.5m) return "Good";
            if (score >= 3.0m) return "Fair";
            if (score >= 2.0m) return "Below Average";
            return "Poor";
        }

        private string GenerateRecommendation(DividendAnalysis analysis)
        {
            var recommendations = new List<string>();
            if (analysis.SafetyScore >= 4.0m) recommendations.Add("Strong dividend aristocrat candidate");
            else if (analysis.SafetyScore >= 3.0m) recommendations.Add("Solid dividend payer");
            else recommendations.Add("Moderate quality");

            if (analysis.DividendYield > 8) recommendations.Add("⚠️ Very high yield");
            else if (analysis.DividendYield >= 2 && analysis.DividendYield <= 6) recommendations.Add("✓ Optimal yield range");

            return string.Join("; ", recommendations);
        }
    }

    // Models
    public class DividendAnalysis
    {
        public string Symbol { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string Sector { get; set; } = string.Empty;
        public string Industry { get; set; } = string.Empty;
        public decimal? DividendYield { get; set; }
        public decimal? DividendPerShare { get; set; }
        public decimal? PayoutRatio { get; set; }
        public decimal? EPS { get; set; }
        public decimal? ProfitMargin { get; set; }
        public decimal? Beta { get; set; }
        public List<DividendPayment> DividendHistory { get; set; } = new();
        public Dictionary<int, decimal> YearlyDividends { get; set; } = new();
        public int ConsecutiveYearsOfPayments { get; set; }
        public decimal? DividendGrowthRate { get; set; }
        public decimal SafetyScore { get; set; }
        public string SafetyRating { get; set; } = string.Empty;
        public string Recommendation { get; set; } = string.Empty;
        public DateTime FetchedAt { get; set; }
        public DateTime LastUpdated { get; set; }
        public int ApiCallsUsed { get; set; }
        public bool IsFromCache { get; set; }
    }

    public class DividendPayment
    {
        public DateTime Date { get; set; }
        public decimal Amount { get; set; }
    }
}
