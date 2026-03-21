#!/usr/bin/env python3
"""
Oil Sentiment & Signal Analyzer — WTI (CL=F) and Brent (BZ=F)
==============================================================
Fetches live price data + news from multiple sources, scores sentiment,
applies technicals (RSI, ATR, Bollinger Bands), and outputs a combined
directional signal with suggested order levels.

Usage:
    python oil_sentiment.py
    python oil_sentiment.py --json        # machine-readable output

DISCLAIMER: This is an analytical tool, not financial advice.
Signals are probabilistic, not guarantees. Always manage risk.
"""

import sys
import json
import argparse
import numpy as np
import yfinance as yf
import urllib.request
import xml.etree.ElementTree as ET
from datetime import datetime, timedelta
from concurrent.futures import ThreadPoolExecutor, as_completed

try:
    from vaderSentiment.vaderSentiment import SentimentIntensityAnalyzer
    _vader = SentimentIntensityAnalyzer()
    def vader_score(text): return _vader.polarity_scores(text)["compound"]
except ImportError:
    print("WARNING: vaderSentiment not installed. Run: pip install vaderSentiment", file=sys.stderr)
    def vader_score(text): return 0.0


# ── Symbols ────────────────────────────────────────────────────────────────────
INSTRUMENTS = {
    "WTI":   {"symbol": "CL=F",  "name": "West Texas Intermediate (WTI)",  "unit": "USD/bbl"},
    "Brent": {"symbol": "BZ=F",  "name": "Brent Crude",                     "unit": "USD/bbl"},
}

# ── News RSS feeds (oil / energy / geopolitical) ───────────────────────────────
RSS_FEEDS = [
    # Energy-specific
    ("https://feeds.reuters.com/reuters/businessNews",         "Reuters Business",     1.0),
    ("https://feeds.finance.yahoo.com/rss/2.0/headline?s=CL%3DF", "Yahoo Finance WTI",1.5),
    ("https://feeds.finance.yahoo.com/rss/2.0/headline?s=BZ%3DF", "Yahoo Finance Brent",1.5),
    ("https://rss.cbc.ca/lineup/business.xml",                "CBC Business",         0.7),
    ("https://globalnews.ca/money/feed/",                     "Global News Money",    0.7),
    # Geopolitical (high weight — biggest driver for oil)
    ("https://feeds.bbci.co.uk/news/world/rss.xml",           "BBC World",            1.2),
    ("https://rss.cbc.ca/lineup/world.xml",                   "CBC World",            1.0),
]

# High-impact oil keywords with directional bias weights
# Positive = bullish for oil price (supply cut / demand rise / risk-on)
# Negative = bearish for oil price (supply increase / demand fall / ceasefire)
KEYWORD_WEIGHTS = {
    # Bullish keywords
    "opec cut":          +0.8,
    "production cut":    +0.7,
    "supply cut":        +0.7,
    "supply disruption": +0.8,
    "pipeline attack":   +0.9,
    "sanctions":         +0.6,
    "iran":              +0.5,
    "russia":            +0.4,
    "conflict":          +0.5,
    "war":               +0.6,
    "attack":            +0.5,
    "missile":           +0.5,
    "drone":             +0.4,
    "houthi":            +0.7,
    "middle east":       +0.4,
    "ceasefire fail":    +0.4,
    "tanker":            +0.4,
    "strait of hormuz":  +0.9,
    "red sea":           +0.6,
    # Bearish keywords
    "ceasefire":         -0.6,
    "peace":             -0.5,
    "deal":              -0.3,
    "surplus":           -0.7,
    "overproduction":    -0.7,
    "opec increase":     -0.8,
    "production boost":  -0.6,
    "recession":         -0.6,
    "demand fall":       -0.7,
    "demand weak":       -0.6,
    "china slowdown":    -0.5,
    "rate hike":         -0.3,
    "dollar strength":   -0.4,
    "inventory build":   -0.5,
    "eia build":         -0.5,
    "drawdown":          +0.4,  # inventory drawdown = bullish
}


# ── Helpers ────────────────────────────────────────────────────────────────────

def log(msg):
    print(f"  {msg}", file=sys.stderr)


