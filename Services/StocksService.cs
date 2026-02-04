using System.Diagnostics;
using System.Text;
using FinanceApi.Data;
using FinanceApi.Models;
using Microsoft.EntityFrameworkCore;

namespace FinanceApi.Services
{
    public class StocksService
    {
        private readonly DividendDbContext _dbContext;
        private readonly ILogger<StocksService> _logger;

        public StocksService(DividendDbContext dbContext, ILogger<StocksService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        #region Stock Queries

        public async Task<List<StockSummaryDto>> GetAllStocksAsync()
        {
            var stocks = await _dbContext.DividendModels.ToListAsync();

            return stocks.Select(s => new StockSummaryDto
            {
                Id = s.Id,
                Symbol = s.Symbol,
                CompanyName = s.CompanyName,
                CurrentPrice = s.CurrentPrice,
                DividendYield = s.DividendYield,
                LastUpdated = s.LastUpdated,
                MinutesOld = (int)(DateTime.UtcNow - s.LastUpdated).TotalMinutes,
                IsStale = (DateTime.UtcNow - s.LastUpdated).TotalMinutes > 15
            }).ToList();
        }

        public async Task<StockSummaryDto?> GetStockByIdAsync(int id)
        {
            var stock = await _dbContext.DividendModels.FindAsync(id);

            if (stock == null) return null;

            return new StockSummaryDto
            {
                Id = stock.Id,
                Symbol = stock.Symbol,
                CompanyName = stock.CompanyName,
                CurrentPrice = stock.CurrentPrice,
                DividendYield = stock.DividendYield,
                LastUpdated = stock.LastUpdated,
                MinutesOld = (int)(DateTime.UtcNow - stock.LastUpdated).TotalMinutes
            };
        }

        public async Task<DividendModel?> GetStockWithDetailsAsync(string symbol)
        {
            return await _dbContext.DividendModels
                .Include(d => d.DividendPayments)
                .Include(d => d.YearlyDividends)
                .FirstOrDefaultAsync(s => s.Symbol == symbol.ToUpper());
        }

        public async Task<bool> StockExistsAsync(string symbol)
        {
            return await _dbContext.DividendModels.AnyAsync(s => s.Symbol == symbol);
        }

        #endregion

        #region Stock Operations

        public async Task<LiveStockResult?> GetLiveStockAsync(string symbol)
        {
            symbol = symbol.ToUpper();
            var success = await FetchStockDataViaPythonAsync(symbol);

            if (!success) return null;

            var stock = await GetStockWithDetailsAsync(symbol);
            if (stock == null) return null;

            return new LiveStockResult
            {
                Symbol = stock.Symbol,
                CompanyName = stock.CompanyName,
                Price = stock.CurrentPrice,
                DividendYield = stock.DividendYield,
                FetchedAt = stock.LastUpdated,
                IsLive = true,
                Source = "Yahoo Finance (Python Script)"
            };
        }

        public async Task<RefreshResult> RefreshAllStocksAsync()
        {
            var stocks = await _dbContext.DividendModels.ToListAsync();
            var result = new RefreshResult
            {
                TotalStocks = stocks.Count
            };

            foreach (var stock in stocks)
            {
                try
                {
                    await Task.Delay(500); // Rate limiting

                    var success = await FetchStockDataViaPythonAsync(stock.Symbol);

                    if (success)
                    {
                        var updatedStock = await _dbContext.DividendModels.FirstOrDefaultAsync(s => s.Symbol == stock.Symbol);
                        if (updatedStock != null)
                        {
                            result.SuccessCount++;
                            result.Results.Add(new RefreshItemResult
                            {
                                Symbol = updatedStock.Symbol,
                                Status = "Updated",
                                Price = updatedStock.CurrentPrice
                            });
                        }
                        else
                        {
                            result.FailCount++;
                            result.Results.Add(new RefreshItemResult
                            {
                                Symbol = stock.Symbol,
                                Status = "Failed",
                                Message = "Stock updated but not found in database"
                            });
                        }
                    }
                    else
                    {
                        result.FailCount++;
                        result.Results.Add(new RefreshItemResult
                        {
                            Symbol = stock.Symbol,
                            Status = "Failed - No price data"
                        });
                    }
                }
                catch (Exception ex)
                {
                    result.FailCount++;
                    _logger.LogError($"Error refreshing {stock.Symbol}: {ex.Message}");
                    result.Results.Add(new RefreshItemResult
                    {
                        Symbol = stock.Symbol,
                        Status = "Error",
                        Message = ex.Message
                    });
                }
            }

            await _dbContext.SaveChangesAsync();
            result.UpdatedAt = DateTime.UtcNow;
            return result;
        }

        public async Task<RefreshItemResult?> RefreshStockByIdAsync(int id)
        {
            var stock = await _dbContext.DividendModels.FindAsync(id);
            if (stock == null) return null;

            var success = await FetchStockDataViaPythonAsync(stock.Symbol);
            if (!success)
            {
                return new RefreshItemResult
                {
                    Symbol = stock.Symbol,
                    Status = "Failed",
                    Message = $"Could not fetch live data for {stock.Symbol}"
                };
            }

            var updatedStock = await _dbContext.DividendModels.FindAsync(id);
            if (updatedStock == null)
            {
                return new RefreshItemResult
                {
                    Symbol = stock.Symbol,
                    Status = "Error",
                    Message = "Stock updated but not found in database"
                };
            }

            return new RefreshItemResult
            {
                Symbol = updatedStock.Symbol,
                Status = "Updated",
                CompanyName = updatedStock.CompanyName,
                Price = updatedStock.CurrentPrice,
                DividendYield = updatedStock.DividendYield,
                LastUpdated = updatedStock.LastUpdated,
                IsLive = true,
                Source = "Yahoo Finance (Python Script)"
            };
        }

        public async Task<BulkImportResult> BulkAddStocksAsync(List<string> symbols, bool addCanadianSuffix)
        {
            var result = new BulkImportResult
            {
                TotalSubmitted = symbols.Count
            };

            _logger.LogInformation($"Bulk import started: {symbols.Count} symbols");

            foreach (var symbolInput in symbols)
            {
                try
                {
                    var symbol = symbolInput.ToUpper().Trim();

                    if (addCanadianSuffix && !symbol.Contains(".TO") && !symbol.Contains("."))
                    {
                        symbol = $"{symbol}.TO";
                        _logger.LogInformation($"  Added .TO suffix: {symbolInput} → {symbol}");
                    }

                    if (await StockExistsAsync(symbol))
                    {
                        result.SkippedCount++;
                        result.Results.Add(new BulkImportItemResult
                        {
                            Symbol = symbol,
                            Status = "Skipped",
                            Message = "Already exists"
                        });
                        continue;
                    }

                    await Task.Delay(500); // Rate limiting

                    var success = await FetchStockDataViaPythonAsync(symbol);

                    if (!success)
                    {
                        result.FailCount++;
                        result.Results.Add(new BulkImportItemResult
                        {
                            Symbol = symbol,
                            Status = "Failed",
                            Message = "Could not fetch data - symbol may be invalid"
                        });
                        continue;
                    }

                    var stock = await GetStockWithDetailsAsync(symbol);

                    if (stock == null)
                    {
                        result.FailCount++;
                        result.Results.Add(new BulkImportItemResult
                        {
                            Symbol = symbol,
                            Status = "Failed",
                            Message = "Stock was fetched but not saved to database"
                        });
                        continue;
                    }

                    result.SuccessCount++;
                    result.Results.Add(new BulkImportItemResult
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
                    result.FailCount++;
                    _logger.LogError($"  ✗ Error adding {symbolInput}: {ex.Message}");
                    result.Results.Add(new BulkImportItemResult
                    {
                        Symbol = symbolInput,
                        Status = "Error",
                        Message = ex.Message
                    });
                }
            }

            result.CompletedAt = DateTime.UtcNow;
            return result;
        }

        public async Task<AddStockResult?> AddStockAsync(string symbol)
        {
            symbol = symbol.ToUpper().Trim();

            if (await StockExistsAsync(symbol))
            {
                return new AddStockResult { Status = "Conflict", Message = $"Stock {symbol} already exists" };
            }

            var success = await FetchStockDataViaPythonAsync(symbol);
            if (!success)
            {
                return new AddStockResult { Status = "Failed", Message = $"Could not fetch data for {symbol}. Please verify the symbol is correct." };
            }

            var stock = await GetStockWithDetailsAsync(symbol);
            if (stock == null)
            {
                return new AddStockResult { Status = "Error", Message = $"Stock {symbol} was fetched but not saved to database" };
            }

            return new AddStockResult
            {
                Status = "Success",
                Id = stock.Id,
                Symbol = stock.Symbol,
                CompanyName = stock.CompanyName,
                CurrentPrice = stock.CurrentPrice,
                DividendYield = stock.DividendYield,
                LastUpdated = stock.LastUpdated
            };
        }

        public async Task<bool> DeleteStockAsync(int id)
        {
            var stock = await _dbContext.DividendModels.FindAsync(id);
            if (stock == null) return false;

            _dbContext.DividendModels.Remove(stock);
            await _dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<string> ExportToCsvAsync()
        {
            var stocks = await _dbContext.DividendModels.OrderBy(s => s.Symbol).ToListAsync();

            var csv = new StringBuilder();
            csv.AppendLine("Symbol,Company Name,Price,Dividend Yield (%),Last Updated,Data Age (minutes)");

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

            return csv.ToString();
        }

        #endregion

        #region Python Script Execution

        public async Task<bool> FetchStockDataViaPythonAsync(string symbol)
        {
            try
            {
                var scriptPath = Path.Combine(Directory.GetCurrentDirectory(), "scripts", "update_stocks_from_yahoo.py");

                if (!File.Exists(scriptPath))
                {
                    _logger.LogError($"Python script not found: {scriptPath}");
                    return false;
                }

                var success = await ExecutePythonScriptAsync(scriptPath, symbol);

                if (!success && !symbol.Contains('.'))
                {
                    _logger.LogInformation($"First attempt failed. Trying {symbol}.TO for Canadian exchange...");
                    success = await ExecutePythonScriptAsync(scriptPath, $"{symbol}.TO");
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error calling Python script: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> ExecutePythonScriptAsync(string scriptPath, string symbol)
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

                var output = new StringBuilder();
                var error = new StringBuilder();

                process.OutputDataReceived += (sender, e) => { if (e.Data != null) output.AppendLine(e.Data); };
                process.ErrorDataReceived += (sender, e) => { if (e.Data != null) error.AppendLine(e.Data); };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    _logger.LogInformation($"✓ Python script succeeded for {symbol}");
                    return true;
                }
                else
                {
                    _logger.LogWarning($"✗ Python script failed for {symbol}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Error executing Python script for {symbol}: {ex.Message}");
                return false;
            }
        }

        #endregion
    }

    #region DTOs

    public class StockSummaryDto
    {
        public int Id { get; set; }
        public string Symbol { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public decimal CurrentPrice { get; set; }
        public decimal? DividendYield { get; set; }
        public DateTime LastUpdated { get; set; }
        public int MinutesOld { get; set; }
        public bool IsStale { get; set; }
    }

    public class LiveStockResult
    {
        public string Symbol { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal? DividendYield { get; set; }
        public DateTime FetchedAt { get; set; }
        public bool IsLive { get; set; }
        public string Source { get; set; } = string.Empty;
    }

    public class RefreshResult
    {
        public int TotalStocks { get; set; }
        public int SuccessCount { get; set; }
        public int FailCount { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<RefreshItemResult> Results { get; set; } = new();
    }

    public class RefreshItemResult
    {
        public string Symbol { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? Message { get; set; }
        public string? CompanyName { get; set; }
        public decimal? Price { get; set; }
        public decimal? DividendYield { get; set; }
        public DateTime? LastUpdated { get; set; }
        public bool IsLive { get; set; }
        public string? Source { get; set; }
    }

    public class BulkImportResult
    {
        public int TotalSubmitted { get; set; }
        public int SuccessCount { get; set; }
        public int SkippedCount { get; set; }
        public int FailCount { get; set; }
        public DateTime CompletedAt { get; set; }
        public List<BulkImportItemResult> Results { get; set; } = new();
    }

    public class BulkImportItemResult
    {
        public string Symbol { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? Message { get; set; }
        public string? CompanyName { get; set; }
        public decimal? Price { get; set; }
    }

    public class AddStockResult
    {
        public string Status { get; set; } = string.Empty;
        public string? Message { get; set; }
        public int Id { get; set; }
        public string Symbol { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public decimal CurrentPrice { get; set; }
        public decimal? DividendYield { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    #endregion
}
