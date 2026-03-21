import json

import os
result_file = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'picks_result.json')
with open(result_file) as f:
    data = json.load(f)

swing    = data.get('swing', [])
rsi_top  = data.get('rsi', [])
seasonal = data.get('seasonal', [])

print(f"Total analyzed: {data['total_analyzed']} | Month: {data['month']}")
print()

# --- SWING ---
print("=== TOP SWING TRADES ===")
print(f"{'#':<3} {'Symbol':<9} {'Price':>8} {'Score':>6} {'RSI':>6} {'ATR%':>6} {'VolRatio':>9} {'BB':>5} {'Trend':<11} {'Sentiment'}")
print("-" * 90)
for i, s in enumerate(swing, 1):
    sym  = s['symbol'].replace('.TO','')
    rsi  = f"{s['rsi']:.1f}" if s['rsi'] else '--'
    sent = s.get('sentiment_label') or ''
    bb   = (s.get('bb_position') or '').split('(')[0].strip()
    print(f"{i:<3} {sym:<9} ${s['price']:>7.2f} {s['swing_score']:>6.1f} {rsi:>6} {s['atr_pct']:>5.2f}% {s['vol_ratio']:>8.2f}x {bb:<25} {s['trend'] or 'N/A':<11} {sent}")

print()
print("=== TOP RSI OVERSOLD ===")
print(f"{'#':<3} {'Symbol':<9} {'Price':>8} {'RSI':>6} {'Trend':<11} {'Signal'}")
print("-" * 65)
for i, s in enumerate(rsi_top, 1):
    sym = s['symbol'].replace('.TO','')
    rsi = f"{s['rsi']:.1f}" if s['rsi'] else '--'
    print(f"{i:<3} {sym:<9} ${s['price']:>7.2f} {rsi:>6} {s['trend'] or 'N/A':<11} {s.get('rsi_signal','')}")

print()
print("=== TOP SEASONAL (March) ===")
print(f"{'#':<3} {'Symbol':<9} {'Price':>8} {'AvgRet%':>8} {'WinRate%':>9} {'Years':>6} {'Trend'}")
print("-" * 65)
for i, s in enumerate(seasonal, 1):
    sym    = s['symbol'].replace('.TO','')
    avgret = f"+{s['month_avg_return']:.1f}%" if s.get('month_avg_return') else 'N/A'
    wr     = f"{s['seasonal_win_rate']:.0f}%" if s.get('seasonal_win_rate') else 'N/A'
    print(f"{i:<3} {sym:<9} ${s['price']:>7.2f} {avgret:>8} {wr:>9} {s.get('seasonal_years',0):>6} {s['trend'] or 'N/A'}")