def fetch_rss(url, source_name, timeout=8):
    try:
        req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"})
        with urllib.request.urlopen(req, timeout=timeout) as resp:
            xml_bytes = resp.read()
        root = ET.fromstring(xml_bytes)
        titles = []
        for item in root.findall(".//item"):
            t = item.findtext("title")
            d = item.findtext("description") or ""
            if t:
                titles.append((t.strip(), d.strip()[:200]))
        return source_name, titles
    except Exception as e:
        log(f"RSS failed [{source_name}]: {e}")
        return source_name, []


def keyword_score(text):
    """Scan text for high-impact oil keywords and return weighted directional score."""
    tl = text.lower()
    score = 0.0
    matched = []
    for kw, weight in KEYWORD_WEIGHTS.items():
        if kw in tl:
            score += weight
            matched.append(kw)
    return round(max(-1.0, min(1.0, score)), 3), matched


def fetch_all_news():
    """Fetch all RSS feeds in parallel, return list of (source, weight, title, desc)."""
    feed_map = {(url, name): weight for url, name, weight in RSS_FEEDS}
    all_articles = []

    with ThreadPoolExecutor(max_workers=len(RSS_FEEDS)) as executor:
        futures = {executor.submit(fetch_rss, url, name): (url, name)
                   for url, name, _ in RSS_FEEDS}
        for future in as_completed(futures):
            key = futures[future]
            weight = feed_map[key]
            source_name, articles = future.result()
            for title, desc in articles:
                all_articles.append((source_name, weight, title, desc))

    log(f"Fetched {len(all_articles)} headlines from {len(RSS_FEEDS)} feeds")
    return all_articles


def score_news(articles, cutoff_hours=48):
    """
    Score all headlines. Returns:
      - weighted_sentiment: -1 to +1 (VADER + keyword combined)
      - direction: "Bullish" / "Bearish" / "Neutral"
      - top_headlines: list of (score, keyword_matches, title) sorted by impact
    """
    if not articles:
        return 0.0, "Neutral", []

    scored = []
    for source, feed_weight, title, desc in articles:
        full_text  = f"{title} {desc}"
        v_score    = vader_score(full_text)
        kw_score, matched = keyword_score(full_text)

        # Blend: VADER sentiment + keyword directional score
        # Keyword score carries more weight for oil (geopolitics > tone)
        combined = round(v_score * 0.35 + kw_score * 0.65, 3)
        weighted = round(combined * feed_weight, 3)

        scored.append({
            "source":   source,
            "title":    title[:100],
            "vader":    round(v_score, 3),
            "keyword":  kw_score,
            "matches":  matched,
            "combined": combined,
            "weighted": weighted,
        })

    if not scored:
        return 0.0, "Neutral", []

    avg_weighted = sum(s["weighted"] for s in scored) / len(scored)
    avg_weighted = round(avg_weighted, 3)

    # Sort by absolute impact for top headlines
    top = sorted(scored, key=lambda x: abs(x["combined"]), reverse=True)[:8]

    if avg_weighted > 0.08:
        direction = "Bullish"   # news favours price rise
    elif avg_weighted < -0.08:
        direction = "Bearish"   # news favours price fall
    else:
        direction = "Neutral"

    return avg_weighted, direction, top


# ── Technical indicators ───────────────────────────────────────────────────────

def calculate_rsi(closes, period=14):
    closes = np.array(closes, dtype=float)
    if len(closes) < period + 1:
        return None
    deltas    = np.diff(closes)
    gains     = np.where(deltas > 0, deltas, 0.0)
    losses    = np.where(deltas < 0, -deltas, 0.0)
    avg_gain  = np.mean(gains[:period])
    avg_loss  = np.mean(losses[:period])
    if avg_loss == 0:
        return 100.0
    for i in range(period, len(deltas)):
        avg_gain = (avg_gain * (period - 1) + gains[i]) / period
        avg_loss = (avg_loss * (period - 1) + losses[i]) / period
    return round(100 - (100 / (1 + avg_gain / avg_loss)), 1) if avg_loss > 0 else 100.0


def calculate_atr(highs, lows, closes, period=14):
    h, l, c = np.array(highs, float), np.array(lows, float), np.array(closes, float)
    if len(c) < period + 1:
        return None
    trs = [max(h[i] - l[i], abs(h[i] - c[i-1]), abs(l[i] - c[i-1]))
           for i in range(1, len(c))]
    return round(float(np.mean(trs[-period:])), 3) if len(trs) >= period else None


