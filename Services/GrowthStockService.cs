using System.Diagnostics;
using System.Text.Json;
using FinanceApi.Data;
using FinanceApi.Models;
using Microsoft.EntityFrameworkCore;

namespace FinanceApi.Services
{
    public class GrowthStockService
    {
        private readonly ILogger<GrowthStockService> _logger;
        private readonly DividendDbContext _dbContext;

        public GrowthStockService(ILogger<GrowthStockService> logger, DividendDbContext dbContext)
        {
            _logger = logger;
            _dbContext = dbContext;
        }

        public async Task<object?> AnalyzeGrowthStockAsync(string symbol)
        {
            try
            {
                var scriptPath = Path.Combine(Directory.GetCurrentDirectory(), "scripts", "growth_stock_analyzer.py");

                if (!File.Exists(scriptPath))
                {
                    _logger.LogError($"Python script not found: {scriptPath}");
                    throw new FileNotFoundException($"Python script not found: {scriptPath}");
                }

                _logger.LogInformation($"Executing growth analysis for {symbol}...");

                var startInfo = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = $"\"{scriptPath}\" {symbol}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = startInfo };
                process.Start();

                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (!string.IsNullOrEmpty(error))
                {
                    _logger.LogInformation($"Python script stderr: {error}");
                }

                if (process.ExitCode == 0 && !string.IsNullOrEmpty(output))
                {
                    try
                    {
                        var result = JsonSerializer.Deserialize<JsonElement>(output);

                        if (result.TryGetProperty("success", out var success) && success.GetBoolean())
                        {
                            // Save or update in database
                            await SaveGrowthDataToDatabase(result);
                            return JsonSerializer.Deserialize<object>(output);
                        }
                        else
                        {
                            var errorMsg = result.TryGetProperty("error", out var err) ? err.GetString() : "Unknown error";
                            _logger.LogError($"Growth analysis failed: {errorMsg}");
                            return null;
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogError($"Failed to parse JSON output: {ex.Message}");
                        _logger.LogError($"Output was: {output}");
                        throw new Exception("Failed to parse growth analysis data");
                    }
                }
                else
                {
                    _logger.LogError($"Python script failed with exit code {process.ExitCode}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error analyzing growth stock {symbol}: {ex.Message}");
                throw;
            }
        }

        private async Task SaveGrowthDataToDatabase(JsonElement result)
        {
            try
            {
                var symbol = result.GetProperty("symbol").GetString()?.ToUpper();
                if (string.IsNullOrEmpty(symbol))
                {
                    _logger.LogWarning("No symbol in result, skipping database save");
                    return;
                }

                // Find or create stock record
                var stock = await _dbContext.DividendModels
                    .FirstOrDefaultAsync(d => d.Symbol == symbol);

                if (stock == null)
                {
                    // Create new record
                    stock = new DividendModel
                    {
                        Symbol = symbol,
                        CompanyName = GetStringProperty(result, "company_name"),
                        Sector = GetStringProperty(result, "sector"),
                        Industry = GetStringProperty(result, "industry"),
                        FetchedAt = DateTime.UtcNow,
                        LastUpdated = DateTime.UtcNow
                    };
                    _dbContext.DividendModels.Add(stock);
                }
                else
                {
                    // Update existing record
                    stock.LastUpdated = DateTime.UtcNow;
                    if (!string.IsNullOrEmpty(GetStringProperty(result, "company_name")))
                        stock.CompanyName = GetStringProperty(result, "company_name");
                    if (!string.IsNullOrEmpty(GetStringProperty(result, "sector")))
                        stock.Sector = GetStringProperty(result, "sector");
                    if (!string.IsNullOrEmpty(GetStringProperty(result, "industry")))
                        stock.Industry = GetStringProperty(result, "industry");
                }

                // Update price
                stock.CurrentPrice = GetDecimalProperty(result, "current_price");

                // Update growth metrics
                stock.RevenueGrowth = GetNullableDecimalProperty(result, "revenue_growth");
                stock.EPSGrowthRate = GetNullableDecimalProperty(result, "eps_growth");
                stock.PEGRatio = GetNullableDecimalProperty(result, "peg_ratio");
                stock.RuleOf40Score = GetNullableDecimalProperty(result, "rule_of_40");
                stock.FreeCashFlow = GetNullableDecimalProperty(result, "free_cash_flow");
                stock.GrowthScore = GetDecimalProperty(result, "growth_score");
                stock.GrowthRating = GetStringProperty(result, "growth_rating");

                // Update profit margin if available
                var profitMargin = GetNullableDecimalProperty(result, "profit_margin");
                if (profitMargin.HasValue && profitMargin.Value > 0)
                {
                    stock.ProfitMargin = profitMargin;
                }

                await _dbContext.SaveChangesAsync();

                _logger.LogInformation($"✓ Saved growth data for {symbol} to database (Score: {stock.GrowthScore})");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error saving growth data to database: {ex.Message}");
                throw;
            }
        }

        private string GetStringProperty(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var prop))
            {
                return prop.GetString() ?? string.Empty;
            }
            return string.Empty;
        }

