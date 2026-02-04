using FinanceApi.Services;
using FinanceApi.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinanceApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GrowthController : ControllerBase
    {
        private readonly GrowthStockService _growthService;
        private readonly DividendDbContext _dbContext;
        private readonly ILogger<GrowthController> _logger;

        public GrowthController(
            GrowthStockService growthService,
            DividendDbContext dbContext,
            ILogger<GrowthController> logger)
        {
            _growthService = growthService;
            _dbContext = dbContext;
            _logger = logger;
        }

        /// <summary>
        /// Analyze growth potential of a stock using 5 growth filters
        /// </summary>
        [HttpGet("analyze/{symbol}")]
        public async Task<ActionResult<object>> AnalyzeGrowthStock(string symbol, [FromQuery] bool refresh = false)
        {
            symbol = symbol.ToUpper();

            try
            {
                var cached = await _dbContext.DividendModels
                    .FirstOrDefaultAsync(d => d.Symbol == symbol);

                bool needsRefresh = cached == null || refresh || cached.GrowthScore == 0;

                if (needsRefresh)
                {
                    var result = await _growthService.AnalyzeGrowthStockAsync(symbol);
                    if (result == null)
                    {
                        return NotFound(new { error = $"Could not analyze {symbol}" });
                    }

                    cached = await _dbContext.DividendModels
                        .FirstOrDefaultAsync(d => d.Symbol == symbol);
                }

                if (cached == null)
                {
                    return NotFound(new { error = $"Stock {symbol} not found" });
                }

                return Ok(new
                {
                    symbol = cached.Symbol,
                    companyName = cached.CompanyName,
                    sector = cached.Sector,
                    industry = cached.Industry,
                    currentPrice = cached.CurrentPrice,
                    growthMetrics = new
                    {
                        revenueGrowth = cached.RevenueGrowth,
                        epsGrowthRate = cached.EPSGrowthRate,
                        pegRatio = cached.PEGRatio,
                        ruleOf40Score = cached.RuleOf40Score,
                        freeCashFlow = cached.FreeCashFlow,
                        growthScore = cached.GrowthScore,
                        growthRating = cached.GrowthRating
                    },
                    dividendMetrics = new
                    {
                        dividendYield = cached.DividendYield,
                        payoutRatio = cached.PayoutRatio,
                        consecutiveYears = cached.ConsecutiveYearsOfPayments,
                        safetyScore = cached.SafetyScore,
                        safetyRating = cached.SafetyRating
                    },
                    lastUpdated = cached.LastUpdated
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error analyzing {symbol}: {ex.Message}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get all stocks with growth scores, sorted by growth score
        /// </summary>
        [HttpGet("all")]
        public async Task<ActionResult<object>> GetAllGrowthStocks()
        {
            try
            {
                var stocks = await _growthService.GetAllGrowthStocksAsync();

                return Ok(new
                {
                    count = stocks.Count,
                    stocks = stocks
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching growth stocks: {ex.Message}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get top N growth stocks
        /// </summary>
        [HttpGet("top/{count}")]
        public async Task<ActionResult<object>> GetTopGrowthStocks(int count = 10)
        {
            try
            {
                if (count < 1 || count > 100)
                {
                    return BadRequest(new { error = "Count must be between 1 and 100" });
                }

                var stocks = await _growthService.GetTopGrowthStocksAsync(count);

                return Ok(new
                {
                    count = stocks.Count,
                    stocks = stocks
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching top growth stocks: {ex.Message}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Compare multiple stocks' growth metrics
        /// </summary>
        [HttpGet("compare")]
        public async Task<ActionResult<object>> CompareGrowthStocks([FromQuery] string symbols)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(symbols))
                {
                    return BadRequest(new { error = "Symbols parameter required (comma-separated)" });
                }

                var symbolList = symbols.ToUpper()
                    .Split(',')
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();

                if (symbolList.Count == 0)
                {
                    return BadRequest(new { error = "No valid symbols provided" });
                }

                var result = await _growthService.CompareGrowthStocksAsync(symbolList);

                return Ok(new
                {
                    found = result.Found,
                    stocks = result.Stocks,
                    missingSymbols = result.MissingSymbols
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error comparing growth stocks: {ex.Message}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get historical data for an ETF (default: 5 years)
        /// </summary>
        [HttpGet("etf-history/{symbol}")]
        public async Task<ActionResult<object>> GetEtfHistory(string symbol, [FromQuery] int years = 5)
        {
            try
            {
                symbol = symbol.ToUpper();

                if (years < 1 || years > 10)
                {
                    return BadRequest(new { error = "Years must be between 1 and 10" });
                }

                var result = await _growthService.GetEtfHistoryAsync(symbol, years);

                if (result == null)
                {
                    return StatusCode(500, new { error = "Failed to fetch ETF history" });
                }

                return Ok(result.RootElement);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching ETF history for {symbol}: {ex.Message}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get hourly analysis for an ETF
        /// </summary>
        [HttpGet("etf-hourly/{symbol}")]
        public async Task<ActionResult<object>> GetEtfHourlyAnalysis(string symbol, [FromQuery] int days = 730)
        {
            try
            {
                symbol = symbol.ToUpper();

                if (days < 1 || days > 730)
                {
                    return BadRequest(new { error = "Days must be between 1 and 730 (yfinance limitation)" });
                }

                var result = await _growthService.GetEtfHourlyAnalysisAsync(symbol, days);

                if (result == null)
                {
                    return StatusCode(500, new { error = "Failed to fetch hourly analysis" });
                }

                return Ok(result.RootElement);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching hourly analysis for {symbol}: {ex.Message}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get intraday/historical data for an ETF with time frame support
        /// </summary>
        [HttpGet("etf-intraday/{symbol}")]
        public async Task<ActionResult<object>> GetEtfIntradayData(string symbol, [FromQuery] string period = "1d")
        {
            try
            {
                symbol = symbol.ToUpper();

                var validPeriods = new[] { "1d", "5d", "1wk", "1mo", "ytd", "1y", "5y" };
                if (!validPeriods.Contains(period.ToLower()))
                {
                    return BadRequest(new { error = "Invalid period. Supported: 1d, 5d, 1wk, 1mo, ytd, 1y, 5y" });
                }

                var result = await _growthService.GetEtfIntradayDataAsync(symbol, period);

                if (result == null)
                {
                    return StatusCode(500, new { error = "Failed to fetch intraday data" });
                }

                return Ok(result.RootElement);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching intraday data for {symbol}: {ex.Message}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get detailed PIP trading strategy for a stock
        /// </summary>
        [HttpGet("strategy/{symbol}")]
        public async Task<ActionResult<object>> GetStockStrategy(
            string symbol,
            [FromQuery] double capital = 1000,
            [FromQuery] int years = 3)
        {
            try
            {
                symbol = symbol.ToUpper();

                if (capital < 100 || capital > 1000000)
                {
                    return BadRequest(new { error = "Capital must be between $100 and $1,000,000" });
                }

                if (years < 1 || years > 10)
                {
                    return BadRequest(new { error = "Years must be between 1 and 10" });
                }

                var result = await _growthService.GetStockStrategyAsync(symbol, capital, years);

                if (result == null)
                {
                    return StatusCode(500, new { error = "Failed to calculate strategy" });
                }

                return Ok(result.RootElement);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error calculating strategy for {symbol}: {ex.Message}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Analyze all cached stocks for growth potential
        /// </summary>
        [HttpPost("analyze-all")]
        public async Task<ActionResult<object>> AnalyzeAllCachedStocks()
        {
            try
            {
                var result = await _growthService.AnalyzeAllCachedStocksAsync();

                return Ok(new
                {
                    message = "Bulk growth analysis complete",
                    totalStocks = result.TotalStocks,
                    processed = result.SuccessCount + result.FailedCount,
                    success = result.SuccessCount,
                    failed = result.FailedCount,
                    errors = result.Errors.Take(10).ToList()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in bulk growth analysis: {ex.Message}");
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}