def calculate_bb(closes, period=20):
    if len(closes) < period:
        return None, None, None
    rc      = np.array(closes[-period:], float)
    mid     = np.mean(rc)
    std     = np.std(rc)
    return round(mid - 2*std, 2), round(mid, 2), round(mid + 2*std, 2)


def get_technicals(symbol, name):
    """Download recent OHLCV and compute indicators."""
    log(f"Fetching price data for {name} ({symbol})...")
    try:
        ticker = yf.Ticker(symbol)
        df = ticker.history(period="3mo", interval="1d", auto_adjust=True)
        if df.empty or len(df) < 20:
            return None
        df = df.dropna(subset=["Close"])

        closes  = df["Close"].tolist()
        highs   = df["High"].tolist()
        lows    = df["Low"].tolist()
        volumes = df["Volume"].tolist()

        price       = round(closes[-1], 2)
        prev_close  = round(closes[-2], 2) if len(closes) >= 2 else price
        day_chg_pct = round((price - prev_close) / prev_close * 100, 2)

        week_chg_pct = round((price - closes[-6]) / closes[-6] * 100, 2) if len(closes) >= 6 else 0
        month_chg_pct= round((price - closes[-22]) / closes[-22] * 100, 2) if len(closes) >= 22 else 0

        rsi         = calculate_rsi(closes)
        atr         = calculate_atr(highs, lows, closes)
        bb_low, bb_mid, bb_high = calculate_bb(closes)

        sma20 = round(float(np.mean(closes[-20:])), 2)
        sma50 = round(float(np.mean(closes[-50:])), 2) if len(closes) >= 50 else None

        above_sma20 = price > sma20
        above_sma50 = price > sma50 if sma50 else None

        # Trend
        if above_sma20 and (above_sma50 is True or above_sma50 is None):
            trend = "Uptrend"
        elif not above_sma20 and (above_sma50 is False or above_sma50 is None):
            trend = "Downtrend"
        else:
            trend = "Mixed"

        # Bollinger position
        bb_range = (bb_high - bb_low) if bb_high and bb_low else 1
        bb_pos_pct = round((price - bb_low) / bb_range * 100, 1) if bb_range > 0 else 50

        # 52-week range
        high_52w = max(closes[-252:]) if len(closes) >= 252 else max(closes)
        low_52w  = min(closes[-252:]) if len(closes) >= 252 else min(closes)
        pct_52w  = round((price - low_52w) / (high_52w - low_52w) * 100, 1) if high_52w > low_52w else 50

        # Volume
        avg_vol = float(np.mean(volumes[-20:])) if volumes else 0
        recent_vol = float(np.mean(volumes[-5:])) if len(volumes) >= 5 else avg_vol
        vol_ratio = round(recent_vol / avg_vol, 2) if avg_vol > 0 else 1

        forecast = forecast_price(closes, highs, lows, atr or price * 0.015, rsi or 50)

        return {
            "symbol":         symbol,
            "price":          price,
            "prev_close":     prev_close,
            "day_chg_pct":    day_chg_pct,
            "week_chg_pct":   week_chg_pct,
            "month_chg_pct":  month_chg_pct,
            "rsi":            rsi,
            "atr":            atr,
            "atr_pct":        round(atr / price * 100, 2) if atr else None,
            "bb_low":         bb_low,
            "bb_mid":         bb_mid,
            "bb_high":        bb_high,
            "bb_pos_pct":     bb_pos_pct,
            "sma20":          sma20,
            "sma50":          sma50,
            "trend":          trend,
            "high_52w":       round(high_52w, 2),
            "low_52w":        round(low_52w, 2),
            "pct_52w_range":  pct_52w,
            "vol_ratio":      vol_ratio,
            "forecast":       forecast,
        }
    except Exception as e:
        log(f"Failed to get data for {symbol}: {e}")
        return None


# ── Price forecast ─────────────────────────────────────────────────────────────

