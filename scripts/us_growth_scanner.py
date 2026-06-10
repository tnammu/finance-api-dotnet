"""
US Growth Stock Scanner
Scans a curated list of US growth stocks and ranks them by growth score.
Uses the same 5-filter logic as growth_stock_analyzer.py.

Usage:
    python us_growth_scanner.py              # scan all, show top 20
    python us_growth_scanner.py --top 10     # show top N
    python us_growth_scanner.py --sector tech
    python us_growth_scanner.py --min-score 60
    python us_growth_scanner.py --json       # machine-readable output
"""

import sys
import json
import time
import argparse
from concurrent.futures import ThreadPoolExecutor, as_completed
from datetime import datetime, timedelta, timezone

import yfinance as yf

# Reuse analysis logic from growth_stock_analyzer
from growth_stock_analyzer import analyze_growth_stock, convert_to_serializable

def log(msg):
    print(msg, file=sys.stderr)


# ETFs are detected by sector group name (see _ETF_SECTORS below)


def _rsi(closes, period=14):
    """Compute RSI from a list/series of closing prices."""
    import numpy as np
    deltas = np.diff(closes)
    gains  = np.where(deltas > 0, deltas, 0.0)
    losses = np.where(deltas < 0, -deltas, 0.0)
    avg_gain = np.mean(gains[:period])
    avg_loss = np.mean(losses[:period])
    for i in range(period, len(gains)):
        avg_gain = (avg_gain * (period - 1) + gains[i]) / period
        avg_loss = (avg_loss * (period - 1) + losses[i]) / period
    if avg_loss == 0:
        return 100.0
    rs = avg_gain / avg_loss
    return round(100 - (100 / (1 + rs)), 1)


