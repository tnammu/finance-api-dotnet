using Microsoft.AspNetCore.Mvc;
using FinanceApi.Services;

namespace FinanceApi.Controllers
{
    [ApiController]
    [Route("api/portfolio")]
    public class PortfolioController : ControllerBase
    {
        private readonly PortfolioService _portfolioService;

        public PortfolioController(PortfolioService portfolioService)
        {
            _portfolioService = portfolioService;
        }

        [HttpGet]
        public async Task<IActionResult> GetHoldings()
        {
            var result = await _portfolioService.GetHoldingsAsync();
            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> AddHolding([FromBody] AddHoldingRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Symbol))
                return BadRequest(new { error = "Symbol is required" });
            if (request.Shares <= 0)
                return BadRequest(new { error = "Shares must be greater than 0" });
            if (request.BuyPrice <= 0)
                return BadRequest(new { error = "Buy price must be greater than 0" });

            var holding = await _portfolioService.AddHoldingAsync(request);
            return Ok(new { message = "Holding added", holding });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateHolding(int id, [FromBody] AddHoldingRequest request)
        {
            var holding = await _portfolioService.UpdateHoldingAsync(id, request);
            if (holding == null)
                return NotFound(new { error = "Holding not found" });

            return Ok(new { message = "Holding updated", holding });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteHolding(int id)
        {
            var holding = await _portfolioService.DeleteHoldingAsync(id);
            if (holding == null)
                return NotFound(new { error = "Holding not found" });

            return Ok(new { message = $"Holding {holding.Symbol} deleted" });
        }
    }
}
