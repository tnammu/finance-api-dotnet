using FinanceApi.Data;
using FinanceApi.Model;
using FinanceApi.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FinanceApi.Repositories
{
    public class RedditSentimentRepository : IRedditSentimentRepository
    {
        private readonly DividendDbContext _db;

        public RedditSentimentRepository(DividendDbContext db) => _db = db;

        public Task<RedditSentimentModel?> GetBySymbolWithDetailsAsync(string symbol)
            => _db.RedditSentiments
                .Include(r => r.Mentions.OrderByDescending(m => m.CreatedAt).Take(20))
                .Include(r => r.DailySummaries.OrderByDescending(d => d.Date).Take(30))
                .FirstOrDefaultAsync(r => r.Symbol == symbol);

        public Task<List<RedditSentimentModel>> GetAllOrderedByTrendingScoreAsync()
            => _db.RedditSentiments
                .OrderByDescending(r => r.TrendingScore)
                .ToListAsync();

        public Task<List<RedditSentimentModel>> GetTrendingAsync(int minMentions, int count)
            => _db.RedditSentiments
                .Where(r => r.MentionCount24h >= minMentions)
                .OrderByDescending(r => r.TrendingScore)
                .Take(count)
                .ToListAsync();
    }
}
