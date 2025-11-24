using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using FinanceApi.Data;
using FinanceApi.Models;
using Newtonsoft.Json.Linq;

namespace FinanceApi.Services
{
    public class PerformanceComparisonService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly DividendDbContext _dbContext;
        private readonly ILogger<PerformanceComparisonService> _logger;
        private const string BENCHMARK_SYMBOL = "SPY"; // S&P 500 ETF

        public PerformanceComparisonService(
            IHttpClientFactory httpClientFactory,
            DividendDbContext dbContext,
            ILogger<PerformanceComparisonService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task<object?> CompareStockToSP500Async(string symbol, int periodYears = 1)
        {
            try
            {
                // Fetch stock and benchmark data
                var stockData = await FetchHistoricalDataFromYahooAsync(symbol, periodYears);
                var benchmarkData = await FetchHistoricalDataFromYahooAsync(BENCHMARK_SYMBOL, periodYears);

                if (stockData == null || benchmarkData == null || stockData.Count == 0 || benchmarkData.Count == 0)
                {
                    return null;
                }

                // Calculate metrics
                var stockMetrics = CalculateMetrics(stockData);
                var benchmarkMetrics = CalculateMetrics(benchmarkData);

                // Calculate relative metrics (beta, alpha)
                var stockReturns = CalculateReturns(stockData);
                var benchmarkReturns = CalculateReturns(benchmarkData);

                var beta = CalculateBeta(stockReturns, benchmarkReturns);
                var alpha = CalculateAlpha(stockMetrics.AnnualizedReturn, benchmarkMetrics.AnnualizedReturn, beta);
                var correlation = CalculateCorrelation(stockReturns, benchmarkReturns);

                return new
                {
                    symbol = symbol,
                    period = periodYears,
                    stock = new
                    {
                        totalReturn = stockMetrics.TotalReturn,
                        annualizedReturn = stockMetrics.AnnualizedReturn,
                        volatility = stockMetrics.Volatility,
                        sharpeRatio = stockMetrics.SharpeRatio,
                        maxDrawdown = stockMetrics.MaxDrawdown
                    },
                    benchmark = new
                    {
                        symbol = BENCHMARK_SYMBOL,
                        totalReturn = benchmarkMetrics.TotalReturn,
                        annualizedReturn = benchmarkMetrics.AnnualizedReturn,
                        volatility = benchmarkMetrics.Volatility,
                        sharpeRatio = benchmarkMetrics.SharpeRatio,
                        maxDrawdown = benchmarkMetrics.MaxDrawdown
                    },
                    relative = new
                    {
                        beta = beta,
                        alpha = alpha,
                        correlation = correlation,
                        excessReturn = stockMetrics.TotalReturn - benchmarkMetrics.TotalReturn
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error comparing {symbol} to S&P 500: {ex.Message}");
                return null;
            }
        }

        public async Task<object?> ComparePortfolioToSP500Async(int periodYears = 1)
        {
            try
            {
                var stocks = await _dbContext.DividendModels.ToListAsync();
                var benchmarkData = await FetchHistoricalDataFromYahooAsync(BENCHMARK_SYMBOL, periodYears);

                if (benchmarkData == null || benchmarkData.Count == 0)
                {
                    return null;
                }

                var benchmarkMetrics = CalculateMetrics(benchmarkData);
                var stockComparisons = new List<object>();

                foreach (var stock in stocks)
                {
                    // Add delay to avoid rate limiting (500ms between requests)
                    await Task.Delay(500);

                    var stockData = await FetchHistoricalDataFromYahooAsync(stock.Symbol, periodYears);

                    if (stockData == null || stockData.Count == 0)
                        continue;

                    var stockMetrics = CalculateMetrics(stockData);
                    var stockReturns = CalculateReturns(stockData);
                    var benchmarkReturns = CalculateReturns(benchmarkData);

                    var beta = CalculateBeta(stockReturns, benchmarkReturns);
                    var alpha = CalculateAlpha(stockMetrics.AnnualizedReturn, benchmarkMetrics.AnnualizedReturn, beta);

                    stockComparisons.Add(new
                    {
                        symbol = stock.Symbol,
                        name = stock.CompanyName,
                        totalReturn = stockMetrics.TotalReturn,
                        annualizedReturn = stockMetrics.AnnualizedReturn,
                        volatility = stockMetrics.Volatility,
                        sharpeRatio = stockMetrics.SharpeRatio,
                        beta = beta,
                        alpha = alpha,
                        vsSpX = stockMetrics.TotalReturn - benchmarkMetrics.TotalReturn
                    });
                }

                return new
                {
                    period = periodYears,
                    benchmark = new
                    {
                        symbol = BENCHMARK_SYMBOL,
                        totalReturn = benchmarkMetrics.TotalReturn,
                        annualizedReturn = benchmarkMetrics.AnnualizedReturn,
                        volatility = benchmarkMetrics.Volatility
                    },
                    stocks = stockComparisons
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error comparing portfolio to S&P 500: {ex.Message}");
                return null;
            }
        }

        public async Task<object?> GetHistoricalPriceDataAsync(string symbol, int periodYears = 1)
        {
            try
            {
                var data = await FetchHistoricalDataFromYahooAsync(symbol, periodYears);

                if (data == null || data.Count == 0)
                    return null;

                return data.Select(d => new
                {
                    date = d.Date.ToString("yyyy-MM-dd"),
                    close = Math.Round(d.Close, 2)
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching historical data for {symbol}: {ex.Message}");
                return null;
            }
        }

        private async Task<List<PriceData>?> FetchHistoricalDataFromYahooAsync(string symbol, int periodYears)
        {
            const int maxRetries = 3;
            const int baseDelayMs = 2000;

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    var endDate = DateTime.Now;
                    var startDate = endDate.AddYears(-periodYears);

                    var endTimestamp = ((DateTimeOffset)endDate).ToUnixTimeSeconds();
                    var startTimestamp = ((DateTimeOffset)startDate).ToUnixTimeSeconds();

                    var url = $"https://query1.finance.yahoo.com/v8/finance/chart/{symbol}?period1={startTimestamp}&period2={endTimestamp}&interval=1d";

                    var client = _httpClientFactory.CreateClient();
                    var response = await client.GetAsync(url);

                    // Handle rate limiting with retry
                    if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    {
                        if (attempt < maxRetries - 1)
                        {
                            var delay = baseDelayMs * (int)Math.Pow(2, attempt); // Exponential backoff
                            _logger.LogWarning($"Rate limited for {symbol}, retrying after {delay}ms (attempt {attempt + 1}/{maxRetries})");
                            await Task.Delay(delay);
                            continue;
                        }
                        _logger.LogWarning($"Failed to fetch data for {symbol} after {maxRetries} attempts (rate limited)");
                        return null;
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning($"Failed to fetch data for {symbol} from Yahoo Finance (Status: {response.StatusCode})");
                        return null;
                    }

                    var json = await response.Content.ReadAsStringAsync();
                    var data = JObject.Parse(json);

                    var timestamps = data["chart"]?["result"]?[0]?["timestamp"]?.ToObject<long[]>();
                    var closes = data["chart"]?["result"]?[0]?["indicators"]?["quote"]?[0]?["close"]?.ToObject<double?[]>();

                    if (timestamps == null || closes == null)
                    {
                        return null;
                    }

                    var priceData = new List<PriceData>();

                    for (int i = 0; i < timestamps.Length; i++)
                    {
                        if (closes[i].HasValue)
                        {
                            priceData.Add(new PriceData
                            {
                                Date = DateTimeOffset.FromUnixTimeSeconds(timestamps[i]).DateTime,
                                Close = closes[i].Value
                            });
                        }
                    }

                    return priceData;
                }
                catch (Exception ex)
                {
                    if (attempt < maxRetries - 1)
                    {
                        _logger.LogWarning($"Error fetching data for {symbol} (attempt {attempt + 1}/{maxRetries}): {ex.Message}");
                        await Task.Delay(baseDelayMs);
                        continue;
                    }
                    _logger.LogError($"Error fetching data for {symbol} after {maxRetries} attempts: {ex.Message}");
                    return null;
                }
            }

            return null;  // All retries exhausted
        }

        private PerformanceMetrics CalculateMetrics(List<PriceData> priceData)
        {
            var returns = CalculateReturns(priceData);

            var totalReturn = (priceData.Last().Close / priceData.First().Close - 1) * 100;

            var avgReturn = returns.Average();
            var stdDev = Math.Sqrt(returns.Select(r => Math.Pow(r - avgReturn, 2)).Average());
            var annualizedReturn = Math.Pow(1 + avgReturn, 252) - 1;
            var annualizedVolatility = stdDev * Math.Sqrt(252);

            var riskFreeRate = 0.04; // 4% risk-free rate
            var sharpeRatio = annualizedVolatility > 0
                ? (annualizedReturn - riskFreeRate) / annualizedVolatility
                : 0;

            var maxDrawdown = CalculateMaxDrawdown(priceData);

            return new PerformanceMetrics
            {
                TotalReturn = totalReturn,
                AnnualizedReturn = annualizedReturn * 100,
                Volatility = annualizedVolatility * 100,
                SharpeRatio = sharpeRatio,
                MaxDrawdown = maxDrawdown
            };
        }

        private List<double> CalculateReturns(List<PriceData> priceData)
        {
            var returns = new List<double>();

            for (int i = 1; i < priceData.Count; i++)
            {
                var dailyReturn = (priceData[i].Close - priceData[i - 1].Close) / priceData[i - 1].Close;
                returns.Add(dailyReturn);
            }

            return returns;
        }

        private double CalculateBeta(List<double> stockReturns, List<double> benchmarkReturns)
        {
            var minLength = Math.Min(stockReturns.Count, benchmarkReturns.Count);

            if (minLength < 20)
                return double.NaN;

            var stockAvg = stockReturns.Take(minLength).Average();
            var benchmarkAvg = benchmarkReturns.Take(minLength).Average();

            double covariance = 0;
            double benchmarkVariance = 0;

            for (int i = 0; i < minLength; i++)
            {
                covariance += (stockReturns[i] - stockAvg) * (benchmarkReturns[i] - benchmarkAvg);
                benchmarkVariance += Math.Pow(benchmarkReturns[i] - benchmarkAvg, 2);
            }

            covariance /= minLength;
            benchmarkVariance /= minLength;

            return benchmarkVariance > 0 ? covariance / benchmarkVariance : double.NaN;
        }

        private double CalculateAlpha(double stockReturn, double benchmarkReturn, double beta, double riskFreeRate = 4.0)
        {
            if (double.IsNaN(beta))
                return double.NaN;

            var expectedReturn = riskFreeRate + beta * (benchmarkReturn - riskFreeRate);
            return stockReturn - expectedReturn;
        }

        private double CalculateCorrelation(List<double> stockReturns, List<double> benchmarkReturns)
        {
            var minLength = Math.Min(stockReturns.Count, benchmarkReturns.Count);

            if (minLength < 20)
                return double.NaN;

            var stockAvg = stockReturns.Take(minLength).Average();
            var benchmarkAvg = benchmarkReturns.Take(minLength).Average();

            double numerator = 0;
            double stockSumSq = 0;
            double benchmarkSumSq = 0;

            for (int i = 0; i < minLength; i++)
            {
                var stockDiff = stockReturns[i] - stockAvg;
                var benchmarkDiff = benchmarkReturns[i] - benchmarkAvg;

                numerator += stockDiff * benchmarkDiff;
                stockSumSq += stockDiff * stockDiff;
                benchmarkSumSq += benchmarkDiff * benchmarkDiff;
            }

            var denominator = Math.Sqrt(stockSumSq * benchmarkSumSq);
            return denominator > 0 ? numerator / denominator : double.NaN;
        }

        private double CalculateMaxDrawdown(List<PriceData> priceData)
        {
            double maxDrawdown = 0;
            double peak = priceData[0].Close;

            foreach (var price in priceData)
            {
                if (price.Close > peak)
                    peak = price.Close;

                var drawdown = (price.Close - peak) / peak;

                if (drawdown < maxDrawdown)
                    maxDrawdown = drawdown;
            }

            return maxDrawdown * 100;
        }

        private class PriceData
        {
            public DateTime Date { get; set; }
            public double Close { get; set; }
        }

        private class PerformanceMetrics
        {
            public double TotalReturn { get; set; }
            public double AnnualizedReturn { get; set; }
            public double Volatility { get; set; }
            public double SharpeRatio { get; set; }
            public double MaxDrawdown { get; set; }
        }
    }
}
