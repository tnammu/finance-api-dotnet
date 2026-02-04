import sys
import json
import yfinance as yf
from datetime import datetime, timedelta
import traceback
import matplotlib.pyplot as plt
import numpy as np
from collections import defaultdict
import argparse

def log(message):
    """Helper function to print to stderr for debugging"""
    print(message, file=sys.stderr)

def fetch_sp500_monthly_growth(years=5):
    """
    Fetch S&P 500 monthly data for the specified years and calculate monthly growth
    """
    try:
        log(f"Fetching S&P 500 monthly data for past {years} years...")

        # S&P 500 ticker symbol
        sp500 = yf.Ticker("^GSPC")

        # Calculate date range - past N years
        end_date = datetime.now()
        start_date = end_date - timedelta(days=years*365)

        log(f"Fetching data from {start_date.date()} to {end_date.date()}")

        # Fetch monthly data
        hist = sp500.history(start=start_date, end=end_date, interval="1mo")

        if hist.empty:
            log("No data received from yfinance")
            return {
                "success": False,
                "error": "No S&P 500 data available"
            }

        log(f"Received {len(hist)} months of data")

        # Calculate monthly growth
        monthly_data = []

        for i in range(len(hist)):
            date = hist.index[i]
            close_price = hist['Close'].iloc[i]

            # Calculate month-over-month growth
            if i > 0:
                prev_close = hist['Close'].iloc[i-1]
                growth_percent = ((close_price - prev_close) / prev_close) * 100
            else:
                growth_percent = 0  # First month has no previous month

            monthly_data.append({
                "date": date.strftime("%Y-%m"),
                "close": round(float(close_price), 2),
                "growth": round(float(growth_percent), 2)
            })

        # Calculate overall statistics
        total_return = ((hist['Close'].iloc[-1] - hist['Close'].iloc[0]) / hist['Close'].iloc[0]) * 100
        avg_monthly_growth = sum(d['growth'] for d in monthly_data[1:]) / (len(monthly_data) - 1) if len(monthly_data) > 1 else 0

        # Count positive and negative months
        positive_months = sum(1 for d in monthly_data if d['growth'] > 0)
        negative_months = sum(1 for d in monthly_data if d['growth'] < 0)

        # Find best and worst months
        best_month = max(monthly_data, key=lambda x: x['growth']) if monthly_data else None
        worst_month = min(monthly_data, key=lambda x: x['growth']) if monthly_data else None

        result = {
            "success": True,
            "symbol": "^GSPC",
            "name": "S&P 500",
            "startDate": start_date.strftime("%Y-%m-%d"),
            "endDate": end_date.strftime("%Y-%m-%d"),
            "monthlyData": monthly_data,
            "statistics": {
                "totalReturn": round(float(total_return), 2),
                "avgMonthlyGrowth": round(float(avg_monthly_growth), 2),
                "totalMonths": len(monthly_data),
                "positiveMonths": positive_months,
                "negativeMonths": negative_months,
                "bestMonth": {
                    "date": best_month['date'],
                    "growth": best_month['growth']
                } if best_month else None,
                "worstMonth": {
                    "date": worst_month['date'],
                    "growth": worst_month['growth']
                } if worst_month else None
            }
        }

        log("Successfully processed S&P 500 data")
        return result

    except Exception as e:
        log(f"Error fetching S&P 500 data: {str(e)}")
        traceback.print_exc(file=sys.stderr)
        return {
            "success": False,
            "error": str(e)
        }

def consolidate_monthly_data(monthly_data):
    """
    Consolidate monthly growth data by calendar month (Jan, Feb, Mar, etc.)
    and calculate average growth for each month across all years
    """
    # Dictionary to store growth values for each month
    month_growth = defaultdict(list)

    # Group growth by calendar month
    for data in monthly_data:
        if data['growth'] != 0:  # Skip the first month which has 0 growth
            date_obj = datetime.strptime(data['date'], "%Y-%m")
            month_name = date_obj.strftime("%B")  # Full month name
            month_growth[month_name].append(data['growth'])

    # Calculate average growth for each month
    month_order = ['January', 'February', 'March', 'April', 'May', 'June',
                   'July', 'August', 'September', 'October', 'November', 'December']

    consolidated = []
    for month in month_order:
        if month in month_growth and month_growth[month]:
            avg_growth = np.mean(month_growth[month])
            positive_count = sum(1 for g in month_growth[month] if g > 0)
            negative_count = sum(1 for g in month_growth[month] if g < 0)
            total_occurrences = len(month_growth[month])

            consolidated.append({
                'month': month,
                'avgGrowth': round(float(avg_growth), 2),
                'occurrences': total_occurrences,
                'positiveCount': positive_count,
                'negativeCount': negative_count,
                'positivePercentage': round((positive_count / total_occurrences) * 100, 1),
                'allGrowthValues': [round(g, 2) for g in month_growth[month]]
            })

    return consolidated

