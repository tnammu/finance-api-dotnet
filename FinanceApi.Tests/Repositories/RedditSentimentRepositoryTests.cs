using FinanceApi.Data;
using FinanceApi.Model;
using FinanceApi.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FinanceApi.Tests.Repositories;

public class RedditSentimentRepositoryTests
{
    private static DividendDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<DividendDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static RedditSentimentModel MakeSentiment(string symbol, int trendingScore = 50, int mentions24h = 10) =>
        new()
        {
            Symbol = symbol,
            CompanyName = $"{symbol} Inc.",
            TrendingScore = trendingScore,
            MentionCount24h = mentions24h,
            LastUpdated = DateTime.UtcNow
        };

    [Fact]
    public async Task GetAllOrderedByTrendingScore_ReturnsSortedDescending()
    {
        using var ctx = CreateContext();
        ctx.RedditSentiments.AddRange(
            MakeSentiment("AAPL", trendingScore: 30),
            MakeSentiment("GME", trendingScore: 95),
            MakeSentiment("TSLA", trendingScore: 60));
        await ctx.SaveChangesAsync();

        var repo = new RedditSentimentRepository(ctx);
        var result = await repo.GetAllOrderedByTrendingScoreAsync();

        Assert.Equal("GME", result[0].Symbol);
        Assert.Equal("AAPL", result[2].Symbol);
    }

    [Fact]
    public async Task GetTrending_FiltersOutBelowMinMentions()
    {
        using var ctx = CreateContext();
        ctx.RedditSentiments.AddRange(
            MakeSentiment("LOUD", trendingScore: 90, mentions24h: 20),
            MakeSentiment("QUIET", trendingScore: 80, mentions24h: 2),  // below threshold
            MakeSentiment("MID", trendingScore: 70, mentions24h: 10));
        await ctx.SaveChangesAsync();

        var repo = new RedditSentimentRepository(ctx);
        var result = await repo.GetTrendingAsync(minMentions: 5, count: 10);

        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, r => r.Symbol == "QUIET");
    }

    [Fact]
    public async Task GetTrending_RespectsCountLimit()
    {
        using var ctx = CreateContext();
        for (int i = 0; i < 10; i++)
            ctx.RedditSentiments.Add(MakeSentiment($"STOCK{i}", trendingScore: i * 10, mentions24h: 10));
        await ctx.SaveChangesAsync();

        var repo = new RedditSentimentRepository(ctx);
        var result = await repo.GetTrendingAsync(minMentions: 5, count: 3);

        Assert.Equal(3, result.Count);
    }
}
