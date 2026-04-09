using FinanceApi.Model;
using FinanceApi.Repositories.Interfaces;

namespace FinanceApi.Services
{
    public class AddHoldingRequest
    {
        public string Symbol { get; set; } = string.Empty;
        public decimal Shares { get; set; }
        public decimal BuyPrice { get; set; }
        public DateTime BuyDate { get; set; }
        public string? Notes { get; set; }
    }

    public class PortfolioService
    {
        private readonly IHoldingRepository _holdingRepo;
        private readonly IDividendRepository _dividendRepo;
        private readonly ILogger<PortfolioService> _logger;

        public PortfolioService(IHoldingRepository holdingRepo, IDividendRepository dividendRepo, ILogger<PortfolioService> logger)
        {
            _holdingRepo = holdingRepo;
            _dividendRepo = dividendRepo;
            _logger = logger;
        }

        public async Task<object> GetHoldingsAsync()
        {
            var holdings = await _holdingRepo.GetAllOrderedBySymbolAsync();

            var symbols = holdings.Select(h => h.Symbol).ToList();
            // Also look up with .TO suffix for Canadian stocks imported from Wealthsimple
            var symbolsWithTo = symbols.Select(s => s.EndsWith(".TO") ? s : s + ".TO").ToList();
            var allLookupSymbols = symbols.Concat(symbolsWithTo).Distinct().ToList();

            var stockData = await _dividendRepo.GetEnrichmentBySymbolsAsync(allLookupSymbols);

            var enriched = holdings.Select(h =>
            {
                // Try exact match first, then with .TO suffix
                if (!stockData.TryGetValue(h.Symbol, out var stock))
                    stockData.TryGetValue(h.Symbol + ".TO", out stock);

                // Use DividendModels price if available and non-zero, else fall back to stored MarketPrice
                var currentPrice = (stock != null && stock.CurrentPrice > 0)
                    ? (decimal?)stock.CurrentPrice
                    : h.MarketPrice;
                var totalCost = h.Shares * h.BuyPrice;
                var currentValue = currentPrice.HasValue ? h.Shares * currentPrice.Value : (decimal?)null;
                var gainLossDollar = currentValue.HasValue ? currentValue.Value - totalCost : (decimal?)null;
                var gainLossPct = gainLossDollar.HasValue && totalCost > 0
                    ? Math.Round(gainLossDollar.Value / totalCost * 100, 2)
                    : (decimal?)null;
                // Dividend per share: prefer DividendModels, fall back to Holdings.AnnualDividendPerShare
                var divPerShare = (stock != null && stock.DividendPerShare.HasValue && stock.DividendPerShare.Value > 0)
                    ? stock.DividendPerShare
                    : h.AnnualDividendPerShare;

                var annualDividendIncome = divPerShare.HasValue
                    ? Math.Round(h.Shares * divPerShare.Value, 2)
                    : (decimal?)null;

                // Yield = div / current price (use whichever price source we have)
                var priceForYield = currentPrice ?? (stock != null ? (decimal?)stock.CurrentPrice : null);
                var dividendYield = divPerShare.HasValue && priceForYield.HasValue && priceForYield.Value > 0
                    ? Math.Round(divPerShare.Value / priceForYield.Value * 100, 2)
                    : (decimal?)null;

                return new
                {
                    h.Id,
                    h.Symbol,
                    companyName = stock?.CompanyName ?? h.Symbol,
                    sector = stock?.Sector,
                    h.Shares,
                    h.BuyPrice,
                    buyDate = h.BuyDate.ToString("yyyy-MM-dd"),
                    h.Notes,
                    h.AddedAt,
                    currentPrice = currentPrice.HasValue ? Math.Round(currentPrice.Value, 2) : (decimal?)null,
                    totalCost = Math.Round(totalCost, 2),
                    currentValue = currentValue.HasValue ? Math.Round(currentValue.Value, 2) : (decimal?)null,
                    gainLossDollar = gainLossDollar.HasValue ? Math.Round(gainLossDollar.Value, 2) : (decimal?)null,
                    gainLossPct,
                    dividendYield,
                    annualDividendIncome
                };
            }).ToList();

            var totalInvested = enriched.Sum(h => h.totalCost);
            var totalCurrentValue = enriched.Where(h => h.currentValue.HasValue).Sum(h => h.currentValue!.Value);
            var totalGainLoss = totalCurrentValue - enriched.Where(h => h.currentValue.HasValue).Sum(h => h.totalCost);
            var totalGainLossPct = enriched.Any(h => h.currentValue.HasValue) && totalInvested > 0
                ? Math.Round(totalGainLoss / enriched.Where(h => h.currentValue.HasValue).Sum(h => h.totalCost) * 100, 2)
                : 0;
            var totalAnnualDividends = enriched.Where(h => h.annualDividendIncome.HasValue).Sum(h => h.annualDividendIncome!.Value);

            return new
            {
                summary = new
                {
                    totalHoldings = holdings.Count,
                    totalInvested = Math.Round(totalInvested, 2),
                    currentValue = Math.Round(totalCurrentValue, 2),
                    gainLoss = Math.Round(totalGainLoss, 2),
                    gainLossPct = totalGainLossPct,
                    annualDividendIncome = Math.Round(totalAnnualDividends, 2)
                },
                holdings = enriched
            };
        }

        public async Task<HoldingModel> AddHoldingAsync(AddHoldingRequest request)
        {
            var holding = new HoldingModel
            {
                Symbol = request.Symbol.ToUpper().Trim(),
                Shares = request.Shares,
                BuyPrice = request.BuyPrice,
                BuyDate = request.BuyDate,
                Notes = request.Notes,
                AddedAt = DateTime.UtcNow
            };

            await _holdingRepo.AddAsync(holding);
            _logger.LogInformation("Added holding: {Symbol} x{Shares} @ {Price}", holding.Symbol, holding.Shares, holding.BuyPrice);
            return holding;
        }

        public async Task<HoldingModel?> UpdateHoldingAsync(int id, AddHoldingRequest request)
        {
            var holding = await _holdingRepo.GetByIdAsync(id);
            if (holding == null) return null;

            holding.Symbol = request.Symbol.ToUpper().Trim();
            holding.Shares = request.Shares;
            holding.BuyPrice = request.BuyPrice;
            holding.BuyDate = request.BuyDate;
            holding.Notes = request.Notes;

            await _holdingRepo.UpdateAsync(holding);
            return holding;
        }

        public async Task<HoldingModel?> DeleteHoldingAsync(int id)
        {
            var holding = await _holdingRepo.GetByIdAsync(id);
            if (holding == null) return null;

            await _holdingRepo.DeleteAsync(holding);
            return holding;
        }
    }
}
