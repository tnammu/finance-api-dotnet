"""
Growth Stock Analyzer
Analyzes stocks using 5 growth filters:
1. Revenue Growth > 15%
2. EPS Growth (positive & consistent)
3. PEG Ratio < 1.5
4. Rule of 40 > 40
5. Free Cash Flow > 0 or rising
"""

import yfinance as yf
import sys
import json
from datetime import datetime, timedelta

def log(message):
    """Print to stderr so it doesn't interfere with JSON output"""
    print(message, file=sys.stderr)

def calculate_revenue_growth(ticker):
    """Calculate YoY Revenue Growth %"""
    try:
        financials = ticker.financials
        if financials.empty or 'Total Revenue' not in financials.index:
            return None, False

        revenues = financials.loc['Total Revenue'].sort_index()
        if len(revenues) < 2:
            return None, False

        # Get most recent 2 years
        recent = revenues.iloc[-1]
        previous = revenues.iloc[-2]

        growth = ((recent - previous) / previous) * 100
        passes = growth > 15

        log(f"✓ Revenue Growth: {growth:.2f}% ({'PASS' if passes else 'FAIL'})")
        return float(growth), passes
    except Exception as e:
        log(f"✗ Revenue Growth calculation failed: {e}")
        return None, False

def calculate_eps_growth(ticker):
    """Calculate EPS Growth with consistency check"""
    try:
        info = ticker.info

        # Get current and forward EPS
        current_eps = info.get('trailingEps')
        forward_eps = info.get('forwardEps')

        # Try to get earnings growth rate directly from info
        earnings_growth = info.get('earningsGrowth')
        earnings_quarterly_growth = info.get('earningsQuarterlyGrowth')

        # Method 1: Use forward vs trailing EPS
        if current_eps and forward_eps and current_eps > 0:
            growth = ((forward_eps - current_eps) / current_eps) * 100
            passes = growth > 0
            log(f"✓ EPS Growth: {growth:.2f}% (Forward vs Trailing) ({'PASS' if passes else 'FAIL'})")
            return float(growth), passes

        # Method 2: Use earnings growth rate from info
        if earnings_growth is not None:
            growth = earnings_growth * 100  # Convert to percentage
            passes = growth > 0
            log(f"✓ EPS Growth: {growth:.2f}% (Annual Growth Rate) ({'PASS' if passes else 'FAIL'})")
            return float(growth), passes

        # Method 3: Use quarterly growth
        if earnings_quarterly_growth is not None:
            growth = earnings_quarterly_growth * 100
            passes = growth > 0
            log(f"✓ EPS Growth: {growth:.2f}% (Quarterly) ({'PASS' if passes else 'FAIL'})")
            return float(growth), passes

        # Method 4: Try to calculate from income statement
        try:
            income_stmt = ticker.income_stmt
            if not income_stmt.empty and 'Net Income' in income_stmt.index:
                net_income = income_stmt.loc['Net Income'].sort_index()
                if len(net_income) >= 2:
                    recent = net_income.iloc[-1]
                    previous = net_income.iloc[-2]

                    # Get shares outstanding to calculate EPS
                    shares = info.get('sharesOutstanding')
                    if shares and shares > 0:
                        current_calc_eps = recent / shares
                        previous_calc_eps = previous / shares

                        if previous_calc_eps > 0:
                            growth = ((current_calc_eps - previous_calc_eps) / previous_calc_eps) * 100
                            passes = growth > 0
                            log(f"✓ EPS Growth: {growth:.2f}% (Calculated from Net Income) ({'PASS' if passes else 'FAIL'})")
                            return float(growth), passes
        except Exception as calc_error:
            log(f"Could not calculate EPS from income statement: {calc_error}")

        log(f"✗ EPS Growth: No data available")
        return None, False
    except Exception as e:
        log(f"✗ EPS Growth calculation failed: {e}")
        return None, False

