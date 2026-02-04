#!/usr/bin/env python3
"""
Backtest single commodity futures with multiple strategies and stop-loss methods

Usage: python backtest_single_commodity.py <symbol> <strategy> <capital> <years> <stopLossMethod> <stopLossValue>
Example: python backtest_single_commodity.py GC=F rsi 10000 5 atr 2.0
"""

import sys
import json
import yfinance as yf
import pandas as pd
import numpy as np
from datetime import datetime, timedelta
import sqlite3
import os

# Technical indicator calculations
def calculate_sma(prices, period):
    """Calculate Simple Moving Average"""
    return prices.rolling(window=period).mean()

def calculate_ema(prices, period):
    """Calculate Exponential Moving Average"""
    return prices.ewm(span=period, adjust=False).mean()

def calculate_rsi(prices, period=14):
    """Calculate Relative Strength Index"""
    delta = prices.diff()
    gain = (delta.where(delta > 0, 0)).rolling(window=period).mean()
    loss = (-delta.where(delta < 0, 0)).rolling(window=period).mean()
    rs = gain / loss
    rsi = 100 - (100 / (1 + rs))
    return rsi

def calculate_macd(prices, fast=12, slow=26, signal=9):
    """Calculate MACD indicator"""
    ema_fast = calculate_ema(prices, fast)
    ema_slow = calculate_ema(prices, slow)
    macd_line = ema_fast - ema_slow
    signal_line = calculate_ema(macd_line, signal)
    return macd_line, signal_line

def calculate_bollinger_bands(prices, period=20, std_dev=2):
    """Calculate Bollinger Bands"""
    sma = calculate_sma(prices, period)
    std = prices.rolling(window=period).std()
    upper_band = sma + (std * std_dev)
    lower_band = sma - (std * std_dev)
    return upper_band, sma, lower_band

def calculate_atr(df, period=14):
    """Calculate Average True Range"""
    high_low = df['High'] - df['Low']
    high_close = np.abs(df['High'] - df['Close'].shift())
    low_close = np.abs(df['Low'] - df['Close'].shift())
    ranges = pd.concat([high_low, high_close, low_close], axis=1)
    true_range = np.max(ranges, axis=1)
    atr = true_range.rolling(period).mean()
    return atr

def calculate_volatility(prices, period=20):
    """Calculate historical volatility (annualized)"""
    log_returns = np.log(prices / prices.shift(1))
    volatility = log_returns.rolling(period).std() * np.sqrt(252) * 100
    return volatility

# Get commodity contract specifications
def get_commodity_specs(symbol):
    """Get commodity contract specifications"""
    specs = {
        'GC=F': {'name': 'Gold', 'contractSize': 100, 'tickSize': 0.10, 'tickValue': 10.00, 'margin': 8000},
        'SI=F': {'name': 'Silver', 'contractSize': 5000, 'tickSize': 0.005, 'tickValue': 25.00, 'margin': 6000},
        'HG=F': {'name': 'Copper', 'contractSize': 25000, 'tickSize': 0.0005, 'tickValue': 12.50, 'margin': 5500},
        'PL=F': {'name': 'Platinum', 'contractSize': 50, 'tickSize': 0.10, 'tickValue': 5.00, 'margin': 5000},
        'CL=F': {'name': 'Crude Oil', 'contractSize': 1000, 'tickSize': 0.01, 'tickValue': 10.00, 'margin': 5000},
        'NG=F': {'name': 'Natural Gas', 'contractSize': 10000, 'tickSize': 0.001, 'tickValue': 10.00, 'margin': 3000},
        'HO=F': {'name': 'Heating Oil', 'contractSize': 42000, 'tickSize': 0.0001, 'tickValue': 4.20, 'margin': 4500}
    }
    return specs.get(symbol, {'name': symbol, 'contractSize': 1, 'tickSize': 0.01, 'tickValue': 1.00, 'margin': 5000})

# Get CME cost profile from database
def get_cme_costs(symbol):
    """Get CME broker costs from database"""
    db_path = os.path.join(os.path.dirname(__file__), '..', 'dividends.db')

    try:
        conn = sqlite3.connect(db_path)
        cursor = conn.cursor()

        # Try to get symbol-specific costs first
        cursor.execute("""
            SELECT CommissionPerContract, ExchangeFeePerContract, ClearingFeePerContract,
                   OvernightFinancingRate, MarginInterestRate
            FROM CmeCostProfiles
            WHERE CommoditySymbol = ? AND IsActive = 1
        """, (symbol,))

        result = cursor.fetchone()

        # If not found, get default costs
        if not result:
            cursor.execute("""
                SELECT CommissionPerContract, ExchangeFeePerContract, ClearingFeePerContract,
                       OvernightFinancingRate, MarginInterestRate
                FROM CmeCostProfiles
                WHERE CommoditySymbol = 'ALL' AND IsActive = 1
            """)
            result = cursor.fetchone()

        conn.close()

        if result:
            return {
                'commission': float(result[0]),
                'exchangeFee': float(result[1]),
                'clearingFee': float(result[2]),
                'overnightRate': float(result[3]),
                'marginRate': float(result[4])
            }
    except:
        pass

    # Default costs if database lookup fails
    return {
        'commission': 2.50,
        'exchangeFee': 1.50,
        'clearingFee': 0.50,
        'overnightRate': 0.000137,  # ~5% annually
        'marginRate': 0.05
    }