def analyze_etf(symbol, sector):
    """Score an ETF on returns, momentum, volume pressure, RSI, and trend."""
    try:
        import numpy as np
        ticker = yf.Ticker(symbol)
        info = ticker.info

        company_name  = info.get("longName") or info.get("shortName") or symbol
        expense_ratio = info.get("annualReportExpenseRatio") or info.get("totalExpenseRatio") or None

        hist = ticker.history(period="3y")
        if hist.empty:
            hist = ticker.history(period="1y")

        if hist.empty:
            log(f"✗ {symbol}: No price history")
            return {"success": True, "symbol": symbol, "company_name": company_name,
                    "sector_group": sector, "is_etf": True,
                    "growth_score": 0, "growth_rating": "No Data",
                    "current_price": 0, "fetched_at": datetime.now(timezone.utc).isoformat()}

        close  = hist["Close"].astype(float)
        volume = hist["Volume"].astype(float)
        now_price = float(close.iloc[-1])

        # ── Price returns ──────────────────────────────────────────────
        def pct_return(days):
            cutoff = close.index[-1] - timedelta(days=days)
            past = close[close.index <= cutoff]
            return ((now_price - float(past.iloc[-1])) / float(past.iloc[-1])) * 100 if not past.empty else None

        ret_1y = pct_return(365)
        ret_3y = pct_return(365 * 3)
        ret_1m = pct_return(30)
        ret_1w = pct_return(7)

        # ── Moving averages & trend ────────────────────────────────────
        sma50  = float(close.tail(50).mean())  if len(close) >= 50  else None
        sma200 = float(close.tail(200).mean()) if len(close) >= 200 else None
        above_sma50  = now_price > sma50  if sma50  else None
        above_sma200 = now_price > sma200 if sma200 else None

        if above_sma50 and above_sma200:   trend = "Uptrend"
        elif not above_sma50 and not above_sma200: trend = "Downtrend"
        else:                               trend = "Mixed"

        # ── RSI ───────────────────────────────────────────────────────
        rsi = _rsi(close.values) if len(close) >= 16 else None

        # ── Volume pressure (are people buying or selling?) ───────────
        # Compare last 5-day avg volume vs 20-day avg volume
        vol_5d  = float(volume.tail(5).mean())  if len(volume) >= 5  else None
        vol_20d = float(volume.tail(20).mean()) if len(volume) >= 20 else None
        vol_ratio = round(vol_5d / vol_20d, 2) if (vol_5d and vol_20d and vol_20d > 0) else None

        # Up-volume vs Down-volume over last 20 days (accumulation vs distribution)
        recent = hist.tail(20)
        up_days   = recent[recent["Close"] >= recent["Open"]]
        down_days = recent[recent["Close"] <  recent["Open"]]
        up_vol   = float(up_days["Volume"].sum())
        down_vol = float(down_days["Volume"].sum())
        total_vol = up_vol + down_vol
        buy_pressure = round((up_vol / total_vol) * 100, 1) if total_vol > 0 else None
        # >55% = accumulation, <45% = distribution

        # ── Scoring (out of 100) ──────────────────────────────────────
        score = 0

        # 1. Returns (25 pts)
        if ret_1y is not None:
            if ret_1y > 20:  score += 25
            elif ret_1y > 8: score += 15
            elif ret_1y > 0: score += 8

        # 2. Trend — are people still holding? (20 pts)
        if trend == "Uptrend":     score += 20
        elif trend == "Mixed":     score += 10

        # 3. RSI — buy zone or overbought? (15 pts)
        if rsi is not None:
            if rsi < 35:     score += 15   # oversold = great entry
            elif rsi < 50:   score += 10   # neutral/leaning bullish
            elif rsi < 65:   score += 5    # fine
            # >65 = overbought, no bonus

        # 4. Volume pressure — are people buying? (20 pts)
        if buy_pressure is not None:
            if buy_pressure > 60:   score += 20   # strong accumulation
            elif buy_pressure > 52: score += 12   # mild buying
            elif buy_pressure > 45: score += 5
            # <45 = distribution, no bonus

        # 5. Volume ratio — rising interest? (10 pts)
        if vol_ratio is not None:
            if vol_ratio > 1.5:   score += 10  # 50% more volume than normal
            elif vol_ratio > 1.1: score += 5

        # 6. Low expense ratio (10 pts)
        if expense_ratio is not None:
            if expense_ratio < 0.0015:  score += 10
            elif expense_ratio < 0.003: score += 5

        if score >= 70:   rating = "Strong Buy"
        elif score >= 50: rating = "Moderate Buy"
        elif score >= 35: rating = "Hold / Watch"
        else:             rating = "Avoid / Downtrend"

        r1y_str = f"{ret_1y:+.1f}%" if ret_1y is not None else "N/A"
        log(f"  {symbol}: price={now_price:.2f} RSI={rsi} trend={trend} buy%={buy_pressure} vol_ratio={vol_ratio} 1y={r1y_str} → score={score}")

        return {
            "success": True, "symbol": symbol, "company_name": company_name,
            "sector_group": sector, "is_etf": True,
            "growth_score": score, "growth_rating": rating,
            "current_price": round(now_price, 2),
            "return_1y":  round(ret_1y, 1)  if ret_1y  is not None else None,
            "return_3y":  round(ret_3y, 1)  if ret_3y  is not None else None,
            "return_1m":  round(ret_1m, 1)  if ret_1m  is not None else None,
            "return_1w":  round(ret_1w, 1)  if ret_1w  is not None else None,
            "rsi": rsi,
            "trend": trend,
            "buy_pressure_pct": buy_pressure,
            "vol_ratio": vol_ratio,
            "expense_ratio": expense_ratio,
            "fetched_at": datetime.now(timezone.utc).isoformat(),
        }
    except Exception as e:
        log(f"✗ ETF {symbol} failed: {e}")
        return {"success": False, "symbol": symbol, "error": str(e)}

