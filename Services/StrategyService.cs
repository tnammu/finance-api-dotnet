using System;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace FinanceApi.Services
{
    /// <summary>
    /// Trading Strategy Analysis Service
    /// Executes multi-strategy backtesting via Python scripts
    /// </summary>
    public class StrategyService
    {
        private readonly ILogger<StrategyService> _logger;
        private readonly string _scriptsPath;

        public StrategyService(ILogger<StrategyService> logger)
        {
            _logger = logger;

            // Get scripts path relative to application directory
            var appDirectory = AppDomain.CurrentDomain.BaseDirectory;

            // Navigate to project root (FinanceApi folder)
            var projectRoot = Directory.GetParent(appDirectory)?.Parent?.Parent?.Parent?.FullName;

            if (projectRoot != null && Directory.Exists(Path.Combine(projectRoot, "scripts")))
            {
                _scriptsPath = Path.Combine(projectRoot, "scripts");
            }
            else
            {
                // Fallback: Try current directory
                var currentDir = Directory.GetCurrentDirectory();
                _scriptsPath = Path.Combine(currentDir, "scripts");
            }

            _logger.LogInformation($"✓ StrategyService initialized");
            _logger.LogInformation($"  App Directory: {appDirectory}");
            _logger.LogInformation($"  Scripts path: {_scriptsPath}");
            _logger.LogInformation($"  Scripts path exists: {Directory.Exists(_scriptsPath)}");
        }

        /// <summary>
        /// Analyze all trading strategies for a symbol
        /// </summary>
        public async Task<JsonDocument?> AnalyzeStrategiesAsync(string symbol, double capital = 100, int years = 5, bool enforceBuyFirst = true)
        {
            try
            {
                symbol = symbol.ToUpper();

                _logger.LogInformation($"========================================");
                _logger.LogInformation($"→ Running multi-strategy analysis for: {symbol}");
                _logger.LogInformation($"  Capital: ${capital:F2}");
                _logger.LogInformation($"  Period: {years} years");
                _logger.LogInformation($"  Enforce Buy-First: {(enforceBuyFirst ? "✓ YES" : "⚠️ NO")}");

                var pythonScript = Path.Combine(_scriptsPath, "multi_strategy_analyzer.py");

                if (!File.Exists(pythonScript))
                {
                    _logger.LogError($"✗ Python script not found: {pythonScript}");
                    return null;
                }

                var enforceFlag = enforceBuyFirst ? "true" : "false";
                var startInfo = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = $"\"{pythonScript}\" {symbol} {capital} {years} {enforceFlag}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = _scriptsPath
                };

                _logger.LogInformation($"📊 Executing Python strategy analyzer...");

                using var process = new Process { StartInfo = startInfo };
                process.Start();

                // Read output
                var output = await process.StandardOutput.ReadToEndAsync();
                var errors = await process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync();

                // Log stderr (contains progress messages)
                if (!string.IsNullOrWhiteSpace(errors))
                {
                    _logger.LogInformation($"Python output:\n{errors}");
                }

                if (process.ExitCode != 0)
                {
                    _logger.LogError($"✗ Python script failed with exit code {process.ExitCode}");
                    return null;
                }

                // Parse JSON output
                if (string.IsNullOrWhiteSpace(output))
                {
                    _logger.LogError($"✗ No output from Python script");
                    return null;
                }

                var jsonDoc = JsonDocument.Parse(output);

                _logger.LogInformation($"✓ Strategy analysis complete!");
                _logger.LogInformation($"========================================");

                return jsonDoc;
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error in AnalyzeStrategiesAsync: {ex.Message}");
                _logger.LogError($"   Stack: {ex.StackTrace}");
                _logger.LogInformation($"========================================");
                return null;
            }
        }

        /// <summary>
        /// Calculate a single strategy with custom parameters
        /// </summary>
        public async Task<JsonDocument?> CalculateSingleStrategyAsync(
            string symbol,
            string strategyType,
            double capital = 100,
            int years = 5,
            Dictionary<string, object>? parameters = null)
        {
            try
            {
                symbol = symbol.ToUpper();

                _logger.LogInformation($"========================================");
                _logger.LogInformation($"→ Running {strategyType} strategy for: {symbol}");
                _logger.LogInformation($"  Capital: ${capital:F2}");
                _logger.LogInformation($"  Period: {years} years");

                // For now, we'll run the full analysis and extract the specific strategy
                // In the future, we could create separate Python scripts for individual strategies
                var fullAnalysis = await AnalyzeStrategiesAsync(symbol, capital, years);

                if (fullAnalysis == null)
                {
                    return null;
                }

                // Extract the specific strategy from the full analysis
                if (fullAnalysis.RootElement.TryGetProperty("strategies", out var strategiesElement) &&
                    strategiesElement.TryGetProperty(strategyType, out var strategyElement))
                {
                    // Create a new JSON document with just this strategy
                    var singleStrategyJson = new
                    {
                        success = true,
                        symbol = fullAnalysis.RootElement.GetProperty("symbol").GetString(),
                        companyName = fullAnalysis.RootElement.GetProperty("companyName").GetString(),
                        currentPrice = fullAnalysis.RootElement.GetProperty("currentPrice").GetDouble(),
                        capital = capital,
                        period = fullAnalysis.RootElement.GetProperty("period").GetString(),
                        strategy = JsonSerializer.Deserialize<object>(strategyElement.GetRawText()),
                        fetched_at = DateTime.Now.ToString("o")
                    };

                    var jsonString = JsonSerializer.Serialize(singleStrategyJson);
                    var result = JsonDocument.Parse(jsonString);

                    _logger.LogInformation($"✓ Single strategy calculation complete!");
                    _logger.LogInformation($"========================================");

                    return result;
                }
                else
                {
                    _logger.LogError($"✗ Strategy type '{strategyType}' not found in results");
                    _logger.LogInformation($"========================================");
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error in CalculateSingleStrategyAsync: {ex.Message}");
                _logger.LogInformation($"========================================");
                return null;
            }
        }

        /// <summary>
        /// Get available strategy types
        /// </summary>
        public List<StrategyInfo> GetAvailableStrategies()
        {
            return new List<StrategyInfo>
            {
                new StrategyInfo
                {
                    Id = "buyHold",
                    Name = "Buy and Hold",
                    Description = "Buy at the beginning and hold until the end",
                    Category = "Passive",
                    RiskLevel = "Low"
                },
                new StrategyInfo
                {
                    Id = "smaCrossover",
                    Name = "SMA Crossover (50/200)",
                    Description = "Buy on Golden Cross (SMA50 > SMA200), Sell on Death Cross",
                    Category = "Trend Following",
                    RiskLevel = "Medium"
                },
                new StrategyInfo
                {
                    Id = "rsi",
                    Name = "RSI Strategy",
                    Description = "Buy when RSI < 30 (oversold), Sell when RSI > 70 (overbought)",
                    Category = "Oscillator",
                    RiskLevel = "Medium"
                },
                new StrategyInfo
                {
                    Id = "macd",
                    Name = "MACD Strategy",
                    Description = "Buy when MACD crosses above signal line, Sell when crosses below",
                    Category = "Momentum",
                    RiskLevel = "Medium"
                },
                new StrategyInfo
                {
                    Id = "bollingerBands",
                    Name = "Bollinger Bands Mean Reversion",
                    Description = "Buy at lower band (oversold), Sell at upper band (overbought)",
                    Category = "Mean Reversion",
                    RiskLevel = "Medium-High"
                },
                new StrategyInfo
                {
                    Id = "momentum",
                    Name = "Momentum Breakout",
                    Description = "Buy on 20-day high breakout, Exit on 8% stop loss",
                    Category = "Breakout",
                    RiskLevel = "High"
                },
                new StrategyInfo
                {
                    Id = "monthlySeasonal",
                    Name = "Monthly Seasonal Pattern",
                    Description = "Trade only during historically favorable months",
                    Category = "Seasonal",
                    RiskLevel = "Medium"
                }
            };
        }
    }

    /// <summary>
    /// Information about a trading strategy
    /// </summary>
    public class StrategyInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string RiskLevel { get; set; } = string.Empty;
    }
}
