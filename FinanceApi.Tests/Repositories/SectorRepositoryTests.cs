using FinanceApi.Data;
using FinanceApi.Models;
using FinanceApi.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FinanceApi.Tests.Repositories;

public class SectorRepositoryTests
{
    private static DividendDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<DividendDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static SectorPerformance MakePerformance(string sector, string period = "current") =>
        new() { Sector = sector, Period = period, AverageReturn = 5m, CalculatedAt = DateTime.UtcNow, LastUpdated = DateTime.UtcNow };

    private static StockSectorComparison MakeComparison(string symbol, string sector = "Energy") =>
        new() { Symbol = symbol, Sector = sector, CalculatedAt = DateTime.UtcNow };

    [Fact]
    public async Task UpsertSectorPerformance_AddsNewRecord_WhenNotExists()
    {
        using var ctx = CreateContext();
        var repo = new SectorRepository(ctx);

        await repo.UpsertSectorPerformanceAsync(MakePerformance("Energy"));

        Assert.Equal(1, await ctx.SectorPerformances.CountAsync());
        Assert.Equal("Energy", ctx.SectorPerformances.Single().Sector);
    }

    [Fact]
    public async Task UpsertSectorPerformance_UpdatesExistingRecord()
    {
        using var ctx = CreateContext();
        ctx.SectorPerformances.Add(MakePerformance("Financials"));
        await ctx.SaveChangesAsync();

        var repo = new SectorRepository(ctx);
        var updated = MakePerformance("Financials");
        updated.AverageReturn = 12.5m;
        updated.StockCount = 20;
        await repo.UpsertSectorPerformanceAsync(updated);

        Assert.Equal(1, await ctx.SectorPerformances.CountAsync()); // no duplicate
        Assert.Equal(12.5m, ctx.SectorPerformances.Single().AverageReturn);
    }

    [Fact]
    public async Task UpsertStockSectorComparison_AddsNewRecord_WhenNotExists()
    {
        using var ctx = CreateContext();
        var repo = new SectorRepository(ctx);

        await repo.UpsertStockSectorComparisonAsync(MakeComparison("ENB.TO"));

        Assert.Equal(1, await ctx.StockSectorComparisons.CountAsync());
    }

    [Fact]
    public async Task UpsertStockSectorComparison_UpdatesExistingRecord()
    {
        using var ctx = CreateContext();
        ctx.StockSectorComparisons.Add(MakeComparison("RY.TO", "Financials"));
        await ctx.SaveChangesAsync();

        var repo = new SectorRepository(ctx);
        var updated = MakeComparison("RY.TO", "Financials");
        updated.PerformanceRank = 3;
        await repo.UpsertStockSectorComparisonAsync(updated);

        Assert.Equal(1, await ctx.StockSectorComparisons.CountAsync()); // no duplicate
        Assert.Equal(3, ctx.StockSectorComparisons.Single().PerformanceRank);
    }

    [Fact]
    public async Task GetAllSectorPerformances_ReturnsOrderedByAverageReturnDescending()
    {
        using var ctx = CreateContext();
        var low = MakePerformance("LowSector"); low.AverageReturn = 2m;
        var high = MakePerformance("HighSector"); high.AverageReturn = 15m;
        var mid = MakePerformance("MidSector"); mid.AverageReturn = 8m;
        ctx.SectorPerformances.AddRange(low, high, mid);
        await ctx.SaveChangesAsync();

        var repo = new SectorRepository(ctx);
        var result = await repo.GetAllSectorPerformancesAsync();

        Assert.Equal("HighSector", result[0].Sector);
        Assert.Equal("LowSector", result[2].Sector);
    }
}
