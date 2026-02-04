#!/usr/bin/env python3
"""
Multi-Strategy Trading Analyzer
Analyzes multiple trading strategies for any stock with comprehensive backtesting
"""

import sys
import json
import yfinance as yf
from datetime import datetime, timedelta
import statistics
import numpy as np
from collections import defaultdict

def log(message):
    """Print to stderr for logging"""
    print(message, file=sys.stderr)

class StrategyAnalyzer:
    def __init__(self, symbol, capital=100, years=5, enforce_buy_first=True):
        self.symbol = symbol.upper()
        self.capital = capital
        self.years = years
        self.enforce_buy_first = enforce_buy_first
        self.ticker = None
        self.hist = None
        self.info = {}
        self.validation_warnings = []

    def fetch_data(self):
        """Fetch historical stock data"""
        try:
            log(f"\n{'='*80}")
            log(f"MULTI-STRATEGY ANALYSIS FOR: {self.symbol}")
            log(f"Capital: ${self.capital:,.2f}")
            log(f"Analysis Period: {self.years} years")
            log(f"{'='*80}\n")

            self.ticker = yf.Ticker(self.symbol)
            end_date = datetime.now()
            start_date = end_date - timedelta(days=self.years*365)

            self.hist = self.ticker.history(start=start_date, end=end_date, interval='1d')

            if self.hist.empty:
                log(f"✗ No data found for {self.symbol}")
                return False

            self.info = self.ticker.info
            log(f"✓ Fetched {len(self.hist)} days of data")
            log(f"Company: {self.info.get('longName', self.symbol)}")
            log(f"Current Price: ${self.info.get('currentPrice', self.hist.iloc[-1]['Close']):.2f}")

            return True

        except Exception as e:
            log(f"✗ Error fetching data: {str(e)}")
            return False

    def calculate_indicators(self):
        """Calculate technical indicators"""
        df = self.hist.copy()

        # Moving Averages
        df['SMA_20'] = df['Close'].rolling(window=20).mean()
        df['SMA_50'] = df['Close'].rolling(window=50).mean()
        df['SMA_200'] = df['Close'].rolling(window=200).mean()
        df['EMA_12'] = df['Close'].ewm(span=12, adjust=False).mean()
        df['EMA_26'] = df['Close'].ewm(span=26, adjust=False).mean()

        # RSI
        delta = df['Close'].diff()
        gain = (delta.where(delta > 0, 0)).rolling(window=14).mean()
        loss = (-delta.where(delta < 0, 0)).rolling(window=14).mean()
        rs = gain / loss
        df['RSI'] = 100 - (100 / (1 + rs))

        # MACD
        df['MACD'] = df['EMA_12'] - df['EMA_26']
        df['MACD_Signal'] = df['MACD'].ewm(span=9, adjust=False).mean()
        df['MACD_Hist'] = df['MACD'] - df['MACD_Signal']

        # Bollinger Bands
        df['BB_Middle'] = df['Close'].rolling(window=20).mean()
        df['BB_Std'] = df['Close'].rolling(window=20).std()
        df['BB_Upper'] = df['BB_Middle'] + (df['BB_Std'] * 2)
        df['BB_Lower'] = df['BB_Middle'] - (df['BB_Std'] * 2)

        # ATR for volatility
        df['TR'] = np.maximum(df['High'] - df['Low'],
                               np.maximum(abs(df['High'] - df['Close'].shift(1)),
                                         abs(df['Low'] - df['Close'].shift(1))))
        df['ATR'] = df['TR'].rolling(window=14).mean()

        return df

    def backtest_buy_hold(self, df):
        """Buy and Hold Strategy"""
        entry_price = df.iloc[0]['Close']
        exit_price = df.iloc[-1]['Close']
        shares = self.capital / entry_price
        final_value = shares * exit_price
        total_return = ((final_value - self.capital) / self.capital) * 100
        annual_return = total_return / self.years

        # Calculate drawdowns
        df['Portfolio_Value'] = (df['Close'] / entry_price) * self.capital
        running_max = df['Portfolio_Value'].expanding().max()
        df['Drawdown'] = ((df['Portfolio_Value'] - running_max) / running_max) * 100
        max_drawdown = df['Drawdown'].min()

        return {
            'name': 'Buy and Hold',
            'description': 'Buy at start and hold until end',
            'entryPrice': round(entry_price, 2),
            'exitPrice': round(exit_price, 2),
            'shares': round(shares, 4),
            'initialValue': self.capital,
            'finalValue': round(final_value, 2),
            'totalReturn': round(total_return, 2),
            'annualReturn': round(annual_return, 2),
            'maxDrawdown': round(max_drawdown, 2),
            'trades': 1,
            'winRate': 100 if total_return > 0 else 0,
            'sharpeRatio': self._calculate_sharpe(df['Close'].pct_change()),
            'signals': []
        }

    def backtest_sma_crossover(self, df):
        """Simple Moving Average Crossover Strategy (50/200)"""
        trades = []
        position = 0
        cash = self.capital

        for i in range(200, len(df)):
            row = df.iloc[i]
            prev_row = df.iloc[i-1]

            # Golden Cross: SMA50 crosses above SMA200 (BUY)
            if prev_row['SMA_50'] <= prev_row['SMA_200'] and row['SMA_50'] > row['SMA_200'] and position == 0:
                shares = cash / row['Close']
                position = shares
                trades.append({
                    'date': df.index[i].strftime('%Y-%m-%d'),
                    'type': 'BUY',
                    'price': round(row['Close'], 2),
                    'shares': round(shares, 4),
                    'reason': 'Golden Cross (SMA50 > SMA200)'
                })
                cash = 0

            # Death Cross: SMA50 crosses below SMA200 (SELL)
            elif prev_row['SMA_50'] >= prev_row['SMA_200'] and row['SMA_50'] < row['SMA_200'] and position > 0:
                cash = position * row['Close']
                trades.append({
                    'date': df.index[i].strftime('%Y-%m-%d'),
                    'type': 'SELL',
                    'price': round(row['Close'], 2),
                    'shares': round(position, 4),
                    'value': round(cash, 2),
                    'reason': 'Death Cross (SMA50 < SMA200)'
                })
                position = 0

        # Close position if still open
        if position > 0:
            cash = position * df.iloc[-1]['Close']
            trades.append({
                'date': df.index[-1].strftime('%Y-%m-%d'),
                'type': 'SELL',
                'price': round(df.iloc[-1]['Close'], 2),
                'shares': round(position, 4),
                'value': round(cash, 2),
                'reason': 'End of period'
            })

        return self._analyze_trades(trades, 'SMA Crossover (50/200)',
                                     'Buy on Golden Cross, Sell on Death Cross')

    def backtest_rsi(self, df):
        """RSI Oversold/Overbought Strategy"""
        trades = []
        position = 0
        cash = self.capital

        for i in range(14, len(df)):
            row = df.iloc[i]

            # Oversold: RSI < 30 (BUY)
            if row['RSI'] < 30 and position == 0:
                shares = cash / row['Close']
                position = shares
                trades.append({
                    'date': df.index[i].strftime('%Y-%m-%d'),
                    'type': 'BUY',
                    'price': round(row['Close'], 2),
                    'shares': round(shares, 4),
                    'reason': f'RSI Oversold ({row["RSI"]:.1f} < 30)'
                })
                cash = 0

            # Overbought: RSI > 70 (SELL)
            elif row['RSI'] > 70 and position > 0:
                cash = position * row['Close']
                profit_pct = ((cash - self.capital) / self.capital) * 100
                trades.append({
                    'date': df.index[i].strftime('%Y-%m-%d'),
                    'type': 'SELL',
                    'price': round(row['Close'], 2),
                    'shares': round(position, 4),
                    'value': round(cash, 2),
                    'profit': round(cash - self.capital, 2),
                    'profitPct': round(profit_pct, 2),
                    'reason': f'RSI Overbought ({row["RSI"]:.1f} > 70)'
                })
                position = 0

        # Close position if still open
        if position > 0:
            cash = position * df.iloc[-1]['Close']
            trades.append({
                'date': df.index[-1].strftime('%Y-%m-%d'),
                'type': 'SELL',
                'price': round(df.iloc[-1]['Close'], 2),
                'shares': round(position, 4),
                'value': round(cash, 2),
                'reason': 'End of period'
            })

        return self._analyze_trades(trades, 'RSI Strategy',
                                     'Buy when RSI < 30 (oversold), Sell when RSI > 70 (overbought)')

    def backtest_macd(self, df):
        """MACD Crossover Strategy"""
        trades = []
        position = 0
        cash = self.capital

        for i in range(26, len(df)):
            row = df.iloc[i]
            prev_row = df.iloc[i-1]

            # MACD crosses above signal line (BUY)
            if prev_row['MACD'] <= prev_row['MACD_Signal'] and row['MACD'] > row['MACD_Signal'] and position == 0:
                shares = cash / row['Close']
                position = shares
                trades.append({
                    'date': df.index[i].strftime('%Y-%m-%d'),
                    'type': 'BUY',
                    'price': round(row['Close'], 2),
                    'shares': round(shares, 4),
                    'reason': 'MACD Bullish Cross'
                })
                cash = 0

            # MACD crosses below signal line (SELL)
            elif prev_row['MACD'] >= prev_row['MACD_Signal'] and row['MACD'] < row['MACD_Signal'] and position > 0:
                cash = position * row['Close']
                trades.append({
                    'date': df.index[i].strftime('%Y-%m-%d'),
                    'type': 'SELL',
                    'price': round(row['Close'], 2),
                    'shares': round(position, 4),
                    'value': round(cash, 2),
                    'reason': 'MACD Bearish Cross'
                })
                position = 0

        # Close position if still open
        if position > 0:
            cash = position * df.iloc[-1]['Close']
            trades.append({
                'date': df.index[-1].strftime('%Y-%m-%d'),
                'type': 'SELL',
                'price': round(df.iloc[-1]['Close'], 2),
                'shares': round(position, 4),
                'value': round(cash, 2),
                'reason': 'End of period'
            })

        return self._analyze_trades(trades, 'MACD Strategy',
                                     'Buy when MACD crosses above signal, Sell when crosses below')

    def backtest_bollinger_bands(self, df):
        """Bollinger Bands Mean Reversion Strategy"""
        trades = []
        position = 0
        cash = self.capital

        for i in range(20, len(df)):
            row = df.iloc[i]

            # Price touches lower band (BUY - oversold)
            if row['Close'] <= row['BB_Lower'] and position == 0:
                shares = cash / row['Close']
                position = shares
                trades.append({
                    'date': df.index[i].strftime('%Y-%m-%d'),
                    'type': 'BUY',
                    'price': round(row['Close'], 2),
                    'shares': round(shares, 4),
                    'reason': 'Price at Lower Bollinger Band'
                })
                cash = 0

            # Price touches upper band (SELL - overbought)
            elif row['Close'] >= row['BB_Upper'] and position > 0:
                cash = position * row['Close']
                trades.append({
                    'date': df.index[i].strftime('%Y-%m-%d'),
                    'type': 'SELL',
                    'price': round(row['Close'], 2),
                    'shares': round(position, 4),
                    'value': round(cash, 2),
                    'reason': 'Price at Upper Bollinger Band'
                })
                position = 0

        # Close position if still open
        if position > 0:
            cash = position * df.iloc[-1]['Close']
            trades.append({
                'date': df.index[-1].strftime('%Y-%m-%d'),
                'type': 'SELL',
                'price': round(df.iloc[-1]['Close'], 2),
                'shares': round(position, 4),
                'value': round(cash, 2),
                'reason': 'End of period'
            })

        return self._analyze_trades(trades, 'Bollinger Bands Mean Reversion',
                                     'Buy at lower band, Sell at upper band')

    def backtest_momentum(self, df):
        """Momentum Strategy (20-day breakout)"""
        trades = []
        position = 0
        cash = self.capital

        for i in range(20, len(df)):
            row = df.iloc[i]
            prev_20_high = df.iloc[i-20:i]['High'].max()
            prev_20_low = df.iloc[i-20:i]['Low'].min()

            # Breakout above 20-day high (BUY)
            if row['Close'] > prev_20_high and position == 0:
                shares = cash / row['Close']
                position = shares
                entry_price = row['Close']
                trades.append({
                    'date': df.index[i].strftime('%Y-%m-%d'),
                    'type': 'BUY',
                    'price': round(row['Close'], 2),
                    'shares': round(shares, 4),
                    'reason': '20-Day High Breakout'
                })
                cash = 0

            # Stop loss: 8% below entry OR breakdown below 20-day low
            elif position > 0:
                if row['Close'] < entry_price * 0.92 or row['Close'] < prev_20_low:
                    cash = position * row['Close']
                    reason = '8% Stop Loss' if row['Close'] < entry_price * 0.92 else '20-Day Low Breakdown'
                    trades.append({
                        'date': df.index[i].strftime('%Y-%m-%d'),
                        'type': 'SELL',
                        'price': round(row['Close'], 2),
                        'shares': round(position, 4),
                        'value': round(cash, 2),
                        'reason': reason
                    })
                    position = 0

        # Close position if still open
        if position > 0:
            cash = position * df.iloc[-1]['Close']
            trades.append({
                'date': df.index[-1].strftime('%Y-%m-%d'),
                'type': 'SELL',
                'price': round(df.iloc[-1]['Close'], 2),
                'shares': round(position, 4),
                'value': round(cash, 2),
                'reason': 'End of period'
            })

        return self._analyze_trades(trades, 'Momentum Breakout',
                                     'Buy on 20-day high breakout, Exit on 8% stop loss or 20-day low')

    def backtest_monthly_seasonal(self, df):
        """Monthly Seasonal Pattern Strategy"""
        # Calculate monthly returns
        monthly_data = defaultdict(list)

        for i in range(1, len(df)):
            prev_close = df.iloc[i-1]['Close']
            curr_close = df.iloc[i]['Close']
            daily_return = ((curr_close - prev_close) / prev_close) * 100
            month_name = df.index[i].strftime('%B')
            monthly_data[month_name].append(daily_return)

        # Find best and worst months
        month_order = ['January', 'February', 'March', 'April', 'May', 'June',
                      'July', 'August', 'September', 'October', 'November', 'December']

        monthly_stats = []
        for month in month_order:
            if month in monthly_data and len(monthly_data[month]) > 0:
                returns = monthly_data[month]
                avg_return = statistics.mean(returns)
                monthly_stats.append({
                    'month': month,
                    'avgReturn': avg_return
                })

        monthly_stats_sorted = sorted(monthly_stats, key=lambda x: x['avgReturn'], reverse=True)
        best_months = [m['month'] for m in monthly_stats_sorted[:4]]

        # Backtest: Buy in best months only
        trades = []
        position = 0
        cash = self.capital

        for i in range(len(df)):
            row = df.iloc[i]
            month_name = df.index[i].strftime('%B')

            # Buy on first trading day of best months
            if month_name in best_months and position == 0 and df.index[i].day <= 5:
                shares = cash / row['Close']
                position = shares
                entry_price = row['Close']
                trades.append({
                    'date': df.index[i].strftime('%Y-%m-%d'),
                    'type': 'BUY',
                    'price': round(row['Close'], 2),
                    'shares': round(shares, 4),
                    'reason': f'Favorable Month ({month_name})'
                })
                cash = 0

            # Sell at end of best month or on stop loss
            elif position > 0:
                # Stop loss: 8%
                if row['Close'] < entry_price * 0.92:
                    cash = position * row['Close']
                    trades.append({
                        'date': df.index[i].strftime('%Y-%m-%d'),
                        'type': 'SELL',
                        'price': round(row['Close'], 2),
                        'shares': round(position, 4),
                        'value': round(cash, 2),
                        'reason': '8% Stop Loss'
                    })
                    position = 0
                # Exit at end of favorable month
                elif month_name not in best_months and df.index[i].day <= 5:
                    cash = position * row['Close']
                    trades.append({
                        'date': df.index[i].strftime('%Y-%m-%d'),
                        'type': 'SELL',
                        'price': round(row['Close'], 2),
                        'shares': round(position, 4),
                        'value': round(cash, 2),
                        'reason': 'End of Favorable Month'
                    })
                    position = 0

        # Close position if still open
        if position > 0:
            cash = position * df.iloc[-1]['Close']
            trades.append({
                'date': df.index[-1].strftime('%Y-%m-%d'),
                'type': 'SELL',
                'price': round(df.iloc[-1]['Close'], 2),
                'shares': round(position, 4),
                'value': round(cash, 2),
                'reason': 'End of period'
            })

        result = self._analyze_trades(trades, 'Monthly Seasonal Pattern',
                                      f'Buy during best months: {", ".join(best_months)}')
        result['bestMonths'] = best_months
        result['monthlyStats'] = [{'month': m['month'], 'avgReturn': round(m['avgReturn'], 2)}
                                  for m in monthly_stats_sorted]
        return result

    def validate_trade_sequence(self, trades, strategy_name):
        """Validate that all trades follow buy-before-sell rule"""
        if not self.enforce_buy_first:
            return True, []

        errors = []
        position_open = False

        for i, trade in enumerate(trades):
            if trade['type'] == 'BUY':
                if position_open:
                    errors.append(f"⚠️  Trade #{i+1}: Attempted to BUY while position already open on {trade['date']}")
                position_open = True
            elif trade['type'] == 'SELL':
                if not position_open:
                    errors.append(f"❌ Trade #{i+1}: Attempted to SELL without owning stock on {trade['date']}")
                position_open = False

        if errors:
            log(f"\n⚠️  VALIDATION ERRORS for {strategy_name}:")
            for error in errors:
                log(f"   {error}")
            return False, errors
        else:
            log(f"✓ Trade validation passed for {strategy_name} ({len(trades)} trades)")
            return True, []

    def _analyze_trades(self, trades, name, description):
        """Analyze a list of trades and calculate metrics"""
        # Validate trade sequence
        is_valid, validation_errors = self.validate_trade_sequence(trades, name)

        if not trades:
            return {
                'name': name,
                'description': description,
                'initialValue': self.capital,
                'finalValue': self.capital,
                'totalReturn': 0,
                'annualReturn': 0,
                'trades': 0,
                'winRate': 0,
                'error': 'No trades executed'
            }

        # If validation fails and enforcement is on, return error
        if not is_valid and self.enforce_buy_first:
            return {
                'name': name,
                'description': description,
                'initialValue': self.capital,
                'finalValue': self.capital,
                'totalReturn': 0,
                'annualReturn': 0,
                'trades': 0,
                'winRate': 0,
                'error': 'Trade sequence validation failed: ' + '; '.join(validation_errors)
            }

        # Calculate profits for each trade pair
        profits = []
        for i in range(0, len(trades), 2):
            if i + 1 < len(trades) and trades[i]['type'] == 'BUY' and trades[i+1]['type'] == 'SELL':
                buy_value = trades[i]['shares'] * trades[i]['price']
                sell_value = trades[i+1].get('value', trades[i+1]['shares'] * trades[i+1]['price'])
                profit_pct = ((sell_value - buy_value) / buy_value) * 100
                profits.append(profit_pct)

        # Get final value
        final_value = self.capital
        if trades and trades[-1]['type'] == 'SELL':
            final_value = trades[-1].get('value', self.capital)

        total_return = ((final_value - self.capital) / self.capital) * 100
        annual_return = total_return / self.years

        winning_trades = [p for p in profits if p > 0]
        losing_trades = [p for p in profits if p < 0]
        win_rate = (len(winning_trades) / len(profits) * 100) if profits else 0

        avg_win = statistics.mean(winning_trades) if winning_trades else 0
        avg_loss = statistics.mean(losing_trades) if losing_trades else 0

        return {
            'name': name,
            'description': description,
            'initialValue': self.capital,
            'finalValue': round(final_value, 2),
            'totalReturn': round(total_return, 2),
            'annualReturn': round(annual_return, 2),
            'trades': len([t for t in trades if t['type'] == 'BUY']),
            'winningTrades': len(winning_trades),
            'losingTrades': len(losing_trades),
            'winRate': round(win_rate, 1),
            'avgWin': round(avg_win, 2),
            'avgLoss': round(avg_loss, 2),
            'riskRewardRatio': round(abs(avg_win / avg_loss), 2) if avg_loss != 0 else 0,
            'recentTrades': trades[-5:],  # Last 5 trades
            'allTrades': trades
        }

    def _calculate_sharpe(self, returns, risk_free_rate=0.02):
        """Calculate Sharpe Ratio"""
        if len(returns) < 2:
            return 0
        excess_returns = returns - (risk_free_rate / 252)  # Daily risk-free rate
        return round(np.sqrt(252) * excess_returns.mean() / excess_returns.std(), 2) if excess_returns.std() != 0 else 0

    def _calculate_valuation_metrics(self):
        """Calculate valuation metrics to determine if stock is over/undervalued"""
        current_price = self.info.get('currentPrice', self.hist.iloc[-1]['Close'])

        # Get valuation ratios
        pe_trailing = self.info.get('trailingPE')
        pe_forward = self.info.get('forwardPE')
        pb_ratio = self.info.get('priceToBook')

        # Get 52-week range
        fifty_two_week_high = self.info.get('fiftyTwoWeekHigh')
        fifty_two_week_low = self.info.get('fiftyTwoWeekLow')

        # Calculate position in 52-week range (0-100%)
        range_position = None
        if fifty_two_week_high and fifty_two_week_low and fifty_two_week_high != fifty_two_week_low:
            range_position = ((current_price - fifty_two_week_low) /
                            (fifty_two_week_high - fifty_two_week_low)) * 100

        # Get analyst target
        target_price = self.info.get('targetMeanPrice')
        upside_potential = None
        if target_price and current_price:
            upside_potential = ((target_price - current_price) / current_price) * 100

        # Determine valuation status
        valuation_signals = []
        undervalued_count = 0
        overvalued_count = 0

        # P/E ratio analysis (use trailing if available, else forward)
        pe_ratio = pe_trailing if pe_trailing else pe_forward
        if pe_ratio:
            if pe_ratio < 15:
                valuation_signals.append('Low P/E (Undervalued)')
                undervalued_count += 2
            elif pe_ratio > 30:
                valuation_signals.append('High P/E (Overvalued)')
                overvalued_count += 2
            else:
                valuation_signals.append('Moderate P/E (Fair)')

        # P/B ratio analysis
        if pb_ratio:
            if pb_ratio < 1:
                valuation_signals.append('Low P/B (Undervalued)')
                undervalued_count += 1
            elif pb_ratio > 3:
                valuation_signals.append('High P/B (Overvalued)')
                overvalued_count += 1
            else:
                valuation_signals.append('Moderate P/B (Fair)')

        # 52-week range analysis
        if range_position is not None:
            if range_position < 30:
                valuation_signals.append('Near 52-week Low (Potential Bargain)')
                undervalued_count += 1
            elif range_position > 90:
                valuation_signals.append('Near 52-week High (Potentially Overbought)')
                overvalued_count += 1
            elif range_position > 70:
                valuation_signals.append('Upper Range (Strong Momentum)')

        # Analyst target analysis
        if upside_potential is not None:
            if upside_potential > 20:
                valuation_signals.append(f'Analyst Target +{upside_potential:.1f}% Upside')
                undervalued_count += 1
            elif upside_potential < -10:
                valuation_signals.append(f'Analyst Target {upside_potential:.1f}% Downside')
                overvalued_count += 1

        # Overall assessment
        if undervalued_count > overvalued_count + 1:
            overall = 'Undervalued'
            status = 'discount'
        elif overvalued_count > undervalued_count + 1:
            overall = 'Overvalued'
            status = 'premium'
        else:
            overall = 'Fairly Valued'
            status = 'fair'

        log(f"\n{'='*80}")
        log(f"VALUATION ANALYSIS")
        log(f"{'='*80}")
        log(f"Current Price: ${current_price:.2f}")
        if pe_ratio:
            log(f"P/E Ratio: {pe_ratio:.2f}")
        if pb_ratio:
            log(f"P/B Ratio: {pb_ratio:.2f}")
        if range_position is not None:
            log(f"52-Week Range Position: {range_position:.1f}%")
        if upside_potential is not None:
            log(f"Analyst Target Upside: {upside_potential:+.1f}%")
        log(f"\nOverall Assessment: {overall}")
        for signal in valuation_signals:
            log(f"  • {signal}")
        log(f"{'='*80}\n")

        return {
            'peRatio': round(pe_ratio, 2) if pe_ratio else None,
            'pbRatio': round(pb_ratio, 2) if pb_ratio else None,
            'fiftyTwoWeekHigh': round(fifty_two_week_high, 2) if fifty_two_week_high else None,
            'fiftyTwoWeekLow': round(fifty_two_week_low, 2) if fifty_two_week_low else None,
            'rangePosition': round(range_position, 1) if range_position is not None else None,
            'targetPrice': round(target_price, 2) if target_price else None,
            'upsidePotential': round(upside_potential, 1) if upside_potential is not None else None,
            'overallAssessment': overall,
            'status': status,
            'signals': valuation_signals
        }

    def analyze_all_strategies(self):
        """Run all strategy backtests"""
        if not self.fetch_data():
            return None

        log("\nCalculating indicators...")
        df = self.calculate_indicators()

        log("Running strategy backtests...\n")

        strategies = {
            'buyHold': self.backtest_buy_hold(df),
            'smaCrossover': self.backtest_sma_crossover(df),
            'rsi': self.backtest_rsi(df),
            'macd': self.backtest_macd(df),
            'bollingerBands': self.backtest_bollinger_bands(df),
            'momentum': self.backtest_momentum(df),
            'monthlySeasonal': self.backtest_monthly_seasonal(df)
        }

        # Sort strategies by total return
        strategies_sorted = sorted(strategies.items(),
                                  key=lambda x: x[1].get('totalReturn', 0),
                                  reverse=True)

        log("="*80)
        log("STRATEGY COMPARISON")
        log("="*80)
        for key, strategy in strategies_sorted:
            log(f"{strategy['name']:30} | Return: {strategy.get('totalReturn', 0):+7.2f}% | "
                f"Win Rate: {strategy.get('winRate', 0):5.1f}% | Trades: {strategy.get('trades', 0):3}")

        best_strategy = strategies_sorted[0]
        log(f"\n✓ BEST STRATEGY: {best_strategy[1]['name']} ({best_strategy[1].get('totalReturn', 0):+.2f}%)")

        # Calculate valuation metrics
        valuation = self._calculate_valuation_metrics()

        return {
            'success': True,
            'symbol': self.symbol,
            'companyName': self.info.get('longName', self.symbol),
            'currentPrice': round(self.info.get('currentPrice', self.hist.iloc[-1]['Close']), 2),
            'capital': self.capital,
            'period': f"{self.years} years",
            'strategies': strategies,
            'bestStrategy': best_strategy[0],
            'valuation': valuation,
            'fetched_at': datetime.now().isoformat()
        }