# TSX-listed ETFs/stocks that give exposure to US growth stocks
# These trade in CAD on the Toronto Stock Exchange
CANADIAN_US_EXPOSURE = {
    "US Index ETFs (TSX)": [
        "VFV.TO",   # Vanguard S&P 500 — most popular CAD S&P500 ETF
        "ZSP.TO",   # BMO S&P 500 ETF
        "XUU.TO",   # iShares S&P 500 (unhedged CAD)
        "XSP.TO",   # iShares S&P 500 (CAD hedged)
        "VUN.TO",   # Vanguard US Total Market (all US stocks)
        "HXS.TO",   # Horizons S&P 500 ETF (swap-based, tax efficient)
    ],
    "US Tech / Nasdaq ETFs (TSX)": [
        "QQC.TO",   # iShares NASDAQ-100 (CAD hedged)
        "QQC-F.TO", # iShares NASDAQ-100 (unhedged)
        "QQCE.TO",  # iShares NASDAQ 100 ETF (CAD, unhedged — user requested)
        "HXQ.TO",   # Horizons Nasdaq-100 (swap-based)
        "ZQQ.TO",   # BMO Nasdaq 100 ETF
        "TEC.TO",   # TD Global Technology Leaders ETF
    ],
    "All-World / Growth ETFs (TSX)": [
        "XEQT.TO",  # iShares All-Equity ETF (80% US weight)
        "VEQT.TO",  # Vanguard All-Equity ETF Portfolio
        "ZGRO.TO",  # BMO Growth ETF (80% equity)
        "HGRO.TO",  # Horizons Growth TRI ETF
    ],
    "Canadian Stocks with Heavy US Revenue": [
        "SHOP.TO",  # Shopify — US-dominant e-commerce platform
        "CSU.TO",   # Constellation Software — buys US/global software companies
        "LSPD.TO",  # Lightspeed Commerce — US + global POS/payments
        "DCBO.TO",  # Docebo — US-focused corporate e-learning
        "OTEX.TO",  # OpenText — enterprise software, heavy US exposure
        "KXS.TO",   # Kinaxis — supply chain SaaS, majority US revenue
        "DSG.TO",   # Descartes Systems — logistics software, US-heavy
    ],
}

# Curated list of US growth candidates across sectors
US_GROWTH_STOCKS = {
    "AI / Semiconductors": [
        "NVDA",   # Nvidia — AI chips dominant
        "AMD",    # Advanced Micro Devices
        "AVGO",   # Broadcom — AI networking chips
        "ARM",    # ARM Holdings — chip design
        "MRVL",   # Marvell — data infra chips
        "ANET",   # Arista Networks — AI data center networking
        "SMCI",   # Super Micro Computer
    ],
    "Cloud / SaaS": [
        "MSFT",   # Microsoft — Azure + AI
        "CRM",    # Salesforce
        "NOW",    # ServiceNow — enterprise automation
        "SNOW",   # Snowflake — cloud data
        "MDB",    # MongoDB
        "DDOG",   # Datadog — observability
        "ZS",     # Zscaler — cloud security
        "NET",    # Cloudflare — edge network
        "TEAM",   # Atlassian
        "GTLB",   # GitLab
    ],
    "Cybersecurity": [
        "CRWD",   # CrowdStrike — endpoint security
        "PANW",   # Palo Alto Networks
        "FTNT",   # Fortinet
        "S",      # SentinelOne
    ],
    "Big Tech / Platforms": [
        "GOOGL",  # Alphabet — Search + Cloud + AI
        "META",   # Meta — social + AI
        "AMZN",   # Amazon — AWS + retail
        "AAPL",   # Apple — services growth
        "TSLA",   # Tesla — EV + energy
        "UBER",   # Uber — rides + delivery
        "ABNB",   # Airbnb
    ],
    "Fintech / Payments": [
        "V",      # Visa
        "MA",     # Mastercard
        "SQ",     # Block (Square)
        "NU",     # Nubank — Latin America fintech
        "AFRM",   # Affirm — BNPL
        "PYPL",   # PayPal
    ],
    "E-commerce / Consumer": [
        "SHOP",   # Shopify
        "TTD",    # The Trade Desk — programmatic ads
        "CELH",   # Celsius Holdings — energy drinks
        "DUOL",   # Duolingo
        "PINS",   # Pinterest
    ],
    "Healthcare / Biotech": [
        "ISRG",   # Intuitive Surgical — robotic surgery
        "DXCM",   # Dexcom — continuous glucose monitor
        "VEEV",   # Veeva Systems — pharma cloud
        "IDXX",   # IDEXX Labs — veterinary diagnostics
        "RXRX",   # Recursion Pharma — AI drug discovery
        "CRSP",   # CRISPR Therapeutics
    ],
    "Space / Defense / Other": [
        "PLTR",   # Palantir — AI/data analytics
        "RKLB",   # Rocket Lab — small launch vehicles
        "SPCE",   # Virgin Galactic (speculative)
    ],
}