def forecast_price(closes, highs, lows, atr, rsi):
    """
    Probabilistic price range forecast using:
    1. ATR-based daily range (68% of days stay within 1 ATR)
    2. Historical volatility (annualised → daily std dev)
    3. RSI mean-reversion: scan past 2 years for similar RSI readings,
       compute actual return over next 1/3/5 days
    4. Bollinger mean-reversion target
    Returns dict with range forecasts and direction probability.
    """
    closes = np.array(closes, dtype=float)
    price  = closes[-1]

    # Daily returns
    rets   = np.diff(closes) / closes[:-1]
    vol_1d = float(np.std(rets[-60:]))   # 60-day realised vol per day

    # Historical RSI-conditional returns (what happened next when RSI was similar)
    rsi_series = _compute_rsi_series(closes)
    lookback_yrs = min(len(closes), 504)   # up to 2 years
    past_closes  = closes[-lookback_yrs:]
    past_rsi     = rsi_series[-lookback_yrs:]

    similar_next_1, similar_next_3, similar_next_5 = [], [], []
    rsi_band = 10  # match RSI within ±10 pts

    for i in range(len(past_closes) - 6):
        r = past_rsi[i]
        if np.isnan(r): continue
        if abs(r - rsi) <= rsi_band:
            base = past_closes[i]
            if base > 0:
                similar_next_1.append((past_closes[i+1] - base) / base * 100)
                similar_next_3.append((past_closes[i+3] - base) / base * 100)
                similar_next_5.append((past_closes[i+5] - base) / base * 100)

    def _stats(arr):
        if not arr:
            return {"mean_pct": 0, "low_pct": 0, "high_pct": 0, "prob_up": 50, "n": 0}
        a = np.array(arr)
        return {
            "mean_pct":  round(float(np.mean(a)), 2),
            "low_pct":   round(float(np.percentile(a, 20)), 2),   # 80% of outcomes above this
            "high_pct":  round(float(np.percentile(a, 80)), 2),   # 80% of outcomes below this
            "prob_up":   round(int(np.sum(a > 0) / len(a) * 100)),
            "n":         len(a),
        }

    hist_1d = _stats(similar_next_1)
    hist_3d = _stats(similar_next_3)
    hist_5d = _stats(similar_next_5)

    def _range(pct_low, pct_high, vol_days):
        """Combine historical percentile range with ATR range."""
        hist_low  = price * (1 + pct_low  / 100)
        hist_high = price * (1 + pct_high / 100)
        atr_low   = price - atr * vol_days * 0.8
        atr_high  = price + atr * vol_days * 0.8
        # Take the wider of the two ranges for safety
        return {
            "low":  round(min(hist_low,  atr_low),  2),
            "high": round(max(hist_high, atr_high), 2),
        }

    # Bollinger mean-reversion target (20-day SMA)
    sma20 = float(np.mean(closes[-20:]))
    bb_std = float(np.std(closes[-20:]))
    bb_upper = sma20 + 2 * bb_std

    # Mean-reversion target: where RSI tends to normalise (RSI 50 ~ SMA20)
    mean_rev_target = round(sma20, 2)
    mean_rev_pct    = round((sma20 - price) / price * 100, 2)

    # Overall direction probability (blend hist + RSI)
    base_prob_up = hist_1d.get("prob_up", 50)
    if rsi > 75:
        base_prob_up = max(base_prob_up - 20, 10)   # strongly overbought → bias down
    elif rsi < 30:
        base_prob_up = min(base_prob_up + 20, 90)   # oversold → bias up

    return {
        "current_price":       round(float(price), 2),
        "prob_up_pct":         base_prob_up,
        "prob_down_pct":       100 - base_prob_up,
        "mean_reversion_target": mean_rev_target,
        "mean_reversion_chg_pct": mean_rev_pct,
        "forecast_1d": {**_range(hist_1d["low_pct"], hist_1d["high_pct"], 1), **{"hist_mean_pct": hist_1d["mean_pct"], "prob_up": hist_1d["prob_up"], "samples": hist_1d["n"]}},
        "forecast_3d": {**_range(hist_3d["low_pct"], hist_3d["high_pct"], 2), **{"hist_mean_pct": hist_3d["mean_pct"], "prob_up": hist_3d["prob_up"], "samples": hist_3d["n"]}},
        "forecast_5d": {**_range(hist_5d["low_pct"], hist_5d["high_pct"], 3), **{"hist_mean_pct": hist_5d["mean_pct"], "prob_up": hist_5d["prob_up"], "samples": hist_5d["n"]}},
        "bb_upper":  round(bb_upper, 2),
        "bb_mean":   round(sma20, 2),
        "note": f"Historical analysis based on {hist_1d['n']} past instances where RSI was {rsi:.0f}±10",
    }


