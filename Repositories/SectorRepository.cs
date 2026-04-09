using FinanceApi.Data;
using FinanceApi.Models;
using FinanceApi.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FinanceApi.Repositories
{
    public class SectorRepository : ISectorRepository
    {
        private readonly DividendDbContext _db;

        public SectorRepository(DividendDbContext db) => _db = db;

        public Task<SectorPerformance?> GetSectorPerformanceAsync(string sector, string period)
            => _db.SectorPerformances.FirstOrDefaultAsync(sp => sp.Sector == sector && sp.Period == period);

        public Task<List<SectorPerformance>> GetAllSectorPerformancesAsync()
            => _db.SectorPerformances.OrderByDescending(sp => sp.AverageReturn).ToListAsync();

        public Task<StockSectorComparison?> GetStockSectorComparisonAsync(string symbol)
            => _db.StockSectorComparisons.FirstOrDefaultAsync(ssc => ssc.Symbol == symbol);

        public async Task UpsertSectorPerformanceAsync(SectorPerformance performance)
        {
            var existing = await GetSectorPerformanceAsync(performance.Sector, performance.Period);

            if (existing != null)
            {
                existing.AverageReturn = performance.AverageReturn;
                existing.MedianReturn = performance.MedianReturn;
                existing.TotalMarketCap = performance.TotalMarketCap;
                existing.StockCount = performance.StockCount;
                existing.RevenueGrowth = performance.RevenueGrowth;
                existing.EarningsGrowth = performance.EarningsGrowth;
                existing.DividendGrowth = performance.DividendGrowth;
                existing.AveragePE = performance.AveragePE;
                existing.AveragePB = performance.AveragePB;
                existing.AverageDividendYield = performance.AverageDividendYield;
                existing.Volatility = performance.Volatility;
                existing.Beta = performance.Beta;
                existing.LastUpdated = DateTime.UtcNow;
            }
            else
            {
                _db.SectorPerformances.Add(performance);
            }

            await _db.SaveChangesAsync();
        }

        public async Task UpsertStockSectorComparisonAsync(StockSectorComparison comparison)
        {
            var existing = await GetStockSectorComparisonAsync(comparison.Symbol);

            if (existing != null)
            {
                existing.Sector = comparison.Sector;
                existing.StockReturn1M = comparison.StockReturn1M;
                existing.SectorReturn1M = comparison.SectorReturn1M;
                existing.OutperformanceVsSector1M = comparison.OutperformanceVsSector1M;
                existing.StockReturn3M = comparison.StockReturn3M;
                existing.SectorReturn3M = comparison.SectorReturn3M;
                existing.OutperformanceVsSector3M = comparison.OutperformanceVsSector3M;
                existing.StockReturn1Y = comparison.StockReturn1Y;
                existing.SectorReturn1Y = comparison.SectorReturn1Y;
                existing.OutperformanceVsSector1Y = comparison.OutperformanceVsSector1Y;
                existing.StockPE = comparison.StockPE;
                existing.SectorAvgPE = comparison.SectorAvgPE;
                existing.PEPremiumDiscount = comparison.PEPremiumDiscount;
                existing.StockDividendYield = comparison.StockDividendYield;
                existing.SectorAvgDividendYield = comparison.SectorAvgDividendYield;
                existing.YieldPremiumDiscount = comparison.YieldPremiumDiscount;
                existing.PerformanceRank = comparison.PerformanceRank;
                existing.TotalStocksInSector = comparison.TotalStocksInSector;
                existing.PerformancePercentile = comparison.PerformancePercentile;
                existing.CalculatedAt = DateTime.UtcNow;
            }
            else
            {
                _db.StockSectorComparisons.Add(comparison);
            }

            await _db.SaveChangesAsync();
        }
    }
}
