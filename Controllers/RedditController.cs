using Microsoft.AspNetCore.Mvc;
using FinanceApi.Services;

namespace FinanceApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RedditController : ControllerBase
    {
        private readonly RedditSentimentService _redditService;
        private readonly ILogger<RedditController> _logger;

        public RedditController(
            RedditSentimentService redditService,
            ILogger<RedditController> logger)
        {
            _redditService = redditService;
            _logger = logger;
        }

        /// <summary>
        /// Get Reddit sentiment for a stock symbol
        /// Example: GET /api/reddit/sentiment/AAPL
        /// Query params: ?refresh=true to force refresh
        /// </summary>
        [HttpGet("sentiment/{symbol}")]
        public async Task<ActionResult<object>> GetSentiment(string symbol, [FromQuery] bool refresh = false)
        {
            try
            {
                var sentiment = await _redditService.GetSentimentAsync(symbol, refresh);

                if (sentiment == null)
                {
                    return NotFound(new { error = $"Could not fetch Reddit sentiment for {symbol}" });
                }

                return Ok(new
                {
                    symbol = sentiment.Symbol,
                    companyName = sentiment.CompanyName,

                    sentiment = new
                    {
                        period24h = new
                        {
                            score = sentiment.Sentiment24h,
                            rating = sentiment.SentimentRating24h,
                            mentionCount = sentiment.MentionCount24h,
                            uniqueAuthors = sentiment.UniqueAuthors24h,
                            positiveRatio = sentiment.PositiveRatio,
                            mentionVelocity = sentiment.MentionVelocity
                        },
                        period7d = new
                        {
                            score = sentiment.Sentiment7d,
                            rating = sentiment.SentimentRating7d,
                            mentionCount = sentiment.MentionCount7d
                        },
                        period30d = new
                        {
                            score = sentiment.Sentiment30d,
                            rating = sentiment.SentimentRating30d,
                            mentionCount = sentiment.MentionCount30d
                        }
                    },

                    mentions = new
                    {
                        positive = sentiment.PositiveMentions,
                        neutral = sentiment.NeutralMentions,
                        negative = sentiment.NegativeMentions,
                        total = sentiment.MentionCount24h
                    },

                    subredditBreakdown = new
                    {
                        wallstreetbets = sentiment.WSBMentions24h,
                        stocks = sentiment.StocksMentions24h,
                        investing = sentiment.InvestingMentions24h,
                        stockMarket = sentiment.StockMarketMentions24h
                    },

                    trending = new
                    {
                        score = sentiment.TrendingScore,
                        topKeywords = sentiment.TopKeywords24h?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>()
                    },

                    recentMentions = sentiment.RecentMentions.Select(m => new
                    {
                        postId = m.PostId,
                        subreddit = m.Subreddit,
                        author = m.Author,
                        title = m.Title,
                        content = m.Content.Length > 200 ? m.Content.Substring(0, 200) + "..." : m.Content,
                        createdAt = m.CreatedAt,
                        upvotes = m.Upvotes,
                        score = m.Score,
                        sentimentScore = m.SentimentScore,
                        sentimentLabel = m.SentimentLabel
                    }).ToList(),

                    dailySummaries = sentiment.DailySummaries,

                    metadata = new
                    {
                        fromCache = sentiment.IsFromCache,
                        lastUpdated = sentiment.LastUpdated,
                        dataSource = "Reddit/PRAW",
                        cacheExpiration = "4 hours"
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching Reddit sentiment for {symbol}: {ex.Message}");
                return StatusCode(500, new { error = "Failed to fetch Reddit sentiment" });
            }
        }

        /// <summary>
        /// Get all Reddit sentiment data
        /// Example: GET /api/reddit/all
        /// </summary>
        [HttpGet("all")]
        public async Task<ActionResult<object>> GetAllSentiments()
        {
            try
            {
                var sentiments = await _redditService.GetAllSentimentsAsync();

                return Ok(new
                {
                    totalStocks = sentiments.Count,
                    sentiments = sentiments.Select(s => new
                    {
                        symbol = s.Symbol,
                        companyName = s.CompanyName,
                        sentiment24h = s.Sentiment24h,
                        sentimentRating = s.SentimentRating24h,
                        mentionCount = s.MentionCount24h,
                        trendingScore = s.TrendingScore,
                        positiveRatio = s.PositiveRatio,
                        lastUpdated = s.LastUpdated,
                        hoursOld = (DateTime.UtcNow - s.LastUpdated).TotalHours
                    }).ToList()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching all Reddit sentiments: {ex.Message}");
                return StatusCode(500, new { error = "Failed to fetch Reddit sentiments" });
            }
        }

        /// <summary>
        /// Get top trending stocks on Reddit
        /// Example: GET /api/reddit/trending?count=10
        /// </summary>
        [HttpGet("trending")]
        public async Task<ActionResult<object>> GetTrendingStocks([FromQuery] int count = 10)
        {
            try
            {
                if (count < 1 || count > 50)
                {
                    return BadRequest(new { error = "Count must be between 1 and 50" });
                }

                var trending = await _redditService.GetTrendingStocksAsync(count);

                return Ok(new
                {
                    count = trending.Count,
                    generatedAt = DateTime.UtcNow,
                    trending = trending.Select((s, index) => new
                    {
                        rank = index + 1,
                        symbol = s.Symbol,
                        companyName = s.CompanyName,
                        trendingScore = s.TrendingScore,
                        sentiment = s.Sentiment24h,
                        sentimentRating = s.SentimentRating24h,
                        mentionCount = s.MentionCount24h,
                        positiveRatio = s.PositiveRatio,
                        lastUpdated = s.LastUpdated,
                        hoursOld = (DateTime.UtcNow - s.LastUpdated).TotalHours
                    }).ToList()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching trending stocks: {ex.Message}");
                return StatusCode(500, new { error = "Failed to fetch trending stocks" });
            }
        }

        /// <summary>
        /// Bulk fetch Reddit sentiment for multiple symbols
        /// Example: POST /api/reddit/bulk with body ["AAPL", "MSFT", "TSLA"]
        /// </summary>
        [HttpPost("bulk")]
        public async Task<ActionResult<object>> BulkFetchSentiment([FromBody] List<string> symbols)
        {
            try
            {
                if (symbols == null || symbols.Count == 0)
                {
                    return BadRequest(new { error = "Symbols list cannot be empty" });
                }

                if (symbols.Count > 20)
                {
                    return BadRequest(new { error = "Maximum 20 symbols allowed per bulk request" });
                }

                var results = new List<object>();
                var failed = new List<string>();

                foreach (var symbol in symbols)
                {
                    // Rate limiting (Reddit API: 60 requests/minute)
                    await Task.Delay(1000);  // 1 second between requests

                    var sentiment = await _redditService.GetSentimentAsync(symbol, forceRefresh: false);

                    if (sentiment != null)
                    {
                        results.Add(new
                        {
                            symbol = sentiment.Symbol,
                            sentiment24h = sentiment.Sentiment24h,
                            sentimentRating = sentiment.SentimentRating24h,
                            mentionCount = sentiment.MentionCount24h,
                            trendingScore = sentiment.TrendingScore,
                            positiveRatio = sentiment.PositiveRatio,
                            lastUpdated = sentiment.LastUpdated
                        });
                    }
                    else
                    {
                        failed.Add(symbol);
                    }
                }

                return Ok(new
                {
                    requested = symbols.Count,
                    successful = results.Count,
                    failed = failed.Count,
                    failedSymbols = failed,
                    results = results
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in bulk fetch: {ex.Message}");
                return StatusCode(500, new { error = "Failed to bulk fetch sentiments" });
            }
        }
    }
}
