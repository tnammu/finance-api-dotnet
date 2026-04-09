using FinanceApi.Model;

namespace FinanceApi.Repositories.Interfaces
{
    public interface IHoldingRepository
    {
        Task<List<HoldingModel>> GetAllOrderedBySymbolAsync();
        Task<HoldingModel?> GetByIdAsync(int id);
        Task<HoldingModel> AddAsync(HoldingModel holding);
        Task UpdateAsync(HoldingModel holding);
        Task DeleteAsync(HoldingModel holding);
    }
}