# Calculate stop-loss price
def calculate_stop_loss(entry_price, method, value, atr=None, volatility=None, direction='long'):
    """Calculate stop-loss price based on method"""
    if method == 'atr' and atr is not None:
        stop_distance = float(value) * atr
        return entry_price - stop_distance if direction == 'long' else entry_price + stop_distance

    elif method == 'percentage':
        percentage = float(value) / 100
        return entry_price * (1 - percentage) if direction == 'long' else entry_price * (1 + percentage)

    elif method == 'volatility' and volatility is not None:
        # Volatility-adjusted: entry ± (multiplier × volatility)
        vol_distance = entry_price * (float(value) * volatility / 100 / 100)
        return entry_price - vol_distance if direction == 'long' else entry_price + vol_distance

    elif method == 'fixed':
        # Fixed dollar amount
        return entry_price - float(value) if direction == 'long' else entry_price + float(value)

    return None

# Trading strategies
def strategy_buy_hold(df, capital, specs, costs, stop_loss_method, stop_loss_value):
    """Buy and hold strategy"""
    trades = []

    # Buy on first day
    entry_price = df['Close'].iloc[0]
    contracts = int(capital / specs['margin'])

    if contracts < 1:
        return [], 0, capital

    # Calculate stop-loss
    atr = df['ATR14'].iloc[0] if 'ATR14' in df.columns else None
    vol = df['Volatility20'].iloc[0] if 'Volatility20' in df.columns else None
    stop_price = calculate_stop_loss(entry_price, stop_loss_method, stop_loss_value, atr, vol, 'long')

    # Entry trade
    entry_costs = (costs['commission'] + costs['exchangeFee'] + costs['clearingFee']) * contracts
    trades.append({
        'date': df.index[0].strftime('%Y-%m-%d'),
        'type': 'BUY',
        'price': float(entry_price),
        'contracts': contracts,
        'reason': 'Initial entry',
        'stopLossPrice': float(stop_price) if stop_price else None,
        'takeProfitPrice': None,
        'pnl': None,
        'commission': entry_costs,
        'exchangeFees': 0,
        'clearingFees': 0,
        'overnightFinancing': 0
    })

    position_value = entry_price * specs['contractSize'] * contracts
    remaining_capital = capital - (specs['margin'] * contracts) - entry_costs
    stop_hit = False
    days_held = 0

    # Hold until end or stop-loss
    for i in range(1, len(df)):
        current_price = df['Close'].iloc[i]
        days_held += 1

        # Check stop-loss
        if stop_price and current_price <= stop_price:
            # Stop-loss hit
            exit_price = stop_price
            exit_costs = (costs['commission'] + costs['exchangeFee'] + costs['clearingFee']) * contracts
            overnight_costs = specs['margin'] * contracts * costs['overnightRate'] * days_held

            pnl = (exit_price - entry_price) * specs['contractSize'] * contracts
            total_costs = entry_costs + exit_costs + overnight_costs

            trades.append({
                'date': df.index[i].strftime('%Y-%m-%d'),
                'type': 'SELL',
                'price': float(exit_price),
                'contracts': contracts,
                'reason': 'Stop-loss triggered',
                'stopLossPrice': None,
                'takeProfitPrice': None,
                'pnl': float(pnl),
                'commission': exit_costs,
                'exchangeFees': 0,
                'clearingFees': 0,
                'overnightFinancing': float(overnight_costs)
            })

            final_value = remaining_capital + (specs['margin'] * contracts) + pnl - total_costs
            stop_hit = True
            break

    # If didn't hit stop-loss, close at end
    if not stop_hit:
        exit_price = df['Close'].iloc[-1]
        exit_costs = (costs['commission'] + costs['exchangeFee'] + costs['clearingFee']) * contracts
        overnight_costs = specs['margin'] * contracts * costs['overnightRate'] * days_held

        pnl = (exit_price - entry_price) * specs['contractSize'] * contracts
        total_costs = entry_costs + exit_costs + overnight_costs

        trades.append({
            'date': df.index[-1].strftime('%Y-%m-%d'),
            'type': 'SELL',
            'price': float(exit_price),
            'contracts': contracts,
            'reason': 'End of period',
            'stopLossPrice': None,
            'takeProfitPrice': None,
            'pnl': float(pnl),
            'commission': exit_costs,
            'exchangeFees': 0,
            'clearingFees': 0,
            'overnightFinancing': float(overnight_costs)
        })

        final_value = remaining_capital + (specs['margin'] * contracts) + pnl - total_costs

    total_costs_sum = sum(t.get('commission', 0) + t.get('overnightFinancing', 0) for t in trades)

    return trades, total_costs_sum, final_value

