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
            _dbContext       = dbContext;
            _logger          = logger;
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
                    beta = cached.Beta,
                    payoutPolicy = cached.PayoutPolicy,
                    dividendAllocationPct = cached.DividendAllocationPct,
                    reinvestmentAllocationPct = cached.ReinvestmentAllocationPct,
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
                    payoutPolicy = c.PayoutPolicy,
                    dividendAllocationPct = c.DividendAllocationPct,
                    reinvestmentAllocationPct = c.ReinvestmentAllocationPct,
                    dividendGrowthRate = c.DividendGrowthRate,
                    safetyScore = c.SafetyScore,
                    safetyRating = c.SafetyRating,
                    consecutiveYears = c.ConsecutiveYearsOfPayments,
                    growthScore = c.GrowthScore,
                    dailyVolatility = c.DailyVolatility,
                    sectorRank = c.SectorRank,
                    week52High = c.Week52High,
                    week52Low = c.Week52Low,
                    month1Low = c.Month1Low,
                    month3Low = c.Month3Low,
                    supportLevel1 = c.SupportLevel1,
                    supportLevel1Volume = c.SupportLevel1Volume,
                    supportLevel2 = c.SupportLevel2,
                    supportLevel2Volume = c.SupportLevel2Volume,
                    supportLevel3 = c.SupportLevel3,
                    supportLevel3Volume = c.SupportLevel3Volume,
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
        [HttpDelete("cache/{symbol}")]
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
            try
            {
                var (bytes, fileName) = await _dividendService.ExportToCsvAsync(type, symbol);
                return File(bytes, "text/csv", fileName);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
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

        /// <summary>
        /// Get top swing trading stocks for Canadian or US market
        /// Scores based on: volatility, support levels, beta, growth, and price position
        /// Example: GET /api/dividends/swing-trading/top?count=5&market=canadian
        /// </summary>
        [HttpGet("swing-trading/top")]
        public async Task<ActionResult<object>> GetTopSwingTradingStocks(
            [FromQuery] int count = 5,
            [FromQuery] string market = "canadian")
        {
            try
            {
                var result = await _dividendService.GetTopSwingTradingStocksAsync(count, market);

                if (!result.TopStocks.Any())
                {
                    return Ok(new
                    {
                        message = $"No {market} stocks found in database",
                        recommendation = "Import stocks first using POST /api/dividends/screen with Canadian symbols"
                    });
                }

                return Ok(new
                {
                    market = result.Market,
                    totalAnalyzed = result.TotalAnalyzed,
                    generatedAt = result.GeneratedAt,
                    scoringFactors = new
                    {
                        volatility = "Moderate volatility (1.5-3%) preferred for swing trading",
                        supportLevels = "Stocks near support levels score higher",
                        beta = "Beta 0.8-1.5 preferred (not too stable, not too volatile)",
                        growthScore = "Higher growth momentum stocks preferred",
                        pricePosition = "Stocks closer to 52-week low (potential upside)"
                    },
                    topStocks = result.TopStocks
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating swing trading stocks");
                return StatusCode(500, new { error = "Failed to calculate swing trading recommendations" });
            }
        }

        /// <summary>
        /// Bulk import all TSX stocks from exchange listing
        /// This triggers a Python script that fetches all TSX listings and imports them
        /// Example: POST /api/dividends/bulk-import/tsx
        /// </summary>
        [HttpPost("bulk-import/tsx")]
        public ActionResult BulkImportTSXStocks()
        {
            try
            {
                _logger.LogInformation("Starting TSX bulk import...");

                var scriptPath = Path.Combine(Directory.GetCurrentDirectory(), "scripts", "bulk_import_tsx_stocks.py");

                if (!System.IO.File.Exists(scriptPath))
                {
                    return NotFound(new { error = $"Bulk import script not found: {scriptPath}" });
                }

                // Execute Python script in background
                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = $"\"{scriptPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                var process = System.Diagnostics.Process.Start(processInfo);
                if (process == null)
                {
                    return StatusCode(500, new { error = "Failed to start bulk import process" });
                }

                _logger.LogInformation("Bulk import process started");

                return Ok(new
                {
                    message = "TSX bulk import started",
                    status = "running",
                    startedAt = DateTime.UtcNow,
                    note = "Import is running in the background. Check logs for progress."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting bulk import");
                return StatusCode(500, new { error = $"Failed to start bulk import: {ex.Message}" });
            }
        }
    }

    public class AddDividendRequest
    {
        public string Symbol { get; set; } = string.Empty;
    }
}
