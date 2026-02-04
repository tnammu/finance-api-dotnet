using FinanceApi.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

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
            try
            {
                symbol = symbol.ToUpper();

                if (string.IsNullOrWhiteSpace(symbol))
                {
                    return BadRequest(new { error = "Symbol is required" });
                }

                if (capital <= 0)
                {
                    return BadRequest(new { error = "Capital must be greater than 0" });
                }

                if (years < 1 || years > 10)
                {
                    return BadRequest(new { error = "Years must be between 1 and 10" });
                }

                _logger.LogInformation($"Analyzing strategies for {symbol} with ${capital} over {years} years (enforce buy-first: {enforceBuyFirst})");

                var result = await _strategyService.AnalyzeStrategiesAsync(symbol, capital, years, enforceBuyFirst);

                if (result == null)
                {
                    return NotFound(new { error = $"Could not analyze strategies for {symbol}" });
                }

                // Convert JsonDocument to object for response
                var jsonString = result.RootElement.GetRawText();
                var response = JsonSerializer.Deserialize<object>(jsonString);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error analyzing strategies for {symbol}: {ex.Message}");
                return StatusCode(500, new { error = "Failed to analyze strategies" });
            }
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
            try
            {
                symbol = symbol.ToUpper();

                if (string.IsNullOrWhiteSpace(symbol))
                {
                    return BadRequest(new { error = "Symbol is required" });
                }

                if (string.IsNullOrWhiteSpace(strategyType))
                {
                    return BadRequest(new { error = "Strategy type is required" });
                }

                if (capital <= 0)
                {
                    return BadRequest(new { error = "Capital must be greater than 0" });
                }

                if (years < 1 || years > 10)
                {
                    return BadRequest(new { error = "Years must be between 1 and 10" });
                }

                _logger.LogInformation($"Analyzing {strategyType} strategy for {symbol} with ${capital} over {years} years");

                var result = await _strategyService.CalculateSingleStrategyAsync(
                    symbol,
                    strategyType,
                    capital,
                    years);

                if (result == null)
                {
                    return NotFound(new { error = $"Could not analyze {strategyType} strategy for {symbol}" });
                }

                // Convert JsonDocument to object for response
                var jsonString = result.RootElement.GetRawText();
                var response = JsonSerializer.Deserialize<object>(jsonString);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error analyzing {strategyType} strategy for {symbol}: {ex.Message}");
                return StatusCode(500, new { error = "Failed to analyze strategy" });
            }
        }

        /// <summary>
        /// Calculate returns for a strategy with different capital amounts
        /// POST: api/strategy/calculator
        /// Body: { "symbol": "AAPL", "strategyType": "rsi", "amounts": [100, 500, 1000], "years": 5 }
        /// </summary>
        [HttpPost("calculator")]
        public async Task<ActionResult<object>> CalculateReturns([FromBody] StrategyCalculatorRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.Symbol))
                {
                    return BadRequest(new { error = "Invalid request" });
                }

                var symbol = request.Symbol.ToUpper();
                var amounts = request.Amounts ?? new List<double> { 100, 500, 1000, 5000 };
                var years = request.Years ?? 5;
                var strategyType = request.StrategyType ?? "buyHold";

                _logger.LogInformation($"Calculating returns for {symbol} strategy {strategyType} with {amounts.Count} different amounts");

                var results = new List<object>();

                foreach (var amount in amounts)
                {
                    var analysis = await _strategyService.CalculateSingleStrategyAsync(
                        symbol,
                        strategyType,
                        amount,
                        years);

                    if (analysis != null && analysis.RootElement.TryGetProperty("strategy", out var strategyElement))
                    {
                        var strategyData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(strategyElement.GetRawText());

                        if (strategyData != null)
                        {
                            results.Add(new
                            {
                                capital = amount,
                                finalValue = strategyData.ContainsKey("finalValue") ? strategyData["finalValue"].GetDouble() : 0,
                                totalReturn = strategyData.ContainsKey("totalReturn") ? strategyData["totalReturn"].GetDouble() : 0,
                                profit = strategyData.ContainsKey("finalValue") ? strategyData["finalValue"].GetDouble() - amount : 0,
                                winRate = strategyData.ContainsKey("winRate") ? strategyData["winRate"].GetDouble() : 0
                            });
                        }
                    }
                }

                return Ok(new
                {
                    success = true,
                    symbol = symbol,
                    strategyType = strategyType,
                    years = years,
                    calculations = results
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error calculating returns: {ex.Message}");
                return StatusCode(500, new { error = "Failed to calculate returns" });
            }
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
