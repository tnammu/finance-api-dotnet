using Microsoft.EntityFrameworkCore;
using FinanceApi.Models;

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
        }
    }
}
