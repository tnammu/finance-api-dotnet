using FinanceApi.Services;
using FinanceApi.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add CORS - Allow frontend to connect
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://localhost:5199", "https://localhost:7065")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Add services
builder.Services.AddControllers();

// Add Database Context
builder.Services.AddDbContext<DividendDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddDbContext<FinanceDbcontext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("FinanceConnection")));


// Add HTTP Client Services
builder.Services.AddHttpClient<DividendAnalysisService>();
builder.Services.AddHttpClient<StockService>();
builder.Services.AddScoped<LiveStockSeeder>();

// Add Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Dividend Analysis API with Database Caching",
        Version = "v1",
        Description = "Smart caching: Checks DB first, only calls API when needed"
    });
});

var app = builder.Build();

// Create databases if they don't exist
using (var scope = app.Services.CreateScope())
{
    var dividendDbContext = scope.ServiceProvider.GetRequiredService<DividendDbContext>();
    dividendDbContext.Database.EnsureCreated();
    Console.WriteLine("✓ Dividend database ready (dividends.db)");

    var financeDbContext = scope.ServiceProvider.GetRequiredService<FinanceDbcontext>();
    financeDbContext.Database.EnsureCreated();
    Console.WriteLine("✓ Finance database ready (finance.db)");
}

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Dividend API v1");
        c.RoutePrefix = string.Empty;
    });
}

app.UseCors("AllowReactApp");
// Disable HTTPS redirect in development to avoid ERR_EMPTY_RESPONSE
// app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

Console.WriteLine("================================================");
Console.WriteLine("💾 Finance API with Permanent Database Storage");
Console.WriteLine("================================================");
Console.WriteLine("✓ Smart Caching: DB first, API only when needed");
Console.WriteLine("✓ Permanent storage - no expiration");
Console.WriteLine("✓ Track API usage automatically");
Console.WriteLine("");
Console.WriteLine("🎯 Try these:");
Console.WriteLine("  GET  /api/dividends/analyze/AAPL");
Console.WriteLine("  GET  /api/dividends/cached              (view all)");
Console.WriteLine("  GET  /api/dividends/usage/today         (API usage)");
Console.WriteLine("  GET  /api/dividends/stats               (DB stats)");
Console.WriteLine("");
Console.WriteLine("📊 Databases: finance.db, dividends.db");
Console.WriteLine("================================================");

app.Run();