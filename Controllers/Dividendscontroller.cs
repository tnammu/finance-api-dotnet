using FinanceApi.Services;
using FinanceApi.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinanceApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DividendsController : ControllerBase
    {
        private readonly DividendAnalysisService _dividendService;
        private readonly DividendDbContext _dbContext;
        private readonly ILogger<DividendsController> _logger;

        public DividendsController(
            DividendAnalysisService dividendService,
            DividendDbContext dbContext,
            ILogger<DividendsController> logger)
        {
            _dividendService = dividendService;
            _dbContext = dbContext;
            _logger = logger;
        }

        /// <summary>
        /// Get dividend analysis for a stock - fetches from cache or Yahoo Finance if needed
        /// </summary>
        [HttpGet("analyze/{symbol}")]
        public async Task<ActionResult<object>> GetDividendAnalysis(string symbol, [FromQuery] bool refresh = false)
        {
            symbol = symbol.ToUpper();

            // Check if stock exists in cache
            var cached = await _dbContext.DividendModels
                .Include(d => d.DividendPayments)
                .Include(d => d.YearlyDividends)
                .FirstOrDefaultAsync(d => d.Symbol == symbol);

            // If not cached or refresh requested, fetch from Yahoo using Python
            if (cached == null || refresh)
            {
                _logger.LogInformation($"Fetching {symbol} using Python script...");

                var success = await _dividendService.FetchStockDataViaPythonAsync(symbol);

                if (!success)
                {
                    _logger.LogWarning($"✗ Python script failed for {symbol}, trying Alpha Vantage fallback...");

                    // Try Alpha Vantage as fallback
                    try
                    {
                        var alphaAnalysis = await _dividendService.GetDividendAnalysisAsync(symbol, forceRefresh: true, preferYahoo: false);
                        if (alphaAnalysis != null)
                        {
                            _logger.LogInformation($"✓ Successfully fetched {symbol} from Alpha Vantage");
                            // Data is now in database, reload it
                            cached = await _dbContext.DividendModels
                                .Include(d => d.DividendPayments)
                                .Include(d => d.YearlyDividends)
                                .FirstOrDefaultAsync(d => d.Symbol == symbol);
                        }
                        else
                        {
                            return NotFound(new { error = $"Could not analyze {symbol} from either Yahoo Finance or Alpha Vantage" });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Alpha Vantage fallback also failed: {ex.Message}");
                        return NotFound(new { error = $"Could not analyze {symbol} from either data source" });
                    }
                }
                else
                {
                    // Reload from database after Python script updates it
                    cached = await _dbContext.DividendModels
                        .Include(d => d.DividendPayments)
                        .Include(d => d.YearlyDividends)
                        .FirstOrDefaultAsync(d => d.Symbol == symbol);
                }

                if (cached == null)
                {
                    return NotFound(new { error = $"Could not analyze {symbol}" });
                }
            }

            // Return cached data
            return Ok(new
            {
                symbol = cached.Symbol,
                companyName = cached.CompanyName,
                sector = cached.Sector,

                currentMetrics = new
                {
                    currentprice = cached.CurrentPrice,
                    dividendYield = cached.DividendYield,
                    dividendPerShare = cached.DividendPerShare,
                    payoutRatio = cached.PayoutRatio,
                    eps = cached.EPS,
                    beta = cached.Beta
                },

                historicalAnalysis = new
                {
                    consecutiveYearsOfPayments = cached.ConsecutiveYearsOfPayments,
                    dividendGrowthRate = cached.DividendGrowthRate,
                    yearlyDividends = cached.YearlyDividends
                        .GroupBy(y => y.Year)
                        .ToDictionary(g => g.Key, g => g.First().TotalDividend)
                },

                safetyAnalysis = new
                {
                    score = cached.SafetyScore,
                    rating = cached.SafetyRating,
                    recommendation = cached.Recommendation
                },

                metadata = new
                {
                    fromCache = true,
                    fetchedAt = cached.FetchedAt,
                    lastUpdated = cached.LastUpdated,
                    apiCallsUsed = 0,
                    dataSource = "Python/Yahoo Finance"
                },

                dividendHistory = cached.DividendPayments.Select(d => new
                {
                    date = d.PaymentDate.ToString("yyyy-MM-dd"),
                    amount = d.Amount
                }).OrderBy(d => d.date).ToList()
            });
        }

        /// <summary>
        /// Get historical chart data for dividend analysis
        /// Returns multi-year trends for charts
        /// </summary>
        [HttpGet("{symbol}/charts")]
        public async Task<ActionResult<object>> GetDividendCharts(string symbol)
        {
            try
            {
                var chartData = await _dividendService.GetHistoricalChartDataAsync(symbol);

                if (chartData == null)
                {
                    return NotFound(new { error = $"Could not fetch chart data for {symbol}" });
                }

                return Ok(chartData);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching chart data for {symbol}: {ex.Message}");
                return StatusCode(500, new { error = "Failed to fetch chart data" });
            }
        }

        /// <summary>
        /// Add a new stock for dividend analysis
        /// Example: POST /api/dividends with body { "symbol": "AAPL" }
        /// Query params: preferYahoo=true to use Yahoo Finance instead of Alpha Vantage
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<object>> AddDividendStock([FromBody] AddDividendRequest request, [FromQuery] bool preferYahoo = false)
        {
            if (string.IsNullOrWhiteSpace(request.Symbol))
            {
                return BadRequest(new { error = "Symbol is required" });
            }

            var symbol = request.Symbol.ToUpper().Trim();

            // Check if already exists
            if (await _dbContext.DividendModels.AnyAsync(s => s.Symbol == symbol))
            {
                return Conflict(new { error = $"Stock {symbol} already exists" });
            }

            // Fetch and analyze - this will save to database
            var analysis = await _dividendService.GetDividendAnalysisAsync(symbol, forceRefresh: false, preferYahoo: preferYahoo);

            if (analysis == null || analysis.CurrentPrice == 0)
            {
                return BadRequest(new { error = $"Could not analyze {symbol}. Please verify the symbol is correct." });
            }

            // Return the created analysis
            return Ok(new
            {
                message = $"Stock {symbol} added successfully",
                stock = new
                {
                    symbol = analysis.Symbol,
                    companyName = analysis.CompanyName,
                    sector = analysis.Sector,
                    currentPrice = analysis.CurrentPrice,
                    dividendYield = analysis.DividendYield,
                    payoutRatio = analysis.PayoutRatio,
                    safetyScore = analysis.SafetyScore,
                    safetyRating = analysis.SafetyRating
                }
            });
        }

        /// <summary>
        /// List all dividend analyses from database
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<object>> GetAllDividends()
        {
            var cached = await _dividendService.GetAllCachedAnalysesAsync();

            return Ok(new
            {
                totalCached = cached.Count,
                stocks = cached.Select(c => new
                {
                    symbol = c.Symbol,
                    companyName = c.CompanyName,
                    sector = c.Sector,
                    currentPrice = c.CurrentPrice,
                    dividendYield = c.DividendYield,
                    payoutRatio = c.PayoutRatio,
                    dividendGrowthRate = c.DividendGrowthRate,
                    safetyScore = c.SafetyScore,
                    safetyRating = c.SafetyRating,
                    consecutiveYears = c.ConsecutiveYearsOfPayments,
                    lastUpdated = c.LastUpdated,
                    daysOld = (DateTime.UtcNow - c.LastUpdated).TotalDays
                }).ToList()
            });
        }

        /// <summary>
        /// Screen multiple stocks for dividend analysis - supports both US and Canadian markets
        /// Examples:
        /// - POST /api/dividends/screen?market=us (US stocks, no suffix)
        /// - POST /api/dividends/screen?market=canadian (Toronto exchange, adds .TO suffix)
        /// - POST /api/dividends/screen?suffix=.TO (custom suffix)
        /// - POST /api/dividends/screen?preferYahoo=true (use Yahoo Finance)
        /// Body: ["AAPL", "MSFT", "TD", "RY"]
        /// </summary>
        [HttpPost("screen")]
        public async Task<ActionResult<object>> ScreenStocks(
            [FromBody] List<string> symbols,
            [FromQuery] string? market = "us",
            [FromQuery] string? suffix = null,
            [FromQuery] bool preferYahoo = false)
        {
            var results = new List<object>();
            int apiCalls = 0;
            int fromCache = 0;

            // Determine suffix based on market parameter (unless explicitly provided)
            var symbolSuffix = suffix ?? (market?.ToLower() == "canadian" ? ".TO" : "");

            foreach (var symbol in symbols)
            {
                await Task.Delay(500); // Rate limit protection

                // Apply suffix if needed and not already present
                var processedSymbol = symbol.ToUpper();
                if (!string.IsNullOrEmpty(symbolSuffix) && !processedSymbol.Contains('.'))
                {
                    processedSymbol = $"{processedSymbol}{symbolSuffix}";
                }

                var analysis = await _dividendService.GetDividendAnalysisAsync(processedSymbol, forceRefresh: false, preferYahoo: preferYahoo);

                if (analysis != null && analysis.DividendYield.HasValue)
                {
                    results.Add(new
                    {
                        symbol = analysis.Symbol,
                        companyName = analysis.CompanyName,
                        sector = analysis.Sector,
                        dividendYield = analysis.DividendYield,
                        payoutRatio = analysis.PayoutRatio,
                        safetyScore = analysis.SafetyScore,
                        safetyRating = analysis.SafetyRating,
                        consecutiveYears = analysis.ConsecutiveYearsOfPayments,
                        dividendGrowthRate = analysis.DividendGrowthRate,
                        fromCache = analysis.IsFromCache
                    });

                    if (analysis.IsFromCache)
                        fromCache++;
                    else
                        apiCalls += analysis.ApiCallsUsed;
                }
            }

            return Ok(new
            {
                market = market ?? "us",
                suffix = symbolSuffix,
                totalScreened = symbols.Count,
                successCount = results.Count,
                apiCallsUsed = apiCalls,
                fromCache = fromCache,
                topDividendStocks = results.OrderByDescending(r => ((dynamic)r).safetyScore).ToList()
            });
        }

        /// <summary>
        /// Get API usage statistics - defaults to today, or specify days parameter for history
        /// Example: /api/dividends/api-usage (today only)
        /// Example: /api/dividends/api-usage?days=30 (last 30 days)
        /// </summary>
        [HttpGet("api-usage")]
        public async Task<ActionResult<object>> GetApiUsage([FromQuery] int? days = null)
        {
            var today = DateTime.UtcNow.Date;

            // If no days parameter, return today's usage only
            if (days == null)
            {
                var log = await _dbContext.ApiUsageLogs.FirstOrDefaultAsync(l => l.Date == today);

                if (log == null)
                {
                    return Ok(new
                    {
                        date = today,
                        callsUsed = 0,
                        dailyLimit = 25,
                        remaining = 25,
                        status = "No API calls made today"
                    });
                }

                var remaining = log.DailyLimit - log.CallsUsed;

                return Ok(new
                {
                    date = log.Date,
                    callsUsed = log.CallsUsed,
                    dailyLimit = log.DailyLimit,
                    remaining = remaining,
                    percentUsed = (log.CallsUsed * 100.0 / log.DailyLimit),
                    status = remaining > 0 ? "OK" : "LIMIT_REACHED",
                    canAnalyzeStocks = remaining / 3
                });
            }

            // If days parameter provided, return history
            var startDate = today.AddDays(-days.Value);
            var history = await _dbContext.ApiUsageLogs
                .Where(l => l.Date >= startDate)
                .OrderByDescending(l => l.Date)
                .ToListAsync();

            var totalCalls = history.Sum(h => h.CallsUsed);
            var avgPerDay = history.Any() ? history.Average(h => h.CallsUsed) : 0;

            return Ok(new
            {
                period = $"Last {days} days",
                startDate = startDate.ToString("yyyy-MM-dd"),
                endDate = today.ToString("yyyy-MM-dd"),
                totalDays = history.Count,
                totalApiCalls = totalCalls,
                averagePerDay = avgPerDay,
                dailyUsage = history.Select(h => new
                {
                    date = h.Date.ToString("yyyy-MM-dd"),
                    callsUsed = h.CallsUsed,
                    limit = h.DailyLimit,
                    remaining = h.DailyLimit - h.CallsUsed
                }).ToList()
            });
        }

        /// <summary>
        /// Delete cached dividend data for a stock (force fresh fetch next time)
        /// </summary>
        [HttpDelete("{symbol}")]
        public async Task<ActionResult<object>> DeleteDividendCache(string symbol)
        {
            symbol = symbol.ToUpper();

            var cached = await _dbContext.DividendModels
                .FirstOrDefaultAsync(d => d.Symbol == symbol);

            if (cached == null)
            {
                return NotFound(new { error = $"No cached data for {symbol}" });
            }

            _dbContext.DividendModels.Remove(cached);
            await _dbContext.SaveChangesAsync();

            return Ok(new
            {
                message = $"Deleted cached data for {symbol}",
                symbol = symbol,
                deletedAt = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Get dividend analytics and statistics
        /// </summary>
        [HttpGet("analytics")]
        public async Task<ActionResult<object>> GetAnalytics()
        {
            var totalStocks = await _dbContext.DividendModels.CountAsync();
            var totalPayments = await _dbContext.DividendPayments.CountAsync();

            // Get top scorers - fetch first, then sort in memory (SQLite doesn't support ORDER BY decimal)
            var allStocks = await _dbContext.DividendModels
                .Select(d => new
                {
                    symbol = d.Symbol,
                    companyName = d.CompanyName,
                    safetyScore = d.SafetyScore,
                    rating = d.SafetyRating
                })
                .ToListAsync();

            var topScorers = allStocks
                .OrderByDescending(d => d.safetyScore)
                .Take(5)
                .ToList();

            // Get by sector - fetch first, calculate average in memory
            var allStocksForSector = await _dbContext.DividendModels
                .Select(d => new
                {
                    sector = d.Sector,
                    safetyScore = d.SafetyScore
                })
                .ToListAsync();

            var bySector = allStocksForSector
                .GroupBy(d => d.sector)
                .Select(g => new
                {
                    sector = g.Key,
                    count = g.Count(),
                    avgScore = g.Average(d => (double)d.safetyScore)
                })
                .OrderByDescending(s => s.count)
                .ToList();

            return Ok(new
            {
                totalStocksCached = totalStocks,
                totalDividendPayments = totalPayments,
                topScoringStocks = topScorers,
                bySector = bySector,
                databaseSize = new
                {
                    analyses = totalStocks,
                    payments = totalPayments,
                    yearlyRecords = await _dbContext.YearlyDividends.CountAsync()
                }
            });
        }

        /// <summary>
        /// Export dividend data to CSV
        /// Examples:
        /// - GET /api/dividends/export?type=analyses (all dividend analyses)
        /// - GET /api/dividends/export?type=payments (all dividend payments)
        /// - GET /api/dividends/export?type=payments&symbol=AAPL (payments for specific stock)
        /// - GET /api/dividends/export?type=usage (API usage history)
        /// </summary>
        [HttpGet("export")]
        public async Task<IActionResult> ExportToCsv([FromQuery] string type = "analyses", [FromQuery] string? symbol = null)
        {
            var csv = new System.Text.StringBuilder();
            string fileName;

            switch (type.ToLower())
            {
                case "analyses":
                    var dividends = await _dbContext.DividendModels
                        .OrderByDescending(d => d.SafetyScore)
                        .ToListAsync();

                    csv.AppendLine("Symbol,Company Name,Sector,Industry,Dividend Yield (%),Payout Ratio (%),Safety Score,Safety Rating," +
                                  "Consecutive Years,Growth Rate (%),Last Updated,Days Old");

                    foreach (var div in dividends)
                    {
                        var daysOld = (DateTime.UtcNow - div.LastUpdated).TotalDays;
                        var dividendYield = div.DividendYield.HasValue ? div.DividendYield.Value.ToString("F2") : "";
                        var payoutRatio = div.PayoutRatio.HasValue ? div.PayoutRatio.Value.ToString("F2") : "";
                        var growthRate = div.DividendGrowthRate.HasValue ? div.DividendGrowthRate.Value.ToString("F2") : "";

                        csv.AppendLine($"{div.Symbol}," +
                                      $"\"{div.CompanyName}\"," +
                                      $"\"{div.Sector}\"," +
                                      $"\"{div.Industry}\"," +
                                      $"{dividendYield}," +
                                      $"{payoutRatio}," +
                                      $"{div.SafetyScore:F2}," +
                                      $"\"{div.SafetyRating}\"," +
                                      $"{div.ConsecutiveYearsOfPayments}," +
                                      $"{growthRate}," +
                                      $"{div.LastUpdated:yyyy-MM-dd HH:mm:ss}," +
                                      $"{daysOld:F1}");
                    }

                    fileName = $"dividends_analyses_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
                    break;

                case "payments":
                    var query = _dbContext.DividendPayments.AsQueryable();

                    if (!string.IsNullOrEmpty(symbol))
                    {
                        query = query.Where(p => p.Symbol == symbol.ToUpper());
                    }

                    var payments = await query
                        .OrderBy(p => p.Symbol)
                        .ThenByDescending(p => p.PaymentDate)
                        .ToListAsync();

                    csv.AppendLine("Symbol,Payment Date,Amount,Year,Quarter");

                    foreach (var payment in payments)
                    {
                        var quarter = (payment.PaymentDate.Month - 1) / 3 + 1;
                        csv.AppendLine($"{payment.Symbol}," +
                                      $"{payment.PaymentDate:yyyy-MM-dd}," +
                                      $"{payment.Amount:F4}," +
                                      $"{payment.PaymentDate.Year}," +
                                      $"Q{quarter}");
                    }

                    fileName = string.IsNullOrEmpty(symbol)
                        ? $"dividend_payments_all_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv"
                        : $"dividend_payments_{symbol}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
                    break;

                case "usage":
                    var usage = await _dbContext.ApiUsageLogs
                        .OrderByDescending(u => u.Date)
                        .ToListAsync();

                    csv.AppendLine("Date,Calls Used,Daily Limit,Remaining,Percentage Used (%),Notes");

                    foreach (var log in usage)
                    {
                        var remaining = log.DailyLimit - log.CallsUsed;
                        var percentUsed = (log.CallsUsed * 100.0 / log.DailyLimit);
                        var notes = log.Notes ?? "";

                        csv.AppendLine($"{log.Date:yyyy-MM-dd}," +
                                      $"{log.CallsUsed}," +
                                      $"{log.DailyLimit}," +
                                      $"{remaining}," +
                                      $"{percentUsed:F1}," +
                                      $"\"{notes}\"");
                    }

                    fileName = $"api_usage_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
                    break;

                default:
                    return BadRequest(new { error = $"Invalid export type: {type}. Valid types are: analyses, payments, usage" });
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", fileName);
        }

        /// <summary>
        /// Delete a dividend stock analysis from database
        /// Example: DELETE /api/dividends/AAPL
        /// </summary>
        [HttpDelete("{symbol}")]
        public async Task<ActionResult> DeleteDividendStock(string symbol)
        {
            try
            {
                var stock = await _dbContext.DividendModels
                    .Include(d => d.DividendPayments)
                    .Include(d => d.YearlyDividends)
                    .FirstOrDefaultAsync(d => d.Symbol == symbol.ToUpper());

                if (stock == null)
                {
                    return NotFound(new { error = $"Stock {symbol} not found" });
                }

                // Remove related data first (cascade should handle this but being explicit)
                _dbContext.DividendPayments.RemoveRange(stock.DividendPayments);
                _dbContext.YearlyDividends.RemoveRange(stock.YearlyDividends);
                _dbContext.DividendModels.Remove(stock);

                await _dbContext.SaveChangesAsync();

                _logger.LogInformation($"✓ Deleted {symbol} and all related data");

                return Ok(new { message = $"Stock {symbol} deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting {symbol}: {ex.Message}");
                return StatusCode(500, new { error = "Failed to delete stock" });
            }
        }
    }

    public class AddDividendRequest
    {
        public string Symbol { get; set; } = string.Empty;
    }
}
