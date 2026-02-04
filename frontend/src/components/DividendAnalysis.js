import React, { useState, useEffect, useRef } from 'react';
import { dividendsAPI, sectorAPI } from '../services/api';
import { LineChart, Line, BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer } from 'recharts';
import DividendCharts from './DividendCharts';
import StrategyAnalysis from './StrategyAnalysis';
import './DividendAnalysis.css';
import { useLocation } from 'react-router-dom';

function DividendAnalysis({ selectedSymbol }) {
  const [symbol, setSymbol] = useState('');
  const [exchange, setExchange] = useState('US');
  const [analysis, setAnalysis] = useState(null);
  const [cachedStocks, setCachedStocks] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const [stats, setStats] = useState(null);
  const [activeTab, setActiveTab] = useState('dividend'); // 'dividend' or 'strategy'
  const [sectorComparison, setSectorComparison] = useState(null);
  const lastSelectedSymbol = useRef(null);

  // Filters and sorting
  const [searchTicker, setSearchTicker] = useState('');
  const [tempFilters, setTempFilters] = useState({
    yieldMin: '',
    priceMax: '',
    sector: 'all',
    safetyRating: 'all',
    minYears: '',
    maxVolatility: '',
    minGrowthScore: '',
    maxSectorRank: '',
    tradingStrategy: 'all'
  });
  const [filters, setFilters] = useState({
    yieldMin: '',
    priceMax: '',
    sector: 'all',
    safetyRating: 'all',
    minYears: '',
    maxVolatility: '',
    minGrowthScore: '',
    maxSectorRank: '',
    tradingStrategy: 'all'
  });
  const [sortConfig, setSortConfig] = useState({ key: 'safetyScore', direction: 'desc' });
  const location = useLocation();

  // Pagination state (declare before using in useEffect)
  const [currentPage, setCurrentPage] = useState(1);
  const [dividendsPerPage, setDividendsPerPage] = useState(50);

  useEffect(() => {
    loadCachedStocks();
    loadStats();
    const urlParams = new URLSearchParams(window.location.search);
    const symbolFromUrl = urlParams.get('symbol');
  
    if (symbolFromUrl && !analysis) {
    // Auto-analyze the symbol from URL
    handleAnalyze(symbolFromUrl);
    }
  }, []);

  useEffect(() => {
    setCurrentPage(1);
  }, [filters, sortConfig]);

  useEffect(() => {
    if (location.state?.symbol && !analysis) {
      handleAnalyze(location.state.symbol);
    }
  }, [location.state]);

  useEffect(() => {
    if (selectedSymbol && selectedSymbol !== lastSelectedSymbol.current) {
      console.log('Analyzing selected symbol:', selectedSymbol);
      lastSelectedSymbol.current = selectedSymbol;
      handleAnalyze(selectedSymbol);
    }
  }, [selectedSymbol]);

  const applyFilters = () => {
    setFilters(tempFilters);
  };

  const clearFilters = () => {
    const defaultFilters = { yieldMin: '', priceMax: '', sector: 'all', safetyRating: 'all', minYears: '', maxVolatility: '', minGrowthScore: '', maxSectorRank: '', tradingStrategy: 'all' };
    setTempFilters(defaultFilters);
    setFilters(defaultFilters);
  };

  // Trading Strategy Classification Function
  const classifyTradingStrategy = (stock) => {
    const strategies = [];

    // Long-term Hold - stable dividend payers for buy & hold
    // Needs: decent dividend history OR high safety score
    if (
      (stock.consecutiveYears >= 3 && stock.dividendYield >= 1.5) ||
      (stock.safetyScore >= 60 && stock.dividendYield >= 1.0)
    ) {
      strategies.push('long-term-hold');
    }

    // Swing Trading - stocks with price movement potential
    // Needs: volatility OR beta indicating movement
    if (
      stock.currentPrice && stock.currentPrice <= 500 &&
      (
        (stock.dailyVolatility && stock.dailyVolatility >= 1.5) ||
        (stock.beta && stock.beta >= 1.0)
      )
    ) {
      strategies.push('swing-trading');
    }

    // RSI/Mean Reversion - stocks good for technical trading
    // Needs: some volatility + not too risky
    if (
      (stock.dailyVolatility && stock.dailyVolatility >= 1.0 && stock.dailyVolatility <= 4.0) ||
      (stock.beta && stock.beta >= 0.8 && stock.beta <= 1.5)
    ) {
      if (stock.safetyScore >= 35 || stock.consecutiveYears >= 1) {
        strategies.push('rsi-mean-reversion');
      }
    }

    // Seasonal/Cyclical - sector-based seasonality
    const seasonalSectors = ['Consumer Cyclical', 'Energy', 'Basic Materials', 'Industrials', 'Financial', 'Real Estate'];
    if (seasonalSectors.some(s => stock.sector?.includes(s))) {
      strategies.push('seasonal');
    }

    // Growth + Dividend - growing companies with dividends
    if (
      (stock.growthScore && stock.growthScore >= 40) ||
      (stock.dividendGrowthRate && stock.dividendGrowthRate >= 3)
    ) {
      if (stock.dividendYield >= 0.5) {
        strategies.push('growth-dividend');
      }
    }

    // Value Investing - potentially undervalued stocks
    const isLowPE = stock.peRatio && stock.peRatio > 0 && stock.peRatio <= 18;
    const isLowPB = stock.pbRatio && stock.pbRatio > 0 && stock.pbRatio <= 3;
    if (
      (isLowPE || isLowPB) &&
      stock.dividendYield >= 1.5 &&
      stock.safetyScore >= 45
    ) {
      strategies.push('value-investing');
    }

    // High Yield Income - focus on high yield
    if (
      stock.dividendYield >= 4.0 &&
      stock.consecutiveYears >= 2
    ) {
      strategies.push('high-yield');
    }

    // DRIP Candidates - good for dividend reinvestment
    if (
      (stock.dividendGrowthRate && stock.dividendGrowthRate >= 4) ||
      (stock.growthScore && stock.growthScore >= 50)
    ) {
      if (stock.consecutiveYears >= 2 && stock.dividendYield >= 0.5) {
        strategies.push('drip');
      }
    }

    return strategies.length > 0 ? strategies : ['general'];
  };

  const getStrategyLabel = (strategy) => {
    const labels = {
      'long-term-hold': 'Long-Term Hold',
      'swing-trading': 'Swing Trading',
      'rsi-mean-reversion': 'RSI/Mean Reversion',
      'seasonal': 'Seasonal/Cyclical',
      'growth-dividend': 'Growth + Dividend',
      'value-investing': 'Value Investing',
      'high-yield': 'High Yield Income',
      'drip': 'DRIP Candidate',
      'general': 'General'
    };
    return labels[strategy] || strategy;
  };

  const getStrategyColor = (strategy) => {
    const colors = {
      'long-term-hold': '#22c55e',
      'swing-trading': '#f59e0b',
      'rsi-mean-reversion': '#8b5cf6',
      'seasonal': '#ec4899',
      'growth-dividend': '#3b82f6',
      'value-investing': '#14b8a6',
      'high-yield': '#ef4444',
      'drip': '#6366f1',
      'general': '#6b7280'
    };
    return colors[strategy] || '#6b7280';
  };

  const getStrategyDescription = (strategy) => {
    const descriptions = {
      'long-term-hold': 'Stable dividend payers with 10+ years history, low volatility - ideal for buy and hold',
      'swing-trading': 'Higher volatility stocks near support levels - good for short-term trades',
      'rsi-mean-reversion': 'Stocks suitable for RSI-based trading strategies',
      'seasonal': 'Cyclical sectors that follow seasonal patterns',
      'growth-dividend': 'Growing companies that also pay increasing dividends',
      'value-investing': 'Undervalued stocks with strong fundamentals',
      'high-yield': 'High dividend yield (5%+) with sustainable payout',
      'drip': 'Great for dividend reinvestment with strong dividend growth',
      'general': 'Does not fit specific strategy criteria'
    };
    return descriptions[strategy] || '';
  };


  const loadCachedStocks = async () => {
    try {
      const response = await dividendsAPI.getCached();
      setCachedStocks(response.data.stocks || []);
    } catch (err) {
      console.error('Failed to load cached stocks:', err);
    }
  };

  const loadStats = async () => {
    try {
      const response = await dividendsAPI.getStats();
      setStats(response.data);
    } catch (err) {
      console.error('Failed to load stats:', err);
    }
  };

  const handleAnalyze = async (symbolToAnalyze = symbol, refresh = false, useExchange = exchange) => {
    if (!symbolToAnalyze.trim()) {
      setError('Please enter a stock symbol');
      return;
    }

    try {
      setLoading(true);
      setError(null);

      // Add exchange suffix if not already present
      let finalSymbol = symbolToAnalyze.toUpperCase();
      if (useExchange === 'CA' && !finalSymbol.includes('.')) {
        finalSymbol = `${finalSymbol}.TO`;
      }

      const response = await dividendsAPI.analyze(finalSymbol, refresh);
      setAnalysis(response.data);
      setSymbol('');
      await loadCachedStocks();
      await loadStats();

      // Load sector comparison data
      await loadSectorComparison(finalSymbol);
    } catch (err) {
      setError(err.response?.data?.error || 'Failed to analyze Dividend');
    } finally {
      setLoading(false);
    }
  };

  const loadSectorComparison = async (stockSymbol) => {
    try {
      const response = await sectorAPI.getStockComparison(stockSymbol);
      setSectorComparison(response.data.comparison);
    } catch (err) {
      console.error('Failed to load sector comparison:', err);
      setSectorComparison(null);
    }
  };

  const handleSelectCached = async (cachedSymbol) => {
    await handleAnalyze(cachedSymbol, false);
  };

  const handleRefreshCached = async (cachedSymbol) => {
    await handleAnalyze(cachedSymbol, true);
  };

  const handleDeleteCached = async (cachedSymbol) => {
    if (!window.confirm(`Delete ${cachedSymbol} from dividend analysis?`)) {
      return;
    }

    try {
      await dividendsAPI.deleteCached(cachedSymbol);
      await loadCachedStocks();
      await loadStats();

      // Clear analysis if currently viewing this stock
      if (analysis && analysis.symbol === cachedSymbol) {
        setAnalysis(null);
      }
    } catch (err) {
      setError(`Failed to delete ${cachedSymbol}: ` + (err.response?.data?.error || err.message));
    }
  };

  const getSafetyColor = (rating) => {
    const colors = {
      'Excellent': '#27ae60',
      'Good': '#2ecc71',
      'Fair': '#f39c12',
      'Poor': '#e67e22',
      'Very Poor': '#e74c3c'
    };
    return colors[rating] || '#95a5a6';
  };

  const getPolicyColor = (policy) => {
    switch (policy) {
      case 'Dividend Only':
        return '#3498db'; // Blue
      case 'Reinvestment Only':
        return '#9b59b6'; // Purple
      case 'Mixed':
        return '#16a085'; // Teal
      default:
        return '#95a5a6'; // Gray
    }
  };

  const getPolicyEmoji = (policy) => {
    switch (policy) {
      case 'Dividend Only':
        return '💰';
      case 'Reinvestment Only':
        return '📈';
      case 'Mixed':
        return '⚖️';
      default:
        return '❓';
    }
  };

  const handleExportDividends = () => {
    const API_BASE_URL = 'http://localhost:5000/api';
    window.open(`${API_BASE_URL}/dividends/export/csv`, '_blank');
  };

  const handleExportPayments = () => {
    const API_BASE_URL = 'http://localhost:5000/api';
    window.open(`${API_BASE_URL}/dividends/export/payments/csv`, '_blank');
  };

  const handleExportUsage = () => {
    const API_BASE_URL = 'http://localhost:5000/api';
    window.open(`${API_BASE_URL}/dividends/export/usage/csv`, '_blank');
  };

  const handleBulkImportTSX = async () => {
    if (!window.confirm('This will fetch and import ALL TSX stocks. This may take several minutes. Continue?')) {
      return;
    }

    try {
      setLoading(true);
      setError(null);

      const response = await dividendsAPI.bulkImportTSX();

      alert(`TSX bulk import started!\n\n${response.data.message}\n\nThe import is running in the background. Check the API logs for progress.`);

      // Reload cached stocks after a delay
      setTimeout(() => {
        loadCachedStocks();
      }, 5000);
    } catch (err) {
      setError(err.response?.data?.error || 'Failed to start bulk import');
      alert(`Error starting bulk import: ${err.response?.data?.error || err.message}`);
    } finally {
      setLoading(false);
    }
  };

  // Filter and sort stocks
  const getFilteredAndSortedStocks = () => {
    let filtered = [...cachedStocks];

    // Apply search filter
    if (searchTicker) {
      const searchLower = searchTicker.toLowerCase();
      filtered = filtered.filter(s =>
        s.symbol.toLowerCase().includes(searchLower) ||
        (s.companyName && s.companyName.toLowerCase().includes(searchLower))
      );
    }

    // Apply filters
    if (filters.yieldMin) {
      filtered = filtered.filter(s => s.dividendYield && s.dividendYield >= parseFloat(filters.yieldMin));
    }
    if (filters.priceMax) {
      filtered = filtered.filter(s => s.currentPrice && s.currentPrice <= parseFloat(filters.priceMax));
    }
    if (filters.sector !== 'all') {
      filtered = filtered.filter(s => s.sector === filters.sector);
    }
    if (filters.safetyRating !== 'all') {
      filtered = filtered.filter(s => s.safetyRating === filters.safetyRating);
    }
    if (filters.policy && filters.policy !== 'all') {
      filtered = filtered.filter(s => s.payoutPolicy === filters.policy);
    }
    if (filters.minYears) {
      filtered = filtered.filter(s => s.consecutiveYears >= parseInt(filters.minYears));
    }
    if (filters.maxVolatility) {
      filtered = filtered.filter(s => s.dailyVolatility && s.dailyVolatility <= parseFloat(filters.maxVolatility));
    }
    if (filters.minGrowthScore) {
      filtered = filtered.filter(s => s.growthScore >= parseFloat(filters.minGrowthScore));
    }
    if (filters.maxSectorRank) {
      filtered = filtered.filter(s => s.sectorRank && s.sectorRank <= parseInt(filters.maxSectorRank));
    }

    // Apply trading strategy filter
    if (filters.tradingStrategy && filters.tradingStrategy !== 'all') {
      filtered = filtered.filter(s => {
        const strategies = classifyTradingStrategy(s);
        return strategies.includes(filters.tradingStrategy);
      });
    }

    // Apply sorting
    if (sortConfig.key) {
      filtered.sort((a, b) => {
        let aVal = a[sortConfig.key];
        let bVal = b[sortConfig.key];

        // Handle null/undefined
        if (aVal == null) return 1;
        if (bVal == null) return -1;

        if (typeof aVal === 'string') {
          aVal = aVal.toLowerCase();
          bVal = bVal.toLowerCase();
        }

        if (aVal < bVal) return sortConfig.direction === 'asc' ? -1 : 1;
        if (aVal > bVal) return sortConfig.direction === 'asc' ? 1 : -1;
        return 0;
      });
    }

    return filtered;
  };

  const handleSort = (key) => {
    setSortConfig({
      key,
      direction: sortConfig.key === key && sortConfig.direction === 'asc' ? 'desc' : 'asc'
    });
  };

  const getSortIcon = (key) => {
    if (sortConfig.key !== key) return '⇅';
    return sortConfig.direction === 'asc' ? '↑' : '↓';
  };

  // Get unique sectors for filter dropdown
  const uniqueSectors = [...new Set(cachedStocks.map(s => s.sector).filter(Boolean))];

  // Get filtered stocks first
  const filteredStocks = getFilteredAndSortedStocks();

  // Dynamic Pagination Logic
  const getPaginatedDividends = () => {
    const allDividends = filteredStocks;
    const totalDividends = allDividends.length;
    if (dividendsPerPage === -1) {
      return { dividends: allDividends, totalPages: 1, totalDividends };
    }

    // Calculate pagination
    const totalPages = Math.ceil(totalDividends / dividendsPerPage);
    const startIndex = (currentPage - 1) * dividendsPerPage;
    const endIndex = startIndex + dividendsPerPage;
    const paginated = allDividends.slice(startIndex, endIndex);

    return { dividends: paginated, totalPages, totalDividends };
  };

  const { dividends: paginatedStocks, totalPages, totalDividends } = getPaginatedDividends(); 

  return (
    <div className="dividend-analysis">
      <div className="card">
        <h2>Dividend Analysis</h2>
        <p style={{ color: '#7f8c8d', marginBottom: '1.5rem' }}>
          Analyze dividend stocks with historical data and safety ratings
        </p>

        <div className="search-section">
          <div style={{ display: 'flex', gap: '0.75rem', alignItems: 'center' }}>
            <select
              value={exchange}
              onChange={(e) => setExchange(e.target.value)}
              disabled={loading}
              style={{
                padding: '0.75rem',
                borderRadius: '8px',
                border: '2px solid #e1e8ed',
                fontSize: '1rem',
                backgroundColor: 'white',
                cursor: 'pointer',
                minWidth: '120px'
              }}
            >
              <option value="US">🇺🇸 US</option>
              <option value="CA">🇨🇦 Canada</option>
            </select>
            <input
              type="text"
              value={symbol}
              onChange={(e) => setSymbol(e.target.value)}
              placeholder={exchange === 'CA' ? 'e.g., XEQT, VFV, TD' : 'e.g., AAPL, MSFT, JNJ'}
              onKeyPress={(e) => e.key === 'Enter' && handleAnalyze()}
              disabled={loading}
              style={{ flex: 1 }}
            />
            <button
              className="primary"
              onClick={() => handleAnalyze()}
              disabled={loading || !symbol.trim()}
            >
              {loading ? 'Analyzing...' : 'Analyze'}
            </button>
            <button
              onClick={handleBulkImportTSX}
              disabled={loading || exchange !== 'CA'}
              style={{
                padding: '0.75rem 1.5rem',
                backgroundColor: exchange === 'CA' ? '#10b981' : '#9ca3af',
                color: 'white',
                border: 'none',
                borderRadius: '8px',
                fontSize: '1rem',
                cursor: (loading || exchange !== 'CA') ? 'not-allowed' : 'pointer',
                fontWeight: '600',
                opacity: (loading || exchange !== 'CA') ? 0.6 : 1,
                transition: 'all 0.2s',
                minWidth: '200px'
              }}
              onMouseEnter={(e) => exchange === 'CA' && !loading && (e.target.style.backgroundColor = '#059669')}
              onMouseLeave={(e) => exchange === 'CA' && (e.target.style.backgroundColor = '#10b981')}
              title={exchange === 'CA' ? 'Import all TSX stocks from exchange listing' : 'NASDAQ bulk import coming soon'}
            >
              🔄 Import All {exchange === 'CA' ? 'TSX' : 'NASDAQ'}
            </button>
          </div>
          {exchange === 'CA' && (
            <p style={{ marginTop: '0.5rem', fontSize: '0.875rem', color: '#7f8c8d' }}>
              Canadian stocks will automatically use Toronto Stock Exchange (.TO)
            </p>
          )}
        </div>

        {error && <div className="error">{error}</div>}
      </div>

      {stats && (
        <div className="card">
          <h3>Database Statistics</h3>
          <div className="stats-grid">
            <div className="stat-card">
              <h4>Cached Stocks</h4>
              <div className="value">{stats.totalStocksCached}</div>
            </div>
            <div className="stat-card">
              <h4>Dividend Payments</h4>
              <div className="value">{stats.totalDividendPayments}</div>
            </div>
            <div className="stat-card">
              <h4>Top Scorer</h4>
              <div className="value" style={{ fontSize: '1.2rem' }}>
                {stats.topScoringStocks?.[0]?.symbol || 'N/A'}
              </div>
            </div>
            <div className="stat-card">
              <h4>Sectors</h4>
              <div className="value">{stats.bySector?.length || 0}</div>
            </div>
          </div>

          {stats.topScoringStocks && stats.topScoringStocks.length > 0 && (
            <div style={{ marginTop: '1.5rem' }}>
              <h4>Top 5 Dividend Stocks</h4>
              <div className="top-stocks">
                {stats.topScoringStocks.map((stock, idx) => (
                  <div key={idx} className="top-stock-item">
                    <div className="rank">{idx + 1}</div>
                    <div className="stock-details">
                      <strong>{stock.symbol}</strong>
                      <span>{stock.companyName}</span>
                    </div>
                    <div className="score" style={{ color: getSafetyColor(stock.rating) }}>
                      {stock.safetyScore.toFixed(1)}
                    </div>
                    <span className="badge" style={{ background: getSafetyColor(stock.rating) + '20', color: getSafetyColor(stock.rating) }}>
                      {stock.rating}
                    </span>
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>
      )}

      {cachedStocks.length > 0 && (
        <div className="card">
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1rem' }}>
            <div>
              <h3>My Portfolio ({paginatedStocks.length} of {filteredStocks.length} {searchTicker ? 'matching' : 'filtered'} stocks)</h3>
            </div>
            <div style={{ display: 'flex', gap: '0.5rem' }}>
              <button className="secondary" onClick={handleExportDividends} style={{ fontSize: '0.875rem', padding: '0.5rem 1rem' }}>
                Export All
              </button>
              <button className="secondary" onClick={handleExportPayments} style={{ fontSize: '0.875rem', padding: '0.5rem 1rem' }}>
                Export Payments
              </button>
            </div>
          </div>

          {/* Search Bar */}
          <div style={{ marginBottom: '1rem' }}>
            <input
              type="text"
              placeholder="🔍 Search by ticker or company name..."
              value={searchTicker}
              onChange={(e) => {
                setSearchTicker(e.target.value);
                setCurrentPage(1);
              }}
              style={{
                width: '100%',
                padding: '0.75rem',
                fontSize: '1rem',
                border: '2px solid #e1e8ed',
                borderRadius: '8px',
                outline: 'none',
                transition: 'border-color 0.2s'
              }}
              onFocus={(e) => e.target.style.borderColor = '#3498db'}
              onBlur={(e) => e.target.style.borderColor = '#e1e8ed'}
            />
            {searchTicker && (
              <div style={{ marginTop: '0.5rem', fontSize: '0.875rem', color: '#64748b' }}>
                Found {filteredStocks.length} stocks matching "{searchTicker}"
                <button
                  onClick={() => setSearchTicker('')}
                  style={{
                    marginLeft: '0.5rem',
                    padding: '0.25rem 0.5rem',
                    background: '#e74c3c',
                    color: 'white',
                    border: 'none',
                    borderRadius: '4px',
                    cursor: 'pointer',
                    fontSize: '0.75rem'
                  }}
                >
                  Clear Search
                </button>
              </div>
            )}
          </div>

          <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'center', marginBottom: '1rem' }}>
           <span>Show:</span>
            <select
              value={dividendsPerPage}
              onChange={(e) => {
              setDividendsPerPage(Number(e.target.value));
              setCurrentPage(1); // reset to first page
            }}
            style={{
              padding: '0.5rem',
              borderRadius: '4px',
              border: '1px solid #ddd',
              fontSize: '0.875rem'
           }}
            >
            <option value={10}>10</option>
            <option value={20}>20</option>
            <option value={50}>50</option>
            <option value={100}>100</option>
            <option value={200}>200</option>
            <option value={-1}>All</option> {/* optional: show all */}
            </select>
          </div>


          {/* Filters */}
          <div style={{
            display: 'grid',
            gridTemplateColumns: 'repeat(auto-fit, minmax(150px, 1fr))',
            gap: '1rem',
            marginBottom: '1.5rem',
            padding: '1rem',
            background: '#f8f9fa',
            borderRadius: '8px'
          }}>
            <div>
              <label style={{ display: 'block', fontSize: '0.875rem', marginBottom: '0.5rem', color: '#555' }}>Min Yield %</label>
              <input
                type="number"
                step="0.5"
                placeholder="e.g., 3"
                value={tempFilters.yieldMin}
                onChange={(e) => setTempFilters({...tempFilters, yieldMin: e.target.value})}
                onKeyPress={(e) => e.key === 'Enter' && applyFilters()}
                style={{
                  width: '100%',
                  padding: '0.5rem',
                  border: '1px solid #ddd',
                  borderRadius: '4px',
                  fontSize: '0.875rem'
                }}
              />
            </div>
            <div>
              <label style={{ display: 'block', fontSize: '0.875rem', marginBottom: '0.5rem', color: '#555' }}>Max Price $</label>
              <input
                type="number"
                step="10"
                placeholder="e.g., 200"
                value={tempFilters.priceMax}
                onChange={(e) => setTempFilters({...tempFilters, priceMax: e.target.value})}
                onKeyPress={(e) => e.key === 'Enter' && applyFilters()}
                style={{
                  width: '100%',
                  padding: '0.5rem',
                  border: '1px solid #ddd',
                  borderRadius: '4px',
                  fontSize: '0.875rem'
                }}
              />
            </div>
            <div>
              <label style={{ display: 'block', fontSize: '0.875rem', marginBottom: '0.5rem', color: '#555' }}>Sector</label>
              <select
                value={tempFilters.sector}
                onChange={(e) => setTempFilters({...tempFilters, sector: e.target.value})}
                style={{
                  width: '100%',
                  padding: '0.5rem',
                  border: '1px solid #ddd',
                  borderRadius: '4px',
                  fontSize: '0.875rem',
                  background: 'white'
                }}
              >
                <option value="all">All Sectors</option>
                {uniqueSectors.map(sector => (
                  <option key={sector} value={sector}>{sector}</option>
                ))}
              </select>
            </div>
            <div>
              <label style={{ display: 'block', fontSize: '0.875rem', marginBottom: '0.5rem', color: '#555' }}>Safety Rating</label>
              <select
                value={tempFilters.safetyRating}
                onChange={(e) => setTempFilters({...tempFilters, safetyRating: e.target.value})}
                style={{
                  width: '100%',
                  padding: '0.5rem',
                  border: '1px solid #ddd',
                  borderRadius: '4px',
                  fontSize: '0.875rem',
                  background: 'white'
                }}
              >
                <option value="all">All Ratings</option>
                <option value="Excellent">Excellent</option>
                <option value="Very Good">Very Good</option>
                <option value="Good">Good</option>
                <option value="Fair">Fair</option>
                <option value="Below Average">Below Average</option>
                <option value="Poor">Poor</option>
              </select>
            </div>
            <div>
              <label style={{ display: 'block', fontSize: '0.875rem', marginBottom: '0.5rem', color: '#555' }}>
                Policy
              </label>
              <select
                value={tempFilters.policy || 'all'}
                onChange={(e) => setTempFilters({ ...tempFilters, policy: e.target.value })}
                style={{
                  width: '100%',
                  padding: '0.5rem',
                  border: '1px solid #ddd',
                  borderRadius: '4px',
                  fontSize: '0.875rem',
                  background: 'white'
                }}
              >
              <option value="all">All Policies</option>
              <option value="Dividend Only">💰 Dividend Only</option>
              <option value="Reinvestment Only">📈 Reinvestment Only</option>
              <option value="Mixed">⚖️ Mixed</option>
              </select>
            </div>

            <div>
              <label style={{ display: 'block', fontSize: '0.875rem', marginBottom: '0.5rem', color: '#555' }}>Min Years</label>
              <input
                type="number"
                placeholder="e.g., 10"
                value={tempFilters.minYears}
                onChange={(e) => setTempFilters({...tempFilters, minYears: e.target.value})}
                onKeyPress={(e) => e.key === 'Enter' && applyFilters()}
                style={{
                  width: '100%',
                  padding: '0.5rem',
                  border: '1px solid #ddd',
                  borderRadius: '4px',
                  fontSize: '0.875rem'
                }}
              />
            </div>
            <div>
              <label style={{ display: 'block', fontSize: '0.875rem', marginBottom: '0.5rem', color: '#555' }}>Max Volatility %</label>
              <input
                type="number"
                step="0.1"
                placeholder="e.g., 2.5"
                value={tempFilters.maxVolatility}
                onChange={(e) => setTempFilters({...tempFilters, maxVolatility: e.target.value})}
                onKeyPress={(e) => e.key === 'Enter' && applyFilters()}
                style={{
                  width: '100%',
                  padding: '0.5rem',
                  border: '1px solid #ddd',
                  borderRadius: '4px',
                  fontSize: '0.875rem'
                }}
              />
            </div>
            <div>
              <label style={{ display: 'block', fontSize: '0.875rem', marginBottom: '0.5rem', color: '#555' }}>Min Growth Score</label>
              <input
                type="number"
                placeholder="e.g., 50"
                value={tempFilters.minGrowthScore}
                onChange={(e) => setTempFilters({...tempFilters, minGrowthScore: e.target.value})}
                onKeyPress={(e) => e.key === 'Enter' && applyFilters()}
                style={{
                  width: '100%',
                  padding: '0.5rem',
                  border: '1px solid #ddd',
                  borderRadius: '4px',
                  fontSize: '0.875rem'
                }}
              />
            </div>
            <div>
              <label style={{ display: 'block', fontSize: '0.875rem', marginBottom: '0.5rem', color: '#555' }}>Max Sector Rank</label>
              <input
                type="number"
                placeholder="e.g., 5"
                value={tempFilters.maxSectorRank}
                onChange={(e) => setTempFilters({...tempFilters, maxSectorRank: e.target.value})}
                onKeyPress={(e) => e.key === 'Enter' && applyFilters()}
                style={{
                  width: '100%',
                  padding: '0.5rem',
                  border: '1px solid #ddd',
                  borderRadius: '4px',
                  fontSize: '0.875rem'
                }}
              />
            </div>
            <div>
              <label style={{ display: 'block', fontSize: '0.875rem', marginBottom: '0.5rem', color: '#555' }}>
                Trading Strategy
              </label>
              <select
                value={tempFilters.tradingStrategy}
                onChange={(e) => setTempFilters({ ...tempFilters, tradingStrategy: e.target.value })}
                style={{
                  width: '100%',
                  padding: '0.5rem',
                  border: '1px solid #ddd',
                  borderRadius: '4px',
                  fontSize: '0.875rem',
                  background: 'white'
                }}
              >
                <option value="all">All Strategies</option>
                <option value="long-term-hold">Long-Term Hold</option>
                <option value="swing-trading">Swing Trading</option>
                <option value="rsi-mean-reversion">RSI/Mean Reversion</option>
                <option value="seasonal">Seasonal/Cyclical</option>
                <option value="growth-dividend">Growth + Dividend</option>
                <option value="value-investing">Value Investing</option>
                <option value="high-yield">High Yield Income</option>
                <option value="drip">DRIP Candidate</option>
              </select>
            </div>
            <div style={{ display: 'flex', alignItems: 'flex-end', gap: '0.5rem' }}>
              <button
                onClick={applyFilters}
                style={{
                  flex: 1,
                  padding: '0.5rem',
                  background: '#3498db',
                  color: 'white',
                  border: 'none',
                  borderRadius: '4px',
                  cursor: 'pointer',
                  fontSize: '0.875rem',
                  fontWeight: '600'
                }}
              >
                Apply
              </button>
              <button
                onClick={clearFilters}
                style={{
                  flex: 1,
                  padding: '0.5rem',
                  background: '#6c757d',
                  color: 'white',
                  border: 'none',
                  borderRadius: '4px',
                  cursor: 'pointer',
                  fontSize: '0.875rem'
                }}
              >
                Clear
              </button>
            </div>
          </div>

          {/* Strategy Legend */}
          {filters.tradingStrategy !== 'all' && (
            <div style={{
              marginBottom: '1rem',
              padding: '1rem',
              background: getStrategyColor(filters.tradingStrategy) + '10',
              borderRadius: '8px',
              borderLeft: `4px solid ${getStrategyColor(filters.tradingStrategy)}`
            }}>
              <strong style={{ color: getStrategyColor(filters.tradingStrategy) }}>
                {getStrategyLabel(filters.tradingStrategy)}:
              </strong>
              <span style={{ marginLeft: '0.5rem', color: '#4b5563' }}>
                {getStrategyDescription(filters.tradingStrategy)}
              </span>
            </div>
          )}

          <div style={{ overflowX: 'auto', maxWidth: '100%' }}>
            <table style={{
              width: '100%',
              borderCollapse: 'collapse',
              fontSize: '0.8rem',
              tableLayout: 'fixed'
            }}>
              <thead>
                <tr style={{
                  backgroundColor: '#e9ecef',
                  borderBottom: '2px solid #adb5bd',
                  borderTop: '2px solid #adb5bd',
                  color: '#495057',
                  fontWeight: '600'
                }}>

                  <th onClick={() => handleSort('symbol')} style={{ padding: '0.5rem', textAlign: 'left', cursor: 'pointer', userSelect: 'none', width: '7%', minWidth: '60px' }}>
                    Symbol {getSortIcon('symbol')}
                  </th>
                  <th onClick={() => handleSort('companyName')} style={{ padding: '0.5rem', textAlign: 'left', cursor: 'pointer', userSelect: 'none', width: '12%', minWidth: '100px' }}>
                    Company {getSortIcon('companyName')}
                  </th>
                  <th onClick={() => handleSort('sector')} style={{ padding: '0.5rem', textAlign: 'left', cursor: 'pointer', userSelect: 'none', width: '8%', minWidth: '70px' }}>
                    Sector {getSortIcon('sector')}
                  </th>
                  <th onClick={() => handleSort('currentPrice')} style={{ padding: '0.5rem', textAlign: 'right', cursor: 'pointer', userSelect: 'none', width: '6%', minWidth: '50px' }}>
                    Price {getSortIcon('currentPrice')}
                  </th>
                  <th onClick={() => handleSort('dividendYield')} style={{ padding: '0.5rem', textAlign: 'right', cursor: 'pointer', userSelect: 'none', width: '6%', minWidth: '50px' }}>
                    Yield {getSortIcon('dividendYield')}
                  </th>
                  <th onClick={() => handleSort('payoutRatio')} style={{ padding: '0.5rem', textAlign: 'right', cursor: 'pointer', userSelect: 'none', width: '6%', minWidth: '50px' }}>
                    Payout {getSortIcon('payoutRatio')}
                  </th>
                  <th onClick={() => handleSort('payoutPolicy')} style={{ padding: '0.5rem', textAlign: 'center', cursor: 'pointer', userSelect: 'none', width: '8%', minWidth: '70px' }}>
                    Policy {getSortIcon('payoutPolicy')}
                  </th>
                  <th onClick={() => handleSort('dividendGrowthRate')} style={{ padding: '0.5rem', textAlign: 'right', cursor: 'pointer', userSelect: 'none', width: '6%', minWidth: '50px' }}>
                    Gr% {getSortIcon('dividendGrowthRate')}
                  </th>
                  <th onClick={() => handleSort('consecutiveYears')} style={{ padding: '0.5rem', textAlign: 'right', cursor: 'pointer', userSelect: 'none', width: '5%', minWidth: '45px' }}>
                    Yrs {getSortIcon('consecutiveYears')}
                  </th>
                  <th onClick={() => handleSort('dailyVolatility')} style={{ padding: '0.5rem', textAlign: 'right', cursor: 'pointer', userSelect: 'none', width: '6%', minWidth: '50px' }}>
                    Vol% {getSortIcon('dailyVolatility')}
                  </th>
                  <th onClick={() => handleSort('growthScore')} style={{ padding: '0.5rem', textAlign: 'right', cursor: 'pointer', userSelect: 'none', width: '6%', minWidth: '50px' }}>
                    GrSc {getSortIcon('growthScore')}
                  </th>
                  <th onClick={() => handleSort('sectorRank')} style={{ padding: '0.5rem', textAlign: 'right', cursor: 'pointer', userSelect: 'none', width: '6%', minWidth: '50px' }} title="Rank by Safety Score within sector">
                    Rank {getSortIcon('sectorRank')}
                  </th>
                  <th onClick={() => handleSort('safetyScore')} style={{ padding: '0.5rem', textAlign: 'right', cursor: 'pointer', userSelect: 'none', width: '6%', minWidth: '50px' }}>
                    Safety {getSortIcon('safetyScore')}
                  </th>
                  <th style={{ padding: '0.5rem', textAlign: 'center', width: '12%', minWidth: '100px' }}>
                    Strategy
                  </th>
                  <th style={{ padding: '0.5rem', textAlign: 'center', width: '10%', minWidth: '85px' }}>Actions</th>
                </tr>
              </thead>
              <tbody>
                {paginatedStocks.map((stock, idx) => (
                  <tr key={idx} style={{
                    borderBottom: '1px solid #e9ecef',
                    transition: 'background 0.2s',
                    background: idx % 2 === 0 ? 'white' : '#f8f9fa'
                  }}
                  onMouseEnter={(e) => e.currentTarget.style.background = '#e3f2fd'}
                  onMouseLeave={(e) => e.currentTarget.style.background = idx % 2 === 0 ? 'white' : '#f8f9fa'}
                  >
                    <td style={{ padding: '0.4rem', fontWeight: '600', color: '#2c3e50', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{stock.symbol}</td>
                    <td style={{ padding: '0.4rem', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                      {stock.companyName}
                    </td>
                    <td style={{ padding: '0.4rem', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                      <span style={{
                        padding: '0.2rem 0.4rem',
                        background: '#e9ecef',
                        borderRadius: '4px',
                        fontSize: '0.7rem'
                      }}>
                        {stock.sector || 'N/A'}
                      </span>
                    </td>
                    <td style={{ padding: '0.4rem', textAlign: 'right' }}>
                      ${stock.currentPrice ? stock.currentPrice.toFixed(2) : 'N/A'}
                    </td>
                    <td style={{ padding: '0.4rem', textAlign: 'right', fontWeight: '600', color: '#27ae60' }}>
                      {stock.dividendYield ? stock.dividendYield.toFixed(2) + '%' : 'N/A'}
                    </td>
                    <td style={{ padding: '0.4rem', textAlign: 'right' }}>
                      {stock.payoutRatio ? stock.payoutRatio.toFixed(1) + '%' : 'N/A'}
                    </td>
                    <td style={{ padding: '0.4rem', textAlign: 'center' }}>
                      <span
                        style={{
                          padding: '0.2rem 0.4rem',
                          background: getPolicyColor(stock.payoutPolicy) + '20',
                          color: getPolicyColor(stock.payoutPolicy),
                          borderRadius: '8px',
                          fontSize: '0.65rem',
                          fontWeight: '600',
                          whiteSpace: 'nowrap',
                          display: 'inline-block'
                        }}
                        title={`Dividend: ${stock.dividendAllocationPct?.toFixed(1) || 0}% | Reinvestment: ${stock.reinvestmentAllocationPct?.toFixed(1) || 0}%`}
                      >
                        {getPolicyEmoji(stock.payoutPolicy)} {stock.payoutPolicy || 'Unknown'}
                      </span>
                    </td>
                    <td style={{ padding: '0.4rem', textAlign: 'right', color: stock.dividendGrowthRate > 0 ? '#27ae60' : '#e74c3c' }}>
                      {stock.dividendGrowthRate ? stock.dividendGrowthRate.toFixed(1) + '%' : 'N/A'}
                    </td>
                    <td style={{ padding: '0.4rem', textAlign: 'right' }}>{stock.consecutiveYears}</td>
                    <td style={{ padding: '0.4rem', textAlign: 'right', color: stock.dailyVolatility > 2.5 ? '#e74c3c' : stock.dailyVolatility > 1.5 ? '#f39c12' : '#27ae60' }}>
                      {stock.dailyVolatility ? stock.dailyVolatility.toFixed(2) + '%' : 'N/A'}
                    </td>
                    <td style={{ padding: '0.4rem', textAlign: 'right' }}>
                      <span style={{
                        padding: '0.2rem 0.4rem',
                        background: stock.growthScore >= 70 ? '#27ae60' + '20' : stock.growthScore >= 40 ? '#f39c12' + '20' : '#e74c3c' + '20',
                        color: stock.growthScore >= 70 ? '#27ae60' : stock.growthScore >= 40 ? '#f39c12' : '#e74c3c',
                        borderRadius: '4px',
                        fontWeight: '600',
                        fontSize: '0.7rem'
                      }}>
                        {stock.growthScore ? stock.growthScore.toFixed(0) : '0'}
                      </span>
                    </td>
                    <td style={{ padding: '0.4rem', textAlign: 'right' }}>
                      <span style={{
                        padding: '0.2rem 0.4rem',
                        background: stock.sectorRank <= 3 ? '#27ae60' + '20' : stock.sectorRank <= 10 ? '#3498db' + '20' : '#95a5a6' + '20',
                        color: stock.sectorRank <= 3 ? '#27ae60' : stock.sectorRank <= 10 ? '#3498db' : '#95a5a6',
                        borderRadius: '4px',
                        fontWeight: '600',
                        fontSize: '0.7rem'
                      }}>
                        #{stock.sectorRank || 'N/A'}
                      </span>
                    </td>
                    <td style={{ padding: '0.4rem', textAlign: 'right' }}>
                      <span style={{
                        padding: '0.2rem 0.4rem',
                        background: getSafetyColor(stock.safetyRating) + '20',
                        color: getSafetyColor(stock.safetyRating),
                        borderRadius: '4px',
                        fontWeight: '600',
                        fontSize: '0.7rem'
                      }}>
                        {stock.safetyScore.toFixed(1)}
                      </span>
                    </td>
                    <td style={{ padding: '0.4rem', textAlign: 'center' }}>
                      <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.2rem', justifyContent: 'center' }}>
                        {classifyTradingStrategy(stock).slice(0, 2).map((strategy, i) => (
                          <span
                            key={i}
                            title={getStrategyDescription(strategy)}
                            style={{
                              padding: '0.15rem 0.3rem',
                              background: getStrategyColor(strategy) + '20',
                              color: getStrategyColor(strategy),
                              borderRadius: '4px',
                              fontSize: '0.6rem',
                              fontWeight: '600',
                              whiteSpace: 'nowrap',
                              cursor: 'help'
                            }}
                          >
                            {getStrategyLabel(strategy).split(' ')[0]}
                          </span>
                        ))}
                        {classifyTradingStrategy(stock).length > 2 && (
                          <span
                            title={classifyTradingStrategy(stock).slice(2).map(s => getStrategyLabel(s)).join(', ')}
                            style={{
                              padding: '0.15rem 0.3rem',
                              background: '#6b728020',
                              color: '#6b7280',
                              borderRadius: '4px',
                              fontSize: '0.6rem',
                              fontWeight: '600',
                              cursor: 'help'
                            }}
                          >
                            +{classifyTradingStrategy(stock).length - 2}
                          </span>
                        )}
                      </div>
                    </td>
                    <td style={{ padding: '0.4rem' }}>
                      <div style={{ display: 'flex', gap: '0.3rem', justifyContent: 'center' }}>
                        <button
                          onClick={() => handleSelectCached(stock.symbol)}
                          style={{
                            padding: '0.3rem 0.5rem',
                            background: '#3498db',
                            color: 'white',
                            border: 'none',
                            borderRadius: '4px',
                            cursor: 'pointer',
                            fontSize: '0.7rem'
                          }}
                        >
                          View
                        </button>
                        <button
                          onClick={() => handleRefreshCached(stock.symbol)}
                          style={{
                            padding: '0.3rem 0.5rem',
                            background: '#95a5a6',
                            color: 'white',
                            border: 'none',
                            borderRadius: '4px',
                            cursor: 'pointer',
                            fontSize: '0.7rem'
                          }}
                        >
                          ↻
                        </button>
                        <button
                          onClick={() => handleDeleteCached(stock.symbol)}
                          style={{
                            padding: '0.3rem 0.5rem',
                            background: '#e74c3c',
                            color: 'white',
                            border: 'none',
                            borderRadius: '4px',
                            cursor: 'pointer',
                            fontSize: '0.7rem'
                          }}
                        >
                          ×
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
        </div>

          {/* Pagination controls below the table */}
        {totalPages > 1 && (
        <div
          style={{
          marginTop: '1rem',
          display: 'flex',
          justifyContent: 'center',
          gap: '1rem',
          alignItems: 'center'
        }}
        >
        <button
          onClick={() => setCurrentPage((p) => Math.max(p - 1, 1))}
          disabled={currentPage === 1}
          style={{
          padding: '0.4rem 0.8rem',
          background: '#3498db',
          color: 'white',
          border: 'none',
          borderRadius: '4px',
          cursor: currentPage === 1 ? 'not-allowed' : 'pointer'
        }}
        >
          Previous
        </button>

        <span>
          Page {currentPage} of {totalPages}
        </span>

        <button
          onClick={() => setCurrentPage((p) => Math.min(p + 1, totalPages))}
          disabled={currentPage === totalPages}
          style={{
            padding: '0.4rem 0.8rem',
            background: '#3498db',
            color: 'white',
            border: 'none',
           borderRadius: '4px',
            cursor: currentPage === totalPages ? 'not-allowed' : 'pointer'
          }}
        >
          Next
        </button>
        </div>
        )}

        {/* No results message */}
        {getFilteredAndSortedStocks().length === 0 && (
          <div
            style={{
              padding: '2rem',
              textAlign: 'center',
              color: '#6c757d'
            }}
          >
            No stocks match your filters
          </div>
        )}
        </div>
      )}

      {analysis && (
        <>
          <div className="card">
            {/* Tab Navigation */}
            <div style={{
              display: 'flex',
              gap: '0.5rem',
              borderBottom: '2px solid #e1e8ed',
              marginBottom: '1.5rem'
            }}>
              <button
                onClick={() => setActiveTab('dividend')}
                style={{
                  padding: '0.75rem 1.5rem',
                  background: activeTab === 'dividend' ? '#3498db' : 'transparent',
                  color: activeTab === 'dividend' ? 'white' : '#64748b',
                  border: 'none',
                  borderBottom: activeTab === 'dividend' ? '3px solid #3498db' : '3px solid transparent',
                  cursor: 'pointer',
                  fontWeight: '600',
                  fontSize: '1rem',
                  transition: 'all 0.2s',
                  borderRadius: '8px 8px 0 0'
                }}
              >
                📊 Dividend Analysis
              </button>
              <button
                onClick={() => setActiveTab('strategy')}
                style={{
                  padding: '0.75rem 1.5rem',
                  background: activeTab === 'strategy' ? '#10b981' : 'transparent',
                  color: activeTab === 'strategy' ? 'white' : '#64748b',
                  border: 'none',
                  borderBottom: activeTab === 'strategy' ? '3px solid #10b981' : '3px solid transparent',
                  cursor: 'pointer',
                  fontWeight: '600',
                  fontSize: '1rem',
                  transition: 'all 0.2s',
                  borderRadius: '8px 8px 0 0'
                }}
              >
                🎯 Trading Strategies
              </button>
            </div>

            {activeTab === 'dividend' && (
              <>
                <div className="analysis-header">
                  <div>
                    <h2>{analysis.symbol}</h2>
                    <p className="company-name">{analysis.companyName}</p>
                    {analysis.sector && <span className="badge info">{analysis.sector}</span>}
                  </div>
                  <div className="safety-badge" style={{ borderColor: getSafetyColor(analysis.safetyAnalysis.rating) }}>
                    <div className="safety-score" style={{ color: getSafetyColor(analysis.safetyAnalysis.rating) }}>
                      {analysis.safetyAnalysis.score.toFixed(1)}
                    </div>
                    <div className="safety-rating">{analysis.safetyAnalysis.rating}</div>
                  </div>
                </div>
              </>
            )}
          </div>

          {activeTab === 'dividend' && (
            <>
              <div className="card">

                {analysis.metadata.fromCache && (
                  <div className="info-banner">
                    Loaded from cache (Updated: {new Date(analysis.metadata.lastUpdated).toLocaleString()})
                  </div>
                )}

                <div className="stats-grid" style={{ marginTop: '1.5rem' }}>
                  <div className="stat-card">
                    <h4>Dividend Yield</h4>
                    <div className="value">
                      {analysis.currentMetrics.dividendYield ? Number(analysis.currentMetrics.dividendYield).toFixed(2) + '%' : 'N/A'}
                    </div>
                  </div>
                  <div className="stat-card">
                    <h4>Payout Ratio</h4>
                    <div className="value">
                      {analysis.currentMetrics.payoutRatio ? Number(analysis.currentMetrics.payoutRatio).toFixed(1) + '%' : 'N/A (ETF)'}
                    </div>
                  </div>
                  <div className="stat-card">
                    <h4>Consecutive Years</h4>
                    <div className="value">{analysis.historicalAnalysis.consecutiveYearsOfPayments}</div>
                  </div>
                  <div className="stat-card">
                    <h4>Growth Rate</h4>
                    <div className="value">
                      {analysis.historicalAnalysis.dividendGrowthRate ? Number(analysis.historicalAnalysis.dividendGrowthRate).toFixed(1) + '%' : 'N/A'}
                    </div>
                  </div>
                </div>

                {/* Payout Policy Breakdown */}
                {analysis.payoutPolicy && (
                  <div style={{ marginTop: '1.5rem', padding: '1.5rem', background: '#f8f9fa', borderRadius: '8px', border: '1px solid #e2e8f0' }}>
                    <h4 style={{ margin: '0 0 1rem 0', display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                      {getPolicyEmoji(analysis.payoutPolicy)} Earnings Allocation Policy
                      <span style={{
                        fontSize: '0.8rem',
                        fontWeight: 'normal',
                        padding: '0.25rem 0.75rem',
                        background: getPolicyColor(analysis.payoutPolicy) + '20',
                        color: getPolicyColor(analysis.payoutPolicy),
                        borderRadius: '12px'
                      }}>
                        {analysis.payoutPolicy}
                      </span>
                    </h4>
                    <div style={{ display: 'flex', gap: '1rem', alignItems: 'center', marginBottom: '0.75rem' }}>
                      <div style={{ flex: 1 }}>
                        <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '0.5rem', fontSize: '0.9rem' }}>
                          <span style={{ fontWeight: '600', color: '#3498db' }}>💰 Dividends</span>
                          <span style={{ fontWeight: '700', color: '#3498db' }}>{analysis.dividendAllocationPct?.toFixed(1) || 0}%</span>
                        </div>
                        <div style={{ height: '10px', background: '#e2e8f0', borderRadius: '5px', overflow: 'hidden' }}>
                          <div style={{
                            width: `${analysis.dividendAllocationPct || 0}%`,
                            height: '100%',
                            background: 'linear-gradient(90deg, #3498db, #2980b9)',
                            transition: 'width 0.3s ease'
                          }}></div>
                        </div>
                      </div>
                    </div>
                    <div style={{ display: 'flex', gap: '1rem', alignItems: 'center' }}>
                      <div style={{ flex: 1 }}>
                        <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '0.5rem', fontSize: '0.9rem' }}>
                          <span style={{ fontWeight: '600', color: '#9b59b6' }}>📈 Reinvestment</span>
                          <span style={{ fontWeight: '700', color: '#9b59b6' }}>{analysis.reinvestmentAllocationPct?.toFixed(1) || 0}%</span>
                        </div>
                        <div style={{ height: '10px', background: '#e2e8f0', borderRadius: '5px', overflow: 'hidden' }}>
                          <div style={{
                            width: `${analysis.reinvestmentAllocationPct || 0}%`,
                            height: '100%',
                            background: 'linear-gradient(90deg, #9b59b6, #8e44ad)',
                            transition: 'width 0.3s ease'
                          }}></div>
                        </div>
                      </div>
                    </div>
                    <p style={{ marginTop: '1rem', fontSize: '0.85rem', color: '#64748b', lineHeight: '1.5' }}>
                      {analysis.payoutPolicy === 'Dividend Only' && '💰 This company pays out all earnings as dividends to shareholders.'}
                      {analysis.payoutPolicy === 'Reinvestment Only' && '📈 This company reinvests all earnings back into growth and does not pay dividends.'}
                      {analysis.payoutPolicy === 'Mixed' && `⚖️ This company balances shareholder returns with growth by paying ${analysis.dividendAllocationPct?.toFixed(0)}% as dividends while reinvesting ${analysis.reinvestmentAllocationPct?.toFixed(0)}% into the business.`}
                      {!analysis.payoutPolicy || analysis.payoutPolicy === 'Unknown' && 'Policy information not available.'}
                    </p>
                  </div>
                )}

                <div style={{ marginTop: '1.5rem' }}>
                  <h4>Analysis</h4>
                  <p style={{ color: '#555', lineHeight: '1.6' }}>{analysis.safetyAnalysis.recommendation}</p>
                </div>
              </div>

              {/* Support Levels and Price Ranges Table */}
              <div className="card">
                <h3 style={{ marginBottom: '1rem', display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                  📊 Support Levels & Price Ranges
                </h3>

                <div style={{ overflowX: 'auto' }}>
                  <table style={{
                    width: '100%',
                    borderCollapse: 'collapse',
                    fontSize: '0.9rem'
                  }}>
                    <thead>
                      <tr style={{
                        backgroundColor: '#e9ecef',
                        borderBottom: '2px solid #adb5bd',
                        color: '#495057',
                        fontWeight: '600'
                      }}>
                        <th style={{ padding: '0.75rem', textAlign: 'left' }}>Metric</th>
                        <th style={{ padding: '0.75rem', textAlign: 'right' }}>Price</th>
                        <th style={{ padding: '0.75rem', textAlign: 'right' }}>Volume</th>
                        <th style={{ padding: '0.75rem', textAlign: 'left' }}>Description</th>
                      </tr>
                    </thead>
                    <tbody>
                      {/* 52-Week High */}
                      {cachedStocks.find(s => s.symbol === analysis.symbol)?.week52High && (
                        <tr style={{ borderBottom: '1px solid #e9ecef' }}>
                          <td style={{ padding: '0.75rem', fontWeight: '600', color: '#22c55e' }}>52-Week High</td>
                          <td style={{ padding: '0.75rem', textAlign: 'right', fontWeight: '600' }}>
                            ${cachedStocks.find(s => s.symbol === analysis.symbol)?.week52High?.toFixed(2)}
                          </td>
                          <td style={{ padding: '0.75rem', textAlign: 'right', color: '#6c757d' }}>-</td>
                          <td style={{ padding: '0.75rem', fontSize: '0.85rem', color: '#6c757d' }}>
                            Highest price in the last 52 weeks
                          </td>
                        </tr>
                      )}

                      {/* 52-Week Low */}
                      {cachedStocks.find(s => s.symbol === analysis.symbol)?.week52Low && (
                        <tr style={{ borderBottom: '1px solid #e9ecef' }}>
                          <td style={{ padding: '0.75rem', fontWeight: '600', color: '#ef4444' }}>52-Week Low</td>
                          <td style={{ padding: '0.75rem', textAlign: 'right', fontWeight: '600' }}>
                            ${cachedStocks.find(s => s.symbol === analysis.symbol)?.week52Low?.toFixed(2)}
                          </td>
                          <td style={{ padding: '0.75rem', textAlign: 'right', color: '#6c757d' }}>-</td>
                          <td style={{ padding: '0.75rem', fontSize: '0.85rem', color: '#6c757d' }}>
                            Lowest price in the last 52 weeks
                          </td>
                        </tr>
                      )}

                      {/* 1-Month Low */}
                      {cachedStocks.find(s => s.symbol === analysis.symbol)?.month1Low && (
                        <tr style={{ borderBottom: '1px solid #e9ecef' }}>
                          <td style={{ padding: '0.75rem', fontWeight: '600', color: '#f59e0b' }}>1-Month Low</td>
                          <td style={{ padding: '0.75rem', textAlign: 'right', fontWeight: '600' }}>
                            ${cachedStocks.find(s => s.symbol === analysis.symbol)?.month1Low?.toFixed(2)}
                          </td>
                          <td style={{ padding: '0.75rem', textAlign: 'right', color: '#6c757d' }}>-</td>
                          <td style={{ padding: '0.75rem', fontSize: '0.85rem', color: '#6c757d' }}>
                            Recent low - potential short-term support
                          </td>
                        </tr>
                      )}

                      {/* 3-Month Low */}
                      {cachedStocks.find(s => s.symbol === analysis.symbol)?.month3Low && (
                        <tr style={{ borderBottom: '1px solid #e9ecef' }}>
                          <td style={{ padding: '0.75rem', fontWeight: '600', color: '#f59e0b' }}>3-Month Low</td>
                          <td style={{ padding: '0.75rem', textAlign: 'right', fontWeight: '600' }}>
                            ${cachedStocks.find(s => s.symbol === analysis.symbol)?.month3Low?.toFixed(2)}
                          </td>
                          <td style={{ padding: '0.75rem', textAlign: 'right', color: '#6c757d' }}>-</td>
                          <td style={{ padding: '0.75rem', fontSize: '0.85rem', color: '#6c757d' }}>
                            Medium-term low - potential support level
                          </td>
                        </tr>
                      )}

                      {/* Support Level 1 */}
                      {cachedStocks.find(s => s.symbol === analysis.symbol)?.supportLevel1 && (
                        <tr style={{ borderBottom: '1px solid #e9ecef', backgroundColor: '#f0fdf4' }}>
                          <td style={{ padding: '0.75rem', fontWeight: '600', color: '#16a34a' }}>
                            🎯 Strong Support Level 1
                          </td>
                          <td style={{ padding: '0.75rem', textAlign: 'right', fontWeight: '700', color: '#16a34a' }}>
                            ${cachedStocks.find(s => s.symbol === analysis.symbol)?.supportLevel1?.toFixed(2)}
                          </td>
                          <td style={{ padding: '0.75rem', textAlign: 'right', fontWeight: '600', color: '#16a34a' }}>
                            {cachedStocks.find(s => s.symbol === analysis.symbol)?.supportLevel1Volume?.toLocaleString()}
                          </td>
                          <td style={{ padding: '0.75rem', fontSize: '0.85rem', color: '#166534' }}>
                            Highest volume zone - strongest support
                          </td>
                        </tr>
                      )}

                      {/* Support Level 2 */}
                      {cachedStocks.find(s => s.symbol === analysis.symbol)?.supportLevel2 && (
                        <tr style={{ borderBottom: '1px solid #e9ecef', backgroundColor: '#fef3c7' }}>
                          <td style={{ padding: '0.75rem', fontWeight: '600', color: '#ca8a04' }}>
                            📍 Support Level 2
                          </td>
                          <td style={{ padding: '0.75rem', textAlign: 'right', fontWeight: '700', color: '#ca8a04' }}>
                            ${cachedStocks.find(s => s.symbol === analysis.symbol)?.supportLevel2?.toFixed(2)}
                          </td>
                          <td style={{ padding: '0.75rem', textAlign: 'right', fontWeight: '600', color: '#ca8a04' }}>
                            {cachedStocks.find(s => s.symbol === analysis.symbol)?.supportLevel2Volume?.toLocaleString()}
                          </td>
                          <td style={{ padding: '0.75rem', fontSize: '0.85rem', color: '#92400e' }}>
                            Second highest volume zone
                          </td>
                        </tr>
                      )}

                      {/* Support Level 3 */}
                      {cachedStocks.find(s => s.symbol === analysis.symbol)?.supportLevel3 && (
                        <tr style={{ borderBottom: '1px solid #e9ecef', backgroundColor: '#fef2f2' }}>
                          <td style={{ padding: '0.75rem', fontWeight: '600', color: '#dc2626' }}>
                            📌 Support Level 3
                          </td>
                          <td style={{ padding: '0.75rem', textAlign: 'right', fontWeight: '700', color: '#dc2626' }}>
                            ${cachedStocks.find(s => s.symbol === analysis.symbol)?.supportLevel3?.toFixed(2)}
                          </td>
                          <td style={{ padding: '0.75rem', textAlign: 'right', fontWeight: '600', color: '#dc2626' }}>
                            {cachedStocks.find(s => s.symbol === analysis.symbol)?.supportLevel3Volume?.toLocaleString()}
                          </td>
                          <td style={{ padding: '0.75rem', fontSize: '0.85rem', color: '#991b1b' }}>
                            Third highest volume zone
                          </td>
                        </tr>
                      )}
                    </tbody>
                  </table>
                </div>

                <div style={{
                  marginTop: '1rem',
                  padding: '1rem',
                  background: '#eff6ff',
                  borderRadius: '8px',
                  borderLeft: '4px solid #3b82f6'
                }}>
                  <strong style={{ color: '#1e40af' }}>💡 How to use Support Levels:</strong>
                  <p style={{ marginTop: '0.5rem', fontSize: '0.875rem', color: '#1e3a8a', lineHeight: '1.6' }}>
                    Support levels indicate price ranges where high trading volume occurred, suggesting strong buying interest.
                    These levels often act as "floors" where the stock price may find support during pullbacks.
                    The higher the volume, the stronger the support level.
                  </p>
                </div>
              </div>

              {/* Sector Comparison */}
              {sectorComparison && (
                <div className="card">
                  <h3 style={{ marginBottom: '1rem', display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                    📈 Sector Performance Comparison
                    <span style={{
                      fontSize: '0.8rem',
                      fontWeight: 'normal',
                      color: '#64748b',
                      background: '#e2e8f0',
                      padding: '0.25rem 0.75rem',
                      borderRadius: '12px'
                    }}>
                      {sectorComparison.sector}
                    </span>
                  </h3>

                  <div className="stats-grid" style={{ gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))' }}>
                    <div className="stat-card">
                      <h4>1M Performance</h4>
                      <div className="value" style={{ color: sectorComparison.outperformanceVsSector1M >= 0 ? '#22c55e' : '#ef4444' }}>
                        {sectorComparison.outperformanceVsSector1M >= 0 ? '+' : ''}
                        {sectorComparison.outperformanceVsSector1M?.toFixed(2)}%
                      </div>
                      <small style={{ color: '#64748b', fontSize: '0.75rem' }}>
                        vs sector avg {sectorComparison.sectorReturn1M?.toFixed(2)}%
                      </small>
                    </div>

                    <div className="stat-card">
                      <h4>3M Performance</h4>
                      <div className="value" style={{ color: sectorComparison.outperformanceVsSector3M >= 0 ? '#22c55e' : '#ef4444' }}>
                        {sectorComparison.outperformanceVsSector3M >= 0 ? '+' : ''}
                        {sectorComparison.outperformanceVsSector3M?.toFixed(2)}%
                      </div>
                      <small style={{ color: '#64748b', fontSize: '0.75rem' }}>
                        vs sector avg {sectorComparison.sectorReturn3M?.toFixed(2)}%
                      </small>
                    </div>

                    <div className="stat-card">
                      <h4>1Y Performance</h4>
                      <div className="value" style={{ color: sectorComparison.outperformanceVsSector1Y >= 0 ? '#22c55e' : '#ef4444' }}>
                        {sectorComparison.outperformanceVsSector1Y >= 0 ? '+' : ''}
                        {sectorComparison.outperformanceVsSector1Y?.toFixed(2)}%
                      </div>
                      <small style={{ color: '#64748b', fontSize: '0.75rem' }}>
                        vs sector avg {sectorComparison.sectorReturn1Y?.toFixed(2)}%
                      </small>
                    </div>

                    <div className="stat-card">
                      <h4>Performance Rank</h4>
                      <div className="value" style={{ color: '#3b82f6' }}>
                        #{sectorComparison.i}
                      </div>
                      <small style={{ color: '#64748b', fontSize: '0.75rem' }}>
                        of {sectorComparison.totalStocksInSector} stocks ({sectorComparison.performancePercentile}th percentile)
                      </small>
                    </div>

                    <div className="stat-card">
                      <h4>P/E vs Sector</h4>
                      <div className="value" style={{ color: sectorComparison.pePremiumDiscount >= 0 ? '#ef4444' : '#22c55e' }}>
                        {sectorComparison.pePremiumDiscount >= 0 ? '+' : ''}
                        {sectorComparison.pePremiumDiscount?.toFixed(1)}%
                      </div>
                      <small style={{ color: '#64748b', fontSize: '0.75rem' }}>
                        Stock: {sectorComparison.stockPE?.toFixed(1)} | Sector: {sectorComparison.sectorAvgPE?.toFixed(1)}
                      </small>
                    </div>

                    <div className="stat-card">
                      <h4>Yield vs Sector</h4>
                      <div className="value" style={{ color: sectorComparison.yieldPremiumDiscount >= 0 ? '#22c55e' : '#ef4444' }}>
                        {sectorComparison.yieldPremiumDiscount >= 0 ? '+' : ''}
                        {sectorComparison.yieldPremiumDiscount?.toFixed(1)}%
                      </div>
                      <small style={{ color: '#64748b', fontSize: '0.75rem' }}>
                        Stock: {sectorComparison.stockDividendYield?.toFixed(2)}% | Sector: {sectorComparison.sectorAvgDividendYield?.toFixed(2)}%
                      </small>
                    </div>
                  </div>

                  <div style={{
                    marginTop: '1rem',
                    padding: '1rem',
                    background: sectorComparison.performancePercentile >= 75 ? '#dcfce7' : sectorComparison.performancePercentile >= 50 ? '#fef3c7' : '#fee2e2',
                    borderRadius: '8px',
                    borderLeft: `4px solid ${sectorComparison.performancePercentile >= 75 ? '#22c55e' : sectorComparison.performancePercentile >= 50 ? '#f59e0b' : '#ef4444'}`
                  }}>
                    <strong style={{ color: sectorComparison.performancePercentile >= 75 ? '#166534' : sectorComparison.performancePercentile >= 50 ? '#92400e' : '#991b1b' }}>
                      {sectorComparison.performancePercentile >= 75 ? '🌟 Top Performer:' : sectorComparison.performancePercentile >= 50 ? '📊 Above Average:' : '⚠️ Below Average:'}
                    </strong>
                    <span style={{ marginLeft: '0.5rem', color: sectorComparison.performancePercentile >= 75 ? '#166534' : sectorComparison.performancePercentile >= 50 ? '#92400e' : '#991b1b' }}>
                      This stock ranks in the {sectorComparison.performancePercentile >= 75 ? 'top 25%' : sectorComparison.performancePercentile >= 50 ? 'top 50%' : 'bottom 50%'} of {sectorComparison.sector} stocks
                    </span>
                  </div>
                </div>
              )}

              {analysis.historicalAnalysis.yearlyDividends && analysis.historicalAnalysis.yearlyDividends.length > 0 && (
                <div className="card">
                  <h3>Dividend History</h3>
                  <ResponsiveContainer width="100%" height={300}>
                    <BarChart data={analysis.historicalAnalysis.yearlyDividends}>
                      <CartesianGrid strokeDasharray="3 3" />
                      <XAxis dataKey="year" />
                      <YAxis />
                      <Tooltip />
                      <Legend />
                      <Bar dataKey="totalDividend" fill="#3498db" name="Annual Dividend ($)" />
                    </BarChart>
                  </ResponsiveContainer>
                </div>
              )}

              {analysis.dividendHistory && analysis.dividendHistory.length > 0 && (
                <div className="card">
                  <h3>Payment History ({analysis.dividendHistory.length} payments)</h3>
                  <ResponsiveContainer width="100%" height={300}>
                    <LineChart data={analysis.dividendHistory.slice().reverse()}>
                      <CartesianGrid strokeDasharray="3 3" />
                      <XAxis dataKey="date" />
                      <YAxis />
                      <Tooltip />
                      <Legend />
                      <Line type="monotone" dataKey="amount" stroke="#27ae60" name="Dividend Amount ($)" />
                    </LineChart>
                  </ResponsiveContainer>
                </div>
              )}

              {/* New Advanced Charts Section */}
              {analysis && (
                <div className="advanced-charts-section">
                  <DividendCharts symbol={analysis.symbol} />
                </div>
              )}
            </>
          )}

          {/* Strategy Analysis Tab */}
          {activeTab === 'strategy' && (
            <StrategyAnalysis symbol={analysis.symbol} />
          )}
        </>
      )}
    </div>
  );
}

export default DividendAnalysis;
