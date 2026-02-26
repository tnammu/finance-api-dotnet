#!/usr/bin/env python3
"""
Reddit Sentiment Analysis for Stocks
Fetches mentions from r/wallstreetbets, r/stocks, r/investing, r/StockMarket
Analyzes sentiment using VADER
Updates dividends.db (RedditSentiments table)
"""

import sqlite3
import praw
from vaderSentiment.vaderSentiment import SentimentIntensityAnalyzer
from datetime import datetime, timedelta
import time
import sys
import os
import re

# Windows encoding handling
if sys.platform == "win32":
    try:
        sys.stdout.reconfigure(encoding='utf-8')
    except:
        pass

# Database setup
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
PROJECT_ROOT = os.path.dirname(SCRIPT_DIR)
DB_PATH = os.path.join(PROJECT_ROOT, "dividends.db")


class RedditSentimentAnalyzer:
    def __init__(self, db_path, reddit_client_id, reddit_client_secret, reddit_user_agent):
        self.db_path = db_path
        self.conn = None
        self.cursor = None

        # Initialize PRAW (Reddit API)
        self.reddit = praw.Reddit(
            client_id=reddit_client_id,
            client_secret=reddit_client_secret,
            user_agent=reddit_user_agent
        )

        # Initialize VADER sentiment analyzer
        self.sentiment_analyzer = SentimentIntensityAnalyzer()

        # Subreddits to monitor
        self.subreddits = ['wallstreetbets', 'stocks', 'investing', 'StockMarket']

        # Stock symbol regex (e.g., $AAPL, AAPL)
        self.symbol_pattern = re.compile(r'\b([A-Z]{1,5})\b|\$([A-Z]{1,5})\b')

        # False positives to exclude
        self.excluded_symbols = {'I', 'A', 'DD', 'YOLO', 'CEO', 'WSB', 'IMO', 'FDA', 'IPO', 'ETF',
                                'THE', 'FOR', 'AND', 'OR', 'NOT', 'BUT', 'ARE', 'WAS', 'ALL',
                                'ANY', 'CAN', 'MAY', 'NEW', 'NOW', 'OUT', 'SEE', 'GET', 'HAS', 'HAD'}

    def connect_db(self):
        """Connect to SQLite database"""
        try:
            self.conn = sqlite3.connect(self.db_path)
            self.cursor = self.conn.cursor()
            print(f"✓ Connected to database: {self.db_path}")
            return True
        except Exception as e:
            print(f"✗ Failed to connect to database: {e}")
            return False

    def extract_symbols(self, text):
        """Extract stock symbols from text"""
        if not text:
            return []
        matches = self.symbol_pattern.findall(text.upper())
        # Flatten tuples and remove empty strings
        symbols = [s for match in matches for s in match if s]
        # Filter out common false positives
        return list(set([s for s in symbols if s not in self.excluded_symbols]))

    def analyze_sentiment(self, text):
        """Analyze sentiment using VADER (-1.0 to +1.0)"""
        if not text:
            return 0.0, "neutral"

        scores = self.sentiment_analyzer.polarity_scores(text)
        compound = scores['compound']

        # Classify
        if compound >= 0.05:
            label = "positive"
        elif compound <= -0.05:
            label = "negative"
        else:
            label = "neutral"

        return round(compound, 3), label

    def fetch_reddit_mentions(self, symbol, time_period_hours=24):
        """Fetch Reddit mentions for a symbol in the last N hours"""
        mentions = []
        cutoff_time = datetime.utcnow() - timedelta(hours=time_period_hours)

        print(f"  Fetching mentions from last {time_period_hours} hours...")

        for subreddit_name in self.subreddits:
            try:
                subreddit = self.reddit.subreddit(subreddit_name)

                # Determine time filter based on period
                if time_period_hours <= 24:
                    time_filter = 'day'
                elif time_period_hours <= 168:  # 7 days
                    time_filter = 'week'
                else:
                    time_filter = 'month'

                # Search recent posts
                for submission in subreddit.search(
                    query=symbol,
                    time_filter=time_filter,
                    limit=100
                ):
                    created_time = datetime.utcfromtimestamp(submission.created_utc)
                    if created_time < cutoff_time:
                        continue

                    # Check if symbol is actually mentioned (case-insensitive)
                    text = f"{submission.title} {submission.selftext}"
                    if symbol.upper() not in text.upper():
                        continue

                    # Analyze post sentiment
                    sentiment_score, sentiment_label = self.analyze_sentiment(text)

                    mentions.append({
                        'post_id': submission.id,
                        'comment_id': None,
                        'subreddit': subreddit_name,
                        'author': str(submission.author) if submission.author else '[deleted]',
                        'title': submission.title[:500],
                        'content': submission.selftext[:2000] if submission.selftext else '',
                        'created_at': created_time,
                        'upvotes': submission.ups,
                        'downvotes': 0,  # Reddit doesn't expose this anymore
                        'score': submission.score,
                        'sentiment_score': sentiment_score,
                        'sentiment_label': sentiment_label
                    })

                    # Rate limiting
                    time.sleep(0.1)

                sub_count = len([m for m in mentions if m['subreddit'] == subreddit_name])
                if sub_count > 0:
                    print(f"    ✓ r/{subreddit_name}: {sub_count} mentions")

            except Exception as e:
                print(f"    ⚠️  r/{subreddit_name}: Error - {e}")

        return mentions

    def calculate_aggregates(self, mentions_24h, mentions_7d, mentions_30d):
        """Calculate aggregated metrics"""
        def calc_metrics(mentions):
            if not mentions:
                return {
                    'count': 0,
                    'avg_sentiment': 0.0,
                    'positive': 0,
                    'neutral': 0,
                    'negative': 0,
                    'unique_authors': 0,
                    'wsb': 0,
                    'stocks': 0,
                    'investing': 0,
                    'stockmarket': 0,
                    'sentiment_rating': 'No Data'
                }

            sentiments = [m['sentiment_score'] for m in mentions]
            avg_sentiment = sum(sentiments) / len(sentiments)

            # Rating
            if avg_sentiment >= 0.3:
                rating = "Very Positive"
            elif avg_sentiment >= 0.1:
                rating = "Positive"
            elif avg_sentiment >= -0.1:
                rating = "Neutral"
            elif avg_sentiment >= -0.3:
                rating = "Negative"
            else:
                rating = "Very Negative"

            return {
                'count': len(mentions),
                'avg_sentiment': round(avg_sentiment, 3),
                'positive': len([m for m in mentions if m['sentiment_label'] == 'positive']),
                'neutral': len([m for m in mentions if m['sentiment_label'] == 'neutral']),
                'negative': len([m for m in mentions if m['sentiment_label'] == 'negative']),
                'unique_authors': len(set([m['author'] for m in mentions])),
                'wsb': len([m for m in mentions if m['subreddit'].lower() == 'wallstreetbets']),
                'stocks': len([m for m in mentions if m['subreddit'].lower() == 'stocks']),
                'investing': len([m for m in mentions if m['subreddit'].lower() == 'investing']),
                'stockmarket': len([m for m in mentions if m['subreddit'].lower() == 'stockmarket']),
                'sentiment_rating': rating
            }

        return {
            '24h': calc_metrics(mentions_24h),
            '7d': calc_metrics(mentions_7d),
            '30d': calc_metrics(mentions_30d)
        }

    def calculate_trending_score(self, metrics_24h, metrics_7d):
        """Calculate 0-100 trending score based on volume and sentiment"""
        score = 0.0

        # Volume score (0-50 points)
        count_24h = metrics_24h['count']
        if count_24h >= 100:
            score += 50
        elif count_24h >= 50:
            score += 40
        elif count_24h >= 20:
            score += 30
        elif count_24h >= 10:
            score += 20
        elif count_24h >= 5:
            score += 10

        # Sentiment score (0-30 points)
        sentiment = metrics_24h['avg_sentiment']
        if abs(sentiment) >= 0.3:  # Strong sentiment (positive or negative)
            score += 30
        elif abs(sentiment) >= 0.1:
            score += 20
        else:
            score += 10

        # Velocity score (0-20 points) - comparing 24h to 7d average
        if metrics_7d['count'] > 0:
            avg_7d_daily = metrics_7d['count'] / 7.0
            if count_24h > avg_7d_daily * 3:  # 3x increase
                score += 20
            elif count_24h > avg_7d_daily * 2:
                score += 15
            elif count_24h > avg_7d_daily * 1.5:
                score += 10

        return min(100, int(round(score, 0)))

    def calculate_mention_velocity(self, mentions_24h):
        """Calculate mentions per hour (24h average)"""
        if not mentions_24h:
            return 0.0
        return round(len(mentions_24h) / 24.0, 2)

    def update_symbol(self, symbol):
        """Fetch and update Reddit sentiment for a single symbol"""
        print(f"\n{'='*60}")
        print(f"Analyzing Reddit Sentiment: {symbol}")
        print(f"{'='*60}\n")

        # Fetch mentions for different time periods
        mentions_24h = self.fetch_reddit_mentions(symbol, 24)
        time.sleep(1)  # Rate limiting between period fetches
        mentions_7d = self.fetch_reddit_mentions(symbol, 24 * 7)
        time.sleep(1)
        mentions_30d = self.fetch_reddit_mentions(symbol, 24 * 30)

        # Calculate aggregates
        metrics = self.calculate_aggregates(mentions_24h, mentions_7d, mentions_30d)

        # Calculate trending score
        trending_score = self.calculate_trending_score(metrics['24h'], metrics['7d'])

        # Calculate mention velocity
        mention_velocity = self.calculate_mention_velocity(mentions_24h)

        # Get company name from DividendModels if exists
        self.cursor.execute("SELECT CompanyName FROM DividendModels WHERE Symbol = ?", (symbol,))
        result = self.cursor.fetchone()
        company_name = result[0] if result else symbol

        # Check if sentiment record exists
        self.cursor.execute("SELECT Id FROM RedditSentiments WHERE Symbol = ?", (symbol,))
        existing = self.cursor.fetchone()

        now = datetime.utcnow().strftime('%Y-%m-%d %H:%M:%S')

        if existing:
            # Update existing record
            query = """
                UPDATE RedditSentiments SET
                    CompanyName = ?,
                    Sentiment24h = ?, Sentiment7d = ?, Sentiment30d = ?,
                    SentimentRating24h = ?, SentimentRating7d = ?, SentimentRating30d = ?,
                    MentionCount24h = ?, MentionCount7d = ?, MentionCount30d = ?,
                    UniqueAuthors24h = ?,
                    MentionVelocity = ?,
                    TrendingScore = ?,
                    WSBMentions24h = ?, StocksMentions24h = ?, InvestingMentions24h = ?, StockMarketMentions24h = ?,
                    PositiveMentions = ?, NeutralMentions = ?, NegativeMentions = ?,
                    PositiveRatio = ?,
                    LastUpdated = ?,
                    DataSource = ?
                WHERE Id = ?
            """
            positive_ratio = (metrics['24h']['positive'] / metrics['24h']['count'] * 100) if metrics['24h']['count'] > 0 else 0

            self.cursor.execute(query, (
                company_name,
                metrics['24h']['avg_sentiment'], metrics['7d']['avg_sentiment'], metrics['30d']['avg_sentiment'],
                metrics['24h']['sentiment_rating'], metrics['7d']['sentiment_rating'], metrics['30d']['sentiment_rating'],
                metrics['24h']['count'], metrics['7d']['count'], metrics['30d']['count'],
                metrics['24h']['unique_authors'],
                mention_velocity,
                trending_score,
                metrics['24h']['wsb'], metrics['24h']['stocks'], metrics['24h']['investing'], metrics['24h']['stockmarket'],
                metrics['24h']['positive'], metrics['24h']['neutral'], metrics['24h']['negative'],
                round(positive_ratio, 2),
                now,
                "Reddit/PRAW",
                existing[0]
            ))
            sentiment_id = existing[0]
            print(f"✓ Updated sentiment record (ID: {sentiment_id})")
        else:
            # Insert new record
            query = """
                INSERT INTO RedditSentiments (
                    Symbol, CompanyName,
                    Sentiment24h, Sentiment7d, Sentiment30d,
                    SentimentRating24h, SentimentRating7d, SentimentRating30d,
                    MentionCount24h, MentionCount7d, MentionCount30d,
                    UniqueAuthors24h, MentionVelocity, TrendingScore,
                    WSBMentions24h, StocksMentions24h, InvestingMentions24h, StockMarketMentions24h,
                    PositiveMentions, NeutralMentions, NegativeMentions,
                    PositiveRatio, FetchedAt, LastUpdated, DataSource, TopKeywords24h
                ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            """
            positive_ratio = (metrics['24h']['positive'] / metrics['24h']['count'] * 100) if metrics['24h']['count'] > 0 else 0

            self.cursor.execute(query, (
                symbol, company_name,
                metrics['24h']['avg_sentiment'], metrics['7d']['avg_sentiment'], metrics['30d']['avg_sentiment'],
                metrics['24h']['sentiment_rating'], metrics['7d']['sentiment_rating'], metrics['30d']['sentiment_rating'],
                metrics['24h']['count'], metrics['7d']['count'], metrics['30d']['count'],
                metrics['24h']['unique_authors'], mention_velocity, trending_score,
                metrics['24h']['wsb'], metrics['24h']['stocks'], metrics['24h']['investing'], metrics['24h']['stockmarket'],
                metrics['24h']['positive'], metrics['24h']['neutral'], metrics['24h']['negative'],
                round(positive_ratio, 2), now, now, "Reddit/PRAW", ""
            ))
            sentiment_id = self.cursor.lastrowid
            print(f"✓ Created new sentiment record (ID: {sentiment_id})")

        # Clear old mentions and insert new ones (keep last 100, 7 days max)
        self.cursor.execute("DELETE FROM RedditMentions WHERE RedditSentimentModelId = ?", (sentiment_id,))

        # Insert up to 100 most recent mentions from 7d period
        mentions_to_save = sorted(mentions_7d, key=lambda x: x['created_at'], reverse=True)[:100]

        for mention in mentions_to_save:
            self.cursor.execute("""
                INSERT INTO RedditMentions (
                    RedditSentimentModelId, Symbol, PostId, CommentId, Subreddit,
                    Author, Title, Content, CreatedAt, Upvotes, DownVotes,
                    Score, SentimentScore, SentimentLabel
                ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            """, (
                sentiment_id, symbol,
                mention['post_id'], mention['comment_id'], mention['subreddit'],
                mention['author'], mention['title'], mention['content'],
                mention['created_at'].strftime('%Y-%m-%d %H:%M:%S'),
                mention['upvotes'], mention['downvotes'], mention['score'],
                mention['sentiment_score'], mention['sentiment_label']
            ))

        self.conn.commit()

        print(f"\n📊 Summary:")
        print(f"  24h: {metrics['24h']['count']} mentions | {metrics['24h']['avg_sentiment']:+.3f} sentiment | {metrics['24h']['sentiment_rating']}")
        print(f"  7d:  {metrics['7d']['count']} mentions | {metrics['7d']['avg_sentiment']:+.3f} sentiment | {metrics['7d']['sentiment_rating']}")
        print(f"  30d: {metrics['30d']['count']} mentions | {metrics['30d']['avg_sentiment']:+.3f} sentiment | {metrics['30d']['sentiment_rating']}")
        print(f"  Trending Score: {trending_score}/100")
        print(f"  Mention Velocity: {mention_velocity} per hour")
        print(f"  Subreddit Breakdown: WSB={metrics['24h']['wsb']}, Stocks={metrics['24h']['stocks']}, Investing={metrics['24h']['investing']}, StockMarket={metrics['24h']['stockmarket']}")
        print(f"{'='*60}\n")

        return True

    def close(self):
        if self.conn:
            self.conn.close()
            print("✓ Database connection closed")


def main():
    if len(sys.argv) < 4:
        print("Usage: python update_reddit_sentiment.py <symbol> <client_id> <client_secret>")
        print("Example: python update_reddit_sentiment.py AAPL your_client_id your_client_secret")
        sys.exit(1)

    symbol = sys.argv[1].upper()
    client_id = sys.argv[2]
    client_secret = sys.argv[3]
    user_agent = "FinanceAPI:RedditSentiment:v1.0 (by /u/YourUsername)"

    analyzer = RedditSentimentAnalyzer(DB_PATH, client_id, client_secret, user_agent)

    if not analyzer.connect_db():
        sys.exit(1)

    try:
        success = analyzer.update_symbol(symbol)
        sys.exit(0 if success else 1)
    except Exception as e:
        print(f"✗ Error: {e}")
        import traceback
        traceback.print_exc()
        sys.exit(1)
    finally:
        analyzer.close()


if __name__ == "__main__":
    main()
