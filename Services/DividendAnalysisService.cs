using System;
using System.Diagnostics;
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
        public async Task<DividendAnalysis?> GetDividendAnalysisAsync(string symbol, bool forceRefresh = false, bool preferYahoo = false)
        {
            try
            {
                symbol = symbol.ToUpper();

                _logger.LogInformation($"========================================");
                _logger.LogInformation($"→ Requesting analysis for: {symbol}");

                // STEP 1: Check database first (30 minute cache)
                const int CACHE_EXPIRATION_MINUTES = 30;

                if (!forceRefresh)
                {
                    var cached = await GetFromDatabaseAsync(symbol);
                    if (cached != null)
                    {
                        var age = DateTime.UtcNow - cached.LastUpdated;

                        // Check if cache is still valid (less than 30 minutes old)
                        if (age.TotalMinutes < CACHE_EXPIRATION_MINUTES)
                        {
                            _logger.LogInformation($"✓ CACHE HIT! Data age: {age.TotalMinutes:F1} minutes (saved 2-3 API calls)");
                            _logger.LogInformation($"========================================");
                            return cached;
                        }
                        else
                        {
                            _logger.LogInformation($"⏰ Cache expired (age: {age.TotalMinutes:F1} minutes > {CACHE_EXPIRATION_MINUTES} min limit), refreshing...");
                        }
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

                // STEP 2: Fetch from API (prefer Yahoo if specified, otherwise try Alpha Vantage with Yahoo fallback)
                JObject? overview;
                decimal? currentPrice;

                if (preferYahoo)
                {
                    _logger.LogInformation($"📡 Calling Yahoo Finance API (preferred)...");
                    overview = await FetchYahooFinanceOverviewAsync(symbol);
                    currentPrice = await FetchYahooFinanceCurrentPriceAsync(symbol);
                }
                else
                {
                    _logger.LogInformation($"📡 Calling Alpha Vantage API...");
                    overview = await FetchCompanyOverviewAsync(symbol);
                    currentPrice = await FetchCurrentPriceAsync(symbol);
                }

                if (overview == null)
                {
                    _logger.LogError($"❌ Failed to fetch overview data");
                    _logger.LogInformation($"========================================");
                    return null;
                }

                var dividendHistory = await FetchDividendHistoryAsync(symbol);

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
                    CurrentPrice = cached.CurrentPrice,
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
                    PayoutPolicy = cached.PayoutPolicy,
                    DividendAllocationPct = cached.DividendAllocationPct,
                    ReinvestmentAllocationPct = cached.ReinvestmentAllocationPct,
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
                    existing.CurrentPrice = analysis.CurrentPrice;
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
                    existing.PayoutPolicy = analysis.PayoutPolicy;
                    existing.DividendAllocationPct = analysis.DividendAllocationPct;
                    existing.ReinvestmentAllocationPct = analysis.ReinvestmentAllocationPct;

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
                        CurrentPrice = analysis.CurrentPrice,
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
                        ApiCallsUsed = analysis.ApiCallsUsed,
                        PayoutPolicy = analysis.PayoutPolicy,
                        DividendAllocationPct = analysis.DividendAllocationPct,
                        ReinvestmentAllocationPct = analysis.ReinvestmentAllocationPct
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

            // Calculate sector ranks
            await CalculateSectorRanksAsync();

            // Reload to get updated ranks
            cached = await _dbContext.DividendModels
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
                    CurrentPrice = c.CurrentPrice,
                    DividendYield = c.DividendYield,
                    PayoutRatio = c.PayoutRatio,
                    DividendGrowthRate = c.DividendGrowthRate,
                    ConsecutiveYearsOfPayments = c.ConsecutiveYearsOfPayments,
                    LastUpdated = c.LastUpdated,
                    PayoutPolicy = c.PayoutPolicy,
                    GrowthScore = c.GrowthScore,
                    DailyVolatility = c.DailyVolatility,
                    SectorRank = c.SectorRank,
                    Week52High = c.Week52High,
                    Week52Low = c.Week52Low,
                    Month1Low = c.Month1Low,
                    Month3Low = c.Month3Low,
                    SupportLevel1 = c.SupportLevel1,
                    SupportLevel1Volume = c.SupportLevel1Volume,
                    SupportLevel2 = c.SupportLevel2,
                    SupportLevel2Volume = c.SupportLevel2Volume,
                    SupportLevel3 = c.SupportLevel3,
                    SupportLevel3Volume = c.SupportLevel3Volume,
                    IsFromCache = true
                }).ToList();
        }

        /// <summary>
        /// Calculate and update sector ranks for all stocks
        /// Rank is based on SafetyScore within each sector (1 = best in sector)
        /// </summary>
        public async Task CalculateSectorRanksAsync()
        {
            try
            {
                var allStocks = await _dbContext.DividendModels.ToListAsync();

                // Group by sector
                var stocksBySector = allStocks
                    .Where(s => !string.IsNullOrEmpty(s.Sector))
                    .GroupBy(s => s.Sector);

                foreach (var sectorGroup in stocksBySector)
                {
                    // Order by SafetyScore descending within each sector
                    var rankedStocks = sectorGroup
                        .OrderByDescending(s => s.SafetyScore)
                        .ToList();

                    // Assign ranks (1-based)
                    for (int i = 0; i < rankedStocks.Count; i++)
                    {
                        rankedStocks[i].SectorRank = i + 1;
                    }
                }

                await _dbContext.SaveChangesAsync();
                _logger.LogInformation($"✓ Updated sector ranks for {allStocks.Count} stocks");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error calculating sector ranks: {ex.Message}");
            }
        }

        public async Task<object?> GetHistoricalChartDataAsync(string symbol)
        {
            try
            {
                _logger.LogInformation($"📊 Fetching historical chart data for {symbol}");

                // Get cached analysis from database
                var cachedAnalysis = await _dbContext.DividendModels
                    .Include(d => d.DividendPayments)
                    .Include(d => d.YearlyDividends)
                    .FirstOrDefaultAsync(d => d.Symbol == symbol);

                // If not cached, analyze first
                if (cachedAnalysis == null)
                {
                    _logger.LogInformation($"No cached data for {symbol}, analyzing first...");
                    var analysis = await GetDividendAnalysisAsync(symbol, forceRefresh: false, preferYahoo: false);
                    if (analysis == null)
                    {
                        return null;
                    }

                    // Reload from DB after analysis
                    cachedAnalysis = await _dbContext.DividendModels
                        .Include(d => d.DividendPayments)
                        .Include(d => d.YearlyDividends)
                        .FirstOrDefaultAsync(d => d.Symbol == symbol);

                    if (cachedAnalysis == null)
                    {
                        return null;
                    }
                }

                // Use cached data - prefer YearlyDividends if DividendPayments is empty
                var currentEps = cachedAnalysis.EPS ?? 0;
                var currentDividendYield = cachedAnalysis.DividendYield ?? 0;
                var currentPayoutRatio = cachedAnalysis.PayoutRatio ?? 0;
                var annualDividend = cachedAnalysis.DividendPerShare ?? 0;
                var payoutPolicy = cachedAnalysis.PayoutPolicy ?? "Not ";

                // Get annual dividends from YearlyDividends table (primary source)
                var annualDividends = cachedAnalysis.YearlyDividends
                    .Where(y => y.TotalDividend > 0)
                    .Select(y => new
                    {
                        year = y.Year,
                        totalAmount = y.TotalDividend,
                        paymentCount = y.PaymentCount
                    })
                    .OrderBy(d => d.year)
                    .ToList();

                // Calculate dividend growth rate year-over-year
                var dividendGrowth = new List<object>();
                const decimal MIN_DIVIDEND_THRESHOLD = 0.10m; // Minimum dividend for reliable growth calculation
                const decimal MAX_GROWTH_RATE = 200m; // Cap unrealistic growth rates at 200%

                for (int i = 1; i < annualDividends.Count; i++)
                {
                    var prev = annualDividends[i - 1];
                    var curr = annualDividends[i];

                    decimal? growthRate = null;
                    string? note = null;

                    // Only calculate growth if previous year had meaningful dividend
                    if (prev.totalAmount >= MIN_DIVIDEND_THRESHOLD)
                    {
                        growthRate = ((curr.totalAmount - prev.totalAmount) / prev.totalAmount) * 100;

                        // Cap extreme growth rates and add explanatory note
                        if (growthRate > MAX_GROWTH_RATE)
                        {
                            note = "Capped from " + Math.Round(growthRate.Value, 0) + "%";
                            growthRate = MAX_GROWTH_RATE;
                        }
                        else if (growthRate < -MAX_GROWTH_RATE)
                        {
                            note = "Capped from " + Math.Round(growthRate.Value, 0) + "%";
                            growthRate = -MAX_GROWTH_RATE;
                        }
                    }
                    else
                    {
                        // Previous year dividend too small for meaningful calculation
                        note = "Previous year dividend too low ($" + Math.Round(prev.totalAmount, 2) + ")";
                    }

                    dividendGrowth.Add(new
                    {
                        year = curr.year,
                        growthRate = growthRate.HasValue ? Math.Round(growthRate.Value, 2) : (decimal?)null,
                        amount = curr.totalAmount,
                        previousAmount = prev.totalAmount,
                        note = note
                    });
                }

                // Calculate payout ratio history using YEARLY EPS (not just current EPS)
                // This provides accurate payout ratios for each historical year
                var payoutRatioHistory = new List<object>();
                const decimal MIN_EPS_THRESHOLD = 0.10m; // Avoid garbage data from tiny EPS
                const decimal MAX_PAYOUT_RATIO = 200m; // Cap unrealistic ratios

                foreach (var div in annualDividends.TakeLast(5))
                {
                    // Use year-specific EPS for accurate payout ratios
                    var yearlyData = cachedAnalysis.YearlyDividends.FirstOrDefault(y => y.Year == div.year);
                    var yearEps = yearlyData?.AnnualEPS ?? currentEps;

                    // Only calculate if EPS is reasonable
                    if (yearEps >= MIN_EPS_THRESHOLD)
                    {
                        var ratio = (div.totalAmount / (decimal)yearEps) * 100;

                        // Cap extreme payout ratios
                        string? note = null;
                        if (ratio > MAX_PAYOUT_RATIO)
                        {
                            note = $"Capped from {Math.Round(ratio, 0)}%";
                            ratio = MAX_PAYOUT_RATIO;
                        }

                        payoutRatioHistory.Add(new
                        {
                            year = div.year,
                            payoutRatio = Math.Round(ratio, 2),
                            dividendPerShare = div.totalAmount,
                            eps = Math.Round((decimal)yearEps, 2),
                            note = note
                        });
                    }
                    else
                    {
                        // EPS too small or unavailable
                        payoutRatioHistory.Add(new
                        {
                            year = div.year,
                            payoutRatio = (decimal?)null,
                            dividendPerShare = div.totalAmount,
                            eps = (decimal?)null,
                            note = yearEps > 0 ? $"EPS too low (${Math.Round((decimal)yearEps, 2)})" : "EPS not available"
                        });
                    }
                }

                var chartData = new
                {
                    symbol = symbol,
                    currentMetrics = new
                    {
                        currentPrice = cachedAnalysis.CurrentPrice,
                        dividendYield = currentDividendYield,
                        annualDividend = annualDividend,
                        payoutRatio = currentPayoutRatio,
                        trailingEps = currentEps
                    },
                    charts = new
                    {
                        // Chart 1: Dividend Payment History (bar chart)
                        dividendHistory = annualDividends.Select(d => new
                        {
                            year = d.year,
                            amount = Math.Round(d.totalAmount, 2),
                            payments = d.paymentCount
                        }).ToList(),

                        // Chart 2: Dividend Growth Trend (line chart)
                        dividendGrowth = dividendGrowth,

                        // Chart 3: Payout Ratio Trend (line chart)
                        payoutRatioTrend = payoutRatioHistory,

                        // Chart 4: EPS vs Dividends (dual-line chart with year-specific EPS)
                        epsVsDividends = annualDividends.Select(d =>
                        {
                            // Use year-specific EPS from YearlyDividends table
                            var yearlyData = cachedAnalysis.YearlyDividends.FirstOrDefault(y => y.Year == d.year);
                            var yearEps = yearlyData?.AnnualEPS ?? currentEps;

                            return new
                            {
                                year = d.year,
                                dividend = Math.Round(d.totalAmount, 2),
                                eps = yearEps >= MIN_EPS_THRESHOLD ? Math.Round((decimal)yearEps, 2) : (decimal?)null
                            };
                        }).ToList()
                    }
                };

                _logger.LogInformation($"✅ Successfully fetched chart data for {symbol}");
                return chartData;
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error fetching chart data for {symbol}: {ex.Message}");
                return null;
            }
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

        public async Task<decimal?> FetchYahooFinanceCurrentPriceAsync(string symbol)
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
                Industry = overview["Industry"]?.ToString() ?? "Unknown",
                CurrentPrice = currentPrice ?? 0
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
            analysis.PayoutPolicy = overview["PayoutPolicy"]?.ToString() ?? "Not Available";

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
                    var growth = ((currentYear - previousYear) / previousYear);
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

        #region Swing Trading Analysis

        /// <summary>
        /// Get top swing trading stocks based on volatility, support levels, beta, and price position
        /// </summary>
        public async Task<SwingTradingResult> GetTopSwingTradingStocksAsync(int count = 5, string market = "canadian")
        {
            var allStocks = await _dbContext.DividendModels.ToListAsync();

            // Filter by market (Canadian = .TO suffix)
            var filteredStocks = market.ToLower() == "canadian"
                ? allStocks.Where(s => s.Symbol.EndsWith(".TO")).ToList()
                : allStocks.Where(s => !s.Symbol.Contains(".")).ToList();

            if (!filteredStocks.Any())
            {
                return new SwingTradingResult
                {
                    Market = market,
                    TotalAnalyzed = 0,
                    TopStocks = new List<SwingTradingStock>()
                };
            }

            // Calculate swing trading score for each stock
            var scoredStocks = filteredStocks
                .Select(s => new
                {
                    Stock = s,
                    SwingScore = CalculateSwingTradingScore(s),
                    ScoreBreakdown = GetSwingScoreBreakdown(s)
                })
                .Where(x => x.SwingScore > 0)
                .OrderByDescending(x => x.SwingScore)
                .Take(count)
                .ToList();

            var topStocks = scoredStocks.Select((x, index) => new SwingTradingStock
            {
                Rank = index + 1,
                Symbol = x.Stock.Symbol,
                CompanyName = x.Stock.CompanyName,
                Sector = x.Stock.Sector,
                SwingScore = Math.Round(x.SwingScore, 2),
                CurrentPrice = x.Stock.CurrentPrice,
                DailyVolatility = x.Stock.DailyVolatility,
                Beta = x.Stock.Beta,
                Week52High = x.Stock.Week52High,
                Week52Low = x.Stock.Week52Low,
                PriceVs52WeekRange = CalculatePricePosition(x.Stock),
                SupportLevel1 = x.Stock.SupportLevel1,
                DistanceToSupport1 = CalculateDistanceToSupport(x.Stock),
                SupportLevel2 = x.Stock.SupportLevel2,
                GrowthScore = x.Stock.GrowthScore,
                GrowthRating = x.Stock.GrowthRating,
                RevenueGrowth = x.Stock.RevenueGrowth,
                ScoreBreakdown = x.ScoreBreakdown,
                Recommendation = GenerateSwingRecommendation(x.Stock)
            }).ToList();

            return new SwingTradingResult
            {
                Market = market,
                TotalAnalyzed = filteredStocks.Count,
                GeneratedAt = DateTime.UtcNow,
                TopStocks = topStocks
            };
        }

        private double CalculateSwingTradingScore(DividendModel stock)
        {
            double score = 0;
            int factors = 0;

            // 1. Volatility Score (0-25 points) - Moderate volatility is best for swing trading
            if (stock.DailyVolatility.HasValue)
            {
                var vol = (double)stock.DailyVolatility.Value;
                if (vol >= 1.5 && vol <= 3.0) score += 25; // Sweet spot
                else if (vol >= 1.0 && vol < 1.5) score += 15; // Acceptable
                else if (vol > 3.0 && vol <= 4.0) score += 15; // Higher risk but tradeable
                else if (vol > 4.0) score += 5; // Too volatile
                else score += 5; // Too stable
                factors++;
            }

            // 2. Beta Score (0-20 points) - Beta 0.8-1.5 is ideal
            if (stock.Beta.HasValue)
            {
                var beta = (double)stock.Beta.Value;
                if (beta >= 0.8 && beta <= 1.5) score += 20;
                else if (beta >= 0.5 && beta < 0.8) score += 12;
                else if (beta > 1.5 && beta <= 2.0) score += 12;
                else score += 5;
                factors++;
            }

            // 3. Price Position Score (0-20 points) - Closer to 52-week low = more upside
            if (stock.Week52High > 0 && stock.Week52Low > 0 && stock.CurrentPrice > 0)
            {
                var range = stock.Week52High - stock.Week52Low;
                if (range > 0)
                {
                    var position = (stock.CurrentPrice - stock.Week52Low) / range;
                    if (position <= 0.3m) score += 20; // Near 52-week low
                    else if (position <= 0.5m) score += 15;
                    else if (position <= 0.7m) score += 10;
                    else score += 5; // Near 52-week high
                    factors++;
                }
            }

            // 4. Support Level Proximity (0-20 points) - Closer to support = better entry
            if (stock.SupportLevel1.HasValue && stock.SupportLevel1 > 0 && stock.CurrentPrice > 0)
            {
                var distanceToSupport = (double)((stock.CurrentPrice - stock.SupportLevel1.Value) / stock.CurrentPrice * 100);
                if (distanceToSupport >= 0 && distanceToSupport <= 5) score += 20; // Very close to support
                else if (distanceToSupport > 5 && distanceToSupport <= 10) score += 15;
                else if (distanceToSupport > 10 && distanceToSupport <= 15) score += 10;
                else score += 5;
                factors++;
            }

            // 5. Growth Score (0-15 points) - Momentum matters
            if (stock.GrowthScore > 0)
            {
                if (stock.GrowthScore >= 70) score += 15;
                else if (stock.GrowthScore >= 50) score += 10;
                else if (stock.GrowthScore >= 30) score += 7;
                else score += 3;
                factors++;
            }

            // Normalize to 100-point scale
            return factors > 0 ? (score / factors) * (factors / 5.0) * 20 : 0;
        }

        private SwingScoreBreakdown GetSwingScoreBreakdown(DividendModel stock)
        {
            return new SwingScoreBreakdown
            {
                VolatilityScore = stock.DailyVolatility.HasValue
                    ? (stock.DailyVolatility >= 1.5m && stock.DailyVolatility <= 3.0m ? "Optimal" :
                       stock.DailyVolatility < 1.5m ? "Low" : "High")
                    : "N/A",
                BetaScore = stock.Beta.HasValue
                    ? (stock.Beta >= 0.8m && stock.Beta <= 1.5m ? "Optimal" :
                       stock.Beta < 0.8m ? "Low" : "High")
                    : "N/A",
                PricePosition = stock.Week52High > 0 && stock.Week52Low > 0
                    ? (stock.CurrentPrice <= stock.Week52Low + (stock.Week52High - stock.Week52Low) * 0.3m ? "Near Low (Good)" :
                       stock.CurrentPrice >= stock.Week52Low + (stock.Week52High - stock.Week52Low) * 0.7m ? "Near High" : "Mid-Range")
                    : "N/A",
                SupportProximity = stock.SupportLevel1.HasValue && stock.SupportLevel1 > 0
                    ? $"{Math.Round((double)((stock.CurrentPrice - stock.SupportLevel1.Value) / stock.CurrentPrice * 100), 1)}% above support"
                    : "N/A",
                GrowthMomentum = stock.GrowthScore >= 70 ? "Strong" : stock.GrowthScore >= 50 ? "Moderate" : "Weak"
            };
        }

        private double? CalculatePricePosition(DividendModel stock)
        {
            if (stock.Week52High > 0 && stock.Week52Low > 0 && stock.CurrentPrice > 0)
            {
                return Math.Round((double)((stock.CurrentPrice - stock.Week52Low) / (stock.Week52High - stock.Week52Low) * 100), 1);
            }
            return null;
        }

        private double? CalculateDistanceToSupport(DividendModel stock)
        {
            if (stock.SupportLevel1.HasValue && stock.SupportLevel1 > 0 && stock.CurrentPrice > 0)
            {
                return Math.Round((double)((stock.CurrentPrice - stock.SupportLevel1.Value) / stock.CurrentPrice * 100), 2);
            }
            return null;
        }

        private string GenerateSwingRecommendation(DividendModel stock)
        {
            var recommendations = new List<string>();

            // Entry point recommendation
            if (stock.SupportLevel1.HasValue && stock.SupportLevel1 > 0)
            {
                var distanceToSupport = (stock.CurrentPrice - stock.SupportLevel1.Value) / stock.CurrentPrice * 100;
                if (distanceToSupport <= 5)
                    recommendations.Add($"Near support at ${stock.SupportLevel1:F2} - potential entry point");
                else
                    recommendations.Add($"Watch for pullback to ${stock.SupportLevel1:F2} support level");
            }

            // Volatility comment
            if (stock.DailyVolatility.HasValue)
            {
                if (stock.DailyVolatility >= 2.0m && stock.DailyVolatility <= 3.0m)
                    recommendations.Add("Good volatility for 2-5 day swing trades");
                else if (stock.DailyVolatility > 3.0m)
                    recommendations.Add("High volatility - use tighter stop losses");
            }

            // Target based on 52-week range
            if (stock.Week52High > 0 && stock.CurrentPrice > 0)
            {
                var potentialUpside = (stock.Week52High - stock.CurrentPrice) / stock.CurrentPrice * 100;
                if (potentialUpside > 10)
                    recommendations.Add($"Potential upside to 52-week high: {potentialUpside:F1}%");
            }

            return recommendations.Any() ? string.Join(". ", recommendations) : "Analyze further before trading";
        }

        #endregion

        #region Python Script Execution

        /// <summary>
        /// Call Python script to fetch stock data from Yahoo Finance
        /// Automatically tries .TO suffix for Canadian stocks if initial fetch fails
        /// </summary>
        public async Task<bool> FetchStockDataViaPythonAsync(string symbol)
        {
            try
            {
                var scriptPath = Path.Combine(Directory.GetCurrentDirectory(), "scripts", "update_stocks_from_yahoo.py");

                if (!File.Exists(scriptPath))
                {
                    _logger.LogError($"Python script not found: {scriptPath}");
                    return false;
                }

                // Try fetching with the symbol as-is
                var success = await ExecutePythonScriptAsync(scriptPath, symbol);

                // If failed and symbol doesn't contain a period, try adding .TO suffix for Canadian stocks
                if (!success && !symbol.Contains('.'))
                {
                    _logger.LogInformation($"First attempt failed. Trying {symbol}.TO for Canadian exchange...");
                    success = await ExecutePythonScriptAsync(scriptPath, $"{symbol}.TO");
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error calling Python script: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Execute Python script with given symbol
        /// </summary>
        private async Task<bool> ExecutePythonScriptAsync(string scriptPath, string symbol)
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = $"\"{scriptPath}\" {symbol}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Directory.GetCurrentDirectory()
                };

                using var process = new Process { StartInfo = processInfo };

                var output = new System.Text.StringBuilder();
                var error = new System.Text.StringBuilder();

                process.OutputDataReceived += (sender, e) => { if (e.Data != null) output.AppendLine(e.Data); };
                process.ErrorDataReceived += (sender, e) => { if (e.Data != null) error.AppendLine(e.Data); };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    _logger.LogInformation($"✓ Python script succeeded for {symbol}");
                    _logger.LogDebug(output.ToString());
                    return true;
                }
                else
                {
                    _logger.LogWarning($"✗ Python script failed for {symbol}");
                    _logger.LogDebug($"Error: {error}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Error executing Python script for {symbol}: {ex.Message}");
                return false;
            }
        }

        #endregion
    }

    // Models
    public class DividendAnalysis
    {
        public string Symbol { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string Sector { get; set; } = string.Empty;
        public string Industry { get; set; } = string.Empty;
        public decimal CurrentPrice { get; set; }
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
        public string PayoutPolicy { get; set; } = "Unknown"; // "Dividend Only", "Reinvestment Only", "Mixed", "None", "Unknown"
        public decimal? DividendAllocationPct { get; set; }    // % of earnings paid as dividends (same as PayoutRatio)
        public decimal? ReinvestmentAllocationPct { get; set; } // % of earnings reinvested (100 - PayoutRatio)
        public decimal GrowthScore { get; set; }               // Growth score 0-100
        public decimal? DailyVolatility { get; set; }          // Daily volatility percentage (30-day)
        public int? SectorRank { get; set; }                   // Rank within sector (1 = best)

        // Price Range Metrics (52-week)
        public decimal? Week52High { get; set; }               // 52-week high price
        public decimal? Week52Low { get; set; }                // 52-week low price
        public decimal? Month1Low { get; set; }                // 1-month low price
        public decimal? Month3Low { get; set; }                // 3-month low price

        // Support Level Analysis
        public decimal? SupportLevel1 { get; set; }            // Primary support level (highest volume)
        public decimal? SupportLevel1Volume { get; set; }      // Volume at primary support
        public decimal? SupportLevel2 { get; set; }            // Secondary support level
        public decimal? SupportLevel2Volume { get; set; }      // Volume at secondary support
        public decimal? SupportLevel3 { get; set; }            // Tertiary support level
        public decimal? SupportLevel3Volume { get; set; }      // Volume at tertiary support

    }

    public class DividendPayment
    {
        public DateTime Date { get; set; }
        public decimal Amount { get; set; }
    }

    // Swing Trading DTOs
    public class SwingTradingResult
    {
        public string Market { get; set; } = string.Empty;
        public int TotalAnalyzed { get; set; }
        public DateTime GeneratedAt { get; set; }
        public List<SwingTradingStock> TopStocks { get; set; } = new();
    }

    public class SwingTradingStock
    {
        public int Rank { get; set; }
        public string Symbol { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string Sector { get; set; } = string.Empty;
        public double SwingScore { get; set; }
        public decimal CurrentPrice { get; set; }
        public decimal? DailyVolatility { get; set; }
        public decimal? Beta { get; set; }
        public decimal? Week52High { get; set; }
        public decimal? Week52Low { get; set; }
        public double? PriceVs52WeekRange { get; set; }
        public decimal? SupportLevel1 { get; set; }
        public double? DistanceToSupport1 { get; set; }
        public decimal? SupportLevel2 { get; set; }
        public decimal GrowthScore { get; set; }
        public string GrowthRating { get; set; } = string.Empty;
        public decimal? RevenueGrowth { get; set; }
        public SwingScoreBreakdown ScoreBreakdown { get; set; } = new();
        public string Recommendation { get; set; } = string.Empty;
    }

    public class SwingScoreBreakdown
    {
        public string VolatilityScore { get; set; } = "N/A";
        public string BetaScore { get; set; } = "N/A";
        public string PricePosition { get; set; } = "N/A";
        public string SupportProximity { get; set; } = "N/A";
        public string GrowthMomentum { get; set; } = "N/A";
    }
}
