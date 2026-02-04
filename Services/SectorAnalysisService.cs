using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using FinanceApi.Data;
using FinanceApi.Models;

namespace FinanceApi.Services
{
    public class SectorAnalysisService
    {
        private readonly DividendDbContext _context;
        private readonly ILogger<SectorAnalysisService> _logger;

        public SectorAnalysisService(DividendDbContext context, ILogger<SectorAnalysisService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Calculate sector performance metrics for all sectors
        /// </summary>
        public async Task<List<SectorPerformance>> CalculateAllSectorPerformances(string period = "current")
        {
            try
            {
                var stocks = await _context.DividendModels.ToListAsync();
                var sectors = stocks
                    .Where(s => !string.IsNullOrEmpty(s.Sector))
                    .GroupBy(s => s.Sector)
                    .ToList();

                var sectorPerformances = new List<SectorPerformance>();

                foreach (var sectorGroup in sectors)
                {
                    var sectorStocks = sectorGroup.ToList();

                    var performance = new SectorPerformance
                    {
                        Sector = sectorGroup.Key,
                        Period = period,

                        // Performance Metrics
                        AverageReturn = CalculateAverageReturn(sectorStocks),
                        MedianReturn = CalculateMedianReturn(sectorStocks),
                        TotalMarketCap = sectorStocks.Sum(s => s.MarketCap),
                        StockCount = sectorStocks.Count,

                        // Growth Metrics
                        RevenueGrowth = sectorStocks
                            .Where(s => s.RevenueGrowth.HasValue)
                            .DefaultIfEmpty()
                            .Average(s => s?.RevenueGrowth ?? 0),
                        EarningsGrowth = sectorStocks.Average(s => s.EarningsGrowth),
                        DividendGrowth = sectorStocks.Where(s => s.DividendGrowthRate > 0)
                            .DefaultIfEmpty()
                            .Average(s => s?.DividendGrowthRate ?? 0),

                        // Valuation
                        AveragePE = CalculateAveragePE(sectorStocks),
                        AveragePB = CalculateAveragePB(sectorStocks),
                        AverageDividendYield = sectorStocks.Where(s => s.DividendYield > 0)
                            .DefaultIfEmpty()
                            .Average(s => s?.DividendYield ?? 0),

                        // Volatility
                        Volatility = CalculateVolatility(sectorStocks),
                        Beta = sectorStocks.Where(s => s.Beta > 0)
                            .DefaultIfEmpty()
                            .Average(s => s?.Beta ?? 1.0m),

                        // Metadata
                        CalculatedAt = DateTime.UtcNow,
                        LastUpdated = DateTime.UtcNow
                    };

                    sectorPerformances.Add(performance);
                }

                // Save to database
                foreach (var performance in sectorPerformances)
                {
                    var existing = await _context.SectorPerformances
                        .FirstOrDefaultAsync(sp => sp.Sector == performance.Sector && sp.Period == performance.Period);

                    if (existing != null)
                    {
                        // Update existing
                        existing.AverageReturn = performance.AverageReturn;
                        existing.MedianReturn = performance.MedianReturn;
                        existing.TotalMarketCap = performance.TotalMarketCap;
                        existing.StockCount = performance.StockCount;
                        existing.RevenueGrowth = performance.RevenueGrowth;
                        existing.EarningsGrowth = performance.EarningsGrowth;
                        existing.DividendGrowth = performance.DividendGrowth;
                        existing.AveragePE = performance.AveragePE;
                        existing.AveragePB = performance.AveragePB;
                        existing.AverageDividendYield = performance.AverageDividendYield;
                        existing.Volatility = performance.Volatility;
                        existing.Beta = performance.Beta;
                        existing.LastUpdated = DateTime.UtcNow;
                    }
                    else
                    {
                        // Add new
                        _context.SectorPerformances.Add(performance);
                    }
                }

                await _context.SaveChangesAsync();
                return sectorPerformances;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating sector performances");
                throw;
            }
        }

        /// <summary>
        /// Calculate stock vs sector comparison for a specific symbol
        /// </summary>
        public async Task<StockSectorComparison?> CalculateStockSectorComparison(string symbol)
        {
            try
            {
                var stock = await _context.DividendModels.FirstOrDefaultAsync(s => s.Symbol == symbol);
                if (stock == null || string.IsNullOrEmpty(stock.Sector))
                {
                    _logger.LogWarning($"Stock {symbol} not found or has no sector");
                    return null;
                }

                var sectorStocks = await _context.DividendModels
                    .Where(s => s.Sector == stock.Sector)
                    .ToListAsync();

                // Calculate sector averages
                var sectorAvgReturn1M = CalculateAverageReturn(sectorStocks);
                var sectorAvgPE = CalculateAveragePE(sectorStocks);
                var sectorAvgDividendYield = sectorStocks
                    .Where(s => s.DividendYield.HasValue && s.DividendYield.Value > 0)
                    .Select(s => s.DividendYield.Value)
                    .DefaultIfEmpty(0)
                    .Average();

                // Calculate stock's rank within sector
                var sortedByReturn = sectorStocks
                    .OrderByDescending(s => CalculateStockReturn(s))
                    .ToList();
                var stockRank = sortedByReturn.FindIndex(s => s.Symbol == symbol) + 1;
                var percentile = (int)((1 - ((double)stockRank / sectorStocks.Count)) * 100);

                var comparison = new StockSectorComparison
                {
                    Symbol = symbol,
                    Sector = stock.Sector,

                    // Stock vs Sector Performance (using available data as proxy)
                    StockReturn1M = CalculateStockReturn(stock),
                    SectorReturn1M = sectorAvgReturn1M,
                    OutperformanceVsSector1M = CalculateStockReturn(stock) - sectorAvgReturn1M,

                    StockReturn3M = CalculateStockReturn(stock) * 3, // Simplified
                    SectorReturn3M = sectorAvgReturn1M * 3,
                    OutperformanceVsSector3M = (CalculateStockReturn(stock) - sectorAvgReturn1M) * 3,

                    StockReturn1Y = CalculateStockReturn(stock) * 12,
                    SectorReturn1Y = sectorAvgReturn1M * 12,
                    OutperformanceVsSector1Y = (CalculateStockReturn(stock) - sectorAvgReturn1M) * 12,

                    // Valuation vs Sector
                    StockPE = stock.PE,
                    SectorAvgPE = sectorAvgPE,
                    PEPremiumDiscount = sectorAvgPE > 0 ? ((stock.PE - sectorAvgPE) / sectorAvgPE) * 100 : 0,

                    StockDividendYield = stock.DividendYield ?? 0m,
                    SectorAvgDividendYield = sectorAvgDividendYield,
                    YieldPremiumDiscount = sectorAvgDividendYield != 0m
                        ? ((stock.DividendYield ?? 0m) - sectorAvgDividendYield) / sectorAvgDividendYield * 100m
                        : 0m,

                    // Rank within sector
                    PerformanceRank = stockRank,
                    TotalStocksInSector = sectorStocks.Count,
                    PerformancePercentile = percentile,

                    CalculatedAt = DateTime.UtcNow
                };

                // Save to database
                var existing = await _context.StockSectorComparisons
                    .FirstOrDefaultAsync(ssc => ssc.Symbol == symbol);

                if (existing != null)
                {
                    // Update existing
                    existing.Sector = comparison.Sector;
                    existing.StockReturn1M = comparison.StockReturn1M;
                    existing.SectorReturn1M = comparison.SectorReturn1M;
                    existing.OutperformanceVsSector1M = comparison.OutperformanceVsSector1M;
                    existing.StockReturn3M = comparison.StockReturn3M;
                    existing.SectorReturn3M = comparison.SectorReturn3M;
                    existing.OutperformanceVsSector3M = comparison.OutperformanceVsSector3M;
                    existing.StockReturn1Y = comparison.StockReturn1Y;
                    existing.SectorReturn1Y = comparison.SectorReturn1Y;
                    existing.OutperformanceVsSector1Y = comparison.OutperformanceVsSector1Y;
                    existing.StockPE = comparison.StockPE;
                    existing.SectorAvgPE = comparison.SectorAvgPE;
                    existing.PEPremiumDiscount = comparison.PEPremiumDiscount;
                    existing.StockDividendYield = comparison.StockDividendYield;
                    existing.SectorAvgDividendYield = comparison.SectorAvgDividendYield;
                    existing.YieldPremiumDiscount = comparison.YieldPremiumDiscount;
                    existing.PerformanceRank = comparison.PerformanceRank;
                    existing.TotalStocksInSector = comparison.TotalStocksInSector;
                    existing.PerformancePercentile = comparison.PerformancePercentile;
                    existing.CalculatedAt = DateTime.UtcNow;
                }
                else
                {
                    _context.StockSectorComparisons.Add(comparison);
                }

                await _context.SaveChangesAsync();
                return comparison;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error calculating stock sector comparison for {symbol}");
                throw;
            }
        }

        /// <summary>
        /// Get sector performance for a specific sector
        /// </summary>
        public async Task<SectorPerformance?> GetSectorPerformance(string sector, string period = "current")
        {
            return await _context.SectorPerformances
                .FirstOrDefaultAsync(sp => sp.Sector == sector && sp.Period == period);
        }

        /// <summary>
        /// Get all sector performances
        /// </summary>
        public async Task<List<SectorPerformance>> GetAllSectorPerformances()
        {
            return await _context.SectorPerformances
                .OrderByDescending(sp => sp.AverageReturn)
                .ToListAsync();
        }

        /// <summary>
        /// Get stock sector comparison
        /// </summary>
        public async Task<StockSectorComparison?> GetStockSectorComparison(string symbol)
        {
            return await _context.StockSectorComparisons
                .FirstOrDefaultAsync(ssc => ssc.Symbol == symbol);
        }

        // Helper methods for calculations
        private decimal CalculateAverageReturn(List<DividendModel> stocks)
        {
            if (!stocks.Any()) return 0;

            // Use dividend yield + growth as proxy for return
            return stocks
                .Where(s => s.DividendYield > 0 || s.DividendGrowthRate > 0)
                .DefaultIfEmpty()
                .Average(s => s?.DividendYield + s?.DividendGrowthRate ?? 0);
        }

        private decimal CalculateMedianReturn(List<DividendModel> stocks)
        {
            if (!stocks.Any()) return 0;
                
            var returns = stocks
                .Where(s => (s.DividendYield ?? 0) > 0 || (s.DividendGrowthRate ?? 0) > 0)
                .Select(s => (s.DividendYield ?? 0)  + (s.DividendGrowthRate ?? 0))
                .OrderBy(r => r)
                .ToList();

            if (!returns.Any()) return 0;

            int count = returns.Count;
            if (count % 2 == 0)
            {
                return (returns[count / 2 - 1] + returns[count / 2]) / 2;
            }
            else
            {
                return returns[count / 2];
            }
        }

        private decimal CalculateAveragePE(List<DividendModel> stocks)
        {
            var validPEs = stocks.Where(s => s.PE > 0 && s.PE < 100).ToList();
            return validPEs.Any() ? validPEs.Average(s => s.PE) : 0;
        }                                       

        private decimal CalculateAveragePB(List<DividendModel> stocks)
        {
            var validPBs = stocks.Where(s => s.PB > 0 && s.PB < 20).ToList();
            return validPBs.Any() ? validPBs.Average(s => s.PB) : 0;
        }

        private decimal CalculateVolatility(List<DividendModel> stocks)
        {
            if (!stocks.Any()) return 0;

            // Use beta as proxy for volatility
            var betas = stocks.Where(s => s.Beta > 0).Select(s => (double)s.Beta).ToList();
            if (!betas.Any()) return 0;

            var mean = betas.Average();
            var variance = betas.Sum(b => Math.Pow(b - mean, 2)) / betas.Count;
            return (decimal)Math.Sqrt(variance);
        }

        private decimal CalculateStockReturn(DividendModel stock)
        {
            // Use dividend yield + growth as proxy for return
            return stock.DividendYield.GetValueOrDefault(0) + stock.DividendGrowthRate.GetValueOrDefault(0);
        }
    }
}
