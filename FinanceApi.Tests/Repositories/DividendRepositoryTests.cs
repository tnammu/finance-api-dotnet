using FinanceApi.Data;
using FinanceApi.Models;
using FinanceApi.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FinanceApi.Tests.Repositories;

public class DividendRepositoryTests
{
    private static DividendDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<DividendDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static DividendModel MakeStock(string symbol, decimal price = 50, decimal growthScore = 0, string sector = "Energy") =>
        new()
        {
            Symbol = symbol,
            CompanyName = $"{symbol} Inc.",
            CurrentPrice = price,
            GrowthScore = growthScore,
            Sector = sector,
            LastUpdated = DateTime.UtcNow,
            FetchedAt = DateTime.UtcNow
        };

    [Fact]
    public async Task GetAll_ReturnsAllStocks()
    {
        using var ctx = CreateContext();
        ctx.DividendModels.AddRange(MakeStock("AAPL"), MakeStock("MSFT"), MakeStock("TD.TO"));
        await ctx.SaveChangesAsync();

        var repo = new DividendRepository(ctx);
        var result = await repo.GetAllAsync();

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task GetBySymbol_ReturnsCorrectStock()
    {
        using var ctx = CreateContext();
        ctx.DividendModels.Add(MakeStock("ENB.TO"));
        await ctx.SaveChangesAsync();

        var repo = new DividendRepository(ctx);
        var result = await repo.GetBySymbolAsync("enb.to"); // test case-insensitive

        Assert.NotNull(result);
        Assert.Equal("ENB.TO", result.Symbol);
    }

    [Fact]
    public async Task GetBySymbol_ReturnsNull_WhenNotFound()
    {
        using var ctx = CreateContext();
        var repo = new DividendRepository(ctx);

        var result = await repo.GetBySymbolAsync("MISSING");

        Assert.Null(result);
    }

    [Fact]
    public async Task Exists_ReturnsTrue_WhenSymbolExists()
    {
        using var ctx = CreateContext();
        ctx.DividendModels.Add(MakeStock("BCE.TO"));
        await ctx.SaveChangesAsync();

        var repo = new DividendRepository(ctx);
        Assert.True(await repo.ExistsAsync("BCE.TO"));
    }

    [Fact]
    public async Task Exists_ReturnsFalse_WhenSymbolMissing()
    {
        using var ctx = CreateContext();
        var repo = new DividendRepository(ctx);
        Assert.False(await repo.ExistsAsync("NOPE"));
    }

    [Fact]
    public async Task GetWithGrowthScore_ReturnsOnlyPositiveScores()
    {
        using var ctx = CreateContext();
        ctx.DividendModels.AddRange(
            MakeStock("SHOP.TO", growthScore: 75),
            MakeStock("TD.TO", growthScore: 0),
            MakeStock("NVDA", growthScore: 90));
        await ctx.SaveChangesAsync();

        var repo = new DividendRepository(ctx);
        var result = await repo.GetWithGrowthScoreAsync();

        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, s => s.Symbol == "TD.TO");
    }

    [Fact]
    public async Task GetEnrichmentBySymbols_ReturnsProjectedDictionary()
    {
        using var ctx = CreateContext();
        ctx.DividendModels.AddRange(
            MakeStock("RY.TO", price: 130),
            MakeStock("TD.TO", price: 80));
        await ctx.SaveChangesAsync();

        var repo = new DividendRepository(ctx);
        var result = await repo.GetEnrichmentBySymbolsAsync(["RY.TO", "TD.TO", "MISSING"]);

        Assert.Equal(2, result.Count);
        Assert.Equal(130, result["RY.TO"].CurrentPrice);
        Assert.False(result.ContainsKey("MISSING"));
    }

    [Fact]
    public async Task DeleteBySymbolWithDetails_RemovesStockAndCascades()
    {
        using var ctx = CreateContext();
        var stock = MakeStock("DIV.TO");
        ctx.DividendModels.Add(stock);
        await ctx.SaveChangesAsync();

        ctx.DividendPayments.Add(new DividendPaymentRecord
        {
            Symbol = "DIV.TO",
            DividendModelId = stock.Id,
            PaymentDate = DateTime.UtcNow,
            Amount = 0.5m
        });
        await ctx.SaveChangesAsync();

        var repo = new DividendRepository(ctx);
        await repo.DeleteBySymbolWithDetailsAsync("DIV.TO");

        Assert.Empty(ctx.DividendModels);
        Assert.Empty(ctx.DividendPayments);
    }

    [Fact]
    public async Task GetApiUsageToday_ReturnsNullWhenNoEntry()
    {
        using var ctx = CreateContext();
        var repo = new DividendRepository(ctx);

        var result = await repo.GetApiUsageTodayAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task GetApiUsageHistory_ReturnsEntriesAfterStartDate()
    {
        using var ctx = CreateContext();
        ctx.ApiUsageLogs.AddRange(
            new ApiUsageLog { Date = DateTime.UtcNow.Date, CallsUsed = 5, DailyLimit = 25 },
            new ApiUsageLog { Date = DateTime.UtcNow.Date.AddDays(-10), CallsUsed = 3, DailyLimit = 25 },
            new ApiUsageLog { Date = DateTime.UtcNow.Date.AddDays(-40), CallsUsed = 1, DailyLimit = 25 });
        await ctx.SaveChangesAsync();

        var repo = new DividendRepository(ctx);
        var result = await repo.GetApiUsageHistoryAsync(DateTime.UtcNow.Date.AddDays(-30));

        Assert.Equal(2, result.Count);
    }
}
