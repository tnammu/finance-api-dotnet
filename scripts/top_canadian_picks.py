#!/usr/bin/env python3
"""
Top 10 Canadian Stock Picks Analyzer (Advanced)
Calculates RSI, Swing Trading, and Seasonal signals for Canadian (.TO) stocks.
Applies quality filters: liquidity, volume, consistency, win rate.
Outputs JSON with top 10 per strategy to stdout.
"""

import sys
import json
import sqlite3
import os
import numpy as np
import yfinance as yf
from datetime import datetime, date


# --- Quality Filters ---
MIN_PRICE = 5.0            # No penny stocks under $5
MIN_AVG_VOLUME = 100000    # Minimum 100K avg daily volume (liquid enough to trade)
MIN_SEASONAL_YEARS = 3     # At least 3 years of seasonal data
MIN_SEASONAL_WIN_RATE = 0.6  # 60%+ of years must be positive for seasonal pick
MIN_DATA_DAYS = 200        # At least ~1 year of daily data


def log(msg):
    print(msg, file=sys.stderr)


def get_canadian_symbols(db_path):
    """Read all .TO symbols from dividends.db"""
    conn = sqlite3.connect(db_path)
    try:
        cursor = conn.execute(
            "SELECT Symbol FROM DividendModels WHERE Symbol LIKE '%.TO'"
        )
        symbols = [row[0] for row in cursor.fetchall()]
        return symbols
    finally:
        conn.close()


def calculate_rsi(closes, period=14):
    """Compute RSI for a price series."""
    closes = np.array(closes, dtype=float)
    if len(closes) < period + 1:
        return None
    deltas = np.diff(closes)
    gains = np.where(deltas > 0, deltas, 0.0)
    losses = np.where(deltas < 0, -deltas, 0.0)
    avg_gain = np.mean(gains[:period])
    avg_loss = np.mean(losses[:period])
    if avg_loss == 0:
        return 100.0
    for i in range(period, len(deltas)):
        avg_gain = (avg_gain * (period - 1) + gains[i]) / period
        avg_loss = (avg_loss * (period - 1) + losses[i]) / period
    if avg_loss == 0:
        return 100.0
    rs = avg_gain / avg_loss
    return round(100 - (100 / (1 + rs)), 2)


def calculate_atr(highs, lows, closes, period=14):
    """Average True Range"""
    highs = np.array(highs, dtype=float)
    lows = np.array(lows, dtype=float)
    closes = np.array(closes, dtype=float)
    if len(closes) < period + 1:
        return None
    trs = []
    for i in range(1, len(closes)):
        tr = max(highs[i] - lows[i],
                 abs(highs[i] - closes[i - 1]),
                 abs(lows[i] - closes[i - 1]))
        trs.append(tr)
    if len(trs) < period:
        return None
    return float(np.mean(trs[-period:]))


def calculate_sma(closes, period):
    """Simple Moving Average"""
    if len(closes) < period:
        return None
    return float(np.mean(closes[-period:]))


def rsi_signal(rsi):
    if rsi is None:
        return "N/A"
    if rsi < 30:
        return "Strongly Oversold - Strong Buy"
    if rsi < 40:
        return "Oversold - Buy"
    if rsi > 70:
        return "Overbought - Sell"
    return "Neutral"


BATCH_SIZE = 150          # Download this many symbols per batch
BATCH_DELAY = 3.0         # Seconds to wait between batches


