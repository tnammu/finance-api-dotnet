using FinanceApi.Repositories.Interfaces;
using System.Diagnostics;

namespace FinanceApi.Services
{
    public class RedditSentimentService
    {
        private readonly ILogger<RedditSentimentService> _logger;
        private readonly IRedditSentimentRepository _redditRepo;
        private readonly IConfiguration _configuration;

        public RedditSentimentService(
            ILogger<RedditSentimentService> logger,
            IRedditSentimentRepository redditRepo,
            IConfiguration configuration)
        {
            _logger = logger;
            _redditRepo = redditRepo;
            _configuration = configuration;
        }

        /// <summary>
        /// Get Reddit sentiment for a symbol - checks cache first (4 hour expiration)
        /// </summary>
        public async Task<RedditSentimentAnalysis?> GetSentimentAsync(string symbol, bool forceRefresh = false)
        {
            symbol = symbol.ToUpper();
            const int CACHE_EXPIRATION_HOURS = 4;  // Reddit data refreshes every 4 hours

            _logger.LogInformation($"========================================");
            _logger.LogInformation($"→ Requesting Reddit sentiment for: {symbol}");

            // Check cache
            if (!forceRefresh)
            {
                var cached = await GetFromDatabaseAsync(symbol);
                if (cached != null)
                {
                    var age = DateTime.UtcNow - cached.LastUpdated;
                    if (age.TotalHours < CACHE_EXPIRATION_HOURS)
                    {
                        _logger.LogInformation($"✓ CACHE HIT! Reddit sentiment age: {age.TotalHours:F1} hours");
                        return cached;
                    }
                    _logger.LogInformation($"⏰ Cache expired ({age.TotalHours:F1}h > {CACHE_EXPIRATION_HOURS}h), refreshing...");
                }
            }

            // Fetch fresh data via Python
            var success = await FetchSentimentViaPythonAsync(symbol);
            if (!success)
            {
                _logger.LogWarning($"Failed to fetch Reddit sentiment for {symbol}");
                // Return stale cache if available
                return await GetFromDatabaseAsync(symbol);
            }

            // Return fresh data
            return await GetFromDatabaseAsync(symbol);
        }

