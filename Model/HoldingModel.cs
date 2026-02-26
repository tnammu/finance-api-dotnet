using System.ComponentModel.DataAnnotations;

namespace FinanceApi.Model
{
    public class HoldingModel
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(20)]
        public string Symbol { get; set; } = string.Empty;

        public decimal Shares { get; set; }

        public decimal BuyPrice { get; set; }

        public DateTime BuyDate { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    }
}
