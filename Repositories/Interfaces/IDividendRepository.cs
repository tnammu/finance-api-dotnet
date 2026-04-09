using FinanceApi.Models;

namespace FinanceApi.Repositories.Interfaces
{
    /// <summary>Projection used by PortfolioService for holdings enrichment.</summary>
    public class StockEnrichmentData
    {
        public string Symbol { get; set; } = string.Empty;
        public string? CompanyName { get; set; }
        public decimal CurrentPrice { get; set; }
        public decimal? DividendPerShare { get; set; }
        public string? Sector { get; set; }
    }

    public interface IDividendRepository
    {
        // Queries
        Task<List<DividendModel>> GetAllAsync();
        Task<List<DividendModel>> GetAllOrderedBySymbolAsync();
        Task<DividendModel?> GetByIdAsync(int id);
        Task<DividendModel?> GetBySymbolAsync(string symbol);
        Task<DividendModel?> GetBySymbolWithDetailsAsync(string symbol);
        Task<bool> ExistsAsync(string symbol);
        Task<int> CountAsync();
        Task<int> CountPaymentsAsync();
        Task<int> CountYearlyAsync();

        // Filtered queries
        Task<List<DividendModel>> GetBySymbolsAsync(IEnumerable<string> symbols);
        Task<List<DividendModel>> GetWithGrowthScoreAsync();
        Task<Dictionary<string, StockEnrichmentData>> GetEnrichmentBySymbolsAsync(IEnumerable<string> symbols);

        // Mutations
        Task AddAsync(DividendModel model);
        Task SaveAsync();
        Task DeleteAsync(DividendModel model);
        Task DeleteBySymbolWithDetailsAsync(string symbol);

        // API usage logs
        Task<ApiUsageLog?> GetApiUsageTodayAsync();
        Task<List<ApiUsageLog>> GetApiUsageHistoryAsync(DateTime startDate);
    }
}