def download_in_batches(symbols):
    """Download stock data in batches to avoid Yahoo Finance rate limiting."""
    all_data = {}
    total = len(symbols)
    batches = [symbols[i:i + BATCH_SIZE] for i in range(0, total, BATCH_SIZE)]

    log(f"Downloading {total} symbols in {len(batches)} batches of ~{BATCH_SIZE}...")

    import time

    for batch_num, batch in enumerate(batches, 1):
        log(f"  Batch {batch_num}/{len(batches)}: {len(batch)} symbols...")
        try:
            if len(batch) == 1:
                raw_batch = yf.download(
                    batch[0],
                    period="5y",
                    interval="1d",
                    auto_adjust=True,
                    progress=False,
                )
                if not raw_batch.empty:
                    all_data[batch[0]] = raw_batch
            else:
                raw_batch = yf.download(
                    batch,
                    period="5y",
                    interval="1d",
                    group_by="ticker",
                    auto_adjust=True,
                    progress=False,
                    threads=False,   # Sequential within batch to reduce load
                )
                if raw_batch is not None and not raw_batch.empty:
                    for sym in batch:
                        try:
                            sym_data = raw_batch[sym].dropna(how="all")
                            if not sym_data.empty:
                                all_data[sym] = sym_data
                        except (KeyError, TypeError):
                            pass
        except Exception as e:
            log(f"  Batch {batch_num} error: {e}")

        # Wait between batches (skip wait after last batch)
        if batch_num < len(batches):
            log(f"  Waiting {BATCH_DELAY}s before next batch...")
            time.sleep(BATCH_DELAY)

    log(f"Downloaded data for {len(all_data)}/{total} symbols")
    return all_data


