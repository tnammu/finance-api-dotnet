using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinanceApi.Models
{
    /// <summary>
    /// Stores market index data (S&P 500, TSX60, etc.)
    /// Used for benchmark comparison against individual stocks
    /// </summary>
    public class IndexData
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(20)]
        public string Symbol { get; set; } = string.Empty; // ^GSPC for S&P 500, ^TSX60 for TSX60

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty; // "S&P 500", "TSX 60", etc.

        [MaxLength(50)]
        public string Market { get; set; } = string.Empty; // "US", "Canada", etc.

        [MaxLength(10)]
        public string Currency { get; set; } = string.Empty; // "USD", "CAD", etc.

        // Current Metrics
        public decimal CurrentPrice { get; set; }
        public decimal? PreviousClose { get; set; }
        public decimal? Open { get; set; }
        public decimal? DayHigh { get; set; }
        public decimal? DayLow { get; set; }
        public long? Volume { get; set; }

        // Performance Metrics (as percentages)
        public decimal? DayChange { get; set; } // 1-day change %
        public decimal? WeekChange { get; set; } // 1-week change %
        public decimal? MonthChange { get; set; } // 1-month change %
        public decimal? ThreeMonthChange { get; set; } // 3-month change %
        public decimal? SixMonthChange { get; set; } // 6-month change %
        public decimal? YearChange { get; set; } // 1-year change %
        public decimal? YTDChange { get; set; } // Year-to-date change %
        public decimal? ThreeYearChange { get; set; } // 3-year change %
        public decimal? FiveYearChange { get; set; } // 5-year change %

        // Annual Return Metrics
        public decimal? AnnualizedReturn1Y { get; set; }
        public decimal? AnnualizedReturn3Y { get; set; }
        public decimal? AnnualizedReturn5Y { get; set; }

        // Volatility
        public decimal? Beta { get; set; }
        public decimal? Volatility { get; set; }

        // Metadata
        public DateTime LastUpdated { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; } = true;

        // Navigation properties
        public virtual ICollection<IndexHistory> History { get; set; } = new List<IndexHistory>();
    }

    /// <summary>
    /// Historical price data for market indices
    /// Stores daily OHLCV data for performance tracking
    /// </summary>
    public class IndexHistory
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int IndexDataId { get; set; }

        [Required]
        [MaxLength(20)]
        public string Symbol { get; set; } = string.Empty;

        [Required]
        public DateTime Date { get; set; }

        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
        public decimal AdjustedClose { get; set; }
        public long Volume { get; set; }

        // Calculated metrics
        public decimal? DayChange { get; set; } // % change from previous day
        public decimal? DayChangeAmount { get; set; } // Dollar change from previous day

        // Navigation property
        [ForeignKey("IndexDataId")]
        public virtual IndexData? IndexData { get; set; }
    }

    /// <summary>
    /// Tracks API usage for index data fetching
    /// </summary>
    public class IndexApiUsageLog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(20)]
        public string Symbol { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string ApiSource { get; set; } = string.Empty; // "Yahoo Finance", "Alpha Vantage", etc.

        [Required]
        public DateTime RequestTime { get; set; }

        public bool Success { get; set; }

        [MaxLength(500)]
        public string? ErrorMessage { get; set; }

        public int RecordsFetched { get; set; }
    }
}