def _compute_rsi_series(closes, period=14):
    closes = np.array(closes, dtype=float)
    out    = np.full(len(closes), np.nan)
    if len(closes) < period + 1:
        return out
    deltas = np.diff(closes)
    gains  = np.where(deltas > 0, deltas, 0.0)
    losses = np.where(deltas < 0, -deltas, 0.0)
    ag = np.mean(gains[:period])
    al = np.mean(losses[:period])
    for i in range(period, len(deltas)):
        ag = (ag * (period - 1) + gains[i]) / period
        al = (al * (period - 1) + losses[i]) / period
        out[i + 1] = 100.0 if al == 0 else round(100 - 100 / (1 + ag / al), 2)
    return out


# ── Signal engine ──────────────────────────────────────────────────────────────

def generate_signal(tech, news_score, news_direction):
    """
    Combine technicals + news sentiment into a composite signal.
    Returns signal dict with direction, confidence, and order suggestions.
    """
    if tech is None:
        return None

    price = tech["price"]
    atr   = tech["atr"] or (price * 0.015)  # fallback 1.5%
    rsi   = tech["rsi"] or 50
    score = 0  # composite: positive = bullish, negative = bearish

    reasons = []

    # 1. Trend (+/- 20)
    if tech["trend"] == "Uptrend":
        score += 20;  reasons.append("Uptrend (above SMA20+SMA50)")
    elif tech["trend"] == "Downtrend":
        score -= 20;  reasons.append("Downtrend (below SMA20+SMA50)")

    # 2. RSI (+/- 15)
    if rsi < 35:
        score += 15;  reasons.append(f"RSI oversold ({rsi})")
    elif rsi > 65:
        score -= 15;  reasons.append(f"RSI overbought ({rsi})")
    elif rsi < 45:
        score += 8;   reasons.append(f"RSI mildly oversold ({rsi})")
    elif rsi > 55:
        score -= 8;   reasons.append(f"RSI mildly overbought ({rsi})")

    # 3. Bollinger Band position (+/- 15)
    bb_pos = tech["bb_pos_pct"]
    if bb_pos < 20:
        score += 15;  reasons.append(f"Near lower Bollinger Band ({bb_pos:.0f}%)")
    elif bb_pos > 80:
        score -= 15;  reasons.append(f"Near upper Bollinger Band ({bb_pos:.0f}%)")

    # 4. Recent momentum (+/- 10)
    wk = tech["week_chg_pct"]
    if wk > 5:
        score -= 10;  reasons.append(f"Strong weekly gain +{wk}% (extended, pullback risk)")
    elif wk > 2:
        score -= 5;   reasons.append(f"Moderate weekly gain +{wk}%")
    elif wk < -5:
        score += 10;  reasons.append(f"Strong weekly drop {wk}% (oversold bounce potential)")
    elif wk < -2:
        score += 5;   reasons.append(f"Moderate weekly drop {wk}%")

    # 5. News sentiment (+/- 25) — biggest single factor for oil
    news_pts = round(news_score * 25)
    score += news_pts
    reasons.append(f"News sentiment: {news_direction} ({news_score:+.3f}, {news_pts:+d} pts)")

    # 6. Volume confirmation (+/- 5)
    if tech["vol_ratio"] > 1.5:
        # High volume confirms the recent direction
        if wk > 0:
            score -= 5;  reasons.append(f"High volume on up-move (vol ratio {tech['vol_ratio']}x)")
        else:
            score += 5;  reasons.append(f"High volume on down-move (vol ratio {tech['vol_ratio']}x)")

    # ── Direction ──────────────────────────────────────────────────────────────
    if score >= 20:
        direction   = "BUY"
        confidence  = min(95, 50 + score)
    elif score <= -20:
        direction   = "SELL"
        confidence  = min(95, 50 + abs(score))
    else:
        direction   = "NEUTRAL / WAIT"
        confidence  = max(30, 50 - abs(score))

    # ── Order level suggestions ────────────────────────────────────────────────
    # Based on ATR-sized zones from current price
    entry_aggressive = round(price - 0.5 * atr, 2)   # small pullback entry
    entry_moderate   = round(price - 1.0 * atr, 2)   # 1 ATR below
    entry_patient    = round(price - 2.0 * atr, 2)   # 2 ATR below (deep dip)
    stop_loss_buy    = round(price - 3.0 * atr, 2)   # 3 ATR stop
    tp1_buy          = round(price + 2.0 * atr, 2)   # 2 ATR TP (R:R 1:2)
    tp2_buy          = round(price + 4.0 * atr, 2)   # 4 ATR TP (R:R 1:4)

    short_aggressive = round(price + 0.5 * atr, 2)
    short_moderate   = round(price + 1.0 * atr, 2)
    short_patient    = round(price + 2.0 * atr, 2)
    stop_loss_sell   = round(price + 3.0 * atr, 2)
    tp1_sell         = round(price - 2.0 * atr, 2)
    tp2_sell         = round(price - 4.0 * atr, 2)

    return {
        "direction":   direction,
        "score":       score,
        "confidence":  confidence,
        "reasons":     reasons,
        "atr":         atr,
        "orders": {
            "buy": {
                "aggressive":  entry_aggressive,
                "moderate":    entry_moderate,
                "patient":     entry_patient,
                "stop_loss":   stop_loss_buy,
                "tp1":         tp1_buy,
                "tp2":         tp2_buy,
                "rr_tp1":      "1:2",
                "rr_tp2":      "1:4",
            },
            "sell": {
                "aggressive":  short_aggressive,
                "moderate":    short_moderate,
                "patient":     short_patient,
                "stop_loss":   stop_loss_sell,
                "tp1":         tp1_sell,
                "tp2":         tp2_sell,
                "rr_tp1":      "1:2",
                "rr_tp2":      "1:4",
            },
        },
    }


