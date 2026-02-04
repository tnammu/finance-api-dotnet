"""
ETF Holdings Fetcher using multiple sources
Fetches ETF information from Yahoo Finance and holdings from web scraping
"""

import sys
import yfinance as yf
import sqlite3
import json
from datetime import datetime
import os
import requests
from bs4 import BeautifulSoup
import time

# Helper function to print to stderr (so it doesn't interfere with JSON output to stdout)
def log(message):
    print(message, file=sys.stderr)

def fetch_stock_price_data(symbol):
    """Fetch current price data for a stock symbol using fast info method"""
    try:
        stock = yf.Ticker(symbol)

        # Try using fast info first (much faster than history)
        try:
            info = stock.info
            current_price = info.get('currentPrice') or info.get('regularMarketPrice')
            previous_close = info.get('previousClose') or info.get('regularMarketPreviousClose')

            if current_price and previous_close:
                price_change = current_price - previous_close
                percent_change = (price_change / previous_close) * 100

                return {
                    'currentPrice': round(float(current_price), 2),
                    'priceChange': round(float(price_change), 2),
                    'percentChange': round(float(percent_change), 2)
                }
        except Exception as e:
            log(f"Info method failed for {symbol}, trying history: {e}")

        # Fallback to history if info fails
        hist = stock.history(period='1d')

        if not hist.empty:
            current_price = hist['Close'].iloc[-1]
            # Get previous close from the ticker info
            ticker_info = stock.info
            previous_close = ticker_info.get('previousClose', current_price)

            price_change = current_price - previous_close
            percent_change = (price_change / previous_close) * 100

            return {
                'currentPrice': round(current_price, 2),
                'priceChange': round(price_change, 2),
                'percentChange': round(percent_change, 2)
            }

        return None

    except Exception as e:
        log(f"Error fetching price data for {symbol}: {e}")
        return None

def connect_db():
    """Connect to the SQLite database"""
    db_path = os.path.join(os.path.dirname(__file__), '..', 'dividends.db')
    return sqlite3.connect(db_path)

def fetch_holdings_from_etfdb(symbol):
    """Scrape holdings data from etfdb.com"""
    holdings = []

    try:
        # Remove .TO suffix for scraping
        scrape_symbol = symbol.replace('.TO', '')

        url = f"https://etfdb.com/etf/{scrape_symbol}/#holdings"
        headers = {
            'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36',
            'Accept': 'text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8',
            'Accept-Language': 'en-US,en;q=0.5',
            'Referer': 'https://etfdb.com/'
        }

        log(f"Fetching holdings from etfdb.com for {scrape_symbol}...")
        response = requests.get(url, headers=headers, timeout=15)

        if response.status_code == 200:
            soup = BeautifulSoup(response.content, 'html.parser')

            # Find all tables on the page
            tables = soup.find_all('table')

            for table in tables:
                rows = table.find_all('tr')

                for row in rows[1:21]:  # Skip header, get top 20
                    cols = row.find_all('td')
                    if len(cols) >= 2:
                        # Try multiple column layouts
                        ticker = ''
                        name = ''
                        weight = 0

                        # Extract ticker (could be in link or plain text)
                        ticker_elem = cols[0].find('a')
                        if ticker_elem:
                            ticker = ticker_elem.text.strip()
                        else:
                            ticker = cols[0].text.strip()

                        # Extract name
                        if len(cols) > 1:
                            name = cols[1].text.strip()

                        # Extract weight (look for % symbol in any column)
                        for col in cols:
                            text = col.text.strip()
                            if '%' in text:
                                try:
                                    weight = float(text.replace('%', '').replace(',', '').strip())
                                    break
                                except:
                                    continue

                        # Only add if we have valid data
                        if ticker and weight > 0 and len(ticker) <= 10:
                            holdings.append({
                                'symbol': ticker.upper(),
                                'name': name if name else ticker,
                                'weight': weight,
                                'sector': 'Unknown'
                            })

                # If we found holdings in this table, stop searching
                if holdings:
                    break

            if holdings:
                log(f"Found {len(holdings)} holdings from etfdb.com")
            else:
                log("Could not find holdings data on etfdb.com")
        else:
            log(f"Failed to fetch from etfdb.com: HTTP {response.status_code}")

    except Exception as e:
        log(f"Error scraping etfdb.com: {e}")

    return holdings