        /// <summary>
        /// Execute Python script to fetch Reddit sentiment
        /// </summary>
        public async Task<bool> FetchSentimentViaPythonAsync(string symbol)
        {
            try
            {
                var scriptPath = Path.Combine(Directory.GetCurrentDirectory(), "scripts", "update_reddit_sentiment.py");

                if (!File.Exists(scriptPath))
                {
                    _logger.LogError($"Python script not found: {scriptPath}");
                    return false;
                }

                // Get Reddit credentials from config
                var clientId = _configuration["Reddit:ClientId"];
                var clientSecret = _configuration["Reddit:ClientSecret"];

                if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
                {
                    _logger.LogError("Reddit API credentials not configured in appsettings.json");
                    return false;
                }

                _logger.LogInformation($"→ Executing Python script for {symbol}...");

                var processInfo = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = $"\"{scriptPath}\" {symbol} {clientId} {clientSecret}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Directory.GetCurrentDirectory()
                };

                using var process = new Process { StartInfo = processInfo };

                var output = new System.Text.StringBuilder();
                var error = new System.Text.StringBuilder();

                process.OutputDataReceived += (sender, e) => { if (e.Data != null) output.AppendLine(e.Data); };
                process.ErrorDataReceived += (sender, e) => { if (e.Data != null) error.AppendLine(e.Data); };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    _logger.LogInformation($"✓ Reddit sentiment fetched successfully for {symbol}");
                    _logger.LogDebug(output.ToString());
                    return true;
                }
                else
                {
                    _logger.LogWarning($"✗ Reddit sentiment fetch failed for {symbol} (exit code: {process.ExitCode})");
                    _logger.LogDebug($"Error: {error}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error executing Reddit sentiment script: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get sentiment from database cache
        /// </summary>
        private async Task<RedditSentimentAnalysis?> GetFromDatabaseAsync(string symbol)
        {
            var cached = await _redditRepo.GetBySymbolWithDetailsAsync(symbol);

            if (cached == null)
                return null;

            return new RedditSentimentAnalysis
            {
                Symbol = cached.Symbol,
                CompanyName = cached.CompanyName,
                Sentiment24h = cached.Sentiment24h,
                Sentiment7d = cached.Sentiment7d,
                Sentiment30d = cached.Sentiment30d,
                SentimentRating24h = cached.SentimentRating24h,
                SentimentRating7d = cached.SentimentRating7d,
                SentimentRating30d = cached.SentimentRating30d,
                MentionCount24h = cached.MentionCount24h,
                MentionCount7d = cached.MentionCount7d,
                MentionCount30d = cached.MentionCount30d,
                UniqueAuthors24h = cached.UniqueAuthors24h,
                MentionVelocity = cached.MentionVelocity,
                TrendingScore = cached.TrendingScore,
                WSBMentions24h = cached.WSBMentions24h,
                StocksMentions24h = cached.StocksMentions24h,
                InvestingMentions24h = cached.InvestingMentions24h,
                StockMarketMentions24h = cached.StockMarketMentions24h,
                PositiveMentions = cached.PositiveMentions,
                NeutralMentions = cached.NeutralMentions,
                NegativeMentions = cached.NegativeMentions,
                PositiveRatio = cached.PositiveRatio,
                TopKeywords24h = cached.TopKeywords24h,
                RecentMentions = cached.Mentions.Select(m => new RedditMentionDto
                {
                    PostId = m.PostId,
                    Subreddit = m.Subreddit,
                    Author = m.Author,
                    Title = m.Title,
                    Content = m.Content,
                    CreatedAt = m.CreatedAt,
                    Upvotes = m.Upvotes,
                    Score = m.Score,
                    SentimentScore = m.SentimentScore,
                    SentimentLabel = m.SentimentLabel
                }).ToList(),
                DailySummaries = cached.DailySummaries.ToDictionary(
                    d => d.Date.ToString("yyyy-MM-dd"),
                    d => new RedditDailySummaryDto
                    {
                        MentionCount = d.MentionCount,
                        AvgSentiment = d.AvgSentiment,
                        PositiveCount = d.PositiveCount,
                        NeutralCount = d.NeutralCount,
                        NegativeCount = d.NegativeCount,
                        UniqueAuthors = d.UniqueAuthors
                    }
                ),
                LastUpdated = cached.LastUpdated,
                IsFromCache = true
            };
        }

        /// <summary>
        /// Get all Reddit sentiment data (for screening/trending)
        /// </summary>
        public async Task<List<RedditSentimentSummary>> GetAllSentimentsAsync()
        {
            var sentiments = await _redditRepo.GetAllOrderedByTrendingScoreAsync();

            return sentiments.Select(s => new RedditSentimentSummary
            {
                Symbol = s.Symbol,
                CompanyName = s.CompanyName,
                Sentiment24h = s.Sentiment24h,
                SentimentRating24h = s.SentimentRating24h,
                MentionCount24h = s.MentionCount24h,
                TrendingScore = s.TrendingScore,
                PositiveRatio = s.PositiveRatio,
                LastUpdated = s.LastUpdated
            }).ToList();
        }

        /// <summary>
        /// Get top trending stocks on Reddit
        /// </summary>
        /// <summary>
        /// Fetch sentiment for multiple symbols with built-in rate limiting (1s between requests).
        /// </summary>
        public async Task<(List<RedditSentimentSummary> Results, List<string> Failed)> BulkFetchSentimentAsync(List<string> symbols)
        {
            var results = new List<RedditSentimentSummary>();
            var failed  = new List<string>();

            foreach (var symbol in symbols)
            {
                await Task.Delay(1000); // Reddit API: 60 req/min
                var sentiment = await GetSentimentAsync(symbol, forceRefresh: false);
                if (sentiment != null)
                    results.Add(new RedditSentimentSummary
                    {
                        Symbol            = sentiment.Symbol,
                        CompanyName       = sentiment.CompanyName,
                        Sentiment24h      = sentiment.Sentiment24h,
                        SentimentRating24h= sentiment.SentimentRating24h,
                        MentionCount24h   = sentiment.MentionCount24h,
                        TrendingScore     = sentiment.TrendingScore,
                        PositiveRatio     = sentiment.PositiveRatio,
                        LastUpdated       = sentiment.LastUpdated,
                    });
                else
                    failed.Add(symbol);
            }

            return (results, failed);
        }

        public async Task<List<RedditSentimentSummary>> GetTrendingStocksAsync(int count = 10)
        {
            var trending = await _redditRepo.GetTrendingAsync(minMentions: 5, count);

            return trending.Select(s => new RedditSentimentSummary
            {
                Symbol = s.Symbol,
                CompanyName = s.CompanyName,
                Sentiment24h = s.Sentiment24h,
                SentimentRating24h = s.SentimentRating24h,
                MentionCount24h = s.MentionCount24h,
                TrendingScore = s.TrendingScore,
                PositiveRatio = s.PositiveRatio,
                LastUpdated = s.LastUpdated
            }).ToList();
        }
    }

    // DTOs
    public class RedditSentimentAnalysis
    {
        public string Symbol { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public decimal? Sentiment24h { get; set; }
        public decimal? Sentiment7d { get; set; }
        public decimal? Sentiment30d { get; set; }
        public string SentimentRating24h { get; set; } = string.Empty;
        public string SentimentRating7d { get; set; } = string.Empty;
        public string SentimentRating30d { get; set; } = string.Empty;
        public int MentionCount24h { get; set; }
        public int MentionCount7d { get; set; }
        public int MentionCount30d { get; set; }
        public int UniqueAuthors24h { get; set; }
        public decimal MentionVelocity { get; set; }
        public int TrendingScore { get; set; }
        public int WSBMentions24h { get; set; }
        public int StocksMentions24h { get; set; }
        public int InvestingMentions24h { get; set; }
        public int StockMarketMentions24h { get; set; }
        public int PositiveMentions { get; set; }
        public int NeutralMentions { get; set; }
        public int NegativeMentions { get; set; }
        public decimal PositiveRatio { get; set; }
        public string TopKeywords24h { get; set; } = string.Empty;
        public List<string> TopKeywordsList =>
            TopKeywords24h?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>();
        public List<RedditMentionDto> RecentMentions { get; set; } = new List<RedditMentionDto>();
        public Dictionary<string, RedditDailySummaryDto> DailySummaries { get; set; } = new Dictionary<string, RedditDailySummaryDto>();
        public DateTime LastUpdated { get; set; }
        public bool IsFromCache { get; set; }
    }

    public class RedditMentionDto
    {
        public string PostId { get; set; } = string.Empty;
        public string Subreddit { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string TruncatedContent => Content.Length > 200 ? Content[..200] + "..." : Content;
        public DateTime CreatedAt { get; set; }
        public int Upvotes { get; set; }
        public decimal Score { get; set; }
        public decimal SentimentScore { get; set; }
        public string SentimentLabel { get; set; } = string.Empty;
    }

    public class RedditDailySummaryDto
    {
        public int MentionCount { get; set; }
        public decimal AvgSentiment { get; set; }
        public int PositiveCount { get; set; }
        public int NeutralCount { get; set; }
        public int NegativeCount { get; set; }
        public int UniqueAuthors { get; set; }
    }

    public class RedditSentimentSummary
    {
        public string Symbol { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public decimal? Sentiment24h { get; set; }
        public string SentimentRating24h { get; set; } = string.Empty;
        public int MentionCount24h { get; set; }
        public int TrendingScore { get; set; }
        public decimal PositiveRatio { get; set; }
        public DateTime LastUpdated { get; set; }
        public double HoursOld => (DateTime.UtcNow - LastUpdated).TotalHours;
    }
}
