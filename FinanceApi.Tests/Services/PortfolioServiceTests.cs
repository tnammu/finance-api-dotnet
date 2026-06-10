using System.Text.Json;
using FinanceApi.Model;
using FinanceApi.Repositories.Interfaces;
using FinanceApi.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace FinanceApi.Tests.Services;

public class PortfolioServiceTests
{
    private static PortfolioService Create(Mock<IHoldingRepository> hr, Mock<IDividendRepository> dr) =>
        new(hr.Object, dr.Object, Mock.Of<ILogger<PortfolioService>>());

    private static HoldingModel Holding(string symbol, decimal shares = 10, decimal buyPrice = 100) =>
        new() { Symbol = symbol, Shares = shares, BuyPrice = buyPrice, BuyDate = DateTime.UtcNow };

    private static StockEnrichmentData Enrichment(string symbol, decimal price, decimal? divPerShare = null) =>
        new() { Symbol = symbol, CompanyName = $"{symbol} Inc.", CurrentPrice = price, DividendPerShare = divPerShare };

    [Fact]
    public async Task GetHoldings_ReturnsEnrichedData_WhenExactSymbolFound()
    {
        var hr = new Mock<IHoldingRepository>();
        var dr = new Mock<IDividendRepository>();

        hr.Setup(r => r.GetAllOrderedBySymbolAsync()).ReturnsAsync([Holding("AAPL", 10, 100)]);
        dr.Setup(r => r.GetEnrichmentBySymbolsAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new Dictionary<string, StockEnrichmentData> { ["AAPL"] = Enrichment("AAPL", 150) });

        var result = await Create(hr, dr).GetHoldingsAsync();
        var json = JsonDocument.Parse(JsonSerializer.Serialize(result)).RootElement;

        Assert.Equal(1, json.GetProperty("summary").GetProperty("totalHoldings").GetInt32());
        Assert.Equal(1500m, json.GetProperty("summary").GetProperty("currentValue").GetDecimal());
    }

    [Fact]
    public async Task GetHoldings_FallsBackToToSuffix_ForCanadianStocks()
    {
        var hr = new Mock<IHoldingRepository>();
        var dr = new Mock<IDividendRepository>();

        hr.Setup(r => r.GetAllOrderedBySymbolAsync()).ReturnsAsync([Holding("ENB", 20, 50)]);
        dr.Setup(r => r.GetEnrichmentBySymbolsAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new Dictionary<string, StockEnrichmentData> { ["ENB.TO"] = Enrichment("ENB.TO", 55) });

        var result = await Create(hr, dr).GetHoldingsAsync();
        var json = JsonDocument.Parse(JsonSerializer.Serialize(result)).RootElement;

        Assert.Equal(1100m, json.GetProperty("summary").GetProperty("currentValue").GetDecimal()); // 20 * 55
    }

    [Fact]
    public async Task GetHoldings_UsesMarketPrice_WhenNoStockDataFound()
    {
        var hr = new Mock<IHoldingRepository>();
        var dr = new Mock<IDividendRepository>();

        var holding = Holding("XYZ", 5, 10);
        holding.MarketPrice = 12;

        hr.Setup(r => r.GetAllOrderedBySymbolAsync()).ReturnsAsync([holding]);
        dr.Setup(r => r.GetEnrichmentBySymbolsAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new Dictionary<string, StockEnrichmentData>());

        var result = await Create(hr, dr).GetHoldingsAsync();
        var json = JsonDocument.Parse(JsonSerializer.Serialize(result)).RootElement;

        Assert.Equal(60m, json.GetProperty("summary").GetProperty("currentValue").GetDecimal()); // 5 * 12
    }

    [Fact]
    public async Task GetHoldings_CalculatesGainLoss_Correctly()
    {
        var hr = new Mock<IHoldingRepository>();
        var dr = new Mock<IDividendRepository>();

        hr.Setup(r => r.GetAllOrderedBySymbolAsync()).ReturnsAsync([Holding("TD.TO", 10, 80)]);
        dr.Setup(r => r.GetEnrichmentBySymbolsAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new Dictionary<string, StockEnrichmentData> { ["TD.TO"] = Enrichment("TD.TO", 100) });

        var result = await Create(hr, dr).GetHoldingsAsync();
        var json = JsonDocument.Parse(JsonSerializer.Serialize(result)).RootElement;
        var summary = json.GetProperty("summary");

        Assert.Equal(200m, summary.GetProperty("gainLoss").GetDecimal());   // (100-80) * 10
        Assert.Equal(25m, summary.GetProperty("gainLossPct").GetDecimal()); // 200/800 * 100
    }

    [Fact]
    public async Task GetHoldings_CalculatesAnnualDividendIncome_FromEnrichment()
    {
        var hr = new Mock<IHoldingRepository>();
        var dr = new Mock<IDividendRepository>();

        hr.Setup(r => r.GetAllOrderedBySymbolAsync()).ReturnsAsync([Holding("RY.TO", 100, 130)]);
        dr.Setup(r => r.GetEnrichmentBySymbolsAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new Dictionary<string, StockEnrichmentData>
            {
                ["RY.TO"] = Enrichment("RY.TO", 130, divPerShare: 5.52m)
            });

        var result = await Create(hr, dr).GetHoldingsAsync();
        var json = JsonDocument.Parse(JsonSerializer.Serialize(result)).RootElement;

        Assert.Equal(552m, json.GetProperty("summary").GetProperty("annualDividendIncome").GetDecimal());
    }

    [Fact]
    public async Task AddHolding_NormalizesSymbolToUppercase()
    {
        var hr = new Mock<IHoldingRepository>();
        var dr = new Mock<IDividendRepository>();
        hr.Setup(r => r.AddAsync(It.IsAny<HoldingModel>())).ReturnsAsync((HoldingModel h) => h);

        var result = await Create(hr, dr).AddHoldingAsync(new AddHoldingRequest
        {
            Symbol = "  aapl  ",
            Shares = 10,
            BuyPrice = 100,
            BuyDate = DateTime.UtcNow
        });

        Assert.Equal("AAPL", result.Symbol);
    }

    [Fact]
    public async Task UpdateHolding_ReturnsNull_WhenIdNotFound()
    {
        var hr = new Mock<IHoldingRepository>();
        var dr = new Mock<IDividendRepository>();
        hr.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((HoldingModel?)null);

        var result = await Create(hr, dr).UpdateHoldingAsync(999, new AddHoldingRequest
        {
            Symbol = "X",
            BuyDate = DateTime.UtcNow
        });

        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteHolding_ReturnsNull_WhenIdNotFound()
    {
        var hr = new Mock<IHoldingRepository>();
        var dr = new Mock<IDividendRepository>();
        hr.Setup(r => r.GetByIdAsync(42)).ReturnsAsync((HoldingModel?)null);

        var result = await Create(hr, dr).DeleteHoldingAsync(42);

        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteHolding_ReturnsHolding_WhenFound()
    {
        var hr = new Mock<IHoldingRepository>();
        var dr = new Mock<IDividendRepository>();
        var holding = Holding("BCE.TO");
        holding.Id = 5;
        hr.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(holding);
        hr.Setup(r => r.DeleteAsync(holding)).Returns(Task.CompletedTask);

        var result = await Create(hr, dr).DeleteHoldingAsync(5);

        Assert.NotNull(result);
        Assert.Equal("BCE.TO", result.Symbol);
    }
}
