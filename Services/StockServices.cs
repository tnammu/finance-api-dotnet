using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;

namespace FinanceApi.Services
{
    /// <summary>
    /// Optimized Stock Service for Alpha Vantage FREE tier (25 calls/day, 5 calls/minute)
    /// This version minimizes API calls by:
    /// 1. Using only GLOBAL_QUOTE (1 call per stock instead of 3)
    /// 2. Longer cache duration (15 minutes instead of 5)
    /// 3. Company name from symbol mapping (no extra API call)
    /// </summary>
    public class StockService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<StockService> _logger;
        private readonly string _apiKey;
        private static readonly Dictionary<string, (DateTime FetchTime, decimal? Price, string CompanyName, decimal? DividendYield)> _cache = new();
        private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(15); // Longer cache for free tier

        // Common stock name mapping to avoid extra API calls
        private static readonly Dictionary<string, string> CompanyNames = new()
        {
            { "AAPL", "Apple Inc." },
            { "MSFT", "Microsoft Corporation" },
            { "GOOGL", "Alphabet Inc." },
            { "TSLA", "Tesla Inc." },
            { "AMZN", "Amazon.com Inc." },
            { "NVDA", "NVIDIA Corporation" },
            { "META", "Meta Platforms Inc." },
            { "NFLX", "Netflix Inc." },
            { "XEQT.TO", "iShares Core Equity ETF Portfolio" },
            { "VEQT.TO", "Vanguard All-Equity ETF Portfolio" },
            { "VGRO.TO", "Vanguard Growth ETF Portfolio" },
            { "XGRO.TO", "iShares Core Growth ETF Portfolio" },
            { "VBAL.TO", "Vanguard Balanced ETF Portfolio" },
            { "XBAL.TO", "iShares Core Balanced ETF Portfolio" },
            { "VFV.TO", "Vanguard S&P 500 Index ETF" },
            { "VCN.TO", "Vanguard FTSE Canada All Cap Index ETF" },
            { "XIC.TO", "iShares Core S&P/TSX Capped Composite Index ETF" },
            { "XUU.TO", "iShares Core S&P U.S. Total Market Index ETF" }
        };

        public StockService(HttpClient httpClient, ILogger<StockService> logger, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _logger = logger;
            _apiKey = configuration["AlphaVantage:ApiKey"] ?? throw new InvalidOperationException("Alpha Vantage API key not configured");
        }