def analyze_stocks(symbols):
    """
    Download data and compute indicators with quality filters.
    Returns dict keyed by symbol.
    """
    if not symbols:
        return {}

    log(f"Downloading data for {len(symbols)} Canadian stocks (batched)...")

    all_data = download_in_batches(symbols)

    if not all_data:
        log("No data downloaded")
        return {}

    results = {}
    skipped_price = 0
    skipped_volume = 0
    skipped_data = 0
    today = date.today()
    current_month = today.month
    current_month_name = today.strftime("%B")

    for symbol in symbols:
        try:
            if symbol not in all_data:
                continue
            df = all_data[symbol].copy()

            df = df.dropna(subset=["Close"])

            # --- FILTER: Minimum data ---
            if len(df) < MIN_DATA_DAYS:
                skipped_data += 1
                continue

            closes = df["Close"].tolist()
            highs = df["High"].tolist()
            lows = df["Low"].tolist()
            volumes = df["Volume"].tolist()
            current_price = closes[-1]

            # --- FILTER: Minimum price ---
            if current_price < MIN_PRICE:
                skipped_price += 1
                continue

            # --- FILTER: Minimum average volume (20-day) ---
            avg_volume_20d = float(np.mean(volumes[-20:])) if len(volumes) >= 20 else 0
            if avg_volume_20d < MIN_AVG_VOLUME:
                skipped_volume += 1
                continue

            # --- RSI ---
            rsi = calculate_rsi(closes)

            # --- Trend: SMA 50 and SMA 200 ---
            sma50 = calculate_sma(closes, 50)
            sma200 = calculate_sma(closes, 200)
            above_sma50 = current_price > sma50 if sma50 else None
            above_sma200 = current_price > sma200 if sma200 else None
            trend = "Uptrend" if above_sma50 and above_sma200 else \
                    "Downtrend" if not above_sma50 and not above_sma200 else "Mixed"

            # --- ATR ---
            atr = calculate_atr(highs, lows, closes)
            atr_pct = (atr / current_price * 100) if atr and current_price else 0

            # --- Bollinger Bands (20-day) ---
            recent_closes = np.array(closes[-20:])
            bb_mid = np.mean(recent_closes)
            bb_std = np.std(recent_closes)
            bb_lower = bb_mid - 2 * bb_std
            bb_upper = bb_mid + 2 * bb_std
            bb_range = bb_upper - bb_lower
            bb_position_pct = ((current_price - bb_lower) / bb_range * 100) if bb_range > 0 else 50

            # --- Volume analysis ---
            avg_vol_20d = float(np.mean(volumes[-20:])) if len(volumes) >= 20 else 1
            recent_vol_5d = float(np.mean(volumes[-5:])) if len(volumes) >= 5 else avg_vol_20d
            vol_ratio = recent_vol_5d / avg_vol_20d if avg_vol_20d > 0 else 1

            # --- 1-month and 3-month returns ---
            ret_1m = ((closes[-1] - closes[-22]) / closes[-22] * 100) if len(closes) >= 22 else 0
            ret_3m = ((closes[-1] - closes[-66]) / closes[-66] * 100) if len(closes) >= 66 else 0

            # --- 52-week high/low proximity ---
            high_52w = max(closes[-252:]) if len(closes) >= 252 else max(closes)
            low_52w = min(closes[-252:]) if len(closes) >= 252 else min(closes)
            pct_from_52w_high = ((current_price - high_52w) / high_52w * 100) if high_52w > 0 else 0
            pct_from_52w_low = ((current_price - low_52w) / low_52w * 100) if low_52w > 0 else 0

            # --- Swing Trading Score (improved) ---
            # Rewards: ATR volatility, near support (lower BB), volume interest,
            # price near 52w low (bounce potential), uptrend confirmation
            swing_score = 0
            # ATR volatility (2-5% is ideal for swing trading)
            if 2.0 <= atr_pct <= 5.0:
                swing_score += 25
            elif 1.5 <= atr_pct < 2.0 or 5.0 < atr_pct <= 7.0:
                swing_score += 15
            elif atr_pct > 7.0:
                swing_score += 5  # Too volatile

            # Near lower Bollinger Band (buy zone)
            if bb_position_pct < 25:
                swing_score += 20
            elif bb_position_pct < 40:
                swing_score += 10

            # Volume spike (institutional interest)
            if vol_ratio > 1.5:
                swing_score += 15
            elif vol_ratio > 1.2:
                swing_score += 10

            # RSI oversold but not dead (30-45 is sweet spot for swing entry)
            if rsi and 30 <= rsi <= 45:
                swing_score += 20
            elif rsi and 25 <= rsi < 30:
                swing_score += 15
            elif rsi and 45 < rsi <= 55:
                swing_score += 10

            # Price bouncing off 52-week low area (within 20% of 52w low)
            if 0 <= pct_from_52w_low <= 20:
                swing_score += 10

            # SMA 200 support (price near or above 200 SMA = safer)
            if above_sma200:
                swing_score += 10

            swing_score = round(min(100, max(0, swing_score)), 1)

            if bb_position_pct < 25:
                bb_label = "Near Lower Band (Buy Zone)"
            elif bb_position_pct > 75:
                bb_label = "Near Upper Band (Sell Zone)"
            else:
                bb_label = "Mid Band"

            # --- Seasonal Analysis (improved with win rate) ---
            df["month"] = df.index.month
            df["year"] = df.index.year

            monthly_returns = []
            for yr in df["year"].unique():
                month_data = df[(df["month"] == current_month) & (df["year"] == yr)]
                if len(month_data) < 5:  # Need at least 5 trading days
                    continue
                start_p = month_data.iloc[0]["Close"]
                end_p = month_data.iloc[-1]["Close"]
                if start_p > 0:
                    monthly_returns.append((end_p - start_p) / start_p * 100)

            month_avg = round(float(np.mean(monthly_returns)), 2) if monthly_returns else None
            seasonal_years = len(monthly_returns)
            win_count = sum(1 for r in monthly_returns if r > 0) if monthly_returns else 0
            win_rate = round(win_count / seasonal_years * 100, 0) if seasonal_years > 0 else 0
            best_return = round(max(monthly_returns), 2) if monthly_returns else None
            worst_return = round(min(monthly_returns), 2) if monthly_returns else None

            results[symbol] = {
                "symbol": symbol,
                "price": round(current_price, 2),
                "avg_volume": int(avg_volume_20d),
                "rsi": rsi,
                "rsi_signal": rsi_signal(rsi),
                "trend": trend,
                "sma50": round(sma50, 2) if sma50 else None,
                "sma200": round(sma200, 2) if sma200 else None,
                "swing_score": swing_score,
                "atr_pct": round(atr_pct, 2),
                "bb_position": bb_label,
                "vol_ratio": round(vol_ratio, 2),
                "ret_1m": round(ret_1m, 2),
                "ret_3m": round(ret_3m, 2),
                "pct_from_52w_high": round(pct_from_52w_high, 2),
                "pct_from_52w_low": round(pct_from_52w_low, 2),
                "month_avg_return": month_avg,
                "seasonal_years": seasonal_years,
                "seasonal_win_rate": win_rate,
                "seasonal_best": best_return,
                "seasonal_worst": worst_return,
                "month": current_month_name
            }

        except Exception as e:
            log(f"  Error processing {symbol}: {e}")
            continue

    log(f"Analyzed {len(results)} stocks (filtered out: {skipped_price} low price, {skipped_volume} low volume, {skipped_data} insufficient data)")
    return results


