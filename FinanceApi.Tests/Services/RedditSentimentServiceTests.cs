using FinanceApi.Model;
using FinanceApi.Repositories.Interfaces;
using FinanceApi.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace FinanceApi.Tests.Services;

public class RedditSentimentServiceTests
{
    private static RedditSentimentService Create(Mock<IRedditSentimentRepository> repo, IConfiguration? config = null)
    {
        config ??= new ConfigurationBuilder().Build();
        return new(Mock.Of<ILogger<RedditSentimentService>>(), repo.Object, config);
    }

    private static RedditSentimentModel MakeModel(string symbol, int trendingScore = 50,
        decimal sentiment = 0.5m, DateTime? lastUpdated = null) =>
        new()
        {
            Symbol = symbol,
            CompanyName = $"{symbol} Corp",
            Sentiment24h = sentiment,
            SentimentRating24h = "Bullish",
            MentionCount24h = 100,
            TrendingScore = trendingScore,
            PositiveRatio = 0.7m,
            LastUpdated = lastUpdated ?? DateTime.UtcNow,
            Mentions = new List<RedditMention>(),
            DailySummaries = new List<RedditDailySummary>()
        };

    [Fact]
    public async Task GetAllSentiments_ReturnsMappedSummaries()
    {
        var repo = new Mock<IRedditSentimentRepository>();
        repo.Setup(r => r.GetAllOrderedByTrendingScoreAsync()).ReturnsAsync([
            MakeModel("AAPL", trendingScore: 80),
            MakeModel("MSFT", trendingScore: 60)
        ]);

        var result = await Create(repo).GetAllSentimentsAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal("AAPL", result[0].Symbol);
        Assert.Equal(80, result[0].TrendingScore);
    }

    [Fact]
    public async Task GetTrendingStocks_ReturnsMappedSummaries()
    {
        var repo = new Mock<IRedditSentimentRepository>();
        repo.Setup(r => r.GetTrendingAsync(5, 10)).ReturnsAsync([
            MakeModel("GME", trendingScore: 95),
            MakeModel("AMC", trendingScore: 85)
        ]);

        var result = await Create(repo).GetTrendingStocksAsync(10);

        Assert.Equal(2, result.Count);
        Assert.Equal("GME", result[0].Symbol);
        Assert.Equal(0.7m, result[0].PositiveRatio);
    }

    [Fact]
    public async Task GetSentiment_ReturnsCachedData_WhenFreshEnough()
    {
        var repo = new Mock<IRedditSentimentRepository>();
        var freshModel = MakeModel("NVDA", lastUpdated: DateTime.UtcNow.AddHours(-1));

        // GetFromDatabaseAsync calls GetBySymbolWithDetailsAsync
        repo.Setup(r => r.GetBySymbolWithDetailsAsync("NVDA")).ReturnsAsync(freshModel);

        var result = await Create(repo).GetSentimentAsync("NVDA");

        Assert.NotNull(result);
        Assert.Equal("NVDA", result.Symbol);
        Assert.True(result.IsFromCache);

        // Should only call repo once (cache hit — no Python fetch)
        repo.Verify(r => r.GetBySymbolWithDetailsAsync("NVDA"), Times.Once);
    }

    [Fact]
    public async Task GetSentiment_ReturnsNull_WhenNotInDb()
    {
        var repo = new Mock<IRedditSentimentRepository>();
        repo.Setup(r => r.GetBySymbolWithDetailsAsync("UNKNOWN")).ReturnsAsync((RedditSentimentModel?)null);

        // Python script won't exist in test environment, so FetchSentimentViaPythonAsync returns false
        // Then stale fallback (GetFromDatabaseAsync) also returns null
        var result = await Create(repo).GetSentimentAsync("UNKNOWN");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetSentiment_UppercasesSymbol()
    {
        var repo = new Mock<IRedditSentimentRepository>();
        repo.Setup(r => r.GetBySymbolWithDetailsAsync("AAPL")).ReturnsAsync(MakeModel("AAPL"));

        var result = await Create(repo).GetSentimentAsync("aapl");

        Assert.NotNull(result);
        Assert.Equal("AAPL", result.Symbol);
    }

    [Fact]
    public async Task GetAllSentiments_ReturnsEmpty_WhenNoData()
    {
        var repo = new Mock<IRedditSentimentRepository>();
        repo.Setup(r => r.GetAllOrderedByTrendingScoreAsync()).ReturnsAsync([]);

        var result = await Create(repo).GetAllSentimentsAsync();

        Assert.Empty(result);
    }
}
