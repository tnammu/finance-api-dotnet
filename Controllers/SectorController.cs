using Microsoft.AspNetCore.Mvc;
using FinanceApi.Services;

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

        [HttpGet("performances")]
        public async Task<IActionResult> GetAllSectorPerformances([FromQuery] bool refresh = false)
        {
            try
            {
                var performances = await _sectorService.GetOrCalculateAllSectorPerformancesAsync(refresh);
                return Ok(new
                {
                    success = true,
                    sectorCount = performances.Count,
                    sectors = performances,
                    calculatedAt = performances.FirstOrDefault()?.CalculatedAt ?? DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting sector performances");
                return StatusCode(500, new { error = "Failed to get sector performances", details = ex.Message });
            }
        }

        [HttpGet("performance/{sector}")]
        public async Task<IActionResult> GetSectorPerformance(string sector, [FromQuery] string period = "current")
        {
            try
            {
                var performance = await _sectorService.GetOrCalculateSectorPerformanceAsync(sector, period);
                if (performance == null)
                    return NotFound(new { error = $"Sector '{sector}' not found" });

                return Ok(new { success = true, sector = performance });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting performance for sector {Sector}", sector);
                return StatusCode(500, new { error = "Failed to get sector performance", details = ex.Message });
            }
        }

        [HttpGet("comparison/{symbol}")]
        public async Task<IActionResult> GetStockSectorComparison(string symbol)
        {
            try
            {
                var comparison = await _sectorService.GetStockSectorComparison(symbol)
                    ?? await _sectorService.CalculateStockSectorComparison(symbol);

                if (comparison == null)
                    return NotFound(new { error = $"Stock '{symbol}' not found or has no sector" });

                return Ok(new { success = true, comparison });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting comparison for {Symbol}", symbol);
                return StatusCode(500, new { error = "Failed to get stock sector comparison", details = ex.Message });
            }
        }

        [HttpPost("comparison/{symbol}/refresh")]
        public async Task<IActionResult> RefreshStockSectorComparison(string symbol)
        {
            try
            {
                var comparison = await _sectorService.CalculateStockSectorComparison(symbol);
                if (comparison == null)
                    return NotFound(new { error = $"Stock '{symbol}' not found or has no sector" });

                return Ok(new { success = true, message = $"Refreshed sector comparison for {symbol}", comparison });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing comparison for {Symbol}", symbol);
                return StatusCode(500, new { error = "Failed to refresh stock sector comparison", details = ex.Message });
            }
        }

        [HttpGet("top-performers/{sector}")]
        public async Task<IActionResult> GetTopPerformers(string sector, [FromQuery] int limit = 10)
        {
            try
            {
                var performances = await _sectorService.GetOrCalculateAllSectorPerformancesAsync();
                var sectorPerformance = performances.FirstOrDefault(p => p.Sector == sector);

                if (sectorPerformance == null)
                    return NotFound(new { error = $"Sector '{sector}' not found" });

                return Ok(new
                {
                    success = true,
                    sector,
                    sectorPerformance,
                    topStocks = new List<object>(),
                    message = "Detailed stock rankings coming soon - use /comparison/{symbol} for individual stocks"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting top performers for sector {Sector}", sector);
                return StatusCode(500, new { error = "Failed to get top performers", details = ex.Message });
            }
        }

        [HttpGet("summary")]
        public async Task<IActionResult> GetSectorSummary()
        {
            try
            {
                var summary = await _sectorService.GetSectorSummaryAsync();
                var performances = await _sectorService.GetAllSectorPerformances();
                return Ok(new { success = true, summary, sectors = performances });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting sector summary");
                return StatusCode(500, new { error = "Failed to get sector summary", details = ex.Message });
            }
        }
    }
}
