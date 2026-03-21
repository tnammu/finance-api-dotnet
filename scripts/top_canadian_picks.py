#!/usr/bin/env python3
"""
Top Canadian Stock/ETF Picks Analyzer
Calculates RSI, Swing, Seasonal, and News Sentiment signals for all TSX symbols.
Outputs JSON with top 10 per strategy to stdout.

CACHING (price_cache.db):
  - Full 5-year download: only when cache is missing or >7 days old
  - Incremental: fetches last 15 days for symbols with stale cache
  - Daily runs read mostly from SQLite — fast (~1 min vs ~10 min)

SENTIMENT:
  - Macro: CBC Business + Global News Money RSS feeds → sector scores
  - Per-stock: yfinance news headlines → VADER compound score
  - Boosts/penalizes swing_score by up to ±15 pts
"""

import sys
import json
import sqlite3
import os
import time
import numpy as np
import pandas as pd
import yfinance as yf
import urllib.request
import xml.etree.ElementTree as ET
from datetime import datetime, date, timedelta
from concurrent.futures import ThreadPoolExecutor, as_completed

try:
    from vaderSentiment.vaderSentiment import SentimentIntensityAnalyzer
    _vader = SentimentIntensityAnalyzer()
    def vader_score(text): return _vader.polarity_scores(text)["compound"]
except ImportError:
    log_warn = lambda m: print(m, file=sys.stderr)
    def vader_score(text): return 0.0


# --- Quality Filters ---
MIN_PRICE          = 5.0
MIN_AVG_VOLUME     = 5_000
MIN_SEASONAL_YEARS = 3
MIN_SEASONAL_WIN_RATE = 0.6
MIN_DATA_DAYS      = 200

# --- Cache Settings ---
CACHE_DB_NAME      = "price_cache.db"
FULL_REFRESH_DAYS  = 7    # Re-download 5y if cache is older than this
INCREMENTAL_DAYS   = 15   # Days to fetch for incremental update
BATCH_SIZE         = 150
BATCH_DELAY        = 3.0

# --- Sentiment: macro RSS feeds ---
MACRO_RSS_FEEDS = [
    "https://rss.cbc.ca/lineup/business.xml",
    "https://globalnews.ca/money/feed/",
    "https://feeds.finance.yahoo.com/rss/2.0/headline?s=^GSPTSE",
]

# Keywords that map headlines to sectors
SECTOR_KEYWORDS = {
    "energy":      ["oil", "gas", "opec", "crude", "pipeline", "energy", "lng", "petroleum"],
    "banks":       ["interest rate", "bank of canada", "rate hike", "rate cut", "inflation",
                    "boc", "federal reserve", "mortgage", "lending"],
    "gold":        ["gold", "silver", "safe haven", "precious metal", "bullion"],
    "real_estate": ["real estate", "housing", "reit", "mortgage", "construction"],
    "tech":        ["tech", "artificial intelligence", " ai ", "semiconductor", "nasdaq"],
    "trade":       ["tariff", "trade war", "usd", "loonie", "canada us", "sanctions",
                    "geopolit", "war", "conflict", "recession"],
}

# Short ticker prefixes that belong to each sector (for macro boost lookup)
SYMBOL_SECTORS = {
    "energy":      ["XEG", "ENB", "SU", "CNQ", "CVE", "TRP", "PPL", "ZEO", "HOU", "HOD",
                    "VET", "ARX", "BTE", "ERF", "POU", "WCP", "BIR", "KEL"],
    "banks":       ["XFN", "ZEB", "ZWB", "TD", "RY", "BMO", "BNS", "CM", "NA", "HMAX",
                    "EQB", "CWB", "LB"],
    "gold":        ["XGD", "ABX", "K", "WPM", "AEM", "HGU", "HZU", "EDV", "NGT", "OR"],
    "real_estate": ["XRE", "ZRE", "AP", "REI", "SRU", "HR", "BEI", "CAR", "CHP", "CRT"],
    "tech":        ["XIT", "SHOP", "CSU", "MDA", "BB", "LSPD", "DCBO", "REAL"],
    "trade":       [],  # broad market — affects all
}


def log(msg):
    print(msg, file=sys.stderr)


# ── Sentiment helpers ──────────────────────────────────────────────────────────

def _fetch_rss_titles(url):
    """Fetch an RSS feed and return list of headline strings."""
    try:
        req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"})
        with urllib.request.urlopen(req, timeout=10) as resp:
            xml_bytes = resp.read()
        root = ET.fromstring(xml_bytes)
        ns   = {"atom": "http://www.w3.org/2005/Atom"}
        titles = []
        # RSS 2.0
        for item in root.findall(".//item"):
            t = item.findtext("title")
            if t:
                titles.append(t.strip())
        # Atom feeds
        for entry in root.findall(".//atom:entry", ns):
            t = entry.findtext("atom:title", namespaces=ns)
            if t:
                titles.append(t.strip())
        return titles
    except Exception as e:
        log(f"RSS fetch failed ({url}): {e}")
        return []