def get_top_rsi(results, n=10):
    """Top RSI picks: oversold stocks in uptrend or mixed trend (not falling knives)."""
    candidates = []
    for sym, data in results.items():
        rsi = data.get("rsi")
        if rsi is None or rsi > 45:  # Only interested in oversold/near-oversold
            continue
        # Reject stocks in strong downtrend (falling knife)
        if data.get("trend") == "Downtrend" and rsi < 25:
            continue
        candidates.append((sym, data))
    candidates.sort(key=lambda x: x[1]["rsi"])
    return [data for _, data in candidates[:n]]


def get_top_swing(results, n=10):
    """Top swing picks: highest swing score with volume confirmation."""
    candidates = []
    for sym, data in results.items():
        if data["swing_score"] < 20:  # Minimum score threshold
            continue
        candidates.append((sym, data))
    candidates.sort(key=lambda x: x[1]["swing_score"], reverse=True)
    return [data for _, data in candidates[:n]]


def get_top_seasonal(results, n=10):
    """Top seasonal picks: consistent positive returns for current month."""
    candidates = []
    for sym, data in results.items():
        avg_ret = data.get("month_avg_return")
        years = data.get("seasonal_years", 0)
        win_rate = data.get("seasonal_win_rate", 0)
        if avg_ret is None:
            continue
        # Must have enough history and decent win rate
        if years < MIN_SEASONAL_YEARS:
            continue
        if win_rate < MIN_SEASONAL_WIN_RATE * 100:
            continue
        if avg_ret <= 0:  # Only positive seasonal patterns
            continue
        candidates.append((sym, data))
    # Sort by average return * win_rate (rewards consistency + magnitude)
    candidates.sort(key=lambda x: x[1]["month_avg_return"] * (x[1]["seasonal_win_rate"] / 100), reverse=True)
    return [data for _, data in candidates[:n]]


def main():
    script_dir = os.path.dirname(os.path.abspath(__file__))
    project_root = os.path.dirname(script_dir)
    db_path = os.path.join(project_root, "dividends.db")

    if not os.path.exists(db_path):
        log(f"Database not found: {db_path}")
        print(json.dumps({"error": f"Database not found: {db_path}"}))
        sys.exit(1)

    symbols = get_canadian_symbols(db_path)
    if not symbols:
        log("No Canadian (.TO) stocks found in database")
        print(json.dumps({"error": "No Canadian stocks found in database"}))
        sys.exit(1)

    log(f"Found {len(symbols)} Canadian stocks")
    log(f"Filters: price >= ${MIN_PRICE}, volume >= {MIN_AVG_VOLUME:,}, data >= {MIN_DATA_DAYS} days")

    results = analyze_stocks(symbols)

    if not results:
        print(json.dumps({"error": "Could not fetch data for any stocks"}))
        sys.exit(1)

    top_rsi = get_top_rsi(results)
    top_swing = get_top_swing(results)
    top_seasonal = get_top_seasonal(results)

    output = {
        "generated_at": datetime.now().isoformat(),
        "total_analyzed": len(results),
        "total_in_db": len(symbols),
        "month": date.today().strftime("%B"),
        "filters": {
            "min_price": MIN_PRICE,
            "min_volume": MIN_AVG_VOLUME,
            "min_seasonal_years": MIN_SEASONAL_YEARS,
            "min_win_rate": f"{MIN_SEASONAL_WIN_RATE * 100:.0f}%"
        },
        "rsi": top_rsi,
        "swing": top_swing,
        "seasonal": top_seasonal
    }

    print(json.dumps(output, indent=2))


if __name__ == "__main__":
    main()