        /// <summary>
        /// Get stock info with MINIMAL API calls (only 1 call per stock)
        /// </summary>
        public async Task<(decimal? Price, string CompanyName, decimal? DividendYield, bool IsLive)> GetLiveStockInfoAsync(string symbol)
        {
            try
            {
                // Check cache first
                if (_cache.TryGetValue(symbol, out var cachedData))
                {
                    if (DateTime.UtcNow - cachedData.FetchTime < CacheExpiration)
                    {
                        _logger.LogInformation($"✓ Returning cached data for {symbol} (saved API call)");
                        return (cachedData.Price, cachedData.CompanyName, cachedData.DividendYield, false);
                    }
                }

                _logger.LogInformation($"→ Fetching live data for {symbol} from Alpha Vantage (API call)");

                // Use GLOBAL_QUOTE only (1 API call instead of 3)
                var url = $"https://www.alphavantage.co/query?function=GLOBAL_QUOTE&symbol={symbol}&apikey={_apiKey}";

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var response = await _httpClient.GetStringAsync(url, cts.Token);

                var json = JObject.Parse(response);

                // Check for API errors or rate limits
                if (json["Error Message"] != null)
                {
                    _logger.LogError($"✗ Alpha Vantage API error for {symbol}: {json["Error Message"]}");
                    return GetCachedOrDefault(symbol);
                }

                if (json["Note"] != null)
                {
                    _logger.LogWarning($"⚠ Alpha Vantage rate limit reached! Using cached data if available.");
                    _logger.LogWarning($"Message: {json["Note"]}");
                    return GetCachedOrDefault(symbol);
                }

                // Extract data from Global Quote
                var globalQuote = json["Global Quote"];
                if (globalQuote == null || !globalQuote.HasValues)
                {
                    _logger.LogWarning($"✗ No data returned for {symbol} - symbol may be invalid");
                    return GetCachedOrDefault(symbol);
                }

                // Parse price
                decimal? price = null;
                var priceString = globalQuote["05. price"]?.ToString();
                if (!string.IsNullOrEmpty(priceString) && decimal.TryParse(priceString, out var parsedPrice))
                {
                    price = parsedPrice;
                }
                else
                {
                    _logger.LogWarning($"✗ Could not parse price for {symbol}");
                    return GetCachedOrDefault(symbol);
                }

                // Get company name from our mapping (no API call)
                string companyName = CompanyNames.ContainsKey(symbol)
                    ? CompanyNames[symbol]
                    : symbol;

                // For dividend yield, we'd need OVERVIEW endpoint (extra API call)
                // To save API calls, we'll skip this on free tier
                // If you need it, uncomment the GetDividendYieldAsync call below
                decimal? dividendYield = null; // await GetDividendYieldAsync(symbol);

                // Cache the result for 15 minutes
                _cache[symbol] = (DateTime.UtcNow, price, companyName, dividendYield);

                _logger.LogInformation($"✓ Successfully fetched {symbol}: ${price} - {companyName}");

                return (price, companyName, dividendYield, true);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError($"✗ HTTP error for {symbol}: {ex.Message}");
                return GetCachedOrDefault(symbol);
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError($"✗ Timeout for {symbol}: {ex.Message}");
                return GetCachedOrDefault(symbol);
            }
            catch (Exception ex)
            {
                _logger.LogError($"✗ Error fetching {symbol}: {ex.Message}");
                return GetCachedOrDefault(symbol);
            }
        }

        /// <summary>
        /// Get dividend yield - requires extra API call, use sparingly!
        /// Commented out by default to save API calls on free tier
        /// </summary>
        private async Task<decimal?> GetDividendYieldAsync(string symbol)
        {
            try
            {
                _logger.LogInformation($"→ Fetching dividend yield for {symbol} (extra API call)");

                var url = $"https://www.alphavantage.co/query?function=OVERVIEW&symbol={symbol}&apikey={_apiKey}";

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var response = await _httpClient.GetStringAsync(url, cts.Token);
                var json = JObject.Parse(response);

                // Check for rate limit
                if (json["Note"] != null)
                {
                    _logger.LogWarning($"⚠ Rate limit hit while fetching dividend for {symbol}");
                    return null;
                }

                var dividendYieldString = json["DividendYield"]?.ToString();
                if (!string.IsNullOrEmpty(dividendYieldString) && decimal.TryParse(dividendYieldString, out var yield))
                {
                    // Alpha Vantage returns 0.0052 for 0.52%, so multiply by 100
                    return yield * 100;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Could not fetch dividend yield for {symbol}: {ex.Message}");
            }

            return null;
        }

        private (decimal? Price, string CompanyName, decimal? DividendYield, bool IsLive) GetCachedOrDefault(string symbol)
        {
            if (_cache.TryGetValue(symbol, out var cachedData))
            {
                _logger.LogInformation($"↻ Returning stale cached data for {symbol}");
                return (cachedData.Price, cachedData.CompanyName, cachedData.DividendYield, false);
            }

            _logger.LogWarning($"✗ No cached data available for {symbol}");

            // Return symbol with mapped name if available
            string fallbackName = CompanyNames.ContainsKey(symbol) ? CompanyNames[symbol] : symbol;
            return (null, fallbackName, null, false);
        }

        /// <summary>
        /// Add a new company name mapping to avoid API calls
        /// </summary>
        public void AddCompanyNameMapping(string symbol, string companyName)
        {
            if (!CompanyNames.ContainsKey(symbol))
            {
                CompanyNames[symbol] = companyName;
                _logger.LogInformation($"Added company name mapping: {symbol} → {companyName}");
            }
        }
    }
}