def fetch_holdings_from_yahoo_finance_web(symbol):
    """Scrape holdings from Yahoo Finance web page"""
    holdings = []

    try:
        # Remove .TO suffix for Yahoo Finance
        scrape_symbol = symbol.replace('.TO', '')

        url = f"https://finance.yahoo.com/quote/{scrape_symbol}/holdings"
        headers = {
            'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36'
        }

        log(f"Fetching holdings from Yahoo Finance web for {scrape_symbol}...")
        response = requests.get(url, headers=headers, timeout=10)

        if response.status_code == 200:
            soup = BeautifulSoup(response.content, 'html.parser')

            # Look for holdings data in various formats
            # Yahoo Finance uses different HTML structures, so we try multiple approaches

            # Try finding tables with holdings data
            tables = soup.find_all('table')
            for table in tables:
                rows = table.find_all('tr')
                for row in rows[:20]:
                    cols = row.find_all('td')
                    if len(cols) >= 2:
                        # Try to extract symbol and weight
                        text_content = [col.get_text(strip=True) for col in cols]

                        # Look for percentage weights
                        for i, text in enumerate(text_content):
                            if '%' in text and i > 0:
                                try:
                                    weight = float(text.replace('%', ''))
                                    symbol_text = text_content[0]
                                    name_text = text_content[1] if len(text_content) > 1 else symbol_text

                                    if symbol_text and weight > 0:
                                        holdings.append({
                                            'symbol': symbol_text,
                                            'name': name_text,
                                            'weight': weight,
                                            'sector': 'Unknown'
                                        })
                                        break
                                except:
                                    continue

            if holdings:
                log(f"Found {len(holdings)} holdings from Yahoo Finance web")
            else:
                log("Could not find holdings on Yahoo Finance web page")
        else:
            log(f"Failed to fetch from Yahoo Finance: HTTP {response.status_code}")

    except Exception as e:
        log(f"Error scraping Yahoo Finance web: {e}")

    return holdings



def fetch_etf_data(symbol):
    """Fetch ETF data from Yahoo Finance"""
    try:
        etf = yf.Ticker(symbol)
        info = etf.info

        log(f"Fetching data for {symbol}...")

        # Get basic ETF information
        etf_data = {
            'symbol': symbol.upper(),
            'name': info.get('longName', info.get('shortName', symbol)),
            'description': info.get('longBusinessSummary', ''),
            'category': info.get('category', info.get('fundFamily', '')),
            'totalAssets': info.get('totalAssets', 0),
            'expenseRatio': info.get('annualReportExpenseRatio', info.get('yield', 0)),
            'inceptionDate': info.get('fundInceptionDate', None)
        }

        # Try to get holdings using yfinance funds_data API
        holdings = []

        try:
            log(f"Fetching holdings from yfinance API for {symbol}...")
            funds_data = etf.get_funds_data()

            if funds_data and hasattr(funds_data, 'top_holdings') and funds_data.top_holdings is not None:
                top_holdings_df = funds_data.top_holdings

                # Convert DataFrame to list of dicts
                for idx, row in top_holdings_df.iterrows():
                    holdings.append({
                        'symbol': str(idx),  # Symbol is the index
                        'name': row.get('Name', str(idx)),
                        'weight': float(row.get('Holding Percent', 0)) * 100,  # Convert to percentage
                        'sector': 'Unknown'
                    })

                log(f"Found {len(holdings)} holdings from yfinance API")
            else:
                log(f"No top_holdings data available from yfinance for {symbol}")
        except Exception as e:
            log(f"Error fetching from yfinance API: {e}")

        # Fallback: Try web scraping if yfinance fails
        if not holdings:
            log("Trying web scraping methods...")
            holdings = fetch_holdings_from_etfdb(symbol)

        if not holdings:
            time.sleep(1)
            holdings = fetch_holdings_from_yahoo_finance_web(symbol)

        if not holdings:
            log(f"All methods failed, no holdings data available for {symbol}")

        # Fetch price data for each holding
        if holdings:
            log(f"Fetching price data for {len(holdings)} holdings...")
            for holding in holdings:
                stock_symbol = holding.get('symbol')
                if stock_symbol:
                    price_data = fetch_stock_price_data(stock_symbol)
                    if price_data:
                        holding['currentPrice'] = price_data['currentPrice']
                        holding['priceChange'] = price_data['priceChange']
                        holding['percentChange'] = price_data['percentChange']
            log(f"Price data fetched for holdings")

        # Calculate sector allocations from holdings
        sector_allocations = {}
        if holdings:
            for holding in holdings:
                sector = holding.get('sector', 'Unknown')
                weight = holding.get('weight', 0)
                if sector in sector_allocations:
                    sector_allocations[sector] += weight
                else:
                    sector_allocations[sector] = weight

        etf_data['holdings'] = holdings
        etf_data['sector_allocations'] = [
            {'sector': sector, 'weight': weight}
            for sector, weight in sector_allocations.items()
        ]
        etf_data['total_holdings'] = len(holdings)

        log(f"Successfully fetched {len(holdings)} holdings for {symbol}")

        return etf_data

    except Exception as e:
        log(f"Error fetching ETF data for {symbol}: {e}")
        import traceback
        traceback.print_exc(file=sys.stderr)
        return None


def main():
    if len(sys.argv) < 2:
        log("Usage: python fetch_etf_holdings.py <ETF_SYMBOL>")
        sys.exit(1)

    symbol = sys.argv[1].upper()
    log(f"Fetching ETF data for {symbol}...")

    etf_data = fetch_etf_data(symbol)

    if etf_data:
        # Return data as JSON without saving to database
        output = {
            'success': True,
            'symbol': symbol,
            'name': etf_data['name'],
            'holdings': etf_data['holdings'],
            'holdings_count': len(etf_data['holdings']),
            'sector_allocations': etf_data['sector_allocations']
        }
        print(json.dumps(output))
        sys.exit(0)
    else:
        print(json.dumps({
            'success': False,
            'error': f'Failed to fetch data for {symbol}'
        }))
        sys.exit(1)

if __name__ == "__main__":
    main()