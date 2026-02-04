using FinanceApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace FinanceApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StocksController : ControllerBase
    {
        private readonly StocksService _stocksService;
        private readonly ILogger<StocksController> _logger;

        public StocksController(StocksService stocksService, ILogger<StocksController> logger)
        {
            _stocksService = stocksService;
            _logger = logger;
        }

        /// <summary>
        /// Get all stocks - returns data from database (fast)
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetStocks()
        {
            var stocks = await _stocksService.GetAllStocksAsync();
            return Ok(stocks);
        }

        /// <summary>
        /// Get a single stock by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetStock(int id)
        {
            var stock = await _stocksService.GetStockByIdAsync(id);

            if (stock == null)
            {
                return NotFound(new { message = $"Stock with ID {id} not found" });
            }

            return Ok(stock);
        }

        /// <summary>
        /// Get live data for a specific stock by symbol (fetches from Yahoo)
        /// GET /api/stocks/live/AAPL
        /// </summary>
        [HttpGet("live/{symbol}")]
        public async Task<ActionResult<object>> GetLiveStock(string symbol)
        {
            var result = await _stocksService.GetLiveStockAsync(symbol);

            if (result == null)
            {
                return NotFound(new { message = $"Could not fetch live data for {symbol}" });
            }

            return Ok(result);
        }

        /// <summary>
        /// Refresh prices for all stocks in database
        /// POST /api/stocks/refresh
        /// </summary>
        [HttpPost("refresh")]
        public async Task<ActionResult<object>> RefreshAllStocks()
        {
            var result = await _stocksService.RefreshAllStocksAsync();
            return Ok(result);
        }

        /// <summary>
        /// Refresh a single stock by ID
        /// POST /api/stocks/refresh/1
        /// </summary>
        [HttpPost("refresh/{id}")]
        public async Task<ActionResult<object>> RefreshStock(int id)
        {
            var result = await _stocksService.RefreshStockByIdAsync(id);

            if (result == null)
            {
                return NotFound(new { message = $"Stock with ID {id} not found" });
            }

            if (result.Status == "Failed" || result.Status == "Error")
            {
                return BadRequest(new { message = result.Message });
            }

            return Ok(new
            {
                Message = "Stock refreshed successfully",
                Stock = result,
                IsLive = result.IsLive,
                Source = result.Source
            });
        }

        /// <summary>
        /// Bulk import multiple stocks by symbols
        /// POST /api/stocks/bulk with body { "symbols": ["AAPL", "MSFT", "TD", "RY"] }
        /// </summary>
        [HttpPost("bulk")]
        public async Task<ActionResult<object>> BulkAddStocks([FromBody] BulkAddStockRequest request)
        {
            if (request.Symbols == null || !request.Symbols.Any())
            {
                return BadRequest(new { message = "Symbols list is required" });
            }

            var result = await _stocksService.BulkAddStocksAsync(request.Symbols, request.AddCanadianSuffix);
            return Ok(result);
        }

        /// <summary>
        /// Add a new stock by symbol
        /// POST /api/stocks with body { "symbol": "GOOGL" }
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<object>> AddStock([FromBody] AddStockRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Symbol))
            {
                return BadRequest(new { message = "Symbol is required" });
            }

            var result = await _stocksService.AddStockAsync(request.Symbol);

            if (result == null)
            {
                return StatusCode(500, new { message = "Failed to add stock" });
            }

            if (result.Status == "Conflict")
            {
                return Conflict(new { message = result.Message });
            }

            if (result.Status == "Failed" || result.Status == "Error")
            {
                return BadRequest(new { message = result.Message });
            }

            return CreatedAtAction(nameof(GetStock), new { id = result.Id }, new
            {
                result.Id,
                result.Symbol,
                result.CompanyName,
                result.CurrentPrice,
                result.DividendYield,
                result.LastUpdated,
                Message = "Stock added successfully"
            });
        }

        /// <summary>
        /// Delete a stock by ID
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteStock(int id)
        {
            var stock = await _stocksService.GetStockByIdAsync(id);
            if (stock == null)
            {
                return NotFound(new { message = $"Stock with ID {id} not found" });
            }

            var deleted = await _stocksService.DeleteStockAsync(id);
            if (!deleted)
            {
                return StatusCode(500, new { message = "Failed to delete stock" });
            }

            return Ok(new { message = $"Stock {stock.Symbol} deleted successfully" });
        }

        /// <summary>
        /// Export all stocks to CSV
        /// GET /api/stocks/export/csv
        /// </summary>
        [HttpGet("export/csv")]
        public async Task<IActionResult> ExportToCsv()
        {
            var csv = await _stocksService.ExportToCsvAsync();
            var fileName = $"stocks_export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
            var bytes = System.Text.Encoding.UTF8.GetBytes(csv);

            return File(bytes, "text/csv", fileName);
        }
    }

    public class AddStockRequest
    {
        public string Symbol { get; set; } = string.Empty;
    }

    public class BulkAddStockRequest
    {
        public List<string> Symbols { get; set; } = new();
        public bool AddCanadianSuffix { get; set; } = true;
    }
}
