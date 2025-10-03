namespace FinanceApi.Model
{
    public class Stock
    {
        public int Id { get; set; }
        public string Symbol { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal DividendYield { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}
