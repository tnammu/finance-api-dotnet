using FinanceApi.Models;
using FinanceApi.Repositories.Interfaces;
using FinanceApi.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace FinanceApi.Tests.Services;

public class StocksServiceTests
{
    private static StocksService Create(Mock<IDividendRepository> dr) =>
        new(dr.Object, Mock.Of<ILogger<StocksService>>());

    private static DividendModel MakeStock(string symbol, decimal price = 50,
        decimal? dividendYield = null, DateTime? lastUpdated = null) =>
        new()
        {
            Symbol = symbol,
            CompanyName = $"{symbol} Inc.",
            CurrentPrice = price,
            DividendYield = dividendYield,
            LastUpdated = lastUpdated ?? DateTime.UtcNow,
            FetchedAt = DateTime.UtcNow
        };

    [Fact]
    public async Task GetAllStocks_MapsToDtoWithIsStaleFlag()
    {
        var dr = new Mock<IDividendRepository>();
        dr.Setup(r => r.GetAllAsync()).ReturnsAsync([
            MakeStock("FRESH", lastUpdated: DateTime.UtcNow.AddMinutes(-5)),
            MakeStock("STALE", lastUpdated: DateTime.UtcNow.AddMinutes(-60))
        ]);

        var result = await Create(dr).GetAllStocksAsync();

        Assert.Equal(2, result.Count);
        Assert.False(result.First(s => s.Symbol == "FRESH").IsStale);
        Assert.True(result.First(s => s.Symbol == "STALE").IsStale);
    }

    [Fact]
    public async Task GetAllStocks_MapsSymbolAndPrice()
    {
        var dr = new Mock<IDividendRepository>();
        dr.Setup(r => r.GetAllAsync()).ReturnsAsync([MakeStock("ENB.TO", 55, 0.06m)]);

        var result = await Create(dr).GetAllStocksAsync();

        Assert.Single(result);
        Assert.Equal("ENB.TO", result[0].Symbol);
        Assert.Equal(55m, result[0].CurrentPrice);
        Assert.Equal(0.06m, result[0].DividendYield);
    }

    [Fact]
    public async Task GetStockById_ReturnsNull_WhenNotFound()
    {
        var dr = new Mock<IDividendRepository>();
        dr.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((DividendModel?)null);

        var result = await Create(dr).GetStockByIdAsync(999);

        Assert.Null(result);
    }

    [Fact]
    public async Task BulkAddStocks_SkipsExistingSymbols()
    {
        var dr = new Mock<IDividendRepository>();
        dr.Setup(r => r.ExistsAsync("AAPL")).ReturnsAsync(true);
        dr.Setup(r => r.ExistsAsync("MSFT")).ReturnsAsync(false);
        // Python script won't run in test env — FetchStockDataViaPythonAsync will return false
        // So MSFT will be attempted but fail, marked as "Failed"

        var result = await Create(dr).BulkAddStocksAsync(["AAPL", "MSFT"], addCanadianSuffix: false);

        Assert.Equal(2, result.TotalSubmitted);
        Assert.Equal(1, result.SkippedCount);
        var skipped = result.Results.First(r => r.Symbol == "AAPL");
        Assert.Equal("Skipped", skipped.Status);
    }

    [Fact]
    public async Task BulkAddStocks_AddsTOSuffix_WhenFlagSetAndNoDotInSymbol()
    {
        var dr = new Mock<IDividendRepository>();
        // Capture what symbol is checked for existence
        string? checkedSymbol = null;
        dr.Setup(r => r.ExistsAsync(It.IsAny<string>()))
            .Callback<string>(s => checkedSymbol = s)
            .ReturnsAsync(false);

        await Create(dr).BulkAddStocksAsync(["TD"], addCanadianSuffix: true);

        Assert.Equal("TD.TO", checkedSymbol);
    }

    [Fact]
    public async Task BulkAddStocks_DoesNotAddTOSuffix_WhenSymbolAlreadyHasDot()
    {
        var dr = new Mock<IDividendRepository>();
        string? checkedSymbol = null;
        dr.Setup(r => r.ExistsAsync(It.IsAny<string>()))
            .Callback<string>(s => checkedSymbol = s)
            .ReturnsAsync(false);

        await Create(dr).BulkAddStocksAsync(["RY.TO"], addCanadianSuffix: true);

        Assert.Equal("RY.TO", checkedSymbol);
    }

    [Fact]
    public async Task ExportToCsv_IncludesHeaderAndRows()
    {
        var dr = new Mock<IDividendRepository>();
        dr.Setup(r => r.GetAllOrderedBySymbolAsync()).ReturnsAsync([
            MakeStock("BCE.TO", 45, 0.085m)
        ]);

        var csv = await Create(dr).ExportToCsvAsync();

        Assert.Contains("Symbol,Company Name", csv);
        Assert.Contains("BCE.TO", csv);
        Assert.Contains("45.00", csv);
    }

    [Fact]
    public async Task DeleteStock_ReturnsFalse_WhenNotFound()
    {
        var dr = new Mock<IDividendRepository>();
        dr.Setup(r => r.GetByIdAsync(1)).ReturnsAsync((DividendModel?)null);

        var result = await Create(dr).DeleteStockAsync(1);

        Assert.False(result);
    }

    [Fact]
    public async Task DeleteStock_ReturnsTrue_WhenDeleted()
    {
        var dr = new Mock<IDividendRepository>();
        var stock = MakeStock("SHOP.TO");
        dr.Setup(r => r.GetByIdAsync(7)).ReturnsAsync(stock);
        dr.Setup(r => r.DeleteAsync(stock)).Returns(Task.CompletedTask);

        var result = await Create(dr).DeleteStockAsync(7);

        Assert.True(result);
    }
}