def get_macro_sentiment():
    """
    Fetch Canadian/global financial RSS feeds, VADER-score headlines,
    and return per-sector sentiment scores (-1 to +1) plus an overall score.
    """
    all_titles = []
    for url in MACRO_RSS_FEEDS:
        titles = _fetch_rss_titles(url)
        all_titles.extend(titles)

    log(f"Macro news: {len(all_titles)} headlines from {len(MACRO_RSS_FEEDS)} feeds")

    if not all_titles:
        return {"overall": 0.0}

    sector_scores = {s: [] for s in SECTOR_KEYWORDS}
    general_scores = []

    for title in all_titles:
        tl = title.lower()
        score = vader_score(title)
        general_scores.append(score)

        for sector, keywords in SECTOR_KEYWORDS.items():
            if any(kw in tl for kw in keywords):
                sector_scores[sector].append(score)

    result = {"overall": round(sum(general_scores) / len(general_scores), 3)}
    for sector, scores in sector_scores.items():
        if scores:
            result[sector] = round(sum(scores) / len(scores), 3)

    log(f"Macro sentiment: {result}")
    return result


def _sector_for_symbol(short_sym):
    """Return sector name for a short ticker (no .TO), or None."""
    upper = short_sym.upper()
    for sector, tickers in SYMBOL_SECTORS.items():
        if upper in tickers:
            return sector
    return None


def _fetch_one_news(sym, cutoff):
    """Fetch and score news for a single symbol. Returns (sym, result_dict) or None."""
    try:
        articles = yf.Ticker(sym).news or []
        recent   = [a for a in articles if a.get("providerPublishTime", 0) >= cutoff]
        if not recent:
            return None
        scored = [(vader_score(a.get("title", "")), a) for a in recent]
        avg    = sum(s for s, _ in scored) / len(scored)
        _, best = max(scored, key=lambda x: abs(x[0]))
        return (sym, {"score": round(avg, 3), "headline": best.get("title", "")[:80]})
    except Exception as e:
        log(f"  News fetch failed {sym}: {e}")
        return None


def get_stock_news_scores(candidates):
    """
    Fetch yfinance news for a list of symbols (top candidates only) in parallel.
    Returns {symbol: {"score": float, "headline": str}}
    """
    cutoff    = datetime.now().timestamp() - 7 * 86400  # last 7 days
    news_data = {}
    with ThreadPoolExecutor(max_workers=10) as executor:
        futures = {executor.submit(_fetch_one_news, sym, cutoff): sym for sym in candidates}
        for future in as_completed(futures):
            result = future.result()
            if result:
                news_data[result[0]] = result[1]
    log(f"Stock news fetched for {len(news_data)}/{len(candidates)} symbols")
    return news_data


def combined_sentiment(sym, stock_score, macro):
    """Blend per-stock news score with relevant macro sector score."""
    short   = sym.replace(".TO", "").replace(".V", "")
    sector  = _sector_for_symbol(short)
    macro_s = macro.get(sector, macro.get("overall", 0.0)) if sector else macro.get("overall", 0.0)
    # 70% stock-specific, 30% macro
    blended = stock_score * 0.7 + macro_s * 0.3
    return round(max(-1.0, min(1.0, blended)), 3)


def sentiment_label(score):
    if score >  0.15: return "Bullish"
    if score < -0.15: return "Bearish"
    return "Neutral"


# ── Symbol sources ─────────────────────────────────────────────────────────────

def _fetch_tsx_endpoint(url_path):
    url = f"https://www.tsx.com/json/company-directory/search/{url_path}/%5E*"
    req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0", "Accept": "application/json"})
    try:
        with urllib.request.urlopen(req, timeout=30) as resp:
            data = json.loads(resp.read().decode("utf-8"))
        return [f"{c['symbol']}.TO" for c in data.get("results", []) if c.get("symbol")]
    except Exception as e:
        log(f"TSX endpoint '{url_path}' failed: {e}")
        return []


def get_tsx_symbols_from_api():
    equities = _fetch_tsx_endpoint("tsx")
    etfs     = _fetch_tsx_endpoint("tsx-etf")
    log(f"TSX API: {len(equities)} equities, {len(etfs)} ETFs")
    return equities + etfs


def get_db_symbols(db_path):
    conn = sqlite3.connect(db_path)
    try:
        cur = conn.execute("SELECT Symbol FROM DividendModels WHERE Symbol LIKE '%.TO'")
        return [r[0] for r in cur.fetchall()]
    finally:
        conn.close()


