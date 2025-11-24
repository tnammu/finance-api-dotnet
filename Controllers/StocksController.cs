using FinanceApi.Data;
using FinanceApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace FinanceApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StocksController : ControllerBase
    {
        private readonly DividendDbContext _context;
        private readonly ILogger<StocksController> _logger;

        public StocksController(DividendDbContext context, ILogger<StocksController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Get all stocks - returns data from database (fast)
        /// Use the refresh endpoint to update prices
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetStocks()
        {
            var stocks = await _context.DividendModels.ToListAsync();

            // Return stocks with indication of how old the data is
            var result = stocks.Select(s => new
            {
                s.Id,
                s.Symbol,
                s.CompanyName,
                s.CurrentPrice,
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
        public async Task<ActionResult<DividendModel>> GetStock(int id)
        {
            var stock = await _context.DividendModels.FindAsync(id);

            if (stock == null)
            {
                return NotFound(new { message = $"Stock with ID {id} not found" });
            }

            return Ok(new
            {
                stock.Id,
                stock.Symbol,
                stock.CompanyName,
                stock.CurrentPrice,
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

            // Use Python script to fetch/update stock data
            var success = await FetchStockDataViaPython(symbol.ToUpper());

            if (!success)
            {
                return NotFound(new { message = $"Could not fetch live data for {symbol}" });
            }

            // Reload from database after Python script updates it
            var stock = await _context.DividendModels
                .Include(d => d.DividendPayments)
                .Include(d => d.YearlyDividends)
                .FirstOrDefaultAsync(s => s.Symbol == symbol.ToUpper());

            if (stock == null)
            {
                return NotFound(new { message = $"Stock {symbol} was fetched but not saved to database" });
            }

            return Ok(new
            {
                stock.Symbol,
                stock.CompanyName,
                Price = stock.CurrentPrice,
                stock.DividendYield,
                FetchedAt = stock.LastUpdated,
                IsLive = true,
                Source = "Yahoo Finance (Python Script)"
            });
        }

        /// <summary>
        /// Refresh prices for all stocks in database (use sparingly - makes multiple API calls)
        /// Example: POST /api/stocks/refresh
        /// </summary>
        [HttpPost("refresh")]
        public async Task<ActionResult<object>> RefreshAllStocks()
        {
            var stocks = await _context.DividendModels.ToListAsync();
            var results = new List<object>();
            var successCount = 0;
            var failCount = 0;

            foreach (var stock in stocks)
            {
                try
                {
                    // Add small delay to avoid rate limiting (500ms between requests)
                    await Task.Delay(500);

                    // Use Python script to refresh stock data
                    var success = await FetchStockDataViaPython(stock.Symbol);

                    if (success)
                    {
                        // Reload updated stock from database
                        var updatedStock = await _context.DividendModels.FirstOrDefaultAsync(s => s.Symbol == stock.Symbol);
                        if (updatedStock != null)
                        {
                            successCount++;
                            results.Add(new
                            {
                                Symbol = updatedStock.Symbol,
                                Status = "Updated",
                                Price = updatedStock.CurrentPrice
                            });
                        }
                        else
                        {
                            failCount++;
                            results.Add(new
                            {
                                Symbol = stock.Symbol,
                                Status = "Failed",
                                Message = "Stock updated but not found in database"
                            });
                        }
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
            var stock = await _context.DividendModels.FindAsync(id);

            if (stock == null)
            {
                return NotFound(new { message = $"Stock with ID {id} not found" });
            }

            // Use Python script to refresh stock data
            var success = await FetchStockDataViaPython(stock.Symbol);

            if (!success)
            {
                return BadRequest(new { message = $"Could not fetch live data for {stock.Symbol}" });
            }

            // Reload updated stock from database
            var updatedStock = await _context.DividendModels.FindAsync(id);
            if (updatedStock == null)
            {
                return StatusCode(500, new { message = $"Stock updated but not found in database" });
            }

            return Ok(new
            {
                Message = "Stock refreshed successfully",
                Stock = new
                {
                    updatedStock.Id,
                    updatedStock.Symbol,
                    updatedStock.CompanyName,
                    updatedStock.CurrentPrice,
                    updatedStock.DividendYield,
                    updatedStock.LastUpdated
                },
                IsLive = true,
                Source = "Yahoo Finance (Python Script)"
            });
        }

        /// <summary>
        /// Bulk import multiple stocks by symbols
        /// Example: POST /api/stocks/bulk with body { "symbols": ["AAPL", "MSFT", "TD", "RY"] }
        /// </summary>
        [HttpPost("bulk")]
        public async Task<ActionResult<object>> BulkAddStocks([FromBody] BulkAddStockRequest request)
        {
            if (request.Symbols == null || !request.Symbols.Any())
            {
                return BadRequest(new { message = "Symbols list is required" });
            }

            var results = new List<object>();
            var successCount = 0;
            var skipCount = 0;
            var failCount = 0;

            _logger.LogInformation($"📦 Bulk import started: {request.Symbols.Count} symbols");

            foreach (var symbolInput in request.Symbols)
            {
                try
                {
                    var symbol = symbolInput.ToUpper().Trim();

                    // Auto-add .TO for Canadian stocks if not present
                    if (request.AddCanadianSuffix && !symbol.Contains(".TO") && !symbol.Contains("."))
                    {
                        symbol = $"{symbol}.TO";
                        _logger.LogInformation($"  🇨🇦 Added .TO suffix: {symbolInput} → {symbol}");
                    }

                    // Check if already exists
                    if (await _context.DividendModels.AnyAsync(s => s.Symbol == symbol))
                    {
                        skipCount++;
                        results.Add(new
                        {
                            Symbol = symbol,
                            Status = "Skipped",
                            Message = "Already exists"
                        });
                        continue;
                    }

                    // Add delay to respect rate limits (500ms = max 2 per second, 120 per minute)
                    await Task.Delay(500);

                    // Fetch data using Python/Yahoo Finance
                    var success = await FetchStockDataViaPython(symbol);

                    if (!success)
                    {
                        failCount++;
                        results.Add(new
                        {
                            Symbol = symbol,
                            Status = "Failed",
                            Message = "Could not fetch data - symbol may be invalid"
                        });
                        continue;
                    }

                    // Reload from database after Python script updates it
                    var stock = await _context.DividendModels
                        .Include(d => d.DividendPayments)
                        .Include(d => d.YearlyDividends)
                        .FirstOrDefaultAsync(s => s.Symbol == symbol);

                    if (stock == null)
                    {
                        failCount++;
                        results.Add(new
                        {
                            Symbol = symbol,
                            Status = "Failed",
                            Message = "Stock was fetched but not saved to database"
                        });
                        continue;
                    }

                    successCount++;
                    results.Add(new
                    {
                        Symbol = symbol,
                        Status = "Added",
                        CompanyName = stock.CompanyName,
                        Price = stock.CurrentPrice
                    });

                    _logger.LogInformation($"  ✓ Added {symbol} - ${stock.CurrentPrice}");
                }
                catch (Exception ex)
                {
                    failCount++;
                    _logger.LogError($"  ✗ Error adding {symbolInput}: {ex.Message}");
                    results.Add(new
                    {
                        Symbol = symbolInput,
                        Status = "Error",
                        Message = ex.Message
                    });
                }
            }

            return Ok(new
            {
                TotalSubmitted = request.Symbols.Count,
                SuccessCount = successCount,
                SkippedCount = skipCount,
                FailCount = failCount,
                CompletedAt = DateTime.UtcNow,
                Results = results
            });
        }

        /// <summary>
        /// Add a new stock by symbol using Python/Yahoo Finance
        /// Example: POST /api/stocks with body { "symbol": "GOOGL" }
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<DividendModel>> AddStock([FromBody] AddStockRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Symbol))
            {
                return BadRequest(new { message = "Symbol is required" });
            }

            var symbol = request.Symbol.ToUpper().Trim();

            // Check if already exists
            if (await _context.DividendModels.AnyAsync(s => s.Symbol == symbol))
            {
                return Conflict(new { message = $"Stock {symbol} already exists" });
            }

            // Fetch data using Python/Yahoo Finance (same as Dividend Analysis)
            var success = await FetchStockDataViaPython(symbol);

            if (!success)
            {
                return BadRequest(new { message = $"Could not fetch data for {symbol}. Please verify the symbol is correct." });
            }

            // Reload from database after Python script updates it
            var stock = await _context.DividendModels
                .Include(d => d.DividendPayments)
                .Include(d => d.YearlyDividends)
                .FirstOrDefaultAsync(s => s.Symbol == symbol);

            if (stock == null)
            {
                return StatusCode(500, new { message = $"Stock {symbol} was fetched but not saved to database" });
            }

            // Return simplified response (avoid circular reference issues with navigation properties)
            return CreatedAtAction(nameof(GetStock), new { id = stock.Id }, new
            {
                stock.Id,
                stock.Symbol,
                stock.CompanyName,
                stock.CurrentPrice,
                stock.DividendYield,
                stock.LastUpdated,
                Message = "Stock added successfully"
            });
        }

        /// <summary>
        /// Delete a stock by ID
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteStock(int id)
        {
            var stock = await _context.DividendModels.FindAsync(id);

            if (stock == null)
            {
                return NotFound(new { message = $"Stock with ID {id} not found" });
            }

            _context.DividendModels.Remove(stock);
            await _context.SaveChangesAsync();

            return Ok(new { message = $"Stock {stock.Symbol} deleted successfully" });
        }

        /// <summary>
        /// Export all stocks to CSV
        /// Example: GET /api/stocks/export/csv
        /// </summary>
        [HttpGet("export/csv")]
        public async Task<IActionResult> ExportToCsv()
        {
            var stocks = await _context.DividendModels.OrderBy(s => s.Symbol).ToListAsync();

            var csv = new System.Text.StringBuilder();

            // Header
            csv.AppendLine("Symbol,Company Name,Price,Dividend Yield (%),Last Updated,Data Age (minutes)");

            // Data rows
            foreach (var stock in stocks)
            {
                var minutesOld = (int)(DateTime.UtcNow - stock.LastUpdated).TotalMinutes;
                var dividendYield = stock.DividendYield.HasValue ? stock.DividendYield.Value.ToString("F2") : "";

                csv.AppendLine($"{stock.Symbol}," +
                              $"\"{stock.CompanyName}\"," +
                              $"{stock.CurrentPrice:F2}," +
                              $"{dividendYield}," +
                              $"{stock.LastUpdated:yyyy-MM-dd HH:mm:ss}," +
                              $"{minutesOld}");
            }

            var fileName = $"stocks_export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
            var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());

            return File(bytes, "text/csv", fileName);
        }

        /// <summary>
        /// Call Python script to fetch stock data from Yahoo Finance
        /// Automatically tries .TO suffix for Canadian stocks if initial fetch fails
        /// </summary>
        private async Task<bool> FetchStockDataViaPython(string symbol)
        {
            try
            {
                var scriptPath = Path.Combine(Directory.GetCurrentDirectory(), "scripts", "update_stocks_from_yahoo.py");

                if (!System.IO.File.Exists(scriptPath))
                {
                    _logger.LogError($"Python script not found: {scriptPath}");
                    return false;
                }

                // Try fetching with the symbol as-is
                var success = await ExecutePythonScript(scriptPath, symbol);

                // If failed and symbol doesn't contain a period, try adding .TO suffix for Canadian stocks
                if (!success && !symbol.Contains('.'))
                {
                    _logger.LogInformation($"First attempt failed. Trying {symbol}.TO for Canadian exchange...");
                    success = await ExecutePythonScript(scriptPath, $"{symbol}.TO");
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error calling Python script: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Execute Python script with given symbol
        /// </summary>
        private async Task<bool> ExecutePythonScript(string scriptPath, string symbol)
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = $"\"{scriptPath}\" {symbol}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Directory.GetCurrentDirectory()
                };

                using var process = new Process { StartInfo = processInfo };

                var output = new System.Text.StringBuilder();
                var error = new System.Text.StringBuilder();

                process.OutputDataReceived += (sender, e) => { if (e.Data != null) output.AppendLine(e.Data); };
                process.ErrorDataReceived += (sender, e) => { if (e.Data != null) error.AppendLine(e.Data); };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    _logger.LogInformation($"✓ Python script succeeded for {symbol}");
                    _logger.LogDebug(output.ToString());
                    return true;
                }
                else
                {
                    _logger.LogWarning($"✗ Python script failed for {symbol}");
                    _logger.LogDebug($"Error: {error}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Error executing Python script for {symbol}: {ex.Message}");
                return false;
            }
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