def strategy_sma_crossover(df, capital, specs, costs, stop_loss_method, stop_loss_value):
    """SMA Crossover Strategy (50/200)"""
    df = df.copy()
    df['SMA50'] = calculate_sma(df['Close'], 50)
    df['SMA200'] = calculate_sma(df['Close'], 200)

    trades = []
    position = None
    remaining_capital = capital
    total_costs = 0

    # Wait for SMA200 to be valid
    start_idx = 200

    for i in range(start_idx, len(df)):
        current_price = df['Close'].iloc[i]
        sma50 = df['SMA50'].iloc[i]
        sma200 = df['SMA200'].iloc[i]
        sma50_prev = df['SMA50'].iloc[i-1]
        sma200_prev = df['SMA200'].iloc[i-1]

        # Entry: SMA50 crosses above SMA200
        if position is None and sma50 > sma200 and sma50_prev <= sma200_prev:
            contracts = int(remaining_capital / specs['margin'])
            if contracts >= 1:
                atr = df['ATR14'].iloc[i] if 'ATR14' in df.columns else None
                vol = df['Volatility20'].iloc[i] if 'Volatility20' in df.columns else None
                stop_price = calculate_stop_loss(current_price, stop_loss_method, stop_loss_value, atr, vol, 'long')

                entry_costs = (costs['commission'] + costs['exchangeFee'] + costs['clearingFee']) * contracts

                position = {
                    'entry_price': current_price,
                    'entry_date': df.index[i],
                    'contracts': contracts,
                    'stop_price': stop_price,
                    'entry_costs': entry_costs
                }

                trades.append({
                    'date': df.index[i].strftime('%Y-%m-%d'),
                    'type': 'BUY',
                    'price': float(current_price),
                    'contracts': contracts,
                    'reason': 'SMA50 crossed above SMA200',
                    'stopLossPrice': float(stop_price) if stop_price else None,
                    'takeProfitPrice': None,
                    'pnl': None,
                    'commission': entry_costs,
                    'exchangeFees': 0,
                    'clearingFees': 0,
                    'overnightFinancing': 0
                })

                remaining_capital -= (specs['margin'] * contracts) + entry_costs
                total_costs += entry_costs

        # Check stop-loss
        elif position and position['stop_price'] and current_price <= position['stop_price']:
            days_held = (df.index[i] - position['entry_date']).days
            exit_price = position['stop_price']
            exit_costs = (costs['commission'] + costs['exchangeFee'] + costs['clearingFee']) * position['contracts']
            overnight_costs = specs['margin'] * position['contracts'] * costs['overnightRate'] * days_held

            pnl = (exit_price - position['entry_price']) * specs['contractSize'] * position['contracts']

            trades.append({
                'date': df.index[i].strftime('%Y-%m-%d'),
                'type': 'SELL',
                'price': float(exit_price),
                'contracts': position['contracts'],
                'reason': 'Stop-loss triggered',
                'stopLossPrice': None,
                'takeProfitPrice': None,
                'pnl': float(pnl),
                'commission': exit_costs,
                'exchangeFees': 0,
                'clearingFees': 0,
                'overnightFinancing': float(overnight_costs)
            })

            remaining_capital += (specs['margin'] * position['contracts']) + pnl - exit_costs - overnight_costs
            total_costs += exit_costs + overnight_costs
            position = None

        # Exit: SMA50 crosses below SMA200
        elif position and sma50 < sma200 and sma50_prev >= sma200_prev:
            days_held = (df.index[i] - position['entry_date']).days
            exit_costs = (costs['commission'] + costs['exchangeFee'] + costs['clearingFee']) * position['contracts']
            overnight_costs = specs['margin'] * position['contracts'] * costs['overnightRate'] * days_held

            pnl = (current_price - position['entry_price']) * specs['contractSize'] * position['contracts']

            trades.append({
                'date': df.index[i].strftime('%Y-%m-%d'),
                'type': 'SELL',
                'price': float(current_price),
                'contracts': position['contracts'],
                'reason': 'SMA50 crossed below SMA200',
                'stopLossPrice': None,
                'takeProfitPrice': None,
                'pnl': float(pnl),
                'commission': exit_costs,
                'exchangeFees': 0,
                'clearingFees': 0,
                'overnightFinancing': float(overnight_costs)
            })

            remaining_capital += (specs['margin'] * position['contracts']) + pnl - exit_costs - overnight_costs
            total_costs += exit_costs + overnight_costs
            position = None

    # Close any open position at end
    if position:
        i = len(df) - 1
        days_held = (df.index[i] - position['entry_date']).days
        current_price = df['Close'].iloc[i]
        exit_costs = (costs['commission'] + costs['exchangeFee'] + costs['clearingFee']) * position['contracts']
        overnight_costs = specs['margin'] * position['contracts'] * costs['overnightRate'] * days_held

        pnl = (current_price - position['entry_price']) * specs['contractSize'] * position['contracts']

        trades.append({
            'date': df.index[i].strftime('%Y-%m-%d'),
            'type': 'SELL',
            'price': float(current_price),
            'contracts': position['contracts'],
            'reason': 'End of period',
            'stopLossPrice': None,
            'takeProfitPrice': None,
            'pnl': float(pnl),
            'commission': exit_costs,
            'exchangeFees': 0,
            'clearingFees': 0,
            'overnightFinancing': float(overnight_costs)
        })

        remaining_capital += (specs['margin'] * position['contracts']) + pnl - exit_costs - overnight_costs
        total_costs += exit_costs + overnight_costs

    final_value = remaining_capital
    return trades, total_costs, final_value

