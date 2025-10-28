using FinanceApi.Model;
using Microsoft.EntityFrameworkCore;

namespace FinanceApi.Data
{
    public class FinanceDbcontext : DbContext
    {
        public FinanceDbcontext(DbContextOptions<FinanceDbcontext> options) : base(options) { }

        public DbSet<Stock> Stocks { get; set; }

       /* protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Seed data
            modelBuilder.Entity<Stock>().HasData(
                new Stock { Id = 1, Symbol = "AAPL", CompanyName = "Apple Inc.", Price = 170.50m, DividendYield = 0.6m, LastUpdated = DateTime.UtcNow },
                new Stock { Id = 2, Symbol = "MSFT", CompanyName = "Microsoft Corp.", Price = 320.75m, DividendYield = 0.8m, LastUpdated = DateTime.UtcNow },
                new Stock { Id = 3, Symbol = "TSLA", CompanyName = "Tesla Inc.", Price = 250.10m, DividendYield = 0.0m, LastUpdated = DateTime.UtcNow }
            );
        }*/
    }

}
