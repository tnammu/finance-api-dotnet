using FinanceApi.Data;
using FinanceApi.Models;
using FinanceApi.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FinanceApi.Repositories
{
    public class DividendRepository : IDividendRepository
    {
        private readonly DividendDbContext _db;

        public DividendRepository(DividendDbContext db) => _db = db;

        public Task<List<DividendModel>> GetAllAsync()
            => _db.DividendModels.ToListAsync();

        public Task<List<DividendModel>> GetAllOrderedBySymbolAsync()
            => _db.DividendModels.OrderBy(d => d.Symbol).ToListAsync();

        public async Task<DividendModel?> GetByIdAsync(int id)
            => await _db.DividendModels.FindAsync(id);

        public Task<DividendModel?> GetBySymbolAsync(string symbol)
            => _db.DividendModels.FirstOrDefaultAsync(d => d.Symbol == symbol.ToUpper());

        public Task<DividendModel?> GetBySymbolWithDetailsAsync(string symbol)
            => _db.DividendModels
                .Include(d => d.DividendPayments)
                .Include(d => d.YearlyDividends)
                .FirstOrDefaultAsync(d => d.Symbol == symbol.ToUpper());

        public Task<bool> ExistsAsync(string symbol)
            => _db.DividendModels.AnyAsync(d => d.Symbol == symbol);

        public Task<int> CountAsync()
            => _db.DividendModels.CountAsync();

        public Task<int> CountPaymentsAsync()
            => _db.DividendPayments.CountAsync();

        public Task<int> CountYearlyAsync()
            => _db.YearlyDividends.CountAsync();

        public Task<List<DividendModel>> GetBySymbolsAsync(IEnumerable<string> symbols)
            => _db.DividendModels.Where(d => symbols.Contains(d.Symbol)).ToListAsync();

        public Task<List<DividendModel>> GetWithGrowthScoreAsync()
            => _db.DividendModels.Where(d => d.GrowthScore > 0).ToListAsync();

        public async Task<Dictionary<string, StockEnrichmentData>> GetEnrichmentBySymbolsAsync(IEnumerable<string> symbols)
            => await _db.DividendModels
                .Where(d => symbols.Contains(d.Symbol))
                .Select(d => new StockEnrichmentData
                {
                    Symbol = d.Symbol,
                    CompanyName = d.CompanyName,
                    CurrentPrice = d.CurrentPrice,
                    DividendPerShare = d.DividendPerShare,
                    Sector = d.Sector
                })
                .ToDictionaryAsync(d => d.Symbol);

        public async Task AddAsync(DividendModel model)
        {
            _db.DividendModels.Add(model);
            await _db.SaveChangesAsync();
        }

        public Task SaveAsync()
            => _db.SaveChangesAsync();

        public async Task DeleteAsync(DividendModel model)
        {
            _db.DividendModels.Remove(model);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteBySymbolWithDetailsAsync(string symbol)
        {
            var stock = await _db.DividendModels
                .Include(d => d.DividendPayments)
                .Include(d => d.YearlyDividends)
                .FirstOrDefaultAsync(d => d.Symbol == symbol.ToUpper());

            if (stock == null) return;

            _db.DividendPayments.RemoveRange(stock.DividendPayments);
            _db.YearlyDividends.RemoveRange(stock.YearlyDividends);
            _db.DividendModels.Remove(stock);
            await _db.SaveChangesAsync();
        }

        public Task<ApiUsageLog?> GetApiUsageTodayAsync()
        {
            var today = DateTime.UtcNow.Date;
            return _db.ApiUsageLogs.FirstOrDefaultAsync(l => l.Date == today);
        }

        public Task<List<ApiUsageLog>> GetApiUsageHistoryAsync(DateTime startDate)
            => _db.ApiUsageLogs
                .Where(l => l.Date >= startDate)
                .OrderByDescending(l => l.Date)
                .ToListAsync();
    }
}