def strategy_rsi(df, capital, specs, costs, stop_loss_method, stop_loss_value, oversold=30, overbought=70):
    """RSI Strategy (30/70 thresholds)"""
    df = df.copy()
    df['RSI'] = calculate_rsi(df['Close'], 14)

    trades = []
    position = None
    remaining_capital = capital
    total_costs = 0

    # Wait for RSI to be valid
    start_idx = 14

    for i in range(start_idx, len(df)):
        current_price = df['Close'].iloc[i]
        rsi = df['RSI'].iloc[i]
        rsi_prev = df['RSI'].iloc[i-1]

        # Entry: RSI crosses above oversold level
        if position is None and rsi > oversold and rsi_prev <= oversold:
            contracts = int(remaining_capital / specs['margin'])
            if contracts >= 1:
                atr = df['ATR14'].iloc[i] if 'ATR14' in df.columns else None
                vol = df['Volatility20'].iloc[i] if 'Volatility20' in df.columns else None
                stop_price = calculate_stop_loss(current_price, stop_loss_method, stop_loss_value, atr, vol, 'long')

                entry_costs = (costs['commission'] + costs['exchangeFee'] + costs['clearingFee']) * contracts

                position = {
                    'entry_price': current_price,
                    'entry_date': df.index[i],
                    'contracts': contracts,
                    'stop_price': stop_price,
                    'entry_costs': entry_costs
                }

                trades.append({
                    'date': df.index[i].strftime('%Y-%m-%d'),
                    'type': 'BUY',
                    'price': float(current_price),
                    'contracts': contracts,
                    'reason': f'RSI crossed above {oversold} (oversold)',
                    'stopLossPrice': float(stop_price) if stop_price else None,
                    'takeProfitPrice': None,
                    'pnl': None,
                    'commission': entry_costs,
                    'exchangeFees': 0,
                    'clearingFees': 0,
                    'overnightFinancing': 0
                })

                remaining_capital -= (specs['margin'] * contracts) + entry_costs
                total_costs += entry_costs

        # Check stop-loss
        elif position and position['stop_price'] and current_price <= position['stop_price']:
            days_held = (df.index[i] - position['entry_date']).days
            exit_price = position['stop_price']
            exit_costs = (costs['commission'] + costs['exchangeFee'] + costs['clearingFee']) * position['contracts']
            overnight_costs = specs['margin'] * position['contracts'] * costs['overnightRate'] * days_held

            pnl = (exit_price - position['entry_price']) * specs['contractSize'] * position['contracts']

            trades.append({
                'date': df.index[i].strftime('%Y-%m-%d'),
                'type': 'SELL',
                'price': float(exit_price),
                'contracts': position['contracts'],
                'reason': 'Stop-loss triggered',
                'stopLossPrice': None,
                'takeProfitPrice': None,
                'pnl': float(pnl),
                'commission': exit_costs,
                'exchangeFees': 0,
                'clearingFees': 0,
                'overnightFinancing': float(overnight_costs)
            })

            remaining_capital += (specs['margin'] * position['contracts']) + pnl - exit_costs - overnight_costs
            total_costs += exit_costs + overnight_costs
            position = None

        # Exit: RSI crosses below overbought level
        elif position and rsi < overbought and rsi_prev >= overbought:
            days_held = (df.index[i] - position['entry_date']).days
            exit_costs = (costs['commission'] + costs['exchangeFee'] + costs['clearingFee']) * position['contracts']
            overnight_costs = specs['margin'] * position['contracts'] * costs['overnightRate'] * days_held

            pnl = (current_price - position['entry_price']) * specs['contractSize'] * position['contracts']

            trades.append({
                'date': df.index[i].strftime('%Y-%m-%d'),
                'type': 'SELL',
                'price': float(current_price),
                'contracts': position['contracts'],
                'reason': f'RSI crossed below {overbought} (overbought)',
                'stopLossPrice': None,
                'takeProfitPrice': None,
                'pnl': float(pnl),
                'commission': exit_costs,
                'exchangeFees': 0,
                'clearingFees': 0,
                'overnightFinancing': float(overnight_costs)
            })

            remaining_capital += (specs['margin'] * position['contracts']) + pnl - exit_costs - overnight_costs
            total_costs += exit_costs + overnight_costs
            position = None

    # Close any open position at end
    if position:
        i = len(df) - 1
        days_held = (df.index[i] - position['entry_date']).days
        current_price = df['Close'].iloc[i]
        exit_costs = (costs['commission'] + costs['exchangeFee'] + costs['clearingFee']) * position['contracts']
        overnight_costs = specs['margin'] * position['contracts'] * costs['overnightRate'] * days_held

        pnl = (current_price - position['entry_price']) * specs['contractSize'] * position['contracts']

        trades.append({
            'date': df.index[i].strftime('%Y-%m-%d'),
            'type': 'SELL',
            'price': float(current_price),
            'contracts': position['contracts'],
            'reason': 'End of period',
            'stopLossPrice': None,
            'takeProfitPrice': None,
            'pnl': float(pnl),
            'commission': exit_costs,
            'exchangeFees': 0,
            'clearingFees': 0,
            'overnightFinancing': float(overnight_costs)
        })

        remaining_capital += (specs['margin'] * position['contracts']) + pnl - exit_costs - overnight_costs
        total_costs += exit_costs + overnight_costs

    final_value = remaining_capital
    return trades, total_costs, final_value

