using Microsoft.EntityFrameworkCore;
using FinanceApi.Data;
using FinanceApi.Model;

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
        private readonly DividendDbContext _db;
        private readonly ILogger<PortfolioService> _logger;

        public PortfolioService(DividendDbContext db, ILogger<PortfolioService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<object> GetHoldingsAsync()
        {
            var holdings = await _db.Holdings.OrderBy(h => h.Symbol).ToListAsync();

            var symbols = holdings.Select(h => h.Symbol).ToList();
            var stockData = await _db.DividendModels
                .Where(d => symbols.Contains(d.Symbol))
                .Select(d => new { d.Symbol, d.CompanyName, d.CurrentPrice, d.DividendPerShare, d.Sector })
                .ToDictionaryAsync(d => d.Symbol);

            var enriched = holdings.Select(h =>
            {
                stockData.TryGetValue(h.Symbol, out var stock);
                var currentPrice = stock != null ? (decimal?)stock.CurrentPrice : null;
                var totalCost = h.Shares * h.BuyPrice;
                var currentValue = currentPrice.HasValue ? h.Shares * currentPrice.Value : (decimal?)null;
                var gainLossDollar = currentValue.HasValue ? currentValue.Value - totalCost : (decimal?)null;
                var gainLossPct = gainLossDollar.HasValue && totalCost > 0
                    ? Math.Round(gainLossDollar.Value / totalCost * 100, 2)
                    : (decimal?)null;
                var annualDividendIncome = stock != null && stock.DividendPerShare.HasValue
                    ? Math.Round(h.Shares * stock.DividendPerShare!.Value, 2)
                    : (decimal?)null;
                var dividendYield = stock != null && stock.DividendPerShare.HasValue && stock.CurrentPrice > 0
                    ? Math.Round(stock.DividendPerShare!.Value / stock.CurrentPrice * 100, 2)
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

            _db.Holdings.Add(holding);
            await _db.SaveChangesAsync();
            _logger.LogInformation("Added holding: {Symbol} x{Shares} @ {Price}", holding.Symbol, holding.Shares, holding.BuyPrice);
            return holding;
        }

        public async Task<HoldingModel?> UpdateHoldingAsync(int id, AddHoldingRequest request)
        {
            var holding = await _db.Holdings.FindAsync(id);
            if (holding == null) return null;

            holding.Symbol = request.Symbol.ToUpper().Trim();
            holding.Shares = request.Shares;
            holding.BuyPrice = request.BuyPrice;
            holding.BuyDate = request.BuyDate;
            holding.Notes = request.Notes;

            await _db.SaveChangesAsync();
            return holding;
        }

        public async Task<HoldingModel?> DeleteHoldingAsync(int id)
        {
            var holding = await _db.Holdings.FindAsync(id);
            if (holding == null) return null;

            _db.Holdings.Remove(holding);
            await _db.SaveChangesAsync();
            return holding;
        }
    }
}