ALL_SYMBOLS = [(symbol, sector) for sector, symbols in US_GROWTH_STOCKS.items() for symbol in symbols]
CANADIAN_US_SYMBOLS = [(symbol, sector) for sector, symbols in CANADIAN_US_EXPOSURE.items() for symbol in symbols]


_ETF_SUFFIXES = ('.TO',)
_ETF_SECTORS = {"US Index ETFs (TSX)", "US Tech / Nasdaq ETFs (TSX)", "All-World / Growth ETFs (TSX)"}

def _is_etf(symbol, sector):
    if sector in _ETF_SECTORS:
        return True
    # Detect via yfinance quoteType dynamically
    try:
        qt = yf.Ticker(symbol).info.get("quoteType", "")
        return qt in ("ETF", "MUTUALFUND")
    except Exception:
        return False

def analyze_one(symbol, sector):
    """Route to ETF scorer or stock scorer based on type."""
    if sector in _ETF_SECTORS:
        return analyze_etf(symbol, sector)
    # For Canadian stocks with US revenue, use standard growth scorer
    result = analyze_growth_stock(symbol)
    result["sector_group"] = sector
    result["is_etf"] = False
    return result


def scan(symbols_with_sector, workers=8):
    results = []
    total = len(symbols_with_sector)
    done = 0

    log(f"\nScanning {total} US growth stocks with {workers} parallel workers...")
    log("This takes ~2-4 minutes (yfinance rate limits apply)\n")

    with ThreadPoolExecutor(max_workers=workers) as executor:
        futures = {executor.submit(analyze_one, sym, sec): sym for sym, sec in symbols_with_sector}
        for future in as_completed(futures):
            sym = futures[future]
            done += 1
            try:
                result = future.result()
                if result.get("success"):
                    results.append(result)
                    log(f"[{done}/{total}] {sym:8s} score={result['growth_score']:3d}  {result['growth_rating']}")
                else:
                    log(f"[{done}/{total}] {sym:8s} FAILED: {result.get('error', 'unknown')}")
            except Exception as e:
                log(f"[{done}/{total}] {sym:8s} ERROR: {e}")

    return results


