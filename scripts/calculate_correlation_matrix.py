#!/usr/bin/env python3
"""
Calculate correlation matrix and suggest commodity pairs for trading

Usage: python calculate_correlation_matrix.py <symbols> <period>
Example: python calculate_correlation_matrix.py "GC=F,SI=F,CL=F,NG=F" 5Y
"""

import sys
import json
import yfinance as yf
import pandas as pd
import numpy as np
from datetime import datetime, timedelta
from statsmodels.tsa.stattools import coint
from statsmodels.regression.linear_model import OLS
from statsmodels.tools.tools import add_constant

def parse_period(period_str):
    """Convert period string (e.g., '5Y') to years"""
    period_str = period_str.upper()
    if period_str.endswith('Y'):
        return int(period_str[:-1])
    return 5  # Default

def calculate_half_life(spread):
    """Calculate mean reversion half-life using AR(1) model"""
    try:
        spread_lag = spread.shift(1)
        spread_lag.iloc[0] = spread_lag.iloc[1]

        spread_ret = spread - spread_lag
        spread_lag_const = add_constant(spread_lag)

        model = OLS(spread_ret, spread_lag_const)
        res = model.fit()

        halflife = -np.log(2) / res.params.iloc[1]
        return int(halflife) if halflife > 0 else None
    except:
        return None

def calculate_optimal_ratio(prices1, prices2):
    """Calculate optimal hedge ratio using OLS regression"""
    try:
        prices2_const = add_constant(prices2)
        model = OLS(prices1, prices2_const)
        res = model.fit()
        return float(res.params.iloc[1])
    except:
        return 1.0

def test_cointegration(prices1, prices2):
    """Test for cointegration using Engle-Granger test"""
    try:
        score, pvalue, _ = coint(prices1, prices2)
        # Lower p-value = stronger cointegration
        # Convert to 0-1 score (1 = perfect cointegration)
        cointegration_score = max(0, min(1, 1 - pvalue))
        is_cointegrated = bool(pvalue < 0.05)  # Convert numpy bool to Python bool
        return cointegration_score, is_cointegrated
    except:
        return 0.0, False

def score_pair(corr, coint_score, is_stationary, half_life):
    """
    Score a commodity pair for trading potential (0-100)

    Criteria:
    - High correlation (positive or negative): 30 points
    - Strong cointegration: 30 points
    - Stationary spread: 20 points
    - Reasonable half-life (5-60 days): 20 points
    """
    score = 0

    # Correlation score (prefer strong correlation, positive or negative)
    abs_corr = abs(corr)
    if abs_corr > 0.8:
        score += 30
    elif abs_corr > 0.6:
        score += 20
    elif abs_corr > 0.4:
        score += 10

    # Cointegration score
    score += coint_score * 30

    # Stationary spread
    if is_stationary:
        score += 20

    # Half-life score (prefer 5-60 days for mean reversion)
    if half_life and 5 <= half_life <= 60:
        score += 20
    elif half_life and half_life <= 90:
        score += 10

    return round(score, 2)

def determine_strategy_type(corr, is_stationary):
    """Determine best strategy type for the pair"""
    abs_corr = abs(corr)

    if is_stationary and abs_corr > 0.6:
        return "MeanReversion"
    elif abs_corr > 0.7:
        return "Correlation"
    elif 0.3 <= abs_corr <= 0.6:
        return "Ratio"
    else:
        return "Diversification"

def get_risk_level(score, max_drawdown=None):
    """Determine risk level based on score and other factors"""
    if score >= 70:
        return "Low"
    elif score >= 50:
        return "Medium"
    else:
        return "High"

