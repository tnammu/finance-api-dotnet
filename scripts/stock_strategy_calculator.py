#!/usr/bin/env python3
"""
Individual Stock Trading Strategy Calculator
Analyzes any stock and provides detailed strategy with exact dollar amounts
"""

import sys
import json
import yfinance as yf
from datetime import datetime, timedelta
import statistics

def log(message):
    """Print to stderr for logging"""
    print(message, file=sys.stderr)

def calculate_stock_strategy(symbol, capital=1000, years=3):
    """
    Calculate detailed trading strategy for a stock with exact dollar amounts
    """
    try:
        log(f"\n{'='*80}")
        log(f"CALCULATING STRATEGY FOR: {symbol}")
        log(f"Capital: ${capital:,.2f}")
        log(f"Analysis Period: {years} years")
        log(f"{'='*80}\n")

        # Fetch stock data
        ticker = yf.Ticker(symbol)
        end_date = datetime.now()
        start_date = end_date - timedelta(days=years*365)

        # Get historical data
        hist = ticker.history(start=start_date, end=end_date, interval='1d')

        if hist.empty:
            log(f"✗ No data found for {symbol}")
            return None

        # Get stock info
        info = ticker.info
        company_name = info.get('longName', symbol)
        current_price = info.get('currentPrice') or hist.iloc[-1]['Close']
        sector = info.get('sector', 'Unknown')
        industry = info.get('industry', 'Unknown')

        log(f"Company: {company_name}")
        log(f"Current Price: ${current_price:.2f}")
        log(f"Sector: {sector}")
        log(f"Industry: {industry}\n")

        # Calculate position sizing
        shares_to_buy = int(capital / current_price)
        position_value = shares_to_buy * current_price
        remaining_cash = capital - position_value

        # Calculate risk levels (percentage based)
        stop_loss_pct = 8  # 8% stop loss
        profit_target_pct_low = 5  # 5% minimum target
        profit_target_pct_mid = 10  # 10% moderate target
        profit_target_pct_high = 15  # 15% aggressive target
        trailing_stop_activation = 10  # Activate at 10% gain
        trailing_stop_distance = 5  # Trail by 5%

        # Calculate exact dollar amounts
        entry_price = current_price
        stop_loss_price = entry_price * (1 - stop_loss_pct / 100)
        profit_target_low = entry_price * (1 + profit_target_pct_low / 100)
        profit_target_mid = entry_price * (1 + profit_target_pct_mid / 100)
        profit_target_high = entry_price * (1 + profit_target_pct_high / 100)
        trailing_stop_activation_price = entry_price * (1 + trailing_stop_activation / 100)

        # Calculate risk and reward in dollars
        risk_per_share = entry_price - stop_loss_price
        max_loss_dollars = risk_per_share * shares_to_buy

        reward_per_share_low = profit_target_low - entry_price
        reward_per_share_mid = profit_target_mid - entry_price
        reward_per_share_high = profit_target_high - entry_price

        potential_profit_low = reward_per_share_low * shares_to_buy
        potential_profit_mid = reward_per_share_mid * shares_to_buy
        potential_profit_high = reward_per_share_high * shares_to_buy

        # Calculate risk/reward ratios
        risk_reward_low = reward_per_share_low / risk_per_share if risk_per_share > 0 else 0
        risk_reward_mid = reward_per_share_mid / risk_per_share if risk_per_share > 0 else 0
        risk_reward_high = reward_per_share_high / risk_per_share if risk_per_share > 0 else 0

        # Analyze monthly patterns
        from collections import defaultdict
        monthly_returns = defaultdict(list)

        prev_close = None
        for date, row in hist.iterrows():
            if prev_close is not None:
                daily_return = ((row['Close'] - prev_close) / prev_close) * 100
                month_name = date.strftime('%B')
                monthly_returns[month_name].append(daily_return)
            prev_close = row['Close']

        # Calculate monthly statistics
        month_order = ['January', 'February', 'March', 'April', 'May', 'June',
                      'July', 'August', 'September', 'October', 'November', 'December']

        monthly_stats = []
        for month in month_order:
            if month in monthly_returns and len(monthly_returns[month]) > 0:
                returns = monthly_returns[month]
                avg_return = statistics.mean(returns)
                win_rate = (sum(1 for r in returns if r > 0) / len(returns)) * 100

                monthly_stats.append({
                    'month': month,
                    'avgReturn': round(avg_return, 3),
                    'winRate': round(win_rate, 1),
                    'occurrences': len(returns)
                })

        # Sort by average return
        monthly_stats_sorted = sorted(monthly_stats, key=lambda x: x['avgReturn'], reverse=True)
        best_months = [m['month'] for m in monthly_stats_sorted[:3]] if len(monthly_stats_sorted) >= 3 else []
        worst_months = [m['month'] for m in monthly_stats_sorted[-3:]] if len(monthly_stats_sorted) >= 3 else []

        # Calculate volatility
        daily_returns = []
        for i in range(1, len(hist)):
            daily_return = ((hist.iloc[i]['Close'] - hist.iloc[i-1]['Close']) / hist.iloc[i-1]['Close']) * 100
            daily_returns.append(daily_return)

        volatility = statistics.stdev(daily_returns) if len(daily_returns) > 1 else 0
        avg_daily_return = statistics.mean(daily_returns) if len(daily_returns) > 0 else 0

        # Calculate support and resistance levels
        high_52week = hist['High'].max()
        low_52week = hist['Low'].min()

        # Calculate moving averages
        ma_20 = hist['Close'].tail(20).mean()
        ma_50 = hist['Close'].tail(50).mean() if len(hist) >= 50 else None
        ma_200 = hist['Close'].tail(200).mean() if len(hist) >= 200 else None

        # Determine trend
        trend = "Unknown"
        if ma_50 and ma_200:
            if current_price > ma_50 > ma_200:
                trend = "Strong Uptrend"
            elif current_price > ma_50:
                trend = "Uptrend"
            elif current_price < ma_50 < ma_200:
                trend = "Strong Downtrend"
            elif current_price < ma_50:
                trend = "Downtrend"
            else:
                trend = "Sideways"
        elif ma_50:
            if current_price > ma_50:
                trend = "Uptrend"
            else:
                trend = "Downtrend"

        log(f"{'='*80}")
        log(f"STRATEGY CALCULATION COMPLETE")
        log(f"{'='*80}\n")

        # Build result
        result = {
            'success': True,
            'symbol': symbol,
            'companyName': company_name,
            'sector': sector,
            'industry': industry,
            'currentPrice': round(current_price, 2),

            # Capital and Position
            'capital': capital,
            'sharesToBuy': shares_to_buy,
            'positionValue': round(position_value, 2),
            'remainingCash': round(remaining_cash, 2),

            # Entry and Exit Levels (Exact Prices)
            'entryPrice': round(entry_price, 2),
            'stopLossPrice': round(stop_loss_price, 2),
            'profitTargetLow': round(profit_target_low, 2),
            'profitTargetMid': round(profit_target_mid, 2),
            'profitTargetHigh': round(profit_target_high, 2),
            'trailingStopActivation': round(trailing_stop_activation_price, 2),

            # Risk and Reward (Dollar Amounts)
            'riskPerShare': round(risk_per_share, 2),
            'maxLossDollars': round(max_loss_dollars, 2),
            'potentialProfitLow': round(potential_profit_low, 2),
            'potentialProfitMid': round(potential_profit_mid, 2),
            'potentialProfitHigh': round(potential_profit_high, 2),

            # Percentages
            'stopLossPct': stop_loss_pct,
            'profitTargetPctLow': profit_target_pct_low,
            'profitTargetPctMid': profit_target_pct_mid,
            'profitTargetPctHigh': profit_target_pct_high,

            # Risk/Reward Ratios
            'riskRewardLow': round(risk_reward_low, 2),
            'riskRewardMid': round(risk_reward_mid, 2),
            'riskRewardHigh': round(risk_reward_high, 2),

            # Monthly Patterns
            'monthlyStats': monthly_stats_sorted,
            'bestMonths': best_months,
            'worstMonths': worst_months,

            # Technical Analysis
            'volatility': round(volatility, 2),
            'avgDailyReturn': round(avg_daily_return, 3),
            'high52Week': round(high_52week, 2),
            'low52Week': round(low_52week, 2),
            'ma20': round(ma_20, 2),
            'ma50': round(ma_50, 2) if ma_50 else None,
            'ma200': round(ma_200, 2) if ma_200 else None,
            'trend': trend,

            # Trading Recommendations
            'recommendations': {
                'buySignal': current_price > ma_20 and trend in ['Uptrend', 'Strong Uptrend'],
                'sellSignal': current_price < stop_loss_price,
                'favorableMonths': best_months,
                'avoidMonths': worst_months,
                'riskLevel': 'LOW' if volatility < 2 else 'MEDIUM' if volatility < 4 else 'HIGH'
            },

            'fetched_at': datetime.now().isoformat()
        }

        return result

    except Exception as e:
        log(f"\n✗ Error calculating strategy for {symbol}: {str(e)}")
        import traceback
        traceback.print_exc(file=sys.stderr)
        return {
            'success': False,
            'error': str(e),
            'symbol': symbol
        }

if __name__ == '__main__':
    if len(sys.argv) < 2:
        log("Usage: python stock_strategy_calculator.py SYMBOL [CAPITAL] [YEARS]")
        log("Example: python stock_strategy_calculator.py AAPL 1000 3")
        sys.exit(1)

    symbol = sys.argv[1].upper()
    capital = float(sys.argv[2]) if len(sys.argv) > 2 else 1000
    years = int(sys.argv[3]) if len(sys.argv) > 3 else 3

    result = calculate_stock_strategy(symbol, capital, years)

    if result and result.get('success'):
        # Output JSON to stdout
        print(json.dumps(result, indent=2))
    else:
        sys.exit(1)
