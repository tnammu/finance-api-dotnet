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
        // Cache permanently - no expiration (stocks in DB are refreshed manually)

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
        /// Uses Alpha Vantage first, falls back to Yahoo Finance if it fails
        /// </summary>
        public async Task<(decimal? Price, string CompanyName, decimal? DividendYield, bool IsLive)> GetLiveStockInfoAsync(string symbol)
        {
            try
            {
                // Check cache first (in-memory cache for current session)
                if (_cache.TryGetValue(symbol, out var cachedData))
                {
                    _logger.LogInformation($"✓ Returning cached data for {symbol} (saved API call)");
                    return (cachedData.Price, cachedData.CompanyName, cachedData.DividendYield, false);
                }

                _logger.LogInformation($"→ Fetching live data for {symbol} from Alpha Vantage (API call)");

                // Try Alpha Vantage first
                var alphaVantageResult = await TryAlphaVantageAsync(symbol);
                if (alphaVantageResult.Price.HasValue)
                {
                    return alphaVantageResult;
                }

                // If Alpha Vantage fails, try Yahoo Finance as fallback
                _logger.LogInformation($"→ Alpha Vantage failed, trying Yahoo Finance fallback for {symbol}");
                var yahooResult = await TryYahooFinanceAsync(symbol);
                if (yahooResult.Price.HasValue)
                {
                    return yahooResult;
                }

                // Both failed, return cached or default
                return GetCachedOrDefault(symbol);
            }
            catch (Exception ex)
            {
                _logger.LogError($"✗ Error fetching {symbol}: {ex.Message}");
                return GetCachedOrDefault(symbol);
            }
        }

        private async Task<(decimal? Price, string CompanyName, decimal? DividendYield, bool IsLive)> TryAlphaVantageAsync(string symbol)
        {
            try
            {
                var url = $"https://www.alphavantage.co/query?function=GLOBAL_QUOTE&symbol={symbol}&apikey={_apiKey}";

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var response = await _httpClient.GetStringAsync(url, cts.Token);

                var json = JObject.Parse(response);

                // Check for API errors or rate limits
                if (json["Error Message"] != null)
                {
                    _logger.LogError($"✗ Alpha Vantage API error for {symbol}: {json["Error Message"]}");
                    return (null, symbol, null, false);
                }

                if (json["Note"] != null)
                {
                    _logger.LogWarning($"⚠ Alpha Vantage rate limit reached!");
                    return (null, symbol, null, false);
                }

                // Extract data from Global Quote
                var globalQuote = json["Global Quote"];
                if (globalQuote == null || !globalQuote.HasValues)
                {
                    _logger.LogWarning($"✗ No Alpha Vantage data for {symbol}");
                    return (null, symbol, null, false);
                }

                // Parse price
                var priceString = globalQuote["05. price"]?.ToString();
                if (string.IsNullOrEmpty(priceString) || !decimal.TryParse(priceString, out var price))
                {
                    return (null, symbol, null, false);
                }

                // Get company name from our mapping
                string companyName = CompanyNames.ContainsKey(symbol) ? CompanyNames[symbol] : symbol;

                // Cache the result
                _cache[symbol] = (DateTime.UtcNow, price, companyName, null);

                _logger.LogInformation($"✓ Alpha Vantage: {symbol} = ${price} - {companyName}");

                return (price, companyName, null, true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"⚠ Alpha Vantage error for {symbol}: {ex.Message}");
                return (null, symbol, null, false);
            }
        }

        private async Task<(decimal? Price, string CompanyName, decimal? DividendYield, bool IsLive)> TryYahooFinanceAsync(string symbol)
        {
            try
            {
                // Yahoo Finance API v7/v8 endpoint (free, no auth required)
                var url = $"https://query1.finance.yahoo.com/v8/finance/chart/{symbol}?interval=1d&range=1d";

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var response = await _httpClient.GetStringAsync(url, cts.Token);

                var json = JObject.Parse(response);

                // Check for errors
                if (json["chart"]?["error"] != null)
                {
                    _logger.LogWarning($"✗ Yahoo Finance error for {symbol}");
                    return (null, symbol, null, false);
                }

                // Extract price
                var result = json["chart"]?["result"]?[0];
                if (result == null)
                {
                    _logger.LogWarning($"✗ No Yahoo Finance data for {symbol}");
                    return (null, symbol, null, false);
                }

                var meta = result["meta"];
                var priceValue = meta?["regularMarketPrice"];
                var longName = meta?["longName"]?.ToString();
                var shortName = meta?["shortName"]?.ToString();

                if (priceValue == null || !decimal.TryParse(priceValue.ToString(), out var price))
                {
                    return (null, symbol, null, false);
                }

                // Use longName or shortName, fallback to symbol
                string companyName = !string.IsNullOrEmpty(longName) ? longName :
                                    (!string.IsNullOrEmpty(shortName) ? shortName : symbol);

                // Try to get dividend yield if available
                decimal? dividendYield = null;
                var trailingAnnualDividendYield = meta?["trailingAnnualDividendYield"];
                if (trailingAnnualDividendYield != null &&
                    decimal.TryParse(trailingAnnualDividendYield.ToString(), out var yieldValue))
                {
                    dividendYield = yieldValue * 100; // Convert to percentage
                }

                // Cache the result
                _cache[symbol] = (DateTime.UtcNow, price, companyName, dividendYield);

                _logger.LogInformation($"✓ Yahoo Finance: {symbol} = ${price} - {companyName}");

                return (price, companyName, dividendYield, true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"⚠ Yahoo Finance error for {symbol}: {ex.Message}");
                return (null, symbol, null, false);
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
