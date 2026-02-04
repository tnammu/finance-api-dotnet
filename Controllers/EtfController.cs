using Microsoft.AspNetCore.Mvc;
using FinanceApi.Services;

namespace FinanceApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EtfController : ControllerBase
    {
        private readonly EtfService _etfService;
        private readonly ILogger<EtfController> _logger;

        public EtfController(EtfService etfService, ILogger<EtfController> logger)
        {
            _etfService = etfService;
            _logger = logger;
        }

        /// <summary>
        /// Fetch ETF holdings data on-demand (no database storage)
        /// </summary>
        [HttpPost("analyze")]
        public async Task<ActionResult> AnalyzeEtfs([FromBody] EtfAnalysisRequest request)
        {
            try
            {
                if (request.Symbols == null || !request.Symbols.Any())
                {
                    return BadRequest(new { error = "No ETF symbols provided" });
                }

                var results = new List<object>();

                foreach (var symbol in request.Symbols)
                {
                    try
                    {
                        var etfData = await _etfService.FetchEtfHoldingsAsync(symbol);
                        if (etfData != null)
                        {
                            results.Add(etfData);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Error fetching data for {Symbol}: {Message}", symbol, ex.Message);
                        results.Add(new
                        {
                            success = false,
                            symbol = symbol,
                            error = ex.Message
                        });
                    }
                }

                return Ok(new
                {
                    success = true,
                    etfs = results
                });
            }
            catch (Exception ex)
            {
                _logger.LogError("Error analyzing ETFs: {Message}", ex.Message);
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }

    public class EtfAnalysisRequest
    {
        public List<string> Symbols { get; set; } = new List<string>();
    }
}