def get_tsx_symbols_cached(cache_path, max_age_hours=24):
    """Return TSX symbols from cache if fresh, else fetch from API and store."""
    conn = init_cache(cache_path)
    cutoff = (datetime.now() - timedelta(hours=max_age_hours)).isoformat()
    row = conn.execute(
        "SELECT Symbols, FetchedAt FROM SymbolListCache WHERE Source='tsx' AND FetchedAt > ?",
        (cutoff,)
    ).fetchone()
    if row:
        log(f"TSX symbols from cache (fetched {row[1][:16]})")
        conn.close()
        return json.loads(row[0])

    log("Fetching TSX symbols from API...")
    symbols = get_tsx_symbols_from_api()
    if symbols:
        conn.execute(
            "INSERT OR REPLACE INTO SymbolListCache (Source, Symbols, FetchedAt) VALUES (?,?,?)",
            ("tsx", json.dumps(symbols), datetime.now().isoformat())
        )
        conn.commit()
    conn.close()
    return symbols


def get_macro_sentiment_cached(cache_path, max_age_hours=1):
    """Return macro sentiment from cache if fresh, else fetch and store."""
    conn = init_cache(cache_path)
    cutoff = (datetime.now() - timedelta(hours=max_age_hours)).isoformat()
    row = conn.execute(
        "SELECT Sentiment, FetchedAt FROM MacroSentimentCache WHERE Id=1 AND FetchedAt > ?",
        (cutoff,)
    ).fetchone()
    if row:
        log(f"Macro sentiment from cache (fetched {row[1][:16]})")
        conn.close()
        return json.loads(row[0])

    log("Fetching macro news sentiment...")
    macro = get_macro_sentiment()
    conn.execute(
        "INSERT OR REPLACE INTO MacroSentimentCache (Id, Sentiment, FetchedAt) VALUES (1,?,?)",
        (json.dumps(macro), datetime.now().isoformat())
    )
    conn.commit()
    conn.close()
    return macro


def get_symbols(db_path, cache_path=None):
    tsx = get_tsx_symbols_cached(cache_path) if cache_path else get_tsx_symbols_from_api()
    db  = get_db_symbols(db_path)
    combined = list(dict.fromkeys(tsx + db))
    log(f"Universe: {len(tsx)} TSX API + {len(db)} DB = {len(combined)} unique symbols")
    return combined


# ── Price cache ────────────────────────────────────────────────────────────────

def init_cache(cache_path):
    conn = sqlite3.connect(cache_path)
    conn.execute("""
        CREATE TABLE IF NOT EXISTS PriceCache (
            Symbol TEXT NOT NULL,
            Date   TEXT NOT NULL,
            Open   REAL, High REAL, Low REAL, Close REAL, Volume INTEGER,
            PRIMARY KEY (Symbol, Date)
        )
    """)
    conn.execute("""
        CREATE TABLE IF NOT EXISTS PriceCacheMeta (
            Symbol        TEXT PRIMARY KEY,
            LastFullFetch TEXT,
            LastDate      TEXT
        )
    """)
    conn.execute("CREATE INDEX IF NOT EXISTS idx_pc_sym ON PriceCache(Symbol)")
    conn.execute("""
        CREATE TABLE IF NOT EXISTS SymbolListCache (
            Source    TEXT PRIMARY KEY,
            Symbols   TEXT NOT NULL,
            FetchedAt TEXT NOT NULL
        )
    """)
    conn.execute("""
        CREATE TABLE IF NOT EXISTS MacroSentimentCache (
            Id        INTEGER PRIMARY KEY CHECK (Id = 1),
            Sentiment TEXT NOT NULL,
            FetchedAt TEXT NOT NULL
        )
    """)
    conn.commit()
    return conn


def _get_meta(conn, symbols):
    ph  = ",".join("?" * len(symbols))
    cur = conn.execute(
        f"SELECT Symbol, LastFullFetch, LastDate FROM PriceCacheMeta WHERE Symbol IN ({ph})",
        symbols
    )
    return {r[0]: (r[1], r[2]) for r in cur.fetchall()}


def _save_df(conn, symbol, df, full_fetch=False):
    if df is None or df.empty:
        return
    rows = []
    for dt, row in df.iterrows():
        d = str(dt.date()) if hasattr(dt, "date") else str(dt)[:10]
        rows.append((symbol, d,
                     float(row.get("Open",   0) or 0),
                     float(row.get("High",   0) or 0),
                     float(row.get("Low",    0) or 0),
                     float(row.get("Close",  0) or 0),
                     int(  row.get("Volume", 0) or 0)))
    conn.executemany(
        "INSERT OR REPLACE INTO PriceCache (Symbol,Date,Open,High,Low,Close,Volume) VALUES (?,?,?,?,?,?,?)",
        rows
    )
    last_date = max(r[1] for r in rows)
    today_str = date.today().isoformat()
    full_str  = today_str if full_fetch else None
    conn.execute("""
        INSERT INTO PriceCacheMeta (Symbol, LastFullFetch, LastDate) VALUES (?,?,?)
        ON CONFLICT(Symbol) DO UPDATE SET
            LastFullFetch = COALESCE(?, LastFullFetch),
            LastDate      = MAX(LastDate, ?)
    """, (symbol, full_str, last_date, full_str, last_date))
    conn.commit()