# ── yfinance per-instrument news ───────────────────────────────────────────────

def fetch_yf_news(symbol, name):
    try:
        articles = yf.Ticker(symbol).news or []
        cutoff   = datetime.now().timestamp() - 48 * 3600
        recent   = [a for a in articles if a.get("providerPublishTime", 0) >= cutoff]
        return [(name, 2.0, a.get("title", ""), "") for a in recent]
    except Exception as e:
        log(f"yfinance news failed for {symbol}: {e}")
        return []


# ── Output ─────────────────────────────────────────────────────────────────────

def print_report(instrument, tech, signal, news_score, news_dir, top_headlines):
    name   = instrument["name"]
    unit   = instrument["unit"]
    p      = tech["price"]
    arrow  = "▲" if tech["day_chg_pct"] > 0 else "▼"
    div    = "=" * 72

    print(f"\n{div}")
    print(f"  {name} ({tech['symbol']})")
    print(div)
    print(f"  Price:     ${p:.2f} {unit}  {arrow} {tech['day_chg_pct']:+.2f}% today")
    print(f"  Week:      {tech['week_chg_pct']:+.2f}%  |  Month: {tech['month_chg_pct']:+.2f}%")
    print(f"  52w range: ${tech['low_52w']:.2f} – ${tech['high_52w']:.2f}  ({tech['pct_52w_range']:.0f}% of range)")
    print(f"  RSI:       {tech['rsi']}  |  ATR: ${tech['atr']:.2f} ({tech['atr_pct']:.1f}%)  |  Trend: {tech['trend']}")
    print(f"  Bollinger: ${tech['bb_low']:.2f} | ${tech['bb_mid']:.2f} | ${tech['bb_high']:.2f}  (pos: {tech['bb_pos_pct']:.0f}%)")
    print(f"  Volume:    {tech['vol_ratio']:.1f}x 20d avg")

    print(f"\n  ── Signal ──────────────────────────────────────────────────────")
    marker = {"BUY": "🟢", "SELL": "🔴", "NEUTRAL / WAIT": "🟡"}.get(signal["direction"], "  ")
    print(f"  {marker}  {signal['direction']}   |  Score: {signal['score']:+d}  |  Confidence: {signal['confidence']}%")
    for r in signal["reasons"]:
        print(f"     • {r}")

    o = signal["orders"]
    print(f"\n  ── Suggested Order Levels (based on ATR = ${signal['atr']:.2f}) ──────────")
    print(f"  BUY entries:   Aggressive=${o['buy']['aggressive']:.2f}  "
          f"Moderate=${o['buy']['moderate']:.2f}  Patient=${o['buy']['patient']:.2f}")
    print(f"  Buy SL / TP:   SL=${o['buy']['stop_loss']:.2f}  TP1=${o['buy']['tp1']:.2f} ({o['buy']['rr_tp1']})  "
          f"TP2=${o['buy']['tp2']:.2f} ({o['buy']['rr_tp2']})")
    print(f"  SELL entries:  Aggressive=${o['sell']['aggressive']:.2f}  "
          f"Moderate=${o['sell']['moderate']:.2f}  Patient=${o['sell']['patient']:.2f}")
    print(f"  Sell SL / TP:  SL=${o['sell']['stop_loss']:.2f}  TP1=${o['sell']['tp1']:.2f} ({o['sell']['rr_tp1']})  "
          f"TP2=${o['sell']['tp2']:.2f} ({o['sell']['rr_tp2']})")

    print(f"\n  ── Top Headlines Driving Signal ─────────────────────────────────")
    for h in top_headlines[:6]:
        bias = "▲" if h["combined"] > 0.05 else ("▼" if h["combined"] < -0.05 else "→")
        kws  = ", ".join(h["matches"][:3]) if h["matches"] else "general tone"
        print(f"  {bias} [{h['source'][:15]:<15}] {h['title'][:70]}")
        if h["matches"]:
            print(f"      Keywords: {kws}")

    print(f"\n  News sentiment: {news_dir} ({news_score:+.3f})")
    print(f"  Generated: {datetime.now().strftime('%Y-%m-%d %H:%M EST')}")
    print(f"\n  ⚠  This is a signal tool, not financial advice. Always use a stop-loss.")