def calculate_peg_ratio(ticker, eps_growth):
    """Calculate PEG Ratio = PE / EPS Growth"""
    try:
        info = ticker.info
        pe_ratio = info.get('trailingPE') or info.get('forwardPE')

        if not pe_ratio or not eps_growth or eps_growth <= 0:
            return None, False

        peg = pe_ratio / eps_growth
        passes = peg < 1.5

        log(f"✓ PEG Ratio: {peg:.2f} ({'PASS' if passes else 'FAIL'})")
        return float(peg), passes
    except Exception as e:
        log(f"✗ PEG Ratio calculation failed: {e}")
        return None, False

def calculate_rule_of_40(revenue_growth, profit_margin):
    """Rule of 40 = Revenue Growth % + Profit Margin %"""
    try:
        if revenue_growth is None or profit_margin is None:
            return None, False

        rule_40 = revenue_growth + profit_margin
        passes = rule_40 > 40

        log(f"✓ Rule of 40: {rule_40:.2f} ({'PASS' if passes else 'FAIL'})")
        return float(rule_40), passes
    except Exception as e:
        log(f"✗ Rule of 40 calculation failed: {e}")
        return None, False

def calculate_free_cash_flow(ticker):
    """Calculate Free Cash Flow = Operating CF - CapEx"""
    try:
        cash_flow = ticker.cashflow
        if cash_flow.empty:
            return None, False

        # Get Operating Cash Flow
        ocf_labels = ['Operating Cash Flow', 'Total Cash From Operating Activities']
        ocf = None
        for label in ocf_labels:
            if label in cash_flow.index:
                ocf = cash_flow.loc[label].sort_index()
                break

        # Get Capital Expenditure (usually negative)
        capex_labels = ['Capital Expenditure', 'Capital Expenditures']
        capex = None
        for label in capex_labels:
            if label in cash_flow.index:
                capex = cash_flow.loc[label].sort_index()
                break

        if ocf is None or capex is None or len(ocf) == 0:
            return None, False

        # Most recent FCF
        recent_ocf = ocf.iloc[-1]
        recent_capex = capex.iloc[-1] if capex is not None else 0

        # CapEx is usually negative, so we add it (subtract absolute value)
        fcf = recent_ocf + recent_capex

        # Check if rising (if we have at least 3 years)
        rising = False
        if len(ocf) >= 3 and len(capex) >= 3:
            fcf_history = []
            for i in range(min(3, len(ocf))):
                fcf_val = ocf.iloc[-(i+1)] + capex.iloc[-(i+1)]
                fcf_history.insert(0, fcf_val)
            rising = fcf_history[-1] > fcf_history[0]

        passes = fcf > 0 or rising

        # Convert to billions for readability
        fcf_billions = fcf / 1_000_000_000
        log(f"✓ Free Cash Flow: ${fcf_billions:.2f}B ({'PASS' if passes else 'FAIL'})")
        return float(fcf), passes
    except Exception as e:
        log(f"✗ Free Cash Flow calculation failed: {e}")
        return None, False

def calculate_growth_score(filters_passed):
    """Calculate composite score 0-100 based on filters passed"""
    score = sum(filters_passed) * 20
    return score

def get_growth_rating(score):
    """Convert score to rating"""
    if score >= 80:
        return "Strong Growth"
    elif score >= 60:
        return "Moderate Growth"
    elif score >= 40:
        return "Weak Growth"
    else:
        return "Not Growth Stock"

