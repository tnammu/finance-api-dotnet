using FinanceApi.Models;
using FinanceApi.Repositories.Interfaces;
using FinanceApi.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace FinanceApi.Tests.Services;

public class GrowthStockServiceTests
{
    private static GrowthStockService Create(Mock<IDividendRepository> dr) =>
        new(Mock.Of<ILogger<GrowthStockService>>(), dr.Object);

    private static DividendModel MakeStock(string symbol, decimal growthScore, string sector = "Tech") =>
        new()
        {
            Symbol = symbol,
            CompanyName = $"{symbol} Inc.",
            Sector = sector,
            GrowthScore = growthScore,
            LastUpdated = DateTime.UtcNow,
            FetchedAt = DateTime.UtcNow
        };

    [Fact]
    public async Task GetAllGrowthStocks_ReturnsOrderedByGrowthScoreDescending()
    {
        var dr = new Mock<IDividendRepository>();
        dr.Setup(r => r.GetWithGrowthScoreAsync()).ReturnsAsync([
            MakeStock("LOW.TO", 40),
            MakeStock("HIGH.TO", 90),
            MakeStock("MID.TO", 65)
        ]);

        var result = await Create(dr).GetAllGrowthStocksAsync();

        Assert.Equal(3, result.Count);
        Assert.Equal("HIGH.TO", result[0].Symbol);
        Assert.Equal("MID.TO", result[1].Symbol);
        Assert.Equal("LOW.TO", result[2].Symbol);
    }

    [Fact]
    public async Task GetTopGrowthStocks_ReturnsTopN()
    {
        var dr = new Mock<IDividendRepository>();
        dr.Setup(r => r.GetWithGrowthScoreAsync()).ReturnsAsync([
            MakeStock("A", 80),
            MakeStock("B", 70),
            MakeStock("C", 60),
            MakeStock("D", 50)
        ]);

        var result = await Create(dr).GetTopGrowthStocksAsync(2);

        Assert.Equal(2, result.Count);
        Assert.Equal("A", result[0].Symbol);
        Assert.Equal("B", result[1].Symbol);
    }

    [Fact]
    public async Task CompareGrowthStocks_ReportsMissingSymbols()
    {
        var dr = new Mock<IDividendRepository>();
        dr.Setup(r => r.GetBySymbolsAsync(It.IsAny<List<string>>())).ReturnsAsync([
            MakeStock("AAPL", 75)
        ]);

        var result = await Create(dr).CompareGrowthStocksAsync(new List<string> { "AAPL", "MISSING1", "MISSING2" });

        Assert.Equal(1, result.Found);
        Assert.Equal(2, result.MissingSymbols.Count);
        Assert.Contains("MISSING1", result.MissingSymbols);
        Assert.Contains("MISSING2", result.MissingSymbols);
    }

    [Fact]
    public async Task CompareGrowthStocks_ParsesCommaSeparatedSymbols()
    {
        var dr = new Mock<IDividendRepository>();
        dr.Setup(r => r.GetBySymbolsAsync(It.Is<List<string>>(l =>
            l.Contains("AAPL") && l.Contains("MSFT") && l.Contains("NVDA"))))
            .ReturnsAsync([MakeStock("AAPL", 80), MakeStock("MSFT", 75), MakeStock("NVDA", 90)]);

        var result = await Create(dr).CompareGrowthStocksAsync("aapl,msft,nvda");

        Assert.Equal(3, result.Found);
        Assert.Empty(result.MissingSymbols);
    }

    [Fact]
    public async Task GetAllGrowthStocks_MapsFieldsToDto()
    {
        var dr = new Mock<IDividendRepository>();
        var stock = MakeStock("SHOP.TO", 88);
        stock.GrowthRating = "Strong Growth";
        stock.RevenueGrowth = 0.25m;
        stock.CurrentPrice = 120;
        dr.Setup(r => r.GetWithGrowthScoreAsync()).ReturnsAsync([stock]);

        var result = await Create(dr).GetAllGrowthStocksAsync();

        Assert.Single(result);
        Assert.Equal("SHOP.TO", result[0].Symbol);
        Assert.Equal(88m, result[0].GrowthScore);
        Assert.Equal("Strong Growth", result[0].GrowthRating);
        Assert.Equal(0.25m, result[0].RevenueGrowth);
        Assert.Equal(120m, result[0].CurrentPrice);
    }
}