# ── Main ───────────────────────────────────────────────────────────────────────

def main():
    parser = argparse.ArgumentParser(description="Oil price sentiment & signal analyzer")
    parser.add_argument("--json", action="store_true", help="Output raw JSON instead of report")
    args = parser.parse_args()

    print("Fetching news from RSS feeds...", file=sys.stderr)
    rss_articles = fetch_all_news()

    # Also pull yfinance news for each instrument
    yf_wti   = fetch_yf_news("CL=F", "Yahoo WTI")
    yf_brent = fetch_yf_news("BZ=F", "Yahoo Brent")
    all_articles = rss_articles + yf_wti + yf_brent

    print("Scoring news sentiment...", file=sys.stderr)
    news_score, news_dir, top_headlines = score_news(all_articles)

    print("Fetching technical data...", file=sys.stderr)
    with ThreadPoolExecutor(max_workers=2) as ex:
        futures = {
            ex.submit(get_technicals, info["symbol"], name): (name, info)
            for name, info in INSTRUMENTS.items()
        }
        tech_results = {}
        for f in as_completed(futures):
            name, info = futures[f]
            tech_results[name] = (info, f.result())

    output = {
        "generated_at":   datetime.now().isoformat(),
        "news_sentiment": news_score,
        "news_direction": news_dir,
        "instruments":    {},
    }

    for name, (info, tech) in tech_results.items():
        signal = generate_signal(tech, news_score, news_dir)
        output["instruments"][name] = {
            "info":    info,
            "tech":    tech,
            "signal":  signal,
        }
        if not args.json:
            print_report(info, tech, signal, news_score, news_dir, top_headlines)

    if not args.json:
        print("\n" + "=" * 72)
        print("  TOP HEADLINES SUMMARY")
        print("=" * 72)
        for h in top_headlines[:8]:
            bias = "BULL" if h["combined"] > 0.05 else ("BEAR" if h["combined"] < -0.05 else "NEUT")
            print(f"  [{bias}] {h['title'][:80]}")
        print()
    else:
        output["top_headlines"] = top_headlines
        print(json.dumps(output, indent=2))


if __name__ == "__main__":
    main()
