using Microsoft.EntityFrameworkCore;
using FinanceApi.Models;
using FinanceApi.Model;

namespace FinanceApi.Data
{
    public class DividendDbContext : DbContext
    {
        public DividendDbContext(DbContextOptions<DividendDbContext> options) : base(options)
        {
        }

        // Configure WAL mode once when database is created/migrated
        public void ConfigureSqlite()
        {
            if (Database.IsSqlite())
            {
                Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
                Database.ExecuteSqlRaw("PRAGMA busy_timeout=30000;");
            }
        }

        public DbSet<DividendModel> DividendModels { get; set; }
        public DbSet<DividendPaymentRecord> DividendPayments { get; set; }
        public DbSet<YearlyDividendSummary> YearlyDividends { get; set; }
        public DbSet<ApiUsageLog> ApiUsageLogs { get; set; }

        // Index Data Tables
        public DbSet<IndexData> IndexData { get; set; }
        public DbSet<IndexHistory> IndexHistory { get; set; }
        public DbSet<IndexApiUsageLog> IndexApiUsageLogs { get; set; }

        // ETF Tables
        public DbSet<EtfModel> Etfs { get; set; }
        public DbSet<EtfHolding> EtfHoldings { get; set; }
        public DbSet<EtfSectorAllocation> EtfSectorAllocations { get; set; }

        // Sector Performance Tables
        public DbSet<SectorPerformance> SectorPerformances { get; set; }
        public DbSet<StockSectorComparison> StockSectorComparisons { get; set; }