def _load_df(conn, symbol):
    cur = conn.execute(
        "SELECT Date,Open,High,Low,Close,Volume FROM PriceCache WHERE Symbol=? ORDER BY Date",
        (symbol,)
    )
    rows = cur.fetchall()
    if not rows:
        return None
    df = pd.DataFrame(rows, columns=["Date", "Open", "High", "Low", "Close", "Volume"])
    df["Date"] = pd.to_datetime(df["Date"])
    df.set_index("Date", inplace=True)
    return df


# ── Download helpers ───────────────────────────────────────────────────────────

def _yf_batch(symbols, period=None, start=None):
    kwargs = dict(interval="1d", auto_adjust=True, progress=False, threads=False)
    if start:
        kwargs["start"] = start
    else:
        kwargs["period"] = period or "5y"

    all_data = {}
    if len(symbols) == 1:
        raw = yf.download(symbols[0], **kwargs)
        if not raw.empty:
            all_data[symbols[0]] = raw
    else:
        raw = yf.download(symbols, group_by="ticker", **kwargs)
        if raw is not None and not raw.empty:
            for sym in symbols:
                try:
                    sd = raw[sym].dropna(how="all")
                    if not sd.empty:
                        all_data[sym] = sd
                except (KeyError, TypeError):
                    pass
    return all_data


def _batch_download(symbols, period=None, start=None, label=""):
    all_data = {}
    batches  = [symbols[i:i + BATCH_SIZE] for i in range(0, len(symbols), BATCH_SIZE)]
    log(f"Downloading {len(symbols)} {label}symbols in {len(batches)} batch(es)...")
    for i, batch in enumerate(batches, 1):
        log(f"  Batch {i}/{len(batches)}: {len(batch)} symbols")
        try:
            all_data.update(_yf_batch(batch, period=period, start=start))
        except Exception as e:
            log(f"  Batch {i} error: {e}")
        if i < len(batches):
            time.sleep(BATCH_DELAY)
    log(f"  Got data for {len(all_data)}/{len(symbols)}")
    return all_data


# ── Smart fetch with cache ─────────────────────────────────────────────────────

def get_price_data(symbols, cache_path):
    """
    Returns {symbol: DataFrame} using cached data where possible.
      Full 5y  — cache missing or last full fetch > FULL_REFRESH_DAYS ago
      Incremental — last cached date is stale by >2 trading days
      Cache hit — load straight from SQLite, no network call
    """
    conn         = init_cache(cache_path)
    today        = date.today()
    full_cutoff  = (today - timedelta(days=FULL_REFRESH_DAYS)).isoformat()
    stale_cutoff = (today - timedelta(days=2)).isoformat()

    meta        = _get_meta(conn, symbols)
    needs_full  = []
    needs_incr  = []

    for sym in symbols:
        m = meta.get(sym)
        if m is None or m[0] is None or m[0] < full_cutoff:
            needs_full.append(sym)
        elif m[1] is None or m[1] < stale_cutoff:
            needs_incr.append(sym)

    from_cache = len(symbols) - len(needs_full) - len(needs_incr)
    log(f"Cache: {len(needs_full)} full download, {len(needs_incr)} incremental, {from_cache} cached")

    if needs_full:
        data = _batch_download(needs_full, period="5y", label="(full 5y) ")
        for sym, df in data.items():
            _save_df(conn, sym, df, full_fetch=True)

    if needs_incr:
        start_str = (today - timedelta(days=INCREMENTAL_DAYS)).isoformat()
        data = _batch_download(needs_incr, start=start_str, label="(incremental) ")
        for sym, df in data.items():
            _save_df(conn, sym, df, full_fetch=False)

    result = {}
    for sym in symbols:
        df = _load_df(conn, sym)
        if df is not None and len(df) >= MIN_DATA_DAYS:
            result[sym] = df

    conn.close()
    log(f"Price data ready for {len(result)} symbols")
    return result


# ── Technical indicators ───────────────────────────────────────────────────────

def calculate_rsi_series(closes, period=14):
    """Returns RSI for every bar (NaN for first period bars)."""
    closes = np.array(closes, dtype=float)
    out = np.full(len(closes), np.nan)
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
        out[i + 1] = 100.0 if al == 0 else round(100 - (100 / (1 + ag / al)), 2)
    return out