def plot_monthly_analysis(consolidated_data, output_file='sp500_monthly_analysis.png'):
    """
    Create visualization of average monthly growth
    """
    if not consolidated_data:
        log("No data to plot")
        return

    # Extract data for plotting
    months = [d['month'][:3] for d in consolidated_data]  # Use 3-letter abbreviations
    avg_growth = [d['avgGrowth'] for d in consolidated_data]
    positive_pct = [d['positivePercentage'] for d in consolidated_data]

    # Create figure with two subplots
    fig, (ax1, ax2) = plt.subplots(2, 1, figsize=(14, 10))

    # Plot 1: Average Monthly Growth
    colors = ['green' if g > 0 else 'red' for g in avg_growth]
    bars1 = ax1.bar(months, avg_growth, color=colors, alpha=0.7, edgecolor='black')
    ax1.axhline(y=0, color='black', linestyle='-', linewidth=0.8)
    ax1.set_ylabel('Average Growth (%)', fontsize=12, fontweight='bold')
    ax1.set_title('S&P 500 Average Monthly Growth by Calendar Month (Past 5 Years)',
                  fontsize=14, fontweight='bold', pad=20)
    ax1.grid(True, alpha=0.3, axis='y')

    # Add value labels on bars
    for bar, value in zip(bars1, avg_growth):
        height = bar.get_height()
        ax1.text(bar.get_x() + bar.get_width()/2., height,
                f'{value:.2f}%',
                ha='center', va='bottom' if height > 0 else 'top',
                fontsize=9, fontweight='bold')

    # Plot 2: Positive Month Percentage
    bars2 = ax2.bar(months, positive_pct, color='steelblue', alpha=0.7, edgecolor='black')
    ax2.axhline(y=50, color='red', linestyle='--', linewidth=1, label='50% threshold')
    ax2.set_xlabel('Month', fontsize=12, fontweight='bold')
    ax2.set_ylabel('Positive Months (%)', fontsize=12, fontweight='bold')
    ax2.set_title('Percentage of Positive Growth Months', fontsize=14, fontweight='bold', pad=20)
    ax2.set_ylim(0, 100)
    ax2.grid(True, alpha=0.3, axis='y')
    ax2.legend()

    # Add value labels on bars
    for bar, value in zip(bars2, positive_pct):
        height = bar.get_height()
        ax2.text(bar.get_x() + bar.get_width()/2., height,
                f'{value:.1f}%',
                ha='center', va='bottom',
                fontsize=9, fontweight='bold')

    plt.tight_layout()
    plt.savefig(output_file, dpi=300, bbox_inches='tight')
    log(f"Plot saved to {output_file}")
    plt.close()

def print_monthly_summary(consolidated_data):
    """
    Print detailed summary of positive and negative months
    """
    log("\n" + "="*80)
    log("S&P 500 MONTHLY ANALYSIS SUMMARY")
    log("="*80)

    # Overall statistics
    total_positive = sum(d['positiveCount'] for d in consolidated_data)
    total_negative = sum(d['negativeCount'] for d in consolidated_data)
    total_observations = total_positive + total_negative

    log(f"\nOVERALL STATISTICS:")
    log(f"  Total observations: {total_observations}")
    log(f"  Positive months: {total_positive} ({(total_positive/total_observations*100):.1f}%)")
    log(f"  Negative months: {total_negative} ({(total_negative/total_observations*100):.1f}%)")

    # Best and worst performing months
    best_month = max(consolidated_data, key=lambda x: x['avgGrowth'])
    worst_month = min(consolidated_data, key=lambda x: x['avgGrowth'])
    most_consistent_positive = max(consolidated_data, key=lambda x: x['positivePercentage'])
    most_consistent_negative = min(consolidated_data, key=lambda x: x['positivePercentage'])

    log(f"\nBEST PERFORMING MONTH:")
    log(f"  {best_month['month']}: {best_month['avgGrowth']:+.2f}% avg growth")
    log(f"  ({best_month['positiveCount']}/{best_month['occurrences']} positive occurrences)")

    log(f"\nWORST PERFORMING MONTH:")
    log(f"  {worst_month['month']}: {worst_month['avgGrowth']:+.2f}% avg growth")
    log(f"  ({worst_month['positiveCount']}/{worst_month['occurrences']} positive occurrences)")

    log(f"\nMOST CONSISTENTLY POSITIVE:")
    log(f"  {most_consistent_positive['month']}: {most_consistent_positive['positivePercentage']:.1f}% positive rate")
    log(f"  ({most_consistent_positive['positiveCount']}/{most_consistent_positive['occurrences']} positive)")

    log(f"\nMOST CONSISTENTLY NEGATIVE:")
    log(f"  {most_consistent_negative['month']}: {most_consistent_negative['positivePercentage']:.1f}% positive rate")
    log(f"  ({most_consistent_negative['positiveCount']}/{most_consistent_negative['occurrences']} positive)")

    # Detailed breakdown by month
    log(f"\nDETAILED MONTHLY BREAKDOWN:")
    log("-" * 80)
    log(f"{'Month':<12} {'Avg Growth':<12} {'Positive':<10} {'Negative':<10} {'Win Rate':<10}")
    log("-" * 80)

    for data in consolidated_data:
        log(f"{data['month']:<12} {data['avgGrowth']:>+10.2f}% "
            f"{data['positiveCount']:>9} {data['negativeCount']:>10} "
            f"{data['positivePercentage']:>9.1f}%")

    log("="*80 + "\n")

if __name__ == "__main__":
    # Parse command line arguments
    parser = argparse.ArgumentParser(description='Fetch S&P 500 monthly growth data')
    parser.add_argument('--years', type=int, default=5, help='Number of years of historical data (1-20)')
    args = parser.parse_args()

    # Validate years
    years = max(1, min(20, args.years))  # Clamp between 1-20

    # Fetch the data
    result = fetch_sp500_monthly_growth(years)

    if result.get('success') and result.get('monthlyData'):
        # Consolidate monthly data by calendar month
        consolidated = consolidate_monthly_data(result['monthlyData'])

        # Add consolidated data to result
        result['consolidatedMonthlyData'] = consolidated

        # Create plots
        plot_monthly_analysis(consolidated)

        # Print summary to stderr for debugging
        print_monthly_summary(consolidated)

    # Output JSON result to stdout
    print(json.dumps(result, indent=2))
