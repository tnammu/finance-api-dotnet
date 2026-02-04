using System.Diagnostics;
using System.Text.Json;

namespace FinanceApi.Services
{
    public class SP500Service
    {
        private readonly ILogger<SP500Service> _logger;

        public SP500Service(ILogger<SP500Service> logger)
        {
            _logger = logger;
        }

        public async Task<object?> FetchMonthlyGrowthAsync(int years = 5)
        {
            try
            {
                var scriptPath = Path.Combine(Directory.GetCurrentDirectory(), "scripts", "fetch_sp500_monthly.py");

                if (!File.Exists(scriptPath))
                {
                    _logger.LogError($"Python script not found: {scriptPath}");
                    throw new FileNotFoundException($"Python script not found: {scriptPath}");
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = $"\"{scriptPath}\" --years {years}",
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
                        return JsonSerializer.Deserialize<object>(output);
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogError("Failed to parse JSON output: {Message}", ex.Message);
                        throw new Exception("Failed to parse S&P 500 data");
                    }
                }
                else
                {
                    throw new Exception("Failed to fetch S&P 500 data");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching S&P 500 data: {ex.Message}");
                throw;
            }
        }
    }
}