def detect_rsi_divergence(closes, rsi_series, lookback=40, min_gap=5):
    """
    Bullish divergence: price makes lower low, RSI makes higher low.
    Returns (bool, price_low1, price_low2, rsi_low1, rsi_low2).
    """
    c = np.array(closes[-lookback:], dtype=float)
    r = rsi_series[-lookback:]
    n = len(c)
    if n < 10:
        return False, None, None, None, None
    pivots = [i for i in range(2, n - 2)
              if c[i] <= c[i-1] and c[i] <= c[i-2]
              and c[i] <= c[i+1] and c[i] <= c[i+2]
              and not np.isnan(r[i])]
    if len(pivots) < 2:
        return False, None, None, None, None
    p1, p2 = pivots[-2], pivots[-1]
    if p2 - p1 < min_gap:
        return False, None, None, None, None
    has_div = bool(c[p2] < c[p1] and r[p2] > r[p1])
    return has_div, round(float(c[p1]), 2), round(float(c[p2]), 2), round(float(r[p1]), 2), round(float(r[p2]), 2)


def calculate_entry_exit(closes, highs, lows, atr, lookback=15):
    """
    Entry zone [L, H], entry mid E, SL, TP1, TP2, R:R.
    L = swing low (lowest intraday low in lookback), H = recent signal bar high.
    E = lower 40pct of zone. SL = E-2xATR, TP1 = E+2xATR, TP2 = E+4xATR.
    """
    if atr is None or atr <= 0:
        return None
    L = float(min(lows[-lookback:]))
    H = float(max(highs[-5:]))
    if H <= L:
        H = L * 1.02
    E = round(L + 0.4 * (H - L), 2)
    sl  = round(E - 2 * atr, 2)
    tp1 = round(E + 2 * atr, 2)
    tp2 = round(E + 4 * atr, 2)
    risk = E - sl
    rr   = round((tp2 - E) / risk, 2) if risk > 0 else 0  # R:R to TP2 (4xATR target)
    return {"entry_low": round(L, 2), "entry_high": round(H, 2), "entry_mid": E,
            "stop_loss": sl, "tp1": tp1, "tp2": tp2, "rr_ratio": rr}


def calculate_rsi(closes, period=14):
    closes = np.array(closes, dtype=float)
    if len(closes) < period + 1:
        return None
    deltas   = np.diff(closes)
    gains    = np.where(deltas > 0, deltas, 0.0)
    losses   = np.where(deltas < 0, -deltas, 0.0)
    avg_gain = np.mean(gains[:period])
    avg_loss = np.mean(losses[:period])
    if avg_loss == 0:
        return 100.0
    for i in range(period, len(deltas)):
        avg_gain = (avg_gain * (period - 1) + gains[i]) / period
        avg_loss = (avg_loss * (period - 1) + losses[i]) / period
    if avg_loss == 0:
        return 100.0
    return round(100 - (100 / (1 + avg_gain / avg_loss)), 2)


def calculate_atr(highs, lows, closes, period=14):
    highs  = np.array(highs,  dtype=float)
    lows   = np.array(lows,   dtype=float)
    closes = np.array(closes, dtype=float)
    if len(closes) < period + 1:
        return None
    trs = [max(highs[i] - lows[i],
               abs(highs[i]  - closes[i - 1]),
               abs(lows[i]   - closes[i - 1]))
           for i in range(1, len(closes))]
    return float(np.mean(trs[-period:])) if len(trs) >= period else None


def calculate_sma(closes, period):
    if len(closes) < period:
        return None
    return float(np.mean(closes[-period:]))


def rsi_signal(rsi):
    if rsi is None: return "N/A"
    if rsi < 30:    return "Strongly Oversold - Strong Buy"
    if rsi < 40:    return "Oversold - Buy"
    if rsi > 70:    return "Overbought - Sell"
    return "Neutral"


# ── Analysis ───────────────────────────────────────────────────────────────────