def strategy_macd(df, capital, specs, costs, stop_loss_method, stop_loss_value):
    """MACD Crossover Strategy"""
    df = df.copy()
    macd_line, signal_line = calculate_macd(df['Close'])
    df['MACD'] = macd_line
    df['Signal'] = signal_line

    trades = []
    position = None
    remaining_capital = capital
    total_costs = 0

    # Wait for MACD to be valid
    start_idx = 26

    for i in range(start_idx, len(df)):
        current_price = df['Close'].iloc[i]
        macd = df['MACD'].iloc[i]
        signal = df['Signal'].iloc[i]
        macd_prev = df['MACD'].iloc[i-1]
        signal_prev = df['Signal'].iloc[i-1]

        # Entry: MACD crosses above Signal
        if position is None and macd > signal and macd_prev <= signal_prev:
            contracts = int(remaining_capital / specs['margin'])
            if contracts >= 1:
                atr = df['ATR14'].iloc[i] if 'ATR14' in df.columns else None
                vol = df['Volatility20'].iloc[i] if 'Volatility20' in df.columns else None
                stop_price = calculate_stop_loss(current_price, stop_loss_method, stop_loss_value, atr, vol, 'long')

                entry_costs = (costs['commission'] + costs['exchangeFee'] + costs['clearingFee']) * contracts

                position = {
                    'entry_price': current_price,
                    'entry_date': df.index[i],
                    'contracts': contracts,
                    'stop_price': stop_price,
                    'entry_costs': entry_costs
                }

                trades.append({
                    'date': df.index[i].strftime('%Y-%m-%d'),
                    'type': 'BUY',
                    'price': float(current_price),
                    'contracts': contracts,
                    'reason': 'MACD crossed above Signal',
                    'stopLossPrice': float(stop_price) if stop_price else None,
                    'takeProfitPrice': None,
                    'pnl': None,
                    'commission': entry_costs,
                    'exchangeFees': 0,
                    'clearingFees': 0,
                    'overnightFinancing': 0
                })

                remaining_capital -= (specs['margin'] * contracts) + entry_costs
                total_costs += entry_costs

        # Check stop-loss
        elif position and position['stop_price'] and current_price <= position['stop_price']:
            days_held = (df.index[i] - position['entry_date']).days
            exit_price = position['stop_price']
            exit_costs = (costs['commission'] + costs['exchangeFee'] + costs['clearingFee']) * position['contracts']
            overnight_costs = specs['margin'] * position['contracts'] * costs['overnightRate'] * days_held

            pnl = (exit_price - position['entry_price']) * specs['contractSize'] * position['contracts']

            trades.append({
                'date': df.index[i].strftime('%Y-%m-%d'),
                'type': 'SELL',
                'price': float(exit_price),
                'contracts': position['contracts'],
                'reason': 'Stop-loss triggered',
                'stopLossPrice': None,
                'takeProfitPrice': None,
                'pnl': float(pnl),
                'commission': exit_costs,
                'exchangeFees': 0,
                'clearingFees': 0,
                'overnightFinancing': float(overnight_costs)
            })

            remaining_capital += (specs['margin'] * position['contracts']) + pnl - exit_costs - overnight_costs
            total_costs += exit_costs + overnight_costs
            position = None

        # Exit: MACD crosses below Signal
        elif position and macd < signal and macd_prev >= signal_prev:
            days_held = (df.index[i] - position['entry_date']).days
            exit_costs = (costs['commission'] + costs['exchangeFee'] + costs['clearingFee']) * position['contracts']
            overnight_costs = specs['margin'] * position['contracts'] * costs['overnightRate'] * days_held

            pnl = (current_price - position['entry_price']) * specs['contractSize'] * position['contracts']

            trades.append({
                'date': df.index[i].strftime('%Y-%m-%d'),
                'type': 'SELL',
                'price': float(current_price),
                'contracts': position['contracts'],
                'reason': 'MACD crossed below Signal',
                'stopLossPrice': None,
                'takeProfitPrice': None,
                'pnl': float(pnl),
                'commission': exit_costs,
                'exchangeFees': 0,
                'clearingFees': 0,
                'overnightFinancing': float(overnight_costs)
            })

            remaining_capital += (specs['margin'] * position['contracts']) + pnl - exit_costs - overnight_costs
            total_costs += exit_costs + overnight_costs
            position = None

    # Close any open position at end
    if position:
        i = len(df) - 1
        days_held = (df.index[i] - position['entry_date']).days
        current_price = df['Close'].iloc[i]
        exit_costs = (costs['commission'] + costs['exchangeFee'] + costs['clearingFee']) * position['contracts']
        overnight_costs = specs['margin'] * position['contracts'] * costs['overnightRate'] * days_held

        pnl = (current_price - position['entry_price']) * specs['contractSize'] * position['contracts']

        trades.append({
            'date': df.index[i].strftime('%Y-%m-%d'),
            'type': 'SELL',
            'price': float(current_price),
            'contracts': position['contracts'],
            'reason': 'End of period',
            'stopLossPrice': None,
            'takeProfitPrice': None,
            'pnl': float(pnl),
            'commission': exit_costs,
            'exchangeFees': 0,
            'clearingFees': 0,
            'overnightFinancing': float(overnight_costs)
        })

        remaining_capital += (specs['margin'] * position['contracts']) + pnl - exit_costs - overnight_costs
        total_costs += exit_costs + overnight_costs

    final_value = remaining_capital
    return trades, total_costs, final_value