def analyze_growth_stock(symbol):
    """Main analysis function"""
    try:
        # Handle TSX tickers
        original_symbol = symbol
        if not symbol.endswith('.TO') and not '.' in symbol:
            # Check if it's likely a Canadian stock
            canadian_stocks = ['CNQ', 'SHOP', 'TD', 'BAM', 'ENB', 'RY', 'BMO', 'BNS', 'CM', 'TRI', 'XEQT', 'VFV', 'XIU']
            if symbol.upper() in canadian_stocks:
                symbol = f"{symbol}.TO"
                log(f"Assuming Canadian stock: {symbol}")

        log(f"\n{'='*60}")
        log(f"GROWTH STOCK ANALYSIS: {symbol}")
        log(f"{'='*60}\n")

        ticker = yf.Ticker(symbol)
        info = ticker.info

        # Get basic info
        company_name = info.get('longName', symbol)
        sector = info.get('sector', 'N/A')
        industry = info.get('industry', 'N/A')
        current_price = info.get('currentPrice', 0)
        profit_margin_decimal = info.get('profitMargins', 0)
        profit_margin = profit_margin_decimal * 100 if profit_margin_decimal else 0

        log(f"Company: {company_name}")
        log(f"Sector: {sector}")
        log(f"Industry: {industry}")
        log(f"Current Price: ${current_price}\n")

        # Calculate all 5 filters
        log("CALCULATING GROWTH METRICS:")
        log("-" * 60)

        revenue_growth, filter_1 = calculate_revenue_growth(ticker)
        eps_growth, filter_2 = calculate_eps_growth(ticker)
        peg_ratio, filter_3 = calculate_peg_ratio(ticker, eps_growth)
        rule_40, filter_4 = calculate_rule_of_40(revenue_growth, profit_margin)
        fcf, filter_5 = calculate_free_cash_flow(ticker)

        filters_passed = [filter_1, filter_2, filter_3, filter_4, filter_5]
        growth_score = calculate_growth_score(filters_passed)
        growth_rating = get_growth_rating(growth_score)

        log("\n" + "="*60)
        log(f"GROWTH SCORE: {growth_score}/100 - {growth_rating}")
        log(f"Filters Passed: {sum(filters_passed)}/5")
        log("="*60 + "\n")

        # Prepare result
        result = {
            "success": True,
            "symbol": original_symbol,
            "analyzed_symbol": symbol,
            "company_name": company_name,
            "sector": sector,
            "industry": industry,
            "current_price": float(current_price) if current_price else 0,
            "profit_margin": float(profit_margin) if profit_margin else 0,
            "revenue_growth": revenue_growth,
            "eps_growth": eps_growth,
            "peg_ratio": peg_ratio,
            "rule_of_40": rule_40,
            "free_cash_flow": float(fcf) if fcf else None,
            "growth_score": growth_score,
            "growth_rating": growth_rating,
            "filters_passed": {
                "revenue_growth": filter_1,
                "eps_growth": filter_2,
                "peg_ratio": filter_3,
                "rule_of_40": filter_4,
                "free_cash_flow": filter_5
            },
            "filters_count": sum(filters_passed),
            "fetched_at": datetime.utcnow().isoformat()
        }

        return result

    except Exception as e:
        log(f"ERROR analyzing {symbol}: {e}")
        return {
            "success": False,
            "error": str(e),
            "symbol": symbol
        }

def convert_to_serializable(obj):
    """Convert numpy/pandas types to Python native types for JSON serialization"""
    try:
        import numpy as np
        import pandas as pd

        if isinstance(obj, (np.integer, np.int64, np.int32)):
            return int(obj)
        elif isinstance(obj, (np.floating, np.float64, np.float32)):
            return float(obj)
        elif isinstance(obj, (np.bool_, bool)):
            return bool(obj)
        elif isinstance(obj, np.ndarray):
            return obj.tolist()
        elif isinstance(obj, pd.Series):
            return obj.tolist()
        elif isinstance(obj, dict):
            return {key: convert_to_serializable(value) for key, value in obj.items()}
        elif isinstance(obj, list):
            return [convert_to_serializable(item) for item in obj]
        return obj
    except Exception:
        # If import fails, just return the object
        return obj

if __name__ == "__main__":
    if len(sys.argv) < 2:
        print(json.dumps({"success": False, "error": "No symbol provided"}))
        sys.exit(1)

    symbol = sys.argv[1]
    result = analyze_growth_stock(symbol)

    # Convert numpy types to native Python types
    result = convert_to_serializable(result)

    # Output JSON to stdout
    print(json.dumps(result, indent=2))