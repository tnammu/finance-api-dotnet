using System;
using System.ComponentModel.DataAnnotations;

namespace FinanceApi.Models
{
    /// <summary>
    /// Tracks sector performance metrics over time
    /// </summary>
    public class SectorPerformance
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Sector { get; set; } = string.Empty;

        // Period (e.g., "2023-Q4", "2024-01", "YTD")
        [Required]
        [MaxLength(20)]
        public string Period { get; set; } = string.Empty;

        // Performance Metrics
        public decimal AverageReturn { get; set; }  // Average return of stocks in sector
        public decimal MedianReturn { get; set; }
        public decimal TotalMarketCap { get; set; }
        public int StockCount { get; set; }

        // Growth Metrics
        public decimal RevenueGrowth { get; set; }
        public decimal EarningsGrowth { get; set; }
        public decimal DividendGrowth { get; set; }

        // Valuation
        public decimal AveragePE { get; set; }
        public decimal AveragePB { get; set; }
        public decimal AverageDividendYield { get; set; }

        // Volatility
        public decimal Volatility { get; set; }  // Standard deviation of returns
        public decimal Beta { get; set; }  // Sector beta vs market

        // Metadata
        public DateTime CalculatedAt { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    /// <summary>
    /// Tracks individual stock's sector performance comparison
    /// </summary>
    public class StockSectorComparison
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(20)]
        public string Symbol { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Sector { get; set; } = string.Empty;

        // Stock vs Sector Performance
        public decimal StockReturn1M { get; set; }
        public decimal SectorReturn1M { get; set; }
        public decimal OutperformanceVsSector1M { get; set; }  // Stock - Sector

        public decimal StockReturn3M { get; set; }
        public decimal SectorReturn3M { get; set; }
        public decimal OutperformanceVsSector3M { get; set; }

        public decimal StockReturn1Y { get; set; }
        public decimal SectorReturn1Y { get; set; }
        public decimal OutperformanceVsSector1Y { get; set; }

        // Valuation vs Sector
        public decimal StockPE { get; set; }
        public decimal SectorAvgPE { get; set; }
        public decimal PEPremiumDiscount { get; set; }  // % vs sector avg

        public decimal StockDividendYield { get; set; }
        public decimal SectorAvgDividendYield { get; set; }
        public decimal YieldPremiumDiscount { get; set; }

        // Rank within sector (1 = best)
        public int PerformanceRank { get; set; }
        public int TotalStocksInSector { get; set; }
        public int PerformancePercentile { get; set; }  // 0-100, higher is better

        public DateTime CalculatedAt { get; set; }
    }
}