def strategy_bollinger(df, capital, specs, costs, stop_loss_method, stop_loss_value):
    """Bollinger Bands Mean Reversion Strategy"""
    df = df.copy()
    upper, middle, lower = calculate_bollinger_bands(df['Close'])
    df['BB_Upper'] = upper
    df['BB_Middle'] = middle
    df['BB_Lower'] = lower

    trades = []
    position = None
    remaining_capital = capital
    total_costs = 0

    # Wait for Bollinger Bands to be valid
    start_idx = 20

    for i in range(start_idx, len(df)):
        current_price = df['Close'].iloc[i]
        bb_lower = df['BB_Lower'].iloc[i]
        bb_middle = df['BB_Middle'].iloc[i]
        bb_upper = df['BB_Upper'].iloc[i]

        # Entry: Price touches or breaks below lower band
        if position is None and current_price <= bb_lower:
            contracts = int(remaining_capital / specs['margin'])
            if contracts >= 1:
                atr = df['ATR14'].iloc[i] if 'ATR14' in df.columns else None
                vol = df['Volatility20'].iloc[i] if 'Volatility20' in df.columns else None
                stop_price = calculate_stop_loss(current_price, stop_loss_method, stop_loss_value, atr, vol, 'long')

                entry_costs = (costs['commission'] + costs['exchangeFee'] + costs['clearingFee']) * contracts

                position = {
                    'entry_price': current_price,
                    'entry_date': df.index[i],
                    'contracts': contracts,
                    'stop_price': stop_price,
                    'entry_costs': entry_costs
                }

                trades.append({
                    'date': df.index[i].strftime('%Y-%m-%d'),
                    'type': 'BUY',
                    'price': float(current_price),
                    'contracts': contracts,
                    'reason': 'Price touched lower Bollinger Band',
                    'stopLossPrice': float(stop_price) if stop_price else None,
                    'takeProfitPrice': None,
                    'pnl': None,
                    'commission': entry_costs,
                    'exchangeFees': 0,
                    'clearingFees': 0,
                    'overnightFinancing': 0
                })

                remaining_capital -= (specs['margin'] * contracts) + entry_costs
                total_costs += entry_costs

        # Check stop-loss
        elif position and position['stop_price'] and current_price <= position['stop_price']:
            days_held = (df.index[i] - position['entry_date']).days
            exit_price = position['stop_price']
            exit_costs = (costs['commission'] + costs['exchangeFee'] + costs['clearingFee']) * position['contracts']
            overnight_costs = specs['margin'] * position['contracts'] * costs['overnightRate'] * days_held

            pnl = (exit_price - position['entry_price']) * specs['contractSize'] * position['contracts']

            trades.append({
                'date': df.index[i].strftime('%Y-%m-%d'),
                'type': 'SELL',
                'price': float(exit_price),
                'contracts': position['contracts'],
                'reason': 'Stop-loss triggered',
                'stopLossPrice': None,
                'takeProfitPrice': None,
                'pnl': float(pnl),
                'commission': exit_costs,
                'exchangeFees': 0,
                'clearingFees': 0,
                'overnightFinancing': float(overnight_costs)
            })

            remaining_capital += (specs['margin'] * position['contracts']) + pnl - exit_costs - overnight_costs
            total_costs += exit_costs + overnight_costs
            position = None

        # Exit: Price returns to middle band
        elif position and current_price >= bb_middle:
            days_held = (df.index[i] - position['entry_date']).days
            exit_costs = (costs['commission'] + costs['exchangeFee'] + costs['clearingFee']) * position['contracts']
            overnight_costs = specs['margin'] * position['contracts'] * costs['overnightRate'] * days_held

            pnl = (current_price - position['entry_price']) * specs['contractSize'] * position['contracts']

            trades.append({
                'date': df.index[i].strftime('%Y-%m-%d'),
                'type': 'SELL',
                'price': float(current_price),
                'contracts': position['contracts'],
                'reason': 'Price returned to middle band',
                'stopLossPrice': None,
                'takeProfitPrice': None,
                'pnl': float(pnl),
                'commission': exit_costs,
                'exchangeFees': 0,
                'clearingFees': 0,
                'overnightFinancing': float(overnight_costs)
            })

            remaining_capital += (specs['margin'] * position['contracts']) + pnl - exit_costs - overnight_costs
            total_costs += exit_costs + overnight_costs
            position = None

    # Close any open position at end
    if position:
        i = len(df) - 1
        days_held = (df.index[i] - position['entry_date']).days
        current_price = df['Close'].iloc[i]
        exit_costs = (costs['commission'] + costs['exchangeFee'] + costs['clearingFee']) * position['contracts']
        overnight_costs = specs['margin'] * position['contracts'] * costs['overnightRate'] * days_held

        pnl = (current_price - position['entry_price']) * specs['contractSize'] * position['contracts']

        trades.append({
            'date': df.index[i].strftime('%Y-%m-%d'),
            'type': 'SELL',
            'price': float(current_price),
            'contracts': position['contracts'],
            'reason': 'End of period',
            'stopLossPrice': None,
            'takeProfitPrice': None,
            'pnl': float(pnl),
            'commission': exit_costs,
            'exchangeFees': 0,
            'clearingFees': 0,
            'overnightFinancing': float(overnight_costs)
        })

        remaining_capital += (specs['margin'] * position['contracts']) + pnl - exit_costs - overnight_costs
        total_costs += exit_costs + overnight_costs

    final_value = remaining_capital
    return trades, total_costs, final_value

