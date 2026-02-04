using Microsoft.AspNetCore.Mvc;
using FinanceApi.Services;

namespace FinanceApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PerformanceController : ControllerBase
    {
        private readonly PerformanceComparisonService _performanceService;
        private readonly ILogger<PerformanceController> _logger;

        public PerformanceController(PerformanceComparisonService performanceService, ILogger<PerformanceController> logger)
        {
            _performanceService = performanceService;
            _logger = logger;
        }

        /// <summary>
        /// Compare a single stock's performance against S&P 500
        /// GET /api/performance/compare/{symbol}?period=1
        /// </summary>
        [HttpGet("compare/{symbol}")]
        public async Task<ActionResult<object>> CompareStock(string symbol, [FromQuery] int period = 1)
        {
            try
            {
                var comparison = await _performanceService.CompareStockToSP500Async(symbol, period);

                if (comparison == null)
                {
                    return NotFound(new { error = $"Could not fetch performance data for {symbol}" });
                }

                return Ok(comparison);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error comparing {symbol}: {ex.Message}");
                return StatusCode(500, new { error = "Failed to compare performance" });
            }
        }

        /// <summary>
        /// Compare entire portfolio's performance against S&P 500
        /// GET /api/performance/portfolio?period=1
        /// </summary>
        [HttpGet("portfolio")]
        public async Task<ActionResult<object>> ComparePortfolio([FromQuery] int period = 1)
        {
            try
            {
                var comparison = await _performanceService.ComparePortfolioToSP500Async(period);

                if (comparison == null)
                {
                    return NotFound(new { error = "Could not fetch portfolio performance data" });
                }

                return Ok(comparison);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error comparing portfolio: {ex.Message}");
                return StatusCode(500, new { error = "Failed to compare portfolio performance" });
            }
        }

        /// <summary>
        /// Get historical price data for chart visualization
        /// GET /api/performance/historical/{symbol}?period=1
        /// </summary>
        [HttpGet("historical/{symbol}")]
        public async Task<ActionResult<object>> GetHistoricalData(string symbol, [FromQuery] int period = 1)
        {
            try
            {
                var data = await _performanceService.GetHistoricalPriceDataAsync(symbol, period);

                if (data == null)
                {
                    return NotFound(new { error = $"Could not fetch historical data for {symbol}" });
                }

                return Ok(data);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching historical data for {symbol}: {ex.Message}");
                return StatusCode(500, new { error = "Failed to fetch historical data" });
            }
        }

        /// <summary>
        /// Run Python script to fetch performance data (workaround for rate limiting)
        /// GET /api/performance/python-portfolio?period=1
        /// </summary>
        [HttpGet("python-portfolio")]
        public async Task<ActionResult<object>> GetPythonPortfolioPerformance([FromQuery] int period = 1)
        {
            try
            {
                var data = await _performanceService.GetPythonPortfolioPerformanceAsync(period);

                if (data == null)
                {
                    return NotFound(new { error = "No performance data returned from Python script" });
                }

                return Ok(data);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error running Python script: {ex.Message}");
                return StatusCode(500, new { error = "Failed to run performance analysis", details = ex.Message });
            }
        }
    }
}
