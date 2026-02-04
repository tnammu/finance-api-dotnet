using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinanceApi.Models
{
    /// <summary>
    /// Stores dividend analysis results in database
    /// Main model for dividend stock data
    /// </summary>
    public class DividendModel
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(20)]
        public string Symbol { get; set; } = string.Empty;

        public string CompanyName { get; set; } = string.Empty;
        public string Sector { get; set; } = string.Empty;
        public string Industry { get; set; } = string.Empty;

        // Current Metrics
        public decimal CurrentPrice { get; set; }
        public decimal? DividendYield { get; set; }
        public decimal? DividendPerShare { get; set; }
        public decimal? PayoutRatio { get; set; }
        public decimal? EPS { get; set; }
        public decimal? ProfitMargin { get; set; }
        public decimal? Beta { get; set; }

        // Valuation Metrics
        public decimal PE { get; set; }          // Price to Earnings ratio
        public decimal PB { get; set; }          // Price to Book ratio
        public decimal MarketCap { get; set; }   // Market Capitalization
        public decimal EarningsGrowth { get; set; } // Earnings growth rate

        // Payout Policy Tracking
        public string PayoutPolicy { get; set; } = "Unknown"; // "Dividend Only", "Reinvestment Only", "Mixed", "None", "Unknown"
        public decimal? DividendAllocationPct { get; set; }    // % of earnings paid as dividends (same as PayoutRatio)
        public decimal? ReinvestmentAllocationPct { get; set; } // % of earnings reinvested (100 - PayoutRatio)

        // Growth Metrics (for growth stock analysis)
        public decimal? RevenueGrowth { get; set; }  // YoY Revenue Growth %
        public decimal? EPSGrowthRate { get; set; }  // YoY EPS Growth %
        public decimal? PEGRatio { get; set; }       // PE / EPS Growth
        public decimal? RuleOf40Score { get; set; }  // Revenue Growth % + Profit Margin %
        public decimal? FreeCashFlow { get; set; }   // Operating CF - CapEx
        public decimal GrowthScore { get; set; }     // Composite score 0-100
        public string GrowthRating { get; set; } = string.Empty; // Strong/Moderate/Weak

        // Historical Analysis
        public int ConsecutiveYearsOfPayments { get; set; }
        public decimal? DividendGrowthRate { get; set; }

        // Safety Analysis
        public decimal SafetyScore { get; set; }
        public string SafetyRating { get; set; } = string.Empty;
        public string Recommendation { get; set; } = string.Empty;

        // Metadata
        public DateTime FetchedAt { get; set; }
        public DateTime LastUpdated { get; set; }
        public int ApiCallsUsed { get; set; }

        // Navigation properties
        public virtual ICollection<DividendPaymentRecord> DividendPayments { get; set; } = new List<DividendPaymentRecord>();
        public virtual ICollection<YearlyDividendSummary> YearlyDividends { get; set; } = new List<YearlyDividendSummary>();
    }

    /// <summary>
    /// Individual dividend payment records
    /// </summary>
    public class DividendPaymentRecord
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int DividendModelId { get; set; }

        [Required]
        [MaxLength(20)]
        public string Symbol { get; set; } = string.Empty;

        public DateTime PaymentDate { get; set; }
        public decimal Amount { get; set; }

        // Navigation property
        [ForeignKey("DividendModelId")]
        public virtual DividendModel? DividendModel { get; set; }
    }

    /// <summary>
    /// Yearly dividend totals for quick access
    /// Includes annual EPS for accurate payout ratio calculation
    /// </summary>
    public class YearlyDividendSummary
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int DividendModelId { get; set; }

        [Required]
        [MaxLength(20)]
        public string Symbol { get; set; } = string.Empty;

        public int Year { get; set; }
        public decimal TotalDividend { get; set; }
        public int PaymentCount { get; set; }

        // Annual EPS for accurate payout ratio calculation
        public decimal? AnnualEPS { get; set; }

        // Navigation property
        [ForeignKey("DividendModelId")]
        public virtual DividendModel? DividendModel { get; set; }
    }

    /// <summary>
    /// Track API usage to stay within limits
    /// </summary>
    public class ApiUsageLog
    {
        [Key]
        public int Id { get; set; }

        public DateTime Date { get; set; }
        public int CallsUsed { get; set; }
        public int DailyLimit { get; set; }
        public string? Notes { get; set; }
    }
}