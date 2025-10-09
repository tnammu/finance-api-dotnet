using FinanceApi.Data;
using FinanceApi.Model;
using FinanceApi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceApi.Services
{
    public class LiveStockSeeder
    {
        private readonly FinanceContext _context;
        private readonly StockService _stockService;
        private readonly ILogger<LiveStockSeeder> _logger;

        public LiveStockSeeder(FinanceContext context, StockService stockService, ILogger<LiveStockSeeder> logger)
        {
            _context = context;
            _stockService = stockService;
            _logger = logger;
        }

        public async Task SeedAsync(List<string> symbols)
        {
            _logger.LogInformation($"Starting to seed {symbols.Count} stocks...");

            var successCount = 0;
            var skipCount = 0;
            var failCount = 0;

            foreach (var symbol in symbols)
            {
                try
                {
                    // Check if stock already exists
                    var existingStock = await _context.Stocks.FirstOrDefaultAsync(s => s.Symbol == symbol);
                    if (existingStock != null)
                    {
                        _logger.LogInformation($"Stock {symbol} already exists, skipping...");
                        skipCount++;
                        continue;
                    }

                    _logger.LogInformation($"Fetching data for {symbol}...");

                    // Add delay to avoid rate limiting (500ms between requests)
                    await Task.Delay(500);

                    var liveInfo = await _stockService.GetLiveStockInfoAsync(symbol);

                    if (liveInfo.Price.HasValue)
                    {
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

                        _logger.LogInformation($"✓ Added {symbol}: {stock.CompanyName} at ${stock.Price}");
                        successCount++;
                    }
                    else
                    {
                        _logger.LogWarning($"✗ Could not fetch price for {symbol}");
                        failCount++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"✗ Error seeding {symbol}: {ex.Message}");
                    failCount++;
                }
            }

            _logger.LogInformation($"Seeding complete: {successCount} added, {skipCount} skipped, {failCount} failed");
        }
    }
}
