using Microsoft.AspNetCore.Mvc;
using FinanceApi.Services;

namespace FinanceApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SP500Controller : ControllerBase
    {
        private readonly SP500Service _sp500Service;
        private readonly ILogger<SP500Controller> _logger;

        public SP500Controller(SP500Service sp500Service, ILogger<SP500Controller> logger)
        {
            _sp500Service = sp500Service;
            _logger = logger;
        }

        [HttpGet("monthly-growth")]
        public async Task<ActionResult> GetMonthlyGrowth([FromQuery] int years = 5)
        {
            try
            {
                // Validate years parameter (1-20)
                if (years < 1 || years > 20)
                {
                    return BadRequest(new { error = "Years parameter must be between 1 and 20" });
                }

                var data = await _sp500Service.FetchMonthlyGrowthAsync(years);
                return Ok(data);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error fetching S&P 500 monthly growth: {Message}", ex.Message);
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}
