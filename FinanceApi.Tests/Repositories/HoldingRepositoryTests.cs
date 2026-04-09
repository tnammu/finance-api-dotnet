using FinanceApi.Data;
using FinanceApi.Model;
using FinanceApi.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FinanceApi.Tests.Repositories;

public class HoldingRepositoryTests
{
    private static DividendDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<DividendDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static HoldingModel MakeHolding(string symbol, decimal shares = 10, decimal buyPrice = 50) =>
        new() { Symbol = symbol, Shares = shares, BuyPrice = buyPrice, BuyDate = DateTime.UtcNow };

    [Fact]
    public async Task GetAllOrderedBySymbol_ReturnsHoldingsAlphabetically()
    {
        using var ctx = CreateContext();
        ctx.Holdings.AddRange(MakeHolding("ZZZ"), MakeHolding("AAA"), MakeHolding("MMM"));
        await ctx.SaveChangesAsync();

        var repo = new HoldingRepository(ctx);
        var result = await repo.GetAllOrderedBySymbolAsync();

        Assert.Equal(["AAA", "MMM", "ZZZ"], result.Select(h => h.Symbol));
    }

    [Fact]
    public async Task GetById_ReturnsCorrectHolding()
    {
        using var ctx = CreateContext();
        var holding = MakeHolding("ENB.TO");
        ctx.Holdings.Add(holding);
        await ctx.SaveChangesAsync();

        var repo = new HoldingRepository(ctx);
        var result = await repo.GetByIdAsync(holding.Id);

        Assert.NotNull(result);
        Assert.Equal("ENB.TO", result.Symbol);
    }

    [Fact]
    public async Task GetById_ReturnsNull_WhenNotFound()
    {
        using var ctx = CreateContext();
        var repo = new HoldingRepository(ctx);

        var result = await repo.GetByIdAsync(999);

        Assert.Null(result);
    }

    [Fact]
    public async Task Add_PersistsHolding()
    {
        using var ctx = CreateContext();
        var repo = new HoldingRepository(ctx);

        var holding = MakeHolding("TD.TO", shares: 25, buyPrice: 80);
        await repo.AddAsync(holding);

        Assert.Equal(1, await ctx.Holdings.CountAsync());
        Assert.Equal("TD.TO", ctx.Holdings.Single().Symbol);
    }

    [Fact]
    public async Task Update_SavesChangesToHolding()
    {
        using var ctx = CreateContext();
        var holding = MakeHolding("RY.TO");
        ctx.Holdings.Add(holding);
        await ctx.SaveChangesAsync();

        var repo = new HoldingRepository(ctx);
        holding.Shares = 99;
        await repo.UpdateAsync(holding);

        Assert.Equal(99, ctx.Holdings.Single().Shares);
    }

    [Fact]
    public async Task Delete_RemovesHolding()
    {
        using var ctx = CreateContext();
        var holding = MakeHolding("BCE.TO");
        ctx.Holdings.Add(holding);
        await ctx.SaveChangesAsync();

        var repo = new HoldingRepository(ctx);
        await repo.DeleteAsync(holding);

        Assert.Empty(ctx.Holdings);
    }
}