def strategy_seasonal(df, capital, specs, costs, stop_loss_method, stop_loss_value):
    """Seasonal/Monthly Pattern Strategy"""
    # Analyze historical monthly performance
    from collections import defaultdict
    import statistics

    monthly_data = defaultdict(lambda: {'returns': []})
    prev_month_close = None
    prev_date = None

    for date, row in df.iterrows():
        if prev_month_close is not None and date.month != prev_date.month:
            monthly_return = ((row['Close'] - prev_month_close) / prev_month_close) * 100
            month_name = prev_date.strftime('%B')
            monthly_data[month_name]['returns'].append(monthly_return)

        if date.month != (prev_date.month if prev_date else 0):
            prev_month_close = row['Close']

        prev_date = date

    # Find best 4 months
    month_order = ['January', 'February', 'March', 'April', 'May', 'June',
                   'July', 'August', 'September', 'October', 'November', 'December']

    monthly_stats = []
    for month in month_order:
        if month in monthly_data and len(monthly_data[month]['returns']) > 0:
            returns = monthly_data[month]['returns']
            avg_return = statistics.mean(returns)
            monthly_stats.append({'month': month, 'avgReturn': avg_return})

    monthly_stats_sorted = sorted(monthly_stats, key=lambda x: x['avgReturn'], reverse=True)
    best_months = [m['month'] for m in monthly_stats_sorted[:4]]

    # Backtest: Buy on first day of best months, sell at end
    trades = []
    position = None
    remaining_capital = capital
    total_costs = 0

    for i in range(len(df)):
        current_date = df.index[i]
        current_price = df['Close'].iloc[i]
        month_name = current_date.strftime('%B')

        # Entry: First trading day of best months (within first 5 days)
        if position is None and month_name in best_months and current_date.day <= 5:
            contracts = int(remaining_capital / specs['margin'])
            if contracts >= 1:
                atr = df['ATR14'].iloc[i] if 'ATR14' in df.columns else None
                vol = df['Volatility20'].iloc[i] if 'Volatility20' in df.columns else None
                stop_price = calculate_stop_loss(current_price, stop_loss_method, stop_loss_value, atr, vol, 'long')

                entry_costs = (costs['commission'] + costs['exchangeFee'] + costs['clearingFee']) * contracts

                position = {
                    'entry_price': current_price,
                    'entry_date': current_date,
                    'contracts': contracts,
                    'stop_price': stop_price,
                    'entry_costs': entry_costs,
                    'entry_month': month_name
                }

                trades.append({
                    'date': current_date.strftime('%Y-%m-%d'),
                    'type': 'BUY',
                    'price': float(current_price),
                    'contracts': contracts,
                    'reason': f'Start of {month_name} (best month)',
                    'stopLossPrice': float(stop_price) if stop_price else None,
                    'takeProfitPrice': None,
                    'pnl': None,
                    'commission': entry_costs,
                    'exchangeFees': 0,
                    'clearingFees': 0,
                    'overnightFinancing': 0
                })

                remaining_capital -= (specs['margin'] * contracts) + entry_costs
                total_costs += entry_costs

        # Check stop-loss
        elif position and position['stop_price'] and current_price <= position['stop_price']:
            days_held = (current_date - position['entry_date']).days
            exit_price = position['stop_price']
            exit_costs = (costs['commission'] + costs['exchangeFee'] + costs['clearingFee']) * position['contracts']
            overnight_costs = specs['margin'] * position['contracts'] * costs['overnightRate'] * days_held

            pnl = (exit_price - position['entry_price']) * specs['contractSize'] * position['contracts']

            trades.append({
                'date': current_date.strftime('%Y-%m-%d'),
                'type': 'SELL',
                'price': float(exit_price),
                'contracts': position['contracts'],
                'reason': 'Stop-loss triggered',
                'stopLossPrice': None,
                'takeProfitPrice': None,
                'pnl': float(pnl),
                'commission': exit_costs,
                'exchangeFees': 0,
                'clearingFees': 0,
                'overnightFinancing': float(overnight_costs)
            })

            remaining_capital += (specs['margin'] * position['contracts']) + pnl - exit_costs - overnight_costs
            total_costs += exit_costs + overnight_costs
            position = None

        # Exit: End of month or start of non-best month
        elif position and (month_name != position['entry_month'] or current_date.day >= 25):
            if month_name != position['entry_month']:
                days_held = (current_date - position['entry_date']).days
                exit_costs = (costs['commission'] + costs['exchangeFee'] + costs['clearingFee']) * position['contracts']
                overnight_costs = specs['margin'] * position['contracts'] * costs['overnightRate'] * days_held

                pnl = (current_price - position['entry_price']) * specs['contractSize'] * position['contracts']

                trades.append({
                    'date': current_date.strftime('%Y-%m-%d'),
                    'type': 'SELL',
                    'price': float(current_price),
                    'contracts': position['contracts'],
                    'reason': f'End of {position["entry_month"]}',
                    'stopLossPrice': None,
                    'takeProfitPrice': None,
                    'pnl': float(pnl),
                    'commission': exit_costs,
                    'exchangeFees': 0,
                    'clearingFees': 0,
                    'overnightFinancing': float(overnight_costs)
                })

                remaining_capital += (specs['margin'] * position['contracts']) + pnl - exit_costs - overnight_costs
                total_costs += exit_costs + overnight_costs
                position = None

    # Close any open position at end
    if position:
        i = len(df) - 1
        days_held = (df.index[i] - position['entry_date']).days
        current_price = df['Close'].iloc[i]
        exit_costs = (costs['commission'] + costs['exchangeFee'] + costs['clearingFee']) * position['contracts']
        overnight_costs = specs['margin'] * position['contracts'] * costs['overnightRate'] * days_held

        pnl = (current_price - position['entry_price']) * specs['contractSize'] * position['contracts']

        trades.append({
            'date': df.index[i].strftime('%Y-%m-%d'),
            'type': 'SELL',
            'price': float(current_price),
            'contracts': position['contracts'],
            'reason': 'End of period',
            'stopLossPrice': None,
            'takeProfitPrice': None,
            'pnl': float(pnl),
            'commission': exit_costs,
            'exchangeFees': 0,
            'clearingFees': 0,
            'overnightFinancing': float(overnight_costs)
        })

        remaining_capital += (specs['margin'] * position['contracts']) + pnl - exit_costs - overnight_costs
        total_costs += exit_costs + overnight_costs

    final_value = remaining_capital
    return trades, total_costs, final_value