def print_table(results, top_n=20):
    ranked = sorted(results, key=lambda r: r["growth_score"], reverse=True)[:top_n]

    etfs   = [r for r in ranked if r.get("is_etf")]
    stocks = [r for r in ranked if not r.get("is_etf")]

    def fmt(val, fmt_str, suffix=""):
        return f"{val:{fmt_str}}{suffix}" if val is not None else "N/A"

    if stocks:
        print(f"\n{'='*95}")
        print(f"  CANADIAN STOCKS WITH US EXPOSURE  —  {datetime.now().strftime('%Y-%m-%d %H:%M')}")
        print(f"{'='*95}")
        print(f"{'#':>3}  {'Symbol':<10}  {'Company':<30}  {'Score':>5}  {'RevGrw%':>8}  {'EPS%':>8}  {'PEG':>5}  {'R40':>5}  {'Rating'}")
        print(f"{'-'*95}")
        for i, s in enumerate(stocks, 1):
            rev  = fmt(s.get('revenue_growth'), '+.1f', '%')
            eps  = fmt(s.get('eps_growth'),     '+.1f', '%')
            peg  = fmt(s.get('peg_ratio'),      '.2f')
            r40  = fmt(s.get('rule_of_40'),     '.1f')
            name = (s.get('company_name') or s['symbol'])[:29]
            print(f"{i:>3}  {s['symbol']:<10}  {name:<30}  {s['growth_score']:>5}  {rev:>8}  {eps:>8}  {peg:>5}  {r40:>5}  {s['growth_rating']}")
        print(f"{'='*95}")

    if etfs:
        print(f"\n{'='*110}")
        print(f"  CANADIAN ETFs TRACKING US MARKETS  —  {datetime.now().strftime('%Y-%m-%d %H:%M')}")
        print(f"{'='*110}")
        print(f"{'#':>3}  {'Symbol':<10}  {'Company':<32}  {'Score':>5}  {'1W':>6}  {'1M':>6}  {'1Y':>7}  {'RSI':>5}  {'Trend':<10}  {'BuyVol%':>7}  {'VolRatio':>8}  {'Rating'}")
        print(f"{'-'*110}")
        for i, s in enumerate(etfs, 1):
            r1w = fmt(s.get('return_1w'), '+.1f', '%')
            r1m = fmt(s.get('return_1m'), '+.1f', '%')
            r1y = fmt(s.get('return_1y'), '+.1f', '%')
            rsi = f"{s['rsi']:.0f}"   if s.get('rsi')        is not None else " N/A"
            bp  = f"{s['buy_pressure_pct']:.0f}%" if s.get('buy_pressure_pct') is not None else "  N/A"
            vr  = f"{s['vol_ratio']:.2f}x"        if s.get('vol_ratio')        is not None else "   N/A"
            trend = s.get('trend') or 'N/A'
            name = (s.get('company_name') or s['symbol'])[:31]
            print(f"{i:>3}  {s['symbol']:<10}  {name:<32}  {s['growth_score']:>5}  {r1w:>6}  {r1m:>6}  {r1y:>7}  {rsi:>5}  {trend:<10}  {bp:>7}  {vr:>8}  {s['growth_rating']}")
        print(f"{'='*110}")
        print()
        print("  BuyVol% = % of last 20 days volume on UP days  (>55% = people buying, <45% = people selling)")
        print("  VolRatio = recent 5-day volume vs 20-day avg   (>1.5x = unusual interest/activity)")
        print("  RSI < 35 = oversold (good entry),  RSI > 65 = overbought (wait for pullback)")

    print()


def main():
    parser = argparse.ArgumentParser(description="US Growth Stock Scanner")
    parser.add_argument("--top",          type=int,   default=20,    help="Show top N results (default 20)")
    parser.add_argument("--sector",       type=str,   default=None,  help="Filter by sector keyword (e.g. 'tech', 'health')")
    parser.add_argument("--min-score",    type=int,   default=0,     help="Only show stocks with score >= N")
    parser.add_argument("--workers",      type=int,   default=8,     help="Parallel workers (default 8)")
    parser.add_argument("--json",         action="store_true",       help="Output JSON instead of table")
    parser.add_argument("--canadian-us",  action="store_true",       help="Scan Canadian-listed stocks/ETFs with US exposure (TSX)")
    parser.add_argument("--etfs-only",    action="store_true",       help="Scan Canadian-listed ETFs only (no individual stocks)")
    args = parser.parse_args()

    if args.etfs_only:
        symbols = [(s, sec) for s, sec in CANADIAN_US_SYMBOLS if sec in _ETF_SECTORS]
    elif args.canadian_us:
        symbols = CANADIAN_US_SYMBOLS
    else:
        symbols = ALL_SYMBOLS

    # Filter by sector keyword
    if args.sector:
        kw = args.sector.lower()
        symbols = [(s, sec) for s, sec in symbols if kw in sec.lower() or kw in s.lower()]
        if not symbols:
            print(f"No stocks found matching sector '{args.sector}'", file=sys.stderr)
            sys.exit(1)
        log(f"Filtered to {len(symbols)} stocks in sectors matching '{args.sector}'")

    start = time.time()
    results = scan(symbols, workers=args.workers)
    elapsed = time.time() - start

    # Apply min-score filter
    if args.min_score > 0:
        results = [r for r in results if r["growth_score"] >= args.min_score]

    log(f"\nDone in {elapsed:.1f}s. {len(results)} stocks analyzed successfully.")

    results = [convert_to_serializable(r) for r in results]

    if args.json:
        ranked = sorted(results, key=lambda r: r["growth_score"], reverse=True)[:args.top]
        print(json.dumps({"success": True, "total": len(results), "results": ranked, "generatedAt": datetime.now(timezone.utc).isoformat()}, indent=2))
    else:
        print_table(results, top_n=args.top)


if __name__ == "__main__":
    main()