def analyze_stocks(all_data, news_scores=None, macro=None):
    """
    Compute indicators from {symbol: DataFrame}.
    news_scores: {symbol: {"score": float, "headline": str}} from get_stock_news_scores()
    macro: macro_sentiment dict from get_macro_sentiment()
    Returns dict of results keyed by symbol.
    """
    if news_scores is None: news_scores = {}
    if macro is None:       macro = {}
    results = {}
    skipped_price = skipped_volume = skipped_data = 0
    today              = date.today()
    current_month      = today.month
    current_month_name = today.strftime("%B")

    for symbol, df in all_data.items():
        try:
            df = df.dropna(subset=["Close"])
            if len(df) < MIN_DATA_DAYS:
                skipped_data += 1
                continue

            closes  = df["Close"].tolist()
            highs   = df["High"].tolist()
            lows    = df["Low"].tolist()
            volumes = df["Volume"].tolist()
            current_price = closes[-1]

            if current_price < MIN_PRICE:
                skipped_price += 1
                continue

            avg_volume_20d = float(np.mean(volumes[-20:])) if len(volumes) >= 20 else 0
            if avg_volume_20d < MIN_AVG_VOLUME:
                skipped_volume += 1
                continue

            rsi        = calculate_rsi(closes)
            rsi_series = calculate_rsi_series(closes)
            div, pl1, pl2, rl1, rl2 = detect_rsi_divergence(closes, rsi_series)
            sma50  = calculate_sma(closes, 50)
            sma200 = calculate_sma(closes, 200)
            above_sma50  = current_price > sma50  if sma50  else None
            above_sma200 = current_price > sma200 if sma200 else None
            trend = ("Uptrend"   if above_sma50 and above_sma200 else
                     "Downtrend" if not above_sma50 and not above_sma200 else "Mixed")

            atr     = calculate_atr(highs, lows, closes)
            ee      = calculate_entry_exit(closes, highs, lows, atr) if atr else None
            atr_pct = (atr / current_price * 100) if atr and current_price else 0

            rc       = np.array(closes[-20:])
            bb_mid   = np.mean(rc);  bb_std = np.std(rc)
            bb_lower = bb_mid - 2 * bb_std;  bb_upper = bb_mid + 2 * bb_std
            bb_range = bb_upper - bb_lower
            bb_pos   = ((current_price - bb_lower) / bb_range * 100) if bb_range > 0 else 50

            avg_vol_20d   = float(np.mean(volumes[-20:])) if len(volumes) >= 20 else 1
            recent_vol_5d = float(np.mean(volumes[-5:]))  if len(volumes) >= 5  else avg_vol_20d
            vol_ratio     = recent_vol_5d / avg_vol_20d   if avg_vol_20d > 0    else 1

            ret_1m = ((closes[-1] - closes[-22]) / closes[-22] * 100) if len(closes) >= 22 else 0
            ret_3m = ((closes[-1] - closes[-66]) / closes[-66] * 100) if len(closes) >= 66 else 0

            high_52w = max(closes[-252:]) if len(closes) >= 252 else max(closes)
            low_52w  = min(closes[-252:]) if len(closes) >= 252 else min(closes)
            pct_high = ((current_price - high_52w) / high_52w * 100) if high_52w > 0 else 0
            pct_low  = ((current_price - low_52w)  / low_52w  * 100) if low_52w  > 0 else 0

            sw = 0
            if 2.0 <= atr_pct <= 5.0:                        sw += 25
            elif 1.5 <= atr_pct < 2.0 or 5.0 < atr_pct <= 7.0: sw += 15
            elif atr_pct > 7.0:                              sw += 5
            if bb_pos < 25:   sw += 20
            elif bb_pos < 40: sw += 10
            if vol_ratio > 1.5:   sw += 15
            elif vol_ratio > 1.2: sw += 10
            if rsi and 30 <= rsi <= 45:  sw += 20
            elif rsi and 25 <= rsi < 30: sw += 15
            elif rsi and 45 < rsi <= 55: sw += 10
            if 0 <= pct_low <= 20: sw += 10
            if above_sma200:       sw += 10
            swing_score = round(min(100, max(0, sw)), 1)
            # Divergence bonus: +15 pts if bullish RSI divergence detected
            if div:
                swing_score = round(min(100, swing_score + 15), 1)

            # Sentiment
            ns       = news_scores.get(symbol, {})
            raw_news = ns.get("score", None)
            headline = ns.get("headline", None)
            if raw_news is not None:
                sent         = combined_sentiment(symbol, raw_news, macro)
                sent_label   = sentiment_label(sent)
                sent_boost   = int(round(sent * 15))
                swing_score  = round(min(100, max(0, swing_score + sent_boost)), 1)
            else:
                sent       = None
                sent_label = None
                sent_boost = 0

            bb_label = ("Near Lower Band (Buy Zone)" if bb_pos < 25 else
                        "Near Upper Band (Sell Zone)" if bb_pos > 75 else "Mid Band")

            # Seasonal — current month + all-month averages for best sell month
            df_s = df.copy()
            df_s["month"] = df_s.index.month
            df_s["year"]  = df_s.index.year

            # Current month returns
            monthly_returns = []
            for yr in df_s["year"].unique():
                md = df_s[(df_s["month"] == current_month) & (df_s["year"] == yr)]
                if len(md) < 5:
                    continue
                sp, ep = md.iloc[0]["Close"], md.iloc[-1]["Close"]
                if sp > 0:
                    monthly_returns.append((ep - sp) / sp * 100)

            month_avg      = round(float(np.mean(monthly_returns)), 2) if monthly_returns else None
            seasonal_years = len(monthly_returns)
            win_rate       = round(sum(1 for r in monthly_returns if r > 0) / seasonal_years * 100, 0) if seasonal_years > 0 else 0

            # All-month averages → find best sell month (highest avg return in next 1–11 months)
            all_month_avgs = {}
            for m in range(1, 13):
                m_rets = []
                for yr in df_s["year"].unique():
                    md = df_s[(df_s["month"] == m) & (df_s["year"] == yr)]
                    if len(md) < 5:
                        continue
                    sp, ep = md.iloc[0]["Close"], md.iloc[-1]["Close"]
                    if sp > 0:
                        m_rets.append((ep - sp) / sp * 100)
                if m_rets:
                    all_month_avgs[m] = round(float(np.mean(m_rets)), 2)

            # Look at the next 11 months (not current) and pick the best-returning one
            future_months = [((current_month - 1 + offset) % 12) + 1 for offset in range(1, 12)]
            best_sell_m = max(future_months, key=lambda m: all_month_avgs.get(m, float('-inf')))
            best_sell_month_name   = date(2000, best_sell_m, 1).strftime("%B") if best_sell_m in all_month_avgs else None
            best_sell_month_return = all_month_avgs.get(best_sell_m)

            results[symbol] = {
                "symbol": symbol, "price": round(current_price, 2),
                "avg_volume": int(avg_volume_20d),
                "rsi": rsi, "rsi_signal": rsi_signal(rsi), "trend": trend,
                "sma50":  round(sma50,  2) if sma50  else None,
                "sma200": round(sma200, 2) if sma200 else None,
                "swing_score": swing_score,
                "atr_pct": round(atr_pct, 2), "bb_position": bb_label,
                "vol_ratio": round(vol_ratio, 2),
                "ret_1m": round(ret_1m, 2), "ret_3m": round(ret_3m, 2),
                "pct_from_52w_high": round(pct_high, 2),
                "pct_from_52w_low":  round(pct_low,  2),
                "month_avg_return": month_avg,
                "seasonal_years": seasonal_years, "seasonal_win_rate": win_rate,
                "seasonal_best":  round(max(monthly_returns), 2) if monthly_returns else None,
                "seasonal_worst": round(min(monthly_returns), 2) if monthly_returns else None,
                "month": current_month_name,
                "best_sell_month": best_sell_month_name,
                "best_sell_month_return": best_sell_month_return,
                "news_score":      sent,
                "sentiment_label": sent_label,
                "sentiment_boost": sent_boost,
                "top_headline":    headline,
                "has_divergence":  div,
                "entry_low":       ee["entry_low"]  if ee else None,
                "entry_high":      ee["entry_high"] if ee else None,
                "entry_mid":       ee["entry_mid"]  if ee else None,
                "stop_loss":       ee["stop_loss"]  if ee else None,
                "tp1":             ee["tp1"]         if ee else None,
                "tp2":             ee["tp2"]         if ee else None,
                "rr_ratio":        ee["rr_ratio"]   if ee else None,
            }

        except Exception as e:
            log(f"  Error {symbol}: {e}")

    log(f"Analyzed {len(results)} (skipped: {skipped_price} price, {skipped_volume} volume, {skipped_data} data)")
    return results


