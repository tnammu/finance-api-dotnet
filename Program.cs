using FinanceApi.Data;
using FinanceApi.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add database context
builder.Services.AddDbContext<FinanceContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add services to the container
builder.Services.AddControllers();

// Register HttpClient with StockService
builder.Services.AddHttpClient<StockService>();

// Add Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Canadian Finance API",
        Version = "v1",
        Description = "API for fetching live stock/ETF data using Alpha Vantage"
    });
});

// Register LiveStockSeeder
builder.Services.AddTransient<LiveStockSeeder>();

var app = builder.Build();

// Seed data at startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        logger.LogInformation("========================================");
        logger.LogInformation("Starting database seeding with Alpha Vantage...");
        logger.LogInformation("FREE TIER: 25 API calls/day, 5 calls/minute");
        logger.LogInformation("========================================");

        var context = services.GetRequiredService<FinanceContext>();

        // Ensure database is created
        context.Database.EnsureCreated();

        var seeder = services.GetRequiredService<LiveStockSeeder>();

        // ⚠️ IMPORTANT: Start with fewer stocks to stay within free tier limits
        // Each stock = 1 API call (with optimized service)
        // Recommended: 5-10 stocks for initial seeding
        var symbols = new List<string>
        { 
            // Popular US Tech Stocks (5 stocks = 5 API calls)
            "AAPL",   // Apple
            "MSFT",   // Microsoft
            "GOOGL",  // Google
            "TSLA",   // Tesla
            "NVDA",   // NVIDIA
            
            // Add more throughout the day using POST /api/stocks endpoint
            // Or uncomment these (but watch your API limits):
            
            // "AMZN",     // Amazon
            // "META",     // Meta/Facebook
            // "NFLX",     // Netflix
            
            // Canadian ETFs (use .TO suffix)
            // "XEQT.TO",  // iShares Core Equity ETF
            // "VEQT.TO",  // Vanguard All-Equity ETF
            // "VGRO.TO",  // Vanguard Growth ETF
        };

        logger.LogInformation($"Seeding {symbols.Count} stocks (uses {symbols.Count} API calls)");
        logger.LogInformation("========================================");

        await seeder.SeedAsync(symbols);

        logger.LogInformation("========================================");
        logger.LogInformation("Database seeding completed!");
        logger.LogInformation("TIP: Add more stocks using POST /api/stocks endpoint");
        logger.LogInformation("========================================");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Canadian Finance API v1");
        c.RoutePrefix = string.Empty; // Set Swagger UI at app's root
    });
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
