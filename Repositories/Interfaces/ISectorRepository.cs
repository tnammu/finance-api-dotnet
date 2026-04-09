using FinanceApi.Models;

namespace FinanceApi.Repositories.Interfaces
{
    public interface ISectorRepository
    {
        Task<SectorPerformance?> GetSectorPerformanceAsync(string sector, string period);
        Task<List<SectorPerformance>> GetAllSectorPerformancesAsync();
        Task<StockSectorComparison?> GetStockSectorComparisonAsync(string symbol);
        Task UpsertSectorPerformanceAsync(SectorPerformance performance);
        Task UpsertStockSectorComparisonAsync(StockSectorComparison comparison);
    }
}