        private decimal GetDecimalProperty(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.Number)
                {
                    return (decimal)prop.GetDouble();
                }
            }
            return 0;
        }

        private decimal? GetNullableDecimalProperty(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.Number)
                {
                    return (decimal)prop.GetDouble();
                }
                else if (prop.ValueKind == JsonValueKind.Null)
                {
                    return null;
                }
            }
            return null;
        }

        #region Growth Stock Queries

        /// <summary>
        /// Get all stocks with growth scores
        /// </summary>
        public async Task<List<GrowthStockDto>> GetAllGrowthStocksAsync()
        {
            var allStocks = await _dbContext.DividendModels
                .Where(d => d.GrowthScore > 0)
                .ToListAsync();

            return allStocks
                .OrderByDescending(d => d.GrowthScore)
                .Select(MapToGrowthStockDto)
                .ToList();
        }

        /// <summary>
        /// Get top N growth stocks
        /// </summary>
        public async Task<List<GrowthStockDto>> GetTopGrowthStocksAsync(int count)
        {
            var allStocks = await _dbContext.DividendModels
                .Where(d => d.GrowthScore > 0)
                .ToListAsync();

            return allStocks
                .OrderByDescending(d => d.GrowthScore)
                .Take(count)
                .Select(MapToGrowthStockDto)
                .ToList();
        }

        /// <summary>
        /// Compare multiple stocks' growth metrics
        /// </summary>
        public async Task<GrowthComparisonResult> CompareGrowthStocksAsync(List<string> symbols)
        {
            var stocks = await _dbContext.DividendModels
                .Where(d => symbols.Contains(d.Symbol))
                .ToListAsync();

            var foundSymbols = stocks.Select(s => s.Symbol).ToList();
            var missingSymbols = symbols.Except(foundSymbols).ToList();

            return new GrowthComparisonResult
            {
                Found = stocks.Count,
                Stocks = stocks.Select(MapToGrowthStockDto).ToList(),
                MissingSymbols = missingSymbols
            };
        }

        /// <summary>
        /// Analyze all cached stocks for growth potential
        /// </summary>
        public async Task<BulkAnalysisResult> AnalyzeAllCachedStocksAsync()
        {
            var allStocks = await _dbContext.DividendModels.ToListAsync();
            var result = new BulkAnalysisResult
            {
                TotalStocks = allStocks.Count
            };

            if (!allStocks.Any())
            {
                return result;
            }

            _logger.LogInformation($"Found {allStocks.Count} cached stocks. Starting analysis...");

            foreach (var stock in allStocks)
            {
                try
                {
                    _logger.LogInformation($"Analyzing {stock.Symbol}...");
                    var analysisResult = await AnalyzeGrowthStockAsync(stock.Symbol);

                    if (analysisResult != null)
                    {
                        result.SuccessCount++;
                    }
                    else
                    {
                        result.FailedCount++;
                        result.Errors.Add($"{stock.Symbol}: Analysis returned null");
                    }

                    await Task.Delay(100); // Rate limiting
                }
                catch (Exception ex)
                {
                    result.FailedCount++;
                    result.Errors.Add($"{stock.Symbol}: {ex.Message}");
                    _logger.LogError($"Error analyzing {stock.Symbol}: {ex.Message}");
                }
            }

            _logger.LogInformation($"Bulk analysis complete. Success: {result.SuccessCount}, Failed: {result.FailedCount}");
            return result;
        }

        private GrowthStockDto MapToGrowthStockDto(DividendModel d) => new GrowthStockDto
        {
            Symbol = d.Symbol,
            CompanyName = d.CompanyName,
            Sector = d.Sector,
            CurrentPrice = d.CurrentPrice,
            RevenueGrowth = d.RevenueGrowth,
            EpsGrowthRate = d.EPSGrowthRate,
            PegRatio = d.PEGRatio,
            RuleOf40Score = d.RuleOf40Score,
            FreeCashFlow = d.FreeCashFlow,
            GrowthScore = d.GrowthScore,
            GrowthRating = d.GrowthRating,
            DividendYield = d.DividendYield,
            SafetyScore = d.SafetyScore,
            LastUpdated = d.LastUpdated
        };

        #endregion

        #region ETF and Strategy Analysis (Python Script Execution)

        /// <summary>
        /// Get historical data for an ETF
        /// </summary>
        public async Task<JsonDocument?> GetEtfHistoryAsync(string symbol, int years)
        {
            return await ExecutePythonScriptAsync("fetch_etf_history.py", $"{symbol} {years}");
        }

        /// <summary>
        /// Get hourly analysis for an ETF
        /// </summary>
        public async Task<JsonDocument?> GetEtfHourlyAnalysisAsync(string symbol, int days)
        {
            return await ExecutePythonScriptAsync("fetch_etf_hourly.py", $"{symbol} {days}");
        }

        /// <summary>
        /// Get intraday data for an ETF
        /// </summary>
        public async Task<JsonDocument?> GetEtfIntradayDataAsync(string symbol, string period)
        {
            return await ExecutePythonScriptAsync("fetch_etf_intraday.py", $"{symbol} {period}");
        }

        /// <summary>
        /// Get PIP trading strategy for a stock
        /// </summary>
        public async Task<JsonDocument?> GetStockStrategyAsync(string symbol, double capital, int years)
        {
            return await ExecutePythonScriptAsync("stock_strategy_calculator.py", $"{symbol} {capital} {years}");
        }

        private async Task<JsonDocument?> ExecutePythonScriptAsync(string scriptName, string arguments)
        {
            var scriptPath = Path.Combine(Directory.GetCurrentDirectory(), "scripts", scriptName);

            if (!File.Exists(scriptPath))
            {
                _logger.LogError($"Python script not found: {scriptPath}");
                return null;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = $"\"{scriptPath}\" {arguments}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                _logger.LogError($"Failed to start Python process for {scriptName}");
                return null;
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            var errors = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (!string.IsNullOrEmpty(errors))
            {
                _logger.LogInformation($"Python script output: {errors}");
            }

            if (process.ExitCode != 0)
            {
                _logger.LogError($"Python script {scriptName} failed with exit code {process.ExitCode}");
                return null;
            }

            // Validate output is valid JSON before parsing
            output = output.Trim();
            if (string.IsNullOrEmpty(output) || (!output.StartsWith("{") && !output.StartsWith("[")))
            {
                _logger.LogError($"Python script {scriptName} returned non-JSON output");
                return null;
            }

            try
            {
                return JsonDocument.Parse(output);
            }
            catch (JsonException ex)
            {
                _logger.LogError($"Failed to parse {scriptName} output as JSON: {ex.Message}");
                return null;
            }
        }

        #endregion
    }

    #region DTOs

    public class GrowthStockDto
    {
        public string Symbol { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string Sector { get; set; } = string.Empty;
        public decimal CurrentPrice { get; set; }
        public decimal? RevenueGrowth { get; set; }
        public decimal? EpsGrowthRate { get; set; }
        public decimal? PegRatio { get; set; }
        public decimal? RuleOf40Score { get; set; }
        public decimal? FreeCashFlow { get; set; }
        public decimal GrowthScore { get; set; }
        public string GrowthRating { get; set; } = string.Empty;
        public decimal? DividendYield { get; set; }
        public decimal SafetyScore { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class GrowthComparisonResult
    {
        public int Found { get; set; }
        public List<GrowthStockDto> Stocks { get; set; } = new();
        public List<string> MissingSymbols { get; set; } = new();
    }

    public class BulkAnalysisResult
    {
        public int TotalStocks { get; set; }
        public int SuccessCount { get; set; }
        public int FailedCount { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    #endregion
}