using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinanceApi.Model
{
    public class EtfModel
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(10)]
        public string Symbol { get; set; } = string.Empty;

        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Category { get; set; } = string.Empty;

        public decimal TotalAssets { get; set; }

        public decimal ExpenseRatio { get; set; }

        public DateTime InceptionDate { get; set; }

        public DateTime LastUpdated { get; set; }

        public int TotalHoldings { get; set; }

        // Navigation property
        public virtual ICollection<EtfHolding> Holdings { get; set; } = new List<EtfHolding>();
    }

    public class EtfHolding
    {
        [Key]
        public int Id { get; set; }

        public int EtfId { get; set; }

        [ForeignKey("EtfId")]
        public virtual EtfModel Etf { get; set; } = null!;

        [Required]
        [MaxLength(10)]
        public string StockSymbol { get; set; } = string.Empty;

        [MaxLength(200)]
        public string StockName { get; set; } = string.Empty;

        public decimal Weight { get; set; } // Percentage weight in ETF

        public long Shares { get; set; }

        public decimal MarketValue { get; set; }

        [MaxLength(100)]
        public string Sector { get; set; } = string.Empty;

        [MaxLength(100)]
        public string AssetType { get; set; } = string.Empty; // Stock, Bond, Cash, etc.
    }

    public class EtfSectorAllocation
    {
        [Key]
        public int Id { get; set; }

        public int EtfId { get; set; }

        [ForeignKey("EtfId")]
        public virtual EtfModel Etf { get; set; } = null!;

        [MaxLength(100)]
        public string Sector { get; set; } = string.Empty;

        public decimal Weight { get; set; } // Percentage allocation to sector
    }
}