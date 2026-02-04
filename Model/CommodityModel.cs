using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinanceApi.Model
{
    /// <summary>
    /// Main commodity metadata for metals and energy futures
    /// </summary>
    public class CommodityModel
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(20)]
        public string Symbol { get; set; } = string.Empty; // e.g., "GC=F", "CL=F"

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty; // e.g., "Gold Futures", "Crude Oil Futures"

        [Required]
        [MaxLength(20)]
        public string Category { get; set; } = string.Empty; // "Metals" or "Energy"

        // Current Pricing
        public decimal CurrentPrice { get; set; }

        // Contract Specifications
        public int ContractSize { get; set; } // e.g., 100 oz for Gold
        public decimal TickSize { get; set; } // Minimum price movement
        public decimal TickValue { get; set; } // Dollar value per tick

        // Margin Requirements
        public decimal MarginRequirement { get; set; } // Initial margin per contract

        // Metadata
        public DateTime LastUpdated { get; set; }

        // Navigation properties
        public virtual ICollection<CommodityHistory> History { get; set; } = new List<CommodityHistory>();
    }

    /// <summary>
    /// Historical OHLCV data with technical indicators
    /// </summary>
    public class CommodityHistory
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int CommodityId { get; set; }

        [Required]
        [MaxLength(20)]
        public string Symbol { get; set; } = string.Empty;

        [Required]
        public DateTime Date { get; set; }

        // OHLCV Data
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
        public long Volume { get; set; }

        // Technical Indicators
        public decimal? ATR14 { get; set; } // 14-day Average True Range
        public decimal? Volatility20 { get; set; } // 20-day historical volatility %

        // Navigation property
        [ForeignKey("CommodityId")]
        public virtual CommodityModel? Commodity { get; set; }
    }

    /// <summary>
    /// Pairwise correlation analysis between commodities
    /// </summary>
    public class CommodityCorrelation
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(20)]
        public string Symbol1 { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string Symbol2 { get; set; } = string.Empty;

        [Required]
        [MaxLength(10)]
        public string Period { get; set; } = string.Empty; // "1Y", "2Y", "3Y", "5Y", "10Y"

        // Correlation Metrics
        public decimal PearsonCorrelation { get; set; } // -1 to 1
        public decimal CointegrationScore { get; set; } // Engle-Granger test result
        public bool IsStationaryPair { get; set; } // Suitable for mean reversion?

        // Pair Trading Parameters
        public int? HalfLife { get; set; } // Mean reversion half-life in days
        public decimal? OptimalRatio { get; set; } // Hedge ratio for pair trading

        // Metadata
        public DateTime CalculatedAt { get; set; }
    }

    /// <summary>
    /// Auto-generated pair trading recommendations
    /// </summary>
    public class PairSuggestion
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(20)]
        public string Symbol1 { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string Symbol2 { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string RecommendationType { get; set; } = string.Empty; // "MeanReversion", "Ratio", "Correlation"

        public decimal Score { get; set; } // 0-100 composite score
        public string Reasoning { get; set; } = string.Empty; // Explanation for suggestion

        // Pair Parameters
        public decimal OptimalRatio { get; set; }
        public decimal? ExpectedReturns { get; set; } // Expected annual returns %

        [MaxLength(20)]
        public string RiskLevel { get; set; } = string.Empty; // "Low", "Medium", "High"

        public DateTime GeneratedAt { get; set; }
    }

    /// <summary>
    /// Backtest results metadata for commodity strategies
    /// </summary>
    public class CommodityBacktest
    {
        [Key]
        public int Id { get; set; }

        // Single Commodity (Symbol) or Pair (Symbol1 + Symbol2)
        [MaxLength(20)]
        public string? Symbol { get; set; } // For single commodity

        [MaxLength(20)]
        public string? Symbol1 { get; set; } // For pairs

        [MaxLength(20)]
        public string? Symbol2 { get; set; } // For pairs

        // Strategy Configuration
        [Required]
        [MaxLength(50)]
        public string StrategyType { get; set; } = string.Empty;
        // "BuyHold", "SmaCrossover", "RSI", "MACD", "BollingerBands", "Seasonal", "PairMeanReversion", "RatioTrading"

        [Required]
        [MaxLength(30)]
        public string StopLossMethod { get; set; } = string.Empty; // "ATR", "Percentage", "Volatility", "FixedDollar"

        public decimal StopLossValue { get; set; } // e.g., 2.0 for 2x ATR, 5.0 for 5%

        [Required]
        [MaxLength(10)]
        public string Period { get; set; } = string.Empty; // "1Y", "2Y", "3Y", "5Y", "10Y"

        public decimal Capital { get; set; } // Initial capital
        public int ContractsTraded { get; set; } // Number of contracts

        // Performance Metrics
        public decimal FinalValue { get; set; }
        public decimal TotalReturn { get; set; } // %
        public decimal AnnualReturn { get; set; } // %
        public decimal MaxDrawdown { get; set; } // %
        public decimal WinRate { get; set; } // %
        public int TotalTrades { get; set; }
        public decimal ProfitFactor { get; set; } // Gross profit / Gross loss
        public decimal SharpeRatio { get; set; }
        public decimal SortinoRatio { get; set; }

        // Cost Analysis
        public decimal TotalCosts { get; set; } // All CME fees + commissions

        // Metadata
        public DateTime CalculatedAt { get; set; }

        // Navigation properties
        public virtual ICollection<BacktestTrade> Trades { get; set; } = new List<BacktestTrade>();
    }

    /// <summary>
    /// Individual trade records from backtesting
    /// </summary>
    public class BacktestTrade
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int BacktestId { get; set; }

        [Required]
        public DateTime Date { get; set; }

        [Required]
        [MaxLength(20)]
        public string Type { get; set; } = string.Empty; // "BUY_OPEN", "SELL_CLOSE", "SELL_SHORT", "BUY_COVER"

        public decimal Price { get; set; }
        public int Contracts { get; set; }

        public string Reason { get; set; } = string.Empty; // e.g., "Golden Cross", "RSI Oversold"

        // Risk Management
        public decimal? StopLossPrice { get; set; }
        public decimal? TakeProfitPrice { get; set; }

        // Performance
        public decimal? PnL { get; set; } // Profit/Loss for this trade

        // Cost Breakdown
        public decimal Commission { get; set; }
        public decimal ExchangeFees { get; set; }
        public decimal ClearingFees { get; set; }
        public decimal OvernightFinancing { get; set; }

        // Navigation property
        [ForeignKey("BacktestId")]
        public virtual CommodityBacktest? Backtest { get; set; }
    }

    /// <summary>
    /// CME broker cost structure configuration
    /// </summary>
    public class CmeCostProfile
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string BrokerName { get; set; } = string.Empty; // e.g., "Standard CME"

        [MaxLength(20)]
        public string CommoditySymbol { get; set; } = "ALL"; // Specific commodity or "ALL"

        // Per-Contract Costs
        public decimal CommissionPerContract { get; set; } // e.g., $2.50
        public decimal ExchangeFeePerContract { get; set; } // e.g., $1.50
        public decimal ClearingFeePerContract { get; set; } // e.g., $0.50

        // Financing Costs
        public decimal OvernightFinancingRate { get; set; } // % per day
        public decimal MarginInterestRate { get; set; } // % annual

        public bool IsActive { get; set; } = true;
    }
}