def calculate_correlation_matrix(symbols, period='5Y'):
    """
    Calculate correlation matrix and suggest pairs

    Args:
        symbols: List of commodity symbols
        period: Time period (e.g., '5Y', '3Y', '1Y')

    Returns:
        dict: JSON response with correlation matrix and pair suggestions
    """
    try:
        years = parse_period(period)
        end_date = datetime.now()
        start_date = end_date - timedelta(days=years*365)

        sys.stderr.write(f"Calculating correlations for {len(symbols)} commodities over {period}...\n")
        sys.stderr.write(f"Symbols: {', '.join(symbols)}\n")

        # Fetch historical data for all symbols
        all_data = {}
        for symbol in symbols:
            sys.stderr.write(f"Fetching {symbol}...\n")
            ticker = yf.Ticker(symbol)
            df = ticker.history(start=start_date, end=end_date)

            if df.empty:
                sys.stderr.write(f"Warning: No data for {symbol}\n")
                continue

            all_data[symbol] = df['Close']

        if len(all_data) < 2:
            return {
                'success': False,
                'error': 'Need at least 2 commodities with valid data'
            }

        # Align all data to common dates
        df_combined = pd.DataFrame(all_data)
        df_combined = df_combined.dropna()

        sys.stderr.write(f"Aligned data: {len(df_combined)} common dates\n")

        # Calculate correlation matrix
        corr_matrix = df_combined.corr()

        # Build correlation matrix response
        correlation_matrix = []
        pair_suggestions = []

        # Analyze all pairs
        for i, sym1 in enumerate(symbols):
            if sym1 not in df_combined.columns:
                continue

            for j, sym2 in enumerate(symbols):
                if sym2 not in df_combined.columns or i >= j:
                    continue

                # Get correlation
                correlation = float(corr_matrix.loc[sym1, sym2])

                # Test cointegration
                prices1 = df_combined[sym1]
                prices2 = df_combined[sym2]
                coint_score, is_stationary = test_cointegration(prices1, prices2)

                # Calculate optimal ratio
                optimal_ratio = calculate_optimal_ratio(prices1, prices2)

                # Calculate half-life if stationary
                if is_stationary:
                    spread = prices1 - optimal_ratio * prices2
                    half_life = calculate_half_life(spread)
                else:
                    half_life = None

                # Add to correlation matrix
                correlation_matrix.append({
                    'symbol1': sym1,
                    'symbol2': sym2,
                    'correlation': round(correlation, 4),
                    'cointegration': round(coint_score, 4)
                })

                # Score and categorize the pair
                score = score_pair(correlation, coint_score, is_stationary, half_life)

                # Only suggest pairs with score >= 30
                if score >= 30:
                    strategy_type = determine_strategy_type(correlation, is_stationary)
                    risk_level = get_risk_level(score)

                    # Generate reasoning
                    reasons = []
                    if abs(correlation) > 0.7:
                        reasons.append(f"{'Strong positive' if correlation > 0 else 'Strong negative'} correlation ({correlation:.2f})")
                    if coint_score > 0.7:
                        reasons.append(f"High cointegration ({coint_score:.2f})")
                    if is_stationary:
                        reasons.append("Stationary spread suitable for mean reversion")
                    if half_life and 5 <= half_life <= 60:
                        reasons.append(f"Optimal half-life of {half_life} days")

                    reasoning = ". ".join(reasons) if reasons else "Moderate correlation suggests potential for pair trading"

                    # Calculate expected returns (simplified estimate)
                    # Higher score + lower risk = higher expected returns
                    expected_returns = round(score * 0.15 if risk_level == "Low" else score * 0.10, 2)

                    pair_suggestions.append({
                        'symbol1': sym1,
                        'symbol2': sym2,
                        'score': score,
                        'recommendationType': strategy_type,
                        'reasoning': reasoning,
                        'optimalRatio': round(optimal_ratio, 4),
                        'correlation': round(correlation, 4),
                        'cointegration': round(coint_score, 4),
                        'halfLife': half_life,
                        'isStationaryPair': is_stationary,
                        'expectedReturns': expected_returns,
                        'riskLevel': risk_level
                    })

        # Sort suggestions by score (descending)
        pair_suggestions.sort(key=lambda x: x['score'], reverse=True)

        # Build response
        response = {
            'success': True,
            'period': period,
            'years': years,
            'symbols': symbols,
            'dataPoints': len(df_combined),
            'startDate': df_combined.index[0].strftime('%Y-%m-%d'),
            'endDate': df_combined.index[-1].strftime('%Y-%m-%d'),
            'correlationMatrix': correlation_matrix,
            'pairSuggestions': pair_suggestions,
            'topPairs': pair_suggestions[:5],  # Top 5 pairs
            'calculatedAt': datetime.now().isoformat()
        }

        sys.stderr.write(f"\nCalculated {len(correlation_matrix)} correlations\n")
        sys.stderr.write(f"Found {len(pair_suggestions)} viable pairs\n")
        if pair_suggestions:
            top = pair_suggestions[0]
            sys.stderr.write(f"Top pair: {top['symbol1']}/{top['symbol2']} (score: {top['score']})\n")

        return response

    except Exception as e:
        sys.stderr.write(f"Error calculating correlations: {str(e)}\n")
        import traceback
        traceback.print_exc(file=sys.stderr)
        return {
            'success': False,
            'error': str(e)
        }

def main():
    if len(sys.argv) < 2:
        print(json.dumps({
            'success': False,
            'error': 'Usage: python calculate_correlation_matrix.py <symbols> [period]'
        }), indent=2)
        sys.exit(1)

    # Parse symbols (comma-separated)
    symbols_str = sys.argv[1]
    symbols = [s.strip().upper() for s in symbols_str.split(',')]

    # Parse period (optional, default 5Y)
    period = sys.argv[2] if len(sys.argv) > 2 else '5Y'

    # Validate symbols
    if len(symbols) < 2:
        print(json.dumps({
            'success': False,
            'error': 'Need at least 2 commodity symbols'
        }), indent=2)
        sys.exit(1)

    # Calculate correlations
    result = calculate_correlation_matrix(symbols, period)

    # Output JSON to stdout
    print(json.dumps(result, indent=2))

    # Exit with appropriate code
    sys.exit(0 if result['success'] else 1)

if __name__ == '__main__':
    main()