def backtest_single_commodity(symbol, strategy, capital, years, stop_loss_method, stop_loss_value):
    """
    Main backtesting function

    Args:
        symbol: Commodity symbol (e.g., 'GC=F')
        strategy: Strategy name ('buyhold', 'sma', 'rsi', 'macd', 'bollinger', 'seasonal')
        capital: Initial capital
        years: Years of historical data
        stop_loss_method: 'atr', 'percentage', 'volatility', 'fixed'
        stop_loss_value: Stop-loss value (multiplier or amount)

    Returns:
        dict: Backtest results with trades and performance metrics
    """
    try:
        # Fetch data
        end_date = datetime.now()
        start_date = end_date - timedelta(days=years*365)

        sys.stderr.write(f"Fetching {symbol} data from {start_date.date()} to {end_date.date()}...\n")

        ticker = yf.Ticker(symbol)
        df = ticker.history(start=start_date, end=end_date)

        if df.empty:
            return {'success': False, 'error': f'No data found for {symbol}'}

        # Calculate technical indicators
        df['ATR14'] = calculate_atr(df, 14)
        df['Volatility20'] = calculate_volatility(df['Close'], 20)

        # Get specs and costs
        specs = get_commodity_specs(symbol)
        costs = get_cme_costs(symbol)

        sys.stderr.write(f"Fetched {len(df)} data points\n")
        sys.stderr.write(f"Running {strategy} strategy with {stop_loss_method} stop-loss...\n")

        # Run strategy
        trades = []
        total_costs = 0
        final_value = capital

        if strategy.lower() == 'buyhold':
            trades, total_costs, final_value = strategy_buy_hold(df, capital, specs, costs, stop_loss_method, stop_loss_value)
        elif strategy.lower() == 'sma':
            trades, total_costs, final_value = strategy_sma_crossover(df, capital, specs, costs, stop_loss_method, stop_loss_value)
        elif strategy.lower() == 'rsi':
            trades, total_costs, final_value = strategy_rsi(df, capital, specs, costs, stop_loss_method, stop_loss_value)
        elif strategy.lower() == 'macd':
            trades, total_costs, final_value = strategy_macd(df, capital, specs, costs, stop_loss_method, stop_loss_value)
        elif strategy.lower() == 'bollinger':
            trades, total_costs, final_value = strategy_bollinger(df, capital, specs, costs, stop_loss_method, stop_loss_value)
        elif strategy.lower() == 'seasonal':
            trades, total_costs, final_value = strategy_seasonal(df, capital, specs, costs, stop_loss_method, stop_loss_value)
        else:
            return {'success': False, 'error': f'Unknown strategy: {strategy}'}

        # Calculate performance metrics
        total_return = ((final_value - capital) / capital) * 100
        annual_return = total_return / years

        # Calculate drawdown, win rate, etc.
        winning_trades = [t for t in trades if t.get('pnl') and t['pnl'] > 0]
        losing_trades = [t for t in trades if t.get('pnl') and t['pnl'] < 0]
        total_trades = len([t for t in trades if t.get('pnl') is not None])
        win_rate = (len(winning_trades) / total_trades * 100) if total_trades > 0 else 0

        # Profit factor
        gross_profit = sum(t['pnl'] for t in winning_trades) if winning_trades else 0
        gross_loss = abs(sum(t['pnl'] for t in losing_trades)) if losing_trades else 0
        profit_factor = (gross_profit / gross_loss) if gross_loss > 0 else 0

        # Sharpe ratio (simplified)
        if total_trades > 0:
            returns = [t.get('pnl', 0) for t in trades if t.get('pnl') is not None]
            avg_return = np.mean(returns)
            std_return = np.std(returns)
            sharpe_ratio = (avg_return / std_return) if std_return > 0 else 0
        else:
            sharpe_ratio = 0

        # Max drawdown (simplified - track equity curve)
        equity_curve = [capital]
        for trade in trades:
            if trade.get('pnl') is not None:
                equity_curve.append(equity_curve[-1] + trade['pnl'])

        peak = equity_curve[0]
        max_dd = 0
        for value in equity_curve:
            if value > peak:
                peak = value
            dd = ((peak - value) / peak) * 100
            if dd > max_dd:
                max_dd = dd

        # Build response
        response = {
            'success': True,
            'symbol': symbol,
            'name': specs['name'],
            'strategyType': strategy,
            'stopLossMethod': stop_loss_method,
            'stopLossValue': float(stop_loss_value),
            'period': f'{years}Y',
            'capital': float(capital),
            'contractsTraded': sum(t.get('contracts', 0) for t in trades if t['type'] == 'BUY'),
            'finalValue': round(final_value, 2),
            'totalReturn': round(total_return, 2),
            'annualReturn': round(annual_return, 2),
            'maxDrawdown': round(max_dd, 2),
            'winRate': round(win_rate, 2),
            'totalTrades': total_trades,
            'profitFactor': round(profit_factor, 2),
            'sharpeRatio': round(sharpe_ratio, 2),
            'sortinoRatio': 0,  # Placeholder
            'totalCosts': round(total_costs, 2),
            'trades': trades,
            'calculatedAt': datetime.now().isoformat()
        }

        sys.stderr.write(f"\nBacktest complete!\n")
        sys.stderr.write(f"Total Return: {total_return:.2f}%\n")
        sys.stderr.write(f"Total Trades: {total_trades}\n")
        sys.stderr.write(f"Win Rate: {win_rate:.2f}%\n")

        return response

    except Exception as e:
        sys.stderr.write(f"Error: {str(e)}\n")
        import traceback
        traceback.print_exc(file=sys.stderr)
        return {'success': False, 'error': str(e)}

def main():
    if len(sys.argv) < 7:
        print(json.dumps({
            'success': False,
            'error': 'Usage: python backtest_single_commodity.py <symbol> <strategy> <capital> <years> <stopLossMethod> <stopLossValue>'
        }, indent=2))
        sys.exit(1)

    symbol = sys.argv[1].upper()
    strategy = sys.argv[2].lower()
    capital = float(sys.argv[3])
    years = int(sys.argv[4])
    stop_loss_method = sys.argv[5].lower()
    stop_loss_value = sys.argv[6]

    # Validate inputs
    valid_strategies = ['buyhold', 'sma', 'rsi', 'macd', 'bollinger', 'seasonal']
    if strategy not in valid_strategies:
        print(json.dumps({
            'success': False,
            'error': f'Invalid strategy. Must be one of: {", ".join(valid_strategies)}'
        }, indent=2))
        sys.exit(1)

    valid_stop_methods = ['atr', 'percentage', 'volatility', 'fixed']
    if stop_loss_method not in valid_stop_methods:
        print(json.dumps({
            'success': False,
            'error': f'Invalid stop-loss method. Must be one of: {", ".join(valid_stop_methods)}'
        }, indent=2))
        sys.exit(1)

    # Run backtest
    result = backtest_single_commodity(symbol, strategy, capital, years, stop_loss_method, stop_loss_value)

    # Output JSON
    print(json.dumps(result, indent=2))

    sys.exit(0 if result['success'] else 1)

if __name__ == '__main__':
    main()