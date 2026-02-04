using System.Diagnostics;
using System.Text.Json;

namespace FinanceApi.Services
{
    public class EtfService
    {
        private readonly ILogger<EtfService> _logger;

        public EtfService(ILogger<EtfService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Fetch ETF holdings data from Python script (no database storage)
        /// </summary>
        public async Task<object?> FetchEtfHoldingsAsync(string symbol)
        {
            try
            {
                var scriptPath = Path.Combine(Directory.GetCurrentDirectory(), "scripts", "fetch_etf_holdings.py");

                if (!File.Exists(scriptPath))
                {
                    _logger.LogError($"Python script not found: {scriptPath}");
                    throw new FileNotFoundException($"Python script not found: {scriptPath}");
                }

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
                    _logger.LogInformation($"Python script stderr for {symbol}: {error}");
                }

                if (process.ExitCode == 0 && !string.IsNullOrEmpty(output))
                {
                    try
                    {
                        return JsonSerializer.Deserialize<object>(output);
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogError("Failed to parse JSON output for {Symbol}: {Message}", symbol, ex.Message);
                        throw new Exception($"Failed to parse ETF data for {symbol}");
                    }
                }
                else
                {
                    throw new Exception($"Failed to fetch data for {symbol}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching ETF data for {symbol}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Fetch multiple ETFs in parallel
        /// </summary>
        public async Task<List<object>> FetchMultipleEtfsAsync(List<string> symbols)
        {
            var tasks = symbols.Select(FetchEtfHoldingsAsync);
            var results = await Task.WhenAll(tasks);

            return results
                .Where(r => r != null)
                .Select(r => r!)
                .ToList();
        }
    }
}
