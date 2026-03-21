using FinanceApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace FinanceApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StrategyController : ControllerBase
    {
        private readonly StrategyService _strategyService;
        private readonly ILogger<StrategyController> _logger;

        public StrategyController(
            StrategyService strategyService,
            ILogger<StrategyController> logger)
        {
            _strategyService = strategyService;
            _logger = logger;
        }

        /// <summary>
        /// Get all available trading strategies
        /// GET: api/strategy/list
        /// </summary>
        [HttpGet("list")]
        public ActionResult<object> GetAvailableStrategies()
        {
            try
            {
                var strategies = _strategyService.GetAvailableStrategies();

                return Ok(new
                {
                    success = true,
                    count = strategies.Count,
                    strategies = strategies
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting strategy list: {ex.Message}");
                return StatusCode(500, new { error = "Failed to retrieve strategy list" });
            }
        }

        /// <summary>
        /// Analyze all trading strategies for a stock
        /// GET: api/strategy/analyze/AAPL?capital=100&years=5&enforceBuyFirst=true
        /// </summary>
        [HttpGet("analyze/{symbol}")]
        public async Task<ActionResult<object>> AnalyzeAllStrategies(
            string symbol,
            [FromQuery] double capital = 100,
            [FromQuery] int years = 5,
            [FromQuery] bool enforceBuyFirst = true)
        {
            if (capital <= 0)     return BadRequest(new { error = "Capital must be greater than 0" });
            if (years < 1 || years > 10) return BadRequest(new { error = "Years must be between 1 and 10" });

            var result = await _strategyService.AnalyzeStrategiesAsync(symbol, capital, years, enforceBuyFirst);
            if (result == null) return NotFound(new { error = $"Could not analyze strategies for {symbol}" });

            return Ok(result.RootElement);
        }

        /// <summary>
        /// Analyze a single trading strategy for a stock
        /// GET: api/strategy/single/AAPL/rsi?capital=100&years=5
        /// </summary>
        [HttpGet("single/{symbol}/{strategyType}")]
        public async Task<ActionResult<object>> AnalyzeSingleStrategy(
            string symbol,
            string strategyType,
            [FromQuery] double capital = 100,
            [FromQuery] int years = 5)
        {
            if (capital <= 0)     return BadRequest(new { error = "Capital must be greater than 0" });
            if (years < 1 || years > 10) return BadRequest(new { error = "Years must be between 1 and 10" });

            var result = await _strategyService.CalculateSingleStrategyAsync(symbol, strategyType, capital, years);
            if (result == null) return NotFound(new { error = $"Could not analyze {strategyType} strategy for {symbol}" });

            return Ok(result.RootElement);
        }

        /// <summary>
        /// Calculate returns for a strategy with different capital amounts
        /// POST: api/strategy/calculator
        /// Body: { "symbol": "AAPL", "strategyType": "rsi", "amounts": [100, 500, 1000], "years": 5 }
        /// </summary>
        [HttpPost("calculator")]
        public async Task<ActionResult<object>> CalculateReturns([FromBody] StrategyCalculatorRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Symbol))
                return BadRequest(new { error = "Invalid request" });

            var result = await _strategyService.CalculateMultiCapitalAsync(
                request.Symbol, request.StrategyType, request.Amounts, request.Years);

            return Ok(result);
        }
    }

    /// <summary>
    /// Request model for strategy calculator
    /// </summary>
    public class StrategyCalculatorRequest
    {
        public string Symbol { get; set; } = string.Empty;
        public string? StrategyType { get; set; }
        public List<double>? Amounts { get; set; }
        public int? Years { get; set; }
    }
}