if __name__ == '__main__':
    if len(sys.argv) < 2:
        log("Usage: python multi_strategy_analyzer.py SYMBOL [CAPITAL] [YEARS] [ENFORCE_BUY_FIRST]")
        log("Example: python multi_strategy_analyzer.py AAPL 100 5")
        log("Example: python multi_strategy_analyzer.py AAPL 100 5 true  (enforce buy-before-sell)")
        log("Example: python multi_strategy_analyzer.py AAPL 100 5 false (allow any trade sequence)")
        sys.exit(1)

    symbol = sys.argv[1].upper()
    capital = float(sys.argv[2]) if len(sys.argv) > 2 else 100
    years = int(sys.argv[3]) if len(sys.argv) > 3 else 5
    enforce_buy_first = sys.argv[4].lower() == 'true' if len(sys.argv) > 4 else True

    log(f"Trade Validation: {'✓ ENFORCED (buy before sell required)' if enforce_buy_first else '⚠️ DISABLED (any sequence allowed)'}\n")

    analyzer = StrategyAnalyzer(symbol, capital, years, enforce_buy_first)
    result = analyzer.analyze_all_strategies()

    if result and result.get('success'):
        result['enforceBuyFirst'] = enforce_buy_first
        print(json.dumps(result, indent=2))
    else:
        sys.exit(1)