# ── Ranking ────────────────────────────────────────────────────────────────────

def get_top_rsi(results, n=10):
    # Quality RSI oversold: genuinely oversold (<35), not in downtrend,
    # some volume interest (>1.1x avg), and not too far from 52w low (near support)
    c = [(s, d) for s, d in results.items()
         if d.get("rsi") is not None and d["rsi"] < 35
         and d.get("trend") != "Downtrend"
         and d.get("vol_ratio", 0) >= 1.1
         and d.get("pct_from_52w_low", 100) <= 50]
    # Rank by composite: lowest RSI + bonus for volume surge + near support
    def rsi_quality(d):
        rsi_score   = d["rsi"]                                  # lower = better
        vol_bonus   = -d.get("vol_ratio", 1.0) * 2             # more volume = better
        support_pct = d.get("pct_from_52w_low", 50) * 0.3     # closer to low = better
        return rsi_score + vol_bonus + support_pct
    c.sort(key=lambda x: rsi_quality(x[1]))
    return [d for _, d in c[:n]]


def get_top_swing(results, n=10):
    # Quality swing: meaningful score (>=45), not in downtrend, RSI not overbought
    c = [(s, d) for s, d in results.items()
         if d["swing_score"] >= 45
         and d.get("trend") != "Downtrend"
         and d.get("rsi", 100) <= 58]
    c.sort(key=lambda x: x[1]["swing_score"], reverse=True)
    return [d for _, d in c[:n]]


