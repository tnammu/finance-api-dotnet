using FinanceApi.Models;
using FinanceApi.Repositories.Interfaces;
using FinanceApi.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace FinanceApi.Tests.Services;

public class SectorAnalysisServiceTests
{
    private static SectorAnalysisService Create(Mock<IDividendRepository> dr, Mock<ISectorRepository> sr) =>
        new(dr.Object, sr.Object, Mock.Of<ILogger<SectorAnalysisService>>());

    private static DividendModel MakeStock(string symbol, string sector, decimal pe = 15, decimal beta = 1.0m) =>
        new()
        {
            Symbol = symbol,
            Sector = sector,
            PE = pe,
            Beta = beta,
            LastUpdated = DateTime.UtcNow,
            FetchedAt = DateTime.UtcNow
        };

    [Fact]
    public async Task CalculateAllSectorPerformances_GroupsStocksBySector()
    {
        var dr = new Mock<IDividendRepository>();
        var sr = new Mock<ISectorRepository>();

        dr.Setup(r => r.GetAllAsync()).ReturnsAsync([
            MakeStock("ENB.TO", "Energy"),
            MakeStock("CNQ.TO", "Energy"),
            MakeStock("RY.TO", "Financials"),
            MakeStock("TD.TO", "Financials"),
            MakeStock("BCE.TO", "Financials")
        ]);
        sr.Setup(r => r.UpsertSectorPerformanceAsync(It.IsAny<SectorPerformance>())).Returns(Task.CompletedTask);

        var result = await Create(dr, sr).CalculateAllSectorPerformances();

        Assert.Equal(2, result.Count);
        Assert.Equal(2, result.First(s => s.Sector == "Energy").StockCount);
        Assert.Equal(3, result.First(s => s.Sector == "Financials").StockCount);
    }

    [Fact]
    public async Task CalculateAllSectorPerformances_SkipsStocksWithNoSector()
    {
        var dr = new Mock<IDividendRepository>();
        var sr = new Mock<ISectorRepository>();

        dr.Setup(r => r.GetAllAsync()).ReturnsAsync([
            MakeStock("A", "Energy"),
            new DividendModel { Symbol = "NOSECTOR", Sector = "", LastUpdated = DateTime.UtcNow, FetchedAt = DateTime.UtcNow }
        ]);
        sr.Setup(r => r.UpsertSectorPerformanceAsync(It.IsAny<SectorPerformance>())).Returns(Task.CompletedTask);

        var result = await Create(dr, sr).CalculateAllSectorPerformances();

        Assert.Single(result);
        Assert.Equal("Energy", result[0].Sector);
    }

    [Fact]
    public async Task CalculateAllSectorPerformances_SavesEachSectorToRepo()
    {
        var dr = new Mock<IDividendRepository>();
        var sr = new Mock<ISectorRepository>();

        dr.Setup(r => r.GetAllAsync()).ReturnsAsync([
            MakeStock("A", "Energy"),
            MakeStock("B", "Tech")
        ]);
        sr.Setup(r => r.UpsertSectorPerformanceAsync(It.IsAny<SectorPerformance>())).Returns(Task.CompletedTask);

        await Create(dr, sr).CalculateAllSectorPerformances();

        sr.Verify(r => r.UpsertSectorPerformanceAsync(It.IsAny<SectorPerformance>()), Times.Exactly(2));
    }

    [Fact]
    public async Task CalculateStockSectorComparison_ReturnsNull_WhenSymbolNotFound()
    {
        var dr = new Mock<IDividendRepository>();
        var sr = new Mock<ISectorRepository>();

        dr.Setup(r => r.GetBySymbolAsync("MISSING")).ReturnsAsync((DividendModel?)null);

        var result = await Create(dr, sr).CalculateStockSectorComparison("MISSING");

        Assert.Null(result);
    }

    [Fact]
    public async Task CalculateStockSectorComparison_ReturnsNull_WhenStockHasNoSector()
    {
        var dr = new Mock<IDividendRepository>();
        var sr = new Mock<ISectorRepository>();

        dr.Setup(r => r.GetBySymbolAsync("XYZ")).ReturnsAsync(new DividendModel
        {
            Symbol = "XYZ",
            Sector = "",
            LastUpdated = DateTime.UtcNow,
            FetchedAt = DateTime.UtcNow
        });

        var result = await Create(dr, sr).CalculateStockSectorComparison("XYZ");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetOrCalculateAllSectorPerformances_UsesCachedData_WhenAvailable()
    {
        var dr = new Mock<IDividendRepository>();
        var sr = new Mock<ISectorRepository>();

        var cached = new List<SectorPerformance>
        {
            new() { Sector = "Energy", StockCount = 5 }
        };
        sr.Setup(r => r.GetAllSectorPerformancesAsync()).ReturnsAsync(cached);

        var result = await Create(dr, sr).GetOrCalculateAllSectorPerformancesAsync();

        Assert.Single(result);
        // GetAllAsync should NOT be called since we used cache
        dr.Verify(r => r.GetAllAsync(), Times.Never);
    }

    [Fact]
    public async Task GetOrCalculateAllSectorPerformances_CalculatesWhenCacheEmpty()
    {
        var dr = new Mock<IDividendRepository>();
        var sr = new Mock<ISectorRepository>();

        sr.Setup(r => r.GetAllSectorPerformancesAsync()).ReturnsAsync([]);
        dr.Setup(r => r.GetAllAsync()).ReturnsAsync([MakeStock("ENB.TO", "Energy")]);
        sr.Setup(r => r.UpsertSectorPerformanceAsync(It.IsAny<SectorPerformance>())).Returns(Task.CompletedTask);

        var result = await Create(dr, sr).GetOrCalculateAllSectorPerformancesAsync();

        Assert.Single(result);
        dr.Verify(r => r.GetAllAsync(), Times.Once);
    }

    [Fact]
    public async Task GetOrCalculateAllSectorPerformances_AlwaysRecalculates_WhenRefreshTrue()
    {
        var dr = new Mock<IDividendRepository>();
        var sr = new Mock<ISectorRepository>();

        // Even with cached data, refresh=true should trigger recalculation
        dr.Setup(r => r.GetAllAsync()).ReturnsAsync([MakeStock("RY.TO", "Financials")]);
        sr.Setup(r => r.UpsertSectorPerformanceAsync(It.IsAny<SectorPerformance>())).Returns(Task.CompletedTask);

        await Create(dr, sr).GetOrCalculateAllSectorPerformancesAsync(refresh: true);

        dr.Verify(r => r.GetAllAsync(), Times.Once);
        sr.Verify(r => r.GetAllSectorPerformancesAsync(), Times.Never);
    }
}
