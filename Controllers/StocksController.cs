using FinanceApi.Data;
using FinanceApi.Model;
using FinanceApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinanceApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StocksController : ControllerBase
    {
        private readonly FinanceContext _context;
        private readonly StockService _stockService;
        private readonly ILogger<StocksController> _logger;

        public StocksController(FinanceContext context, StockService stockService, ILogger<StocksController> logger)
        {
            _context = context;
            _stockService = stockService;
            _logger = logger;
        }

        /// <summary>
        /// Get all stocks - returns data from database (fast)
        /// Use the refresh endpoint to update prices
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetStocks()
        {
            var stocks = await _context.Stocks.ToListAsync();

            // Return stocks with indication of how old the data is
            var result = stocks.Select(s => new
            {
                s.Id,
                s.Symbol,
                s.CompanyName,
                s.Price,
                s.DividendYield,
                s.LastUpdated,
                MinutesOld = (int)(DateTime.UtcNow - s.LastUpdated).TotalMinutes,
                IsStale = (DateTime.UtcNow - s.LastUpdated).TotalMinutes > 15
            });

            return Ok(result);
        }

        /// <summary>
        /// Get a single stock by ID - returns from database
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<Stock>> GetStock(int id)
        {
            var stock = await _context.Stocks.FindAsync(id);

            if (stock == null)
            {
                return NotFound(new { message = $"Stock with ID {id} not found" });
            }

            return Ok(new
            {
                stock.Id,
                stock.Symbol,
                stock.CompanyName,
                stock.Price,
                stock.DividendYield,
                stock.LastUpdated,
                MinutesOld = (int)(DateTime.UtcNow - stock.LastUpdated).TotalMinutes
            });
        }

        /// <summary>
        /// Get live data for a specific stock by symbol (fetches from Yahoo)
        /// Example: GET /api/stocks/live/AAPL
        /// </summary>
        [HttpGet("live/{symbol}")]
        public async Task<ActionResult<object>> GetLiveStock(string symbol)
        {
            _logger.LogInformation($"Fetching live data for {symbol}");

            var liveInfo = await _stockService.GetLiveStockInfoAsync(symbol);

            if (!liveInfo.Price.HasValue)
            {
                return NotFound(new { message = $"Could not fetch live data for {symbol}" });
            }

            // Check if stock exists in database
            var stock = await _context.Stocks.FirstOrDefaultAsync(s => s.Symbol == symbol);

            if (stock != null)
            {
                // Update existing stock
                stock.Price = liveInfo.Price.Value;
                stock.CompanyName = liveInfo.CompanyName ?? stock.CompanyName;
                stock.DividendYield = liveInfo.DividendYield;
                stock.LastUpdated = DateTime.UtcNow;

                _context.Stocks.Update(stock);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Updated {symbol} in database");
            }

            return Ok(new
            {
                Symbol = symbol,
                CompanyName = liveInfo.CompanyName,
                Price = liveInfo.Price,
                DividendYield = liveInfo.DividendYield,
                FetchedAt = DateTime.UtcNow,
                IsLive = liveInfo.IsLive,
                Source = liveInfo.IsLive ? "Yahoo Finance (Live)" : "Cache"
            });
        }

        /// <summary>
        /// Refresh prices for all stocks in database (use sparingly - makes multiple API calls)
        /// Example: POST /api/stocks/refresh
        /// </summary>
        [HttpPost("refresh")]
        public async Task<ActionResult<object>> RefreshAllStocks()
        {
            var stocks = await _context.Stocks.ToListAsync();
            var results = new List<object>();
            var successCount = 0;
            var failCount = 0;

            foreach (var stock in stocks)
            {
                try
                {
                    // Add small delay to avoid rate limiting (500ms between requests)
                    await Task.Delay(500);

                    var liveInfo = await _stockService.GetLiveStockInfoAsync(stock.Symbol);

                    if (liveInfo.Price.HasValue)
                    {
                        stock.Price = liveInfo.Price.Value;
                        stock.CompanyName = liveInfo.CompanyName ?? stock.CompanyName;
                        stock.DividendYield = liveInfo.DividendYield;
                        stock.LastUpdated = DateTime.UtcNow;
                        successCount++;

                        results.Add(new
                        {
                            Symbol = stock.Symbol,
                            Status = "Updated",
                            Price = stock.Price
                        });
                    }
                    else
                    {
                        failCount++;
                        results.Add(new
                        {
                            Symbol = stock.Symbol,
                            Status = "Failed - No price data"
                        });
                    }
                }
                catch (Exception ex)
                {
                    failCount++;
                    _logger.LogError($"Error refreshing {stock.Symbol}: {ex.Message}");
                    results.Add(new
                    {
                        Symbol = stock.Symbol,
                        Status = "Error",
                        Error = ex.Message
                    });
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                TotalStocks = stocks.Count,
                SuccessCount = successCount,
                FailCount = failCount,
                UpdatedAt = DateTime.UtcNow,
                Results = results
            });
        }

        /// <summary>
        /// Refresh a single stock by ID
        /// Example: POST /api/stocks/refresh/1
        /// </summary>
        [HttpPost("refresh/{id}")]
        public async Task<ActionResult<object>> RefreshStock(int id)
        {
            var stock = await _context.Stocks.FindAsync(id);

            if (stock == null)
            {
                return NotFound(new { message = $"Stock with ID {id} not found" });
            }

            var liveInfo = await _stockService.GetLiveStockInfoAsync(stock.Symbol);

            if (!liveInfo.Price.HasValue)
            {
                return BadRequest(new { message = $"Could not fetch live data for {stock.Symbol}" });
            }

            stock.Price = liveInfo.Price.Value;
            stock.CompanyName = liveInfo.CompanyName ?? stock.CompanyName;
            stock.DividendYield = liveInfo.DividendYield;
            stock.LastUpdated = DateTime.UtcNow;

            _context.Stocks.Update(stock);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                Message = "Stock refreshed successfully",
                Stock = new
                {
                    stock.Id,
                    stock.Symbol,
                    stock.CompanyName,
                    stock.Price,
                    stock.DividendYield,
                    stock.LastUpdated
                },
                IsLive = liveInfo.IsLive
            });
        }

        /// <summary>
        /// Add a new stock by symbol
        /// Example: POST /api/stocks with body { "symbol": "GOOGL" }
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<Stock>> AddStock([FromBody] AddStockRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Symbol))
            {
                return BadRequest(new { message = "Symbol is required" });
            }

            var symbol = request.Symbol.ToUpper().Trim();

            // Check if already exists
            if (await _context.Stocks.AnyAsync(s => s.Symbol == symbol))
            {
                return Conflict(new { message = $"Stock {symbol} already exists" });
            }

            // Fetch live data
            var liveInfo = await _stockService.GetLiveStockInfoAsync(symbol);

            if (!liveInfo.Price.HasValue)
            {
                return BadRequest(new { message = $"Could not fetch data for {symbol}. Please verify the symbol is correct." });
            }

            var stock = new Stock
            {
                Symbol = symbol,
                CompanyName = liveInfo.CompanyName ?? symbol,
                Price = liveInfo.Price.Value,
                DividendYield = liveInfo.DividendYield,
                LastUpdated = DateTime.UtcNow
            };

            _context.Stocks.Add(stock);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetStock), new { id = stock.Id }, stock);
        }

        /// <summary>
        /// Delete a stock by ID
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteStock(int id)
        {
            var stock = await _context.Stocks.FindAsync(id);

            if (stock == null)
            {
                return NotFound(new { message = $"Stock with ID {id} not found" });
            }

            _context.Stocks.Remove(stock);
            await _context.SaveChangesAsync();

            return Ok(new { message = $"Stock {stock.Symbol} deleted successfully" });
        }
    }

    public class AddStockRequest
    {
        public string Symbol { get; set; } = string.Empty;
    }
}
