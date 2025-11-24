using Microsoft.AspNetCore.Mvc;
using FinanceApi.Services;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using System.Linq;
using System.Diagnostics;
using System.Text.Json;
using System.IO;

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
                _logger.LogInformation($"📊 Comparing {symbol} vs S&P 500 for {period} year(s)");

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
                _logger.LogInformation($"📊 Comparing portfolio vs S&P 500 for {period} year(s)");

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
                _logger.LogInformation($"📈 Fetching historical data for {symbol} ({period} year(s))");

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
                _logger.LogInformation($"🐍 Running Python script for {period} year(s) performance comparison");

                var scriptPath = Path.Combine(Directory.GetCurrentDirectory(), "scripts", "sp500_performance_comparison.py");

                if (!System.IO.File.Exists(scriptPath))
                {
                    return NotFound(new { error = "Python script not found" });
                }

                var processInfo = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = $"\"{scriptPath}\" -p {period}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.Combine(Directory.GetCurrentDirectory(), "scripts")
                };

                using (var process = Process.Start(processInfo))
                {
                    if (process == null)
                    {
                        return StatusCode(500, new { error = "Failed to start Python process" });
                    }

                    var output = await process.StandardOutput.ReadToEndAsync();
                    var error = await process.StandardError.ReadToEndAsync();
                    await process.WaitForExitAsync();

                    if (process.ExitCode != 0)
                    {
                        _logger.LogError($"Python script failed: {error}");
                        return StatusCode(500, new { error = "Python script execution failed", details = error });
                    }

                    // Find the most recent JSON file
                    var scriptsDir = Path.Combine(Directory.GetCurrentDirectory(), "scripts");
                    var jsonFiles = Directory.GetFiles(scriptsDir, "performance_comparison_*.json")
                        .OrderByDescending(f => System.IO.File.GetLastWriteTime(f))
                        .FirstOrDefault();

                    if (jsonFiles == null)
                    {
                        return NotFound(new { error = "Performance data file not found" });
                    }

                    var jsonContent = await System.IO.File.ReadAllTextAsync(jsonFiles);
                    var data = JsonSerializer.Deserialize<JsonElement>(jsonContent);

                    return Ok(data);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error running Python script: {ex.Message}");
                return StatusCode(500, new { error = "Failed to run performance analysis", details = ex.Message });
            }
        }
    }
}
