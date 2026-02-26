using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinanceApi.Model
{
    // Main entity - one record per stock symbol with aggregated sentiment metrics
    public class RedditSentimentModel
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(20)]
        public string Symbol { get; set; } = string.Empty;

        [MaxLength(200)]
        public string CompanyName { get; set; } = string.Empty;

        // Sentiment Metrics (24h, 7d, 30d) - Range: -1.0 to +1.0
        public decimal? Sentiment24h { get; set; }
        public decimal? Sentiment7d { get; set; }
        public decimal? Sentiment30d { get; set; }

        // Sentiment Ratings
        [MaxLength(20)]
        public string SentimentRating24h { get; set; } = string.Empty;
        [MaxLength(20)]
        public string SentimentRating7d { get; set; } = string.Empty;
        [MaxLength(20)]
        public string SentimentRating30d { get; set; } = string.Empty;

        // Volume Metrics
        public int MentionCount24h { get; set; }
        public int MentionCount7d { get; set; }
        public int MentionCount30d { get; set; }
        public int UniqueAuthors24h { get; set; }
        public decimal MentionVelocity { get; set; }  // Mentions per hour (24h average)

        // Trending Score (0-100 composite)
        public int TrendingScore { get; set; }

        // Subreddit Breakdown (24h)
        public int WSBMentions24h { get; set; }           // r/wallstreetbets
        public int StocksMentions24h { get; set; }        // r/stocks
        public int InvestingMentions24h { get; set; }     // r/investing
        public int StockMarketMentions24h { get; set; }   // r/StockMarket

        // Sentiment Breakdown (24h)
        public int PositiveMentions { get; set; }
        public int NeutralMentions { get; set; }
        public int NegativeMentions { get; set; }
        public decimal PositiveRatio { get; set; }  // Percentage

        // Top Keywords (comma-separated, max 200 chars)
        [MaxLength(200)]
        public string TopKeywords24h { get; set; } = string.Empty;

        // Metadata
        public DateTime FetchedAt { get; set; }
        public DateTime LastUpdated { get; set; }

        [MaxLength(50)]
        public string DataSource { get; set; } = "Reddit/PRAW";

        // Navigation Properties
        public virtual ICollection<RedditMention> Mentions { get; set; } = new List<RedditMention>();
        public virtual ICollection<RedditDailySummary> DailySummaries { get; set; } = new List<RedditDailySummary>();
    }

    // Child entity - individual Reddit posts/comments mentioning the stock
    public class RedditMention
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int RedditSentimentModelId { get; set; }

        [Required]
        [MaxLength(20)]
        public string Symbol { get; set; } = string.Empty;  // Denormalized for query performance

        // Reddit Post Identifiers
        [MaxLength(50)]
        public string PostId { get; set; } = string.Empty;  // Reddit post ID (t3_xxxxx)

        [MaxLength(50)]
        public string? CommentId { get; set; }  // Comment ID if applicable (t1_xxxxx)

        [Required]
        [MaxLength(50)]
        public string Subreddit { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Author { get; set; } = string.Empty;

        [MaxLength(500)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string Content { get; set; } = string.Empty;

        // Post Metrics
        public DateTime CreatedAt { get; set; }
        public int Upvotes { get; set; }
        public int DownVotes { get; set; }
        public decimal Score { get; set; }  // Net score (upvotes - downvotes)

        // Sentiment Analysis
        public decimal SentimentScore { get; set; }  // -1.0 to +1.0 (VADER compound)

        [MaxLength(20)]
        public string SentimentLabel { get; set; } = string.Empty;  // "positive", "neutral", "negative"

        // Navigation Property
        [ForeignKey("RedditSentimentModelId")]
        public virtual RedditSentimentModel? RedditSentiment { get; set; }
    }

    // Child entity - daily aggregated summary for time-series analysis
    public class RedditDailySummary
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int RedditSentimentModelId { get; set; }

        [Required]
        [MaxLength(20)]
        public string Symbol { get; set; } = string.Empty;  // Denormalized for query performance

        [Required]
        public DateTime Date { get; set; }  // Date only (no time component)

        // Daily Metrics
        public int MentionCount { get; set; }
        public decimal AvgSentiment { get; set; }  // -1.0 to +1.0
        public int PositiveCount { get; set; }
        public int NeutralCount { get; set; }
        public int NegativeCount { get; set; }
        public int UniqueAuthors { get; set; }

        [MaxLength(50)]
        public string TopSubreddit { get; set; } = string.Empty;  // Which subreddit had most mentions that day

        // Navigation Property
        [ForeignKey("RedditSentimentModelId")]
        public virtual RedditSentimentModel? RedditSentiment { get; set; }
    }
}
