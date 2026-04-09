using FinanceApi.Model;

namespace FinanceApi.Repositories.Interfaces
{
    public interface IRedditSentimentRepository
    {
        Task<RedditSentimentModel?> GetBySymbolWithDetailsAsync(string symbol);
        Task<List<RedditSentimentModel>> GetAllOrderedByTrendingScoreAsync();
        Task<List<RedditSentimentModel>> GetTrendingAsync(int minMentions, int count);
    }
}
