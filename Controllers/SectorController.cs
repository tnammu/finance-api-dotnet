using Microsoft.AspNetCore.Mvc;
using FinanceApi.Services;
using FinanceApi.Models;

namespace FinanceApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SectorController : ControllerBase
    {
        private readonly SectorAnalysisService _sectorService;
        private readonly ILogger<SectorController> _logger;

        public SectorController(SectorAnalysisService sectorService, ILogger<SectorController> logger)
        {
            _sectorService = sectorService;
            _logger = logger;
        }

        /// <summary>
        /// Calculate and get all sector performances
        /// </summary>
        [HttpGet("performances")]
        public async Task<IActionResult> GetAllSectorPerformances([FromQuery] bool refresh = false)
        {
            try
            {
                List<SectorPerformance> performances;

                if (refresh)
                {
                    // Recalculate all sector performances
                    performances = await _sectorService.CalculateAllSectorPerformances();
                    _logger.LogInformation($"Recalculated performances for {performances.Count} sectors");
                }
                else
                {
                    // Get cached performances
                    performances = await _sectorService.GetAllSectorPerformances();

                    // If no cached data, calculate
                    if (!performances.Any())
                    {
                        performances = await _sectorService.CalculateAllSectorPerformances();
                        _logger.LogInformation($"Calculated initial performances for {performances.Count} sectors");
                    }
                }

                return Ok(new
                {
                    success = true,
                    sectorCount = performances.Count,
                    sectors = performances.OrderByDescending(p => p.AverageReturn).ToList(),
                    calculatedAt = performances.FirstOrDefault()?.CalculatedAt ?? DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting sector performances");
                return StatusCode(500, new { error = "Failed to get sector performances", details = ex.Message });
            }
        }

        /// <summary>
        /// Get performance for a specific sector
        /// </summary>
        [HttpGet("performance/{sector}")]
        public async Task<IActionResult> GetSectorPerformance(string sector, [FromQuery] string period = "current")
        {
            try
            {
                var performance = await _sectorService.GetSectorPerformance(sector, period);

                if (performance == null)
                {
                    // Calculate if not found
                    await _sectorService.CalculateAllSectorPerformances(period);
                    performance = await _sectorService.GetSectorPerformance(sector, period);

                    if (performance == null)
                    {
                        return NotFound(new { error = $"Sector '{sector}' not found" });
                    }
                }

                return Ok(new
                {
                    success = true,
                    sector = performance
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting performance for sector {sector}");
                return StatusCode(500, new { error = $"Failed to get sector performance", details = ex.Message });
            }
        }

        /// <summary>
        /// Get stock vs sector comparison for a specific symbol
        /// </summary>
        [HttpGet("comparison/{symbol}")]
        public async Task<IActionResult> GetStockSectorComparison(string symbol)
        {
            try
            {
                var comparison = await _sectorService.GetStockSectorComparison(symbol);

                if (comparison == null)
                {
                    // Calculate if not found
                    comparison = await _sectorService.CalculateStockSectorComparison(symbol);

                    if (comparison == null)
                    {
                        return NotFound(new { error = $"Stock '{symbol}' not found or has no sector" });
                    }
                }

                return Ok(new
                {
                    success = true,
                    comparison = comparison
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting comparison for {symbol}");
                return StatusCode(500, new { error = $"Failed to get stock sector comparison", details = ex.Message });
            }
        }

        /// <summary>
        /// Recalculate stock vs sector comparison for a specific symbol
        /// </summary>
        [HttpPost("comparison/{symbol}/refresh")]
        public async Task<IActionResult> RefreshStockSectorComparison(string symbol)
        {
            try
            {
                var comparison = await _sectorService.CalculateStockSectorComparison(symbol);

                if (comparison == null)
                {
                    return NotFound(new { error = $"Stock '{symbol}' not found or has no sector" });
                }

                return Ok(new
                {
                    success = true,
                    message = $"Refreshed sector comparison for {symbol}",
                    comparison = comparison
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error refreshing comparison for {symbol}");
                return StatusCode(500, new { error = $"Failed to refresh stock sector comparison", details = ex.Message });
            }
        }

        /// <summary>
        /// Get top performing stocks in a sector
        /// </summary>
        [HttpGet("top-performers/{sector}")]
        public async Task<IActionResult> GetTopPerformers(string sector, [FromQuery] int limit = 10)
        {
            try
            {
                // First ensure sector comparison data exists
                await _sectorService.CalculateAllSectorPerformances();

                var performances = await _sectorService.GetAllSectorPerformances();
                var sectorPerformance = performances.FirstOrDefault(p => p.Sector == sector);

                if (sectorPerformance == null)
                {
                    return NotFound(new { error = $"Sector '{sector}' not found" });
                }

                // Get stock comparisons for this sector - we need to calculate them all first
                var topStocks = new List<object>();

                return Ok(new
                {
                    success = true,
                    sector = sector,
                    sectorPerformance = sectorPerformance,
                    topStocks = topStocks,
                    message = "Detailed stock rankings coming soon - use /comparison/{symbol} for individual stocks"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting top performers for sector {sector}");
                return StatusCode(500, new { error = $"Failed to get top performers", details = ex.Message });
            }
        }

        /// <summary>
        /// Get sector summary statistics
        /// </summary>
        [HttpGet("summary")]
        public async Task<IActionResult> GetSectorSummary()
        {
            try
            {
                var performances = await _sectorService.GetAllSectorPerformances();

                if (!performances.Any())
                {
                    performances = await _sectorService.CalculateAllSectorPerformances();
                }

                var summary = new
                {
                    totalSectors = performances.Count,
                    bestPerformingSector = performances.OrderByDescending(p => p.AverageReturn).FirstOrDefault(),
                    worstPerformingSector = performances.OrderBy(p => p.AverageReturn).FirstOrDefault(),
                    highestYieldSector = performances.OrderByDescending(p => p.AverageDividendYield).FirstOrDefault(),
                    lowestVolatilitySector = performances.OrderBy(p => p.Volatility).FirstOrDefault(),
                    totalMarketCap = performances.Sum(p => p.TotalMarketCap),
                    totalStocks = performances.Sum(p => p.StockCount),
                    calculatedAt = performances.FirstOrDefault()?.CalculatedAt ?? DateTime.UtcNow
                };

                return Ok(new
                {
                    success = true,
                    summary = summary,
                    sectors = performances
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting sector summary");
                return StatusCode(500, new { error = "Failed to get sector summary", details = ex.Message });
            }
        }
    }
}