        // Commodity Trading Tables
        public DbSet<CommodityModel> Commodities { get; set; }
        public DbSet<CommodityHistory> CommodityHistory { get; set; }
        public DbSet<CommodityCorrelation> CommodityCorrelations { get; set; }
        public DbSet<PairSuggestion> PairSuggestions { get; set; }
        public DbSet<CommodityBacktest> CommodityBacktests { get; set; }
        public DbSet<BacktestTrade> BacktestTrades { get; set; }
        public DbSet<CmeCostProfile> CmeCostProfiles { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure DividendModel
            modelBuilder.Entity<DividendModel>(entity =>
            {
                entity.ToTable("DividendModels");
                entity.HasIndex(e => e.Symbol).IsUnique();
                entity.HasIndex(e => e.LastUpdated);
                entity.HasIndex(e => e.SafetyScore);
            });

            // Configure DividendPaymentRecord
            modelBuilder.Entity<DividendPaymentRecord>(entity =>
            {
                entity.ToTable("DividendPayments");
                entity.HasIndex(e => e.Symbol);
                entity.HasIndex(e => e.PaymentDate);

                entity.HasOne(d => d.DividendModel)
                    .WithMany(p => p.DividendPayments)
                    .HasForeignKey(d => d.DividendModelId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure YearlyDividendSummary
            modelBuilder.Entity<YearlyDividendSummary>(entity =>
            {
                entity.ToTable("YearlyDividends");
                entity.HasIndex(e => new { e.Symbol, e.Year }).IsUnique();

                entity.HasOne(d => d.DividendModel)
                    .WithMany(p => p.YearlyDividends)
                    .HasForeignKey(d => d.DividendModelId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure ApiUsageLog
            modelBuilder.Entity<ApiUsageLog>(entity =>
            {
                entity.ToTable("ApiUsageLogs");
                entity.HasIndex(e => e.Date).IsUnique();
            });

            // Configure IndexData
            modelBuilder.Entity<IndexData>(entity =>
            {
                entity.ToTable("IndexData");
                entity.HasIndex(e => e.Symbol).IsUnique();
                entity.HasIndex(e => e.Name);
                entity.HasIndex(e => e.Market);
                entity.HasIndex(e => e.LastUpdated);
            });

            // Configure IndexHistory
            modelBuilder.Entity<IndexHistory>(entity =>
            {
                entity.ToTable("IndexHistory");
                entity.HasIndex(e => e.Symbol);
                entity.HasIndex(e => e.Date);
                entity.HasIndex(e => new { e.Symbol, e.Date }).IsUnique();

                entity.HasOne(d => d.IndexData)
                    .WithMany(p => p.History)
                    .HasForeignKey(d => d.IndexDataId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure IndexApiUsageLog
            modelBuilder.Entity<IndexApiUsageLog>(entity =>
            {
                entity.ToTable("IndexApiUsageLogs");
                entity.HasIndex(e => e.Symbol);
                entity.HasIndex(e => e.RequestTime);
            });

            // Configure EtfModel
            modelBuilder.Entity<EtfModel>(entity =>
            {
                entity.ToTable("Etfs");
                entity.HasIndex(e => e.Symbol).IsUnique();
                entity.HasIndex(e => e.LastUpdated);
                entity.HasIndex(e => e.Category);
            });

            // Configure EtfHolding
            modelBuilder.Entity<EtfHolding>(entity =>
            {
                entity.ToTable("EtfHoldings");
                entity.HasIndex(e => e.EtfId);
                entity.HasIndex(e => e.StockSymbol);
                entity.HasIndex(e => new { e.EtfId, e.StockSymbol });

                entity.HasOne(d => d.Etf)
                    .WithMany(p => p.Holdings)
                    .HasForeignKey(d => d.EtfId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure EtfSectorAllocation
            modelBuilder.Entity<EtfSectorAllocation>(entity =>
            {
                entity.ToTable("EtfSectorAllocations");
                entity.HasIndex(e => e.EtfId);
                entity.HasIndex(e => new { e.EtfId, e.Sector }).IsUnique();

                entity.HasOne(d => d.Etf)
                    .WithMany()
                    .HasForeignKey(d => d.EtfId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure SectorPerformance
            modelBuilder.Entity<SectorPerformance>(entity =>
            {
                entity.ToTable("SectorPerformances");
                entity.HasIndex(e => e.Sector);
                entity.HasIndex(e => e.Period);
                entity.HasIndex(e => new { e.Sector, e.Period }).IsUnique();
                entity.HasIndex(e => e.CalculatedAt);
            });

            // Configure StockSectorComparison
            modelBuilder.Entity<StockSectorComparison>(entity =>
            {
                entity.ToTable("StockSectorComparisons");
                entity.HasIndex(e => e.Symbol).IsUnique();
                entity.HasIndex(e => e.Sector);
                entity.HasIndex(e => e.PerformanceRank);
                entity.HasIndex(e => e.CalculatedAt);
            });

            // Configure CommodityModel
            modelBuilder.Entity<CommodityModel>(entity =>
            {
                entity.ToTable("Commodities");
                entity.HasIndex(e => e.Symbol).IsUnique();
                entity.HasIndex(e => e.Category);
                entity.HasIndex(e => e.LastUpdated);
            });

            // Configure CommodityHistory
            modelBuilder.Entity<CommodityHistory>(entity =>
            {
                entity.ToTable("CommodityHistory");
                entity.HasIndex(e => e.Symbol);
                entity.HasIndex(e => e.Date);
                entity.HasIndex(e => new { e.Symbol, e.Date }).IsUnique();

                entity.HasOne(d => d.Commodity)
                    .WithMany(p => p.History)
                    .HasForeignKey(d => d.CommodityId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure CommodityCorrelation
            modelBuilder.Entity<CommodityCorrelation>(entity =>
            {
                entity.ToTable("CommodityCorrelations");
                entity.HasIndex(e => e.Symbol1);
                entity.HasIndex(e => e.Symbol2);
                entity.HasIndex(e => new { e.Symbol1, e.Symbol2, e.Period }).IsUnique();
                entity.HasIndex(e => e.CalculatedAt);
            });

            // Configure PairSuggestion
            modelBuilder.Entity<PairSuggestion>(entity =>
            {
                entity.ToTable("PairSuggestions");
                entity.HasIndex(e => e.Score);
                entity.HasIndex(e => new { e.Symbol1, e.Symbol2 });
                entity.HasIndex(e => e.GeneratedAt);
            });

            // Configure CommodityBacktest
            modelBuilder.Entity<CommodityBacktest>(entity =>
            {
                entity.ToTable("CommodityBacktests");
                entity.HasIndex(e => e.Symbol);
                entity.HasIndex(e => new { e.Symbol1, e.Symbol2 });
                entity.HasIndex(e => e.StrategyType);
                entity.HasIndex(e => e.Period);
                entity.HasIndex(e => e.CalculatedAt);
            });

            // Configure BacktestTrade
            modelBuilder.Entity<BacktestTrade>(entity =>
            {
                entity.ToTable("BacktestTrades");
                entity.HasIndex(e => e.BacktestId);
                entity.HasIndex(e => e.Date);

                entity.HasOne(d => d.Backtest)
                    .WithMany(p => p.Trades)
                    .HasForeignKey(d => d.BacktestId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure CmeCostProfile
            modelBuilder.Entity<CmeCostProfile>(entity =>
            {
                entity.ToTable("CmeCostProfiles");
                entity.HasIndex(e => e.CommoditySymbol);
                entity.HasIndex(e => e.IsActive);
            });
        }
    }
}
