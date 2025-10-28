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
        /// Analyze stock - checks DB cache first, fetches from API only if needed
        /// </summary>
        [HttpGet("analyze/{symbol}")]
        public async Task<ActionResult<object>> AnalyzeDividend(string symbol, [FromQuery] bool refresh = false)
        {
            var analysis = await _dividendService.GetDividendAnalysisAsync(symbol, refresh);


            if (analysis == null)
            {
                return NotFound(new { error = $"Could not analyze {symbol}" });
            }

            return Ok(new
            {
                symbol = analysis.Symbol,
                companyName = analysis.CompanyName,
                sector = analysis.Sector,

                currentMetrics = new
                {
                    dividendYield = analysis.DividendYield,
                    dividendPerShare = analysis.DividendPerShare,
                    payoutRatio = analysis.PayoutRatio,
                    eps = analysis.EPS,
                    beta = analysis.Beta
                },

                historicalAnalysis = new
                {
                    consecutiveYearsOfPayments = analysis.ConsecutiveYearsOfPayments,
                    dividendGrowthRate = analysis.DividendGrowthRate,
                    yearlyDividends = analysis.YearlyDividends
                },

                safetyAnalysis = new
                {
                    score = analysis.SafetyScore,
                    rating = analysis.SafetyRating,
                    recommendation = analysis.Recommendation
                },

                metadata = new
                {
                    fromCache = analysis.IsFromCache,
                    fetchedAt = analysis.FetchedAt,
                    lastUpdated = analysis.LastUpdated,
                    apiCallsUsed = analysis.ApiCallsUsed
                },

                dividendHistory = analysis.DividendHistory.Select(d => new
                {
                    date = d.Date.ToString("yyyy-MM-dd"),
                    amount = d.Amount
                }).ToList()
            });
        }

        /// <summary>
        /// View all cached dividend analyses from database
        /// </summary>
        [HttpGet("cached")]
        public async Task<ActionResult<object>> GetAllCached()
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
                    dividendYield = c.DividendYield,
                    safetyScore = c.SafetyScore,
                    safetyRating = c.SafetyRating,
                    consecutiveYears = c.ConsecutiveYearsOfPayments,
                    lastUpdated = c.LastUpdated,
                    daysOld = (DateTime.UtcNow - c.LastUpdated).TotalDays
                }).ToList()
            });
        }

        /// <summary>
        /// Screen Canadian stocks - uses cache when possible
        /// </summary>
        [HttpPost("screen/canadian")]
        public async Task<ActionResult<object>> ScreenCanadian([FromBody] List<string> symbols)
        {
            var results = new List<object>();
            int apiCalls = 0;
            int fromCache = 0;

            foreach (var symbol in symbols)
            {
                await Task.Delay(500); // Rate limit protection

                var canadianSymbol = symbol.ToUpper().EndsWith(".TO") ? symbol : $"{symbol}.TO";
                var analysis = await _dividendService.GetDividendAnalysisAsync(canadianSymbol);

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
                totalScreened = symbols.Count,
                successCount = results.Count,
                apiCallsUsed = apiCalls,
                fromCache = fromCache,
                topDividendStocks = results.OrderByDescending(r => ((dynamic)r).safetyScore).ToList()
            });
        }

        /// <summary>
        /// View API usage statistics
        /// </summary>
        [HttpGet("usage/today")]
        public async Task<ActionResult<object>> GetTodayUsage()
        {
            var today = DateTime.UtcNow.Date;
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

        /// <summary>
        /// View API usage history (last 30 days)
        /// </summary>
        [HttpGet("usage/history")]
        public async Task<ActionResult<object>> GetUsageHistory()
        {
            var thirtyDaysAgo = DateTime.UtcNow.Date.AddDays(-30);

            var history = await _dbContext.ApiUsageLogs
                .Where(l => l.Date >= thirtyDaysAgo)
                .OrderByDescending(l => l.Date)
                .ToListAsync();

            var totalCalls = history.Sum(h => h.CallsUsed);
            var avgPerDay = history.Any() ? history.Average(h => h.CallsUsed) : 0;

            return Ok(new
            {
                period = "Last 30 days",
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
        /// View dividend payment history for a stock
        /// </summary>
        [HttpGet("history/{symbol}")]
        public async Task<ActionResult<object>> GetDividendHistory(string symbol)
        {
            symbol = symbol.ToUpper();

            var analysis = await _dbContext.DividendModels
                .Include(d => d.DividendPayments)
                .Include(d => d.YearlyDividends)
                .FirstOrDefaultAsync(d => d.Symbol == symbol);

            if (analysis == null)
            {
                return NotFound(new { error = $"No cached data for {symbol}. Analyze it first!" });
            }

            return Ok(new
            {
                symbol = analysis.Symbol,
                companyName = analysis.CompanyName,
                totalPayments = analysis.DividendPayments.Count,
                yearlyDividends = analysis.YearlyDividends
                    .OrderByDescending(y => y.Year)
                    .Select(y => new
                    {
                        year = y.Year,
                        totalDividend = y.TotalDividend,
                        paymentCount = y.PaymentCount,
                        avgPerPayment = y.TotalDividend / y.PaymentCount
                    }).ToList(),
                allPayments = analysis.DividendPayments
                    .OrderByDescending(p => p.PaymentDate)
                    .Select(p => new
                    {
                        date = p.PaymentDate.ToString("yyyy-MM-dd"),
                        amount = p.Amount
                    }).ToList()
            });
        }

        /// <summary>
        /// Delete cached data for a stock (force fresh fetch next time)
        /// </summary>
        [HttpDelete("cached/{symbol}")]
        public async Task<ActionResult<object>> DeleteCached(string symbol)
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
        /// Get database statistics
        /// </summary>
        [HttpGet("stats")]
        public async Task<ActionResult<object>> GetStats()
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
        /// Export all dividend analyses to CSV
        /// Example: GET /api/dividends/export/csv
        /// </summary>
        [HttpGet("export/csv")]
        public async Task<IActionResult> ExportToCsv()
        {
            var dividends = await _dbContext.DividendModels
                .OrderByDescending(d => d.SafetyScore)
                .ToListAsync();

            var csv = new System.Text.StringBuilder();

            // Header
            csv.AppendLine("Symbol,Company Name,Sector,Industry,Dividend Yield (%),Payout Ratio (%),Safety Score,Safety Rating," +
                          "Consecutive Years,Growth Rate (%),Last Updated,Days Old");

            // Data rows
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

            var fileName = $"dividends_export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
            var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());

            return File(bytes, "text/csv", fileName);
        }

        /// <summary>
        /// Export dividend payment history to CSV
        /// Example: GET /api/dividends/export/payments/csv
        /// </summary>
        [HttpGet("export/payments/csv")]
        public async Task<IActionResult> ExportPaymentsToCsv([FromQuery] string? symbol = null)
        {
            var query = _dbContext.DividendPayments.AsQueryable();

            if (!string.IsNullOrEmpty(symbol))
            {
                query = query.Where(p => p.Symbol == symbol.ToUpper());
            }

            var payments = await query
                .OrderBy(p => p.Symbol)
                .ThenByDescending(p => p.PaymentDate)
                .ToListAsync();

            var csv = new System.Text.StringBuilder();

            // Header
            csv.AppendLine("Symbol,Payment Date,Amount,Year,Quarter");

            // Data rows
            foreach (var payment in payments)
            {
                var quarter = (payment.PaymentDate.Month - 1) / 3 + 1;
                csv.AppendLine($"{payment.Symbol}," +
                              $"{payment.PaymentDate:yyyy-MM-dd}," +
                              $"{payment.Amount:F4}," +
                              $"{payment.PaymentDate.Year}," +
                              $"Q{quarter}");
            }

            var fileName = string.IsNullOrEmpty(symbol)
                ? $"dividend_payments_all_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv"
                : $"dividend_payments_{symbol}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());

            return File(bytes, "text/csv", fileName);
        }

        /// <summary>
        /// Export API usage history to CSV
        /// Example: GET /api/dividends/export/usage/csv
        /// </summary>
        [HttpGet("export/usage/csv")]
        public async Task<IActionResult> ExportUsageToCsv()
        {
            var usage = await _dbContext.ApiUsageLogs
                .OrderByDescending(u => u.Date)
                .ToListAsync();

            var csv = new System.Text.StringBuilder();

            // Header
            csv.AppendLine("Date,Calls Used,Daily Limit,Remaining,Percentage Used (%),Notes");

            // Data rows
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

            var fileName = $"api_usage_export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
            var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());

            return File(bytes, "text/csv", fileName);
        }
    }
}
