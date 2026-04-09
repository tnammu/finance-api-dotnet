using FinanceApi.Data;
using FinanceApi.Model;
using FinanceApi.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FinanceApi.Repositories
{
    public class HoldingRepository : IHoldingRepository
    {
        private readonly DividendDbContext _db;

        public HoldingRepository(DividendDbContext db) => _db = db;

        public Task<List<HoldingModel>> GetAllOrderedBySymbolAsync()
            => _db.Holdings.OrderBy(h => h.Symbol).ToListAsync();

        public async Task<HoldingModel?> GetByIdAsync(int id)
            => await _db.Holdings.FindAsync(id);

        public async Task<HoldingModel> AddAsync(HoldingModel holding)
        {
            _db.Holdings.Add(holding);
            await _db.SaveChangesAsync();
            return holding;
        }

        public Task UpdateAsync(HoldingModel holding)
            => _db.SaveChangesAsync();

        public async Task DeleteAsync(HoldingModel holding)
        {
            _db.Holdings.Remove(holding);
            await _db.SaveChangesAsync();
        }
    }
}