def get_top_news(results, n=10):
    """Top picks ranked purely by positive news sentiment score."""
    c = [(s, d) for s, d in results.items()
         if d.get("news_score") is not None and d["news_score"] > 0.1
         and d.get("top_headline")]
    c.sort(key=lambda x: x[1]["news_score"], reverse=True)
    return [d for _, d in c[:n]]


def get_top_seasonal(results, n=10):
    c = [(s, d) for s, d in results.items()
         if d.get("month_avg_return") is not None and d["month_avg_return"] > 0
         and d.get("seasonal_years", 0)    >= MIN_SEASONAL_YEARS
         and d.get("seasonal_win_rate", 0) >= MIN_SEASONAL_WIN_RATE * 100
         and d.get("trend") != "Downtrend"]   # exclude falling knives
    c.sort(key=lambda x: x[1]["month_avg_return"] * (x[1]["seasonal_win_rate"] / 100), reverse=True)
    return [d for _, d in c[:n]]


# ── Entry point ────────────────────────────────────────────────────────────────

def main():
    script_dir   = os.path.dirname(os.path.abspath(__file__))
    project_root = os.path.dirname(script_dir)
    db_path      = os.path.join(project_root, "dividends.db")
    cache_path   = os.path.join(project_root, CACHE_DB_NAME)

    if not os.path.exists(db_path):
        print(json.dumps({"error": f"Database not found: {db_path}"}))
        sys.exit(1)

    symbols = get_symbols(db_path, cache_path)
    if not symbols:
        print(json.dumps({"error": "No Canadian stocks found"}))
        sys.exit(1)

    log(f"Universe: {len(symbols)} symbols | Cache: {cache_path}")
    log(f"Filters: price>=${MIN_PRICE}, volume>={MIN_AVG_VOLUME:,}, days>={MIN_DATA_DAYS}")

    # Step 1: Price data (cached)
    all_data = get_price_data(symbols, cache_path)

    # Step 2: Macro geo-political sentiment from RSS (cached 1h)
    macro = get_macro_sentiment_cached(cache_path)

    # Step 3: Per-stock news — only for top ~50 candidates to keep it fast
    #         We pick candidates by doing a quick pre-analysis pass first,
    #         then fetch news only for the most promising symbols.
    log("Pre-screening candidates for news fetch...")
    pre_results = analyze_stocks(all_data)  # no sentiment yet

    # Use BROAD thresholds for news candidate selection (not the strict final thresholds)
    # — ensures ~50 candidates get news fetched so get_top_news has enough to rank from
    broad_rsi    = sorted([(s, d) for s, d in pre_results.items()
                            if d.get("rsi", 100) < 45 and d.get("trend") != "Downtrend"],
                           key=lambda x: x[1]["rsi"])
    broad_swing  = sorted([(s, d) for s, d in pre_results.items()
                            if d.get("swing_score", 0) >= 25],
                           key=lambda x: x[1]["swing_score"], reverse=True)
    season_cands = [d["symbol"] for d in get_top_seasonal(pre_results, n=20)]
    rsi_cands    = [d["symbol"] for _, d in broad_rsi[:25]]
    swing_cands  = [d["symbol"] for _, d in broad_swing[:25]]
    news_cands   = list(dict.fromkeys(rsi_cands + swing_cands + season_cands))  # ~50-70 unique

    log(f"Fetching yfinance news for {len(news_cands)} candidates...")
    news_scores = get_stock_news_scores(news_cands)

    # Step 4: Full analysis with sentiment applied
    results = analyze_stocks(all_data, news_scores=news_scores, macro=macro)

    if not results:
        print(json.dumps({"error": "Could not analyze any stocks"}))
        sys.exit(1)

    # Macro summary for the message (sector label + arrow)
    macro_summary = {k: v for k, v in macro.items() if k != "overall"}

    print(json.dumps({
        "generated_at":      datetime.now().isoformat(),
        "total_analyzed":    len(results),
        "total_in_universe": len(symbols),
        "month":             date.today().strftime("%B"),
        "macro_sentiment":   macro_summary,
        "macro_overall":     macro.get("overall", 0.0),
        "filters": {
            "min_price":               MIN_PRICE,
            "min_volume":              MIN_AVG_VOLUME,
            "min_seasonal_years":      MIN_SEASONAL_YEARS,
            "min_win_rate":            f"{MIN_SEASONAL_WIN_RATE * 100:.0f}%",
            "cache_full_refresh_days": FULL_REFRESH_DAYS,
        },
        "rsi":      get_top_rsi(results),
        "swing":    get_top_swing(results),
        "seasonal": get_top_seasonal(results),
        "news":     get_top_news(results),
    }, indent=2))


if __name__ == "__main__":
    main()
