import React, { useState, useEffect } from 'react';
import { dividendsAPI } from '../services/api';
import { LineChart, Line, BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer } from 'recharts';
import DividendCharts from './DividendCharts';
import './DividendAnalysis.css';

function DividendAnalysis() {
  const [symbol, setSymbol] = useState('');
  const [exchange, setExchange] = useState('US');
  const [analysis, setAnalysis] = useState(null);
  const [cachedStocks, setCachedStocks] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const [stats, setStats] = useState(null);

  // Filters and sorting
  const [filters, setFilters] = useState({
    yieldMin: '',
    priceMax: '',
    sector: 'all',
    safetyRating: 'all',
    minYears: ''
  });
  const [sortConfig, setSortConfig] = useState({ key: 'safetyScore', direction: 'desc' });

  useEffect(() => {
    loadCachedStocks();
    loadStats();
  }, []);

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
    } catch (err) {
      setError(err.response?.data?.error || 'Failed to analyze Dividend');
    } finally {
      setLoading(false);
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

  const handleExportDividends = () => {
    const API_BASE_URL = 'http://localhost:7065/api';
    window.open(`${API_BASE_URL}/dividends/export/csv`, '_blank');
  };

  const handleExportPayments = () => {
    const API_BASE_URL = 'http://localhost:7065/api';
    window.open(`${API_BASE_URL}/dividends/export/payments/csv`, '_blank');
  };

  const handleExportUsage = () => {
    const API_BASE_URL = 'http://localhost:7065/api';
    window.open(`${API_BASE_URL}/dividends/export/usage/csv`, '_blank');
  };

  // Filter and sort stocks
  const getFilteredAndSortedStocks = () => {
    let filtered = [...cachedStocks];

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
    if (filters.minYears) {
      filtered = filtered.filter(s => s.consecutiveYears >= parseInt(filters.minYears));
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
            <h3>My Portfolio ({getFilteredAndSortedStocks().length} of {cachedStocks.length})</h3>
            <div style={{ display: 'flex', gap: '0.5rem' }}>
              <button className="secondary" onClick={handleExportDividends} style={{ fontSize: '0.875rem', padding: '0.5rem 1rem' }}>
                Export All
              </button>
              <button className="secondary" onClick={handleExportPayments} style={{ fontSize: '0.875rem', padding: '0.5rem 1rem' }}>
                Export Payments
              </button>
            </div>
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
                value={filters.yieldMin}
                onChange={(e) => setFilters({...filters, yieldMin: e.target.value})}
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
                value={filters.priceMax}
                onChange={(e) => setFilters({...filters, priceMax: e.target.value})}
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
                value={filters.sector}
                onChange={(e) => setFilters({...filters, sector: e.target.value})}
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
                value={filters.safetyRating}
                onChange={(e) => setFilters({...filters, safetyRating: e.target.value})}
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
              <label style={{ display: 'block', fontSize: '0.875rem', marginBottom: '0.5rem', color: '#555' }}>Min Years</label>
              <input
                type="number"
                placeholder="e.g., 10"
                value={filters.minYears}
                onChange={(e) => setFilters({...filters, minYears: e.target.value})}
                style={{
                  width: '100%',
                  padding: '0.5rem',
                  border: '1px solid #ddd',
                  borderRadius: '4px',
                  fontSize: '0.875rem'
                }}
              />
            </div>
            <div style={{ display: 'flex', alignItems: 'flex-end' }}>
              <button
                onClick={() => setFilters({ yieldMin: '', priceMax: '', sector: 'all', safetyRating: 'all', minYears: '' })}
                style={{
                  width: '100%',
                  padding: '0.5rem',
                  background: '#6c757d',
                  color: 'white',
                  border: 'none',
                  borderRadius: '4px',
                  cursor: 'pointer',
                  fontSize: '0.875rem'
                }}
              >
                Clear Filters
              </button>
            </div>
          </div>

          {/* Sortable Table */}
          <div style={{ overflowX: 'auto' }}>
            <table style={{
              width: '100%',
              borderCollapse: 'collapse',
              fontSize: '0.9rem'
            }}>
              <thead>
                <tr style={{ background: '#f8f9fa', borderBottom: '2px solid #dee2e6' }}>
                  <th onClick={() => handleSort('symbol')} style={{ padding: '1rem', textAlign: 'left', cursor: 'pointer', userSelect: 'none' }}>
                    Symbol {getSortIcon('symbol')}
                  </th>
                  <th onClick={() => handleSort('companyName')} style={{ padding: '1rem', textAlign: 'left', cursor: 'pointer', userSelect: 'none' }}>
                    Company {getSortIcon('companyName')}
                  </th>
                  <th onClick={() => handleSort('sector')} style={{ padding: '1rem', textAlign: 'left', cursor: 'pointer', userSelect: 'none' }}>
                    Sector {getSortIcon('sector')}
                  </th>
                  <th onClick={() => handleSort('currentPrice')} style={{ padding: '1rem', textAlign: 'right', cursor: 'pointer', userSelect: 'none' }}>
                    Price {getSortIcon('currentPrice')}
                  </th>
                  <th onClick={() => handleSort('dividendYield')} style={{ padding: '1rem', textAlign: 'right', cursor: 'pointer', userSelect: 'none' }}>
                    Yield {getSortIcon('dividendYield')}
                  </th>
                  <th onClick={() => handleSort('payoutRatio')} style={{ padding: '1rem', textAlign: 'right', cursor: 'pointer', userSelect: 'none' }}>
                    Payout {getSortIcon('payoutRatio')}
                  </th>
                  <th onClick={() => handleSort('dividendGrowthRate')} style={{ padding: '1rem', textAlign: 'right', cursor: 'pointer', userSelect: 'none' }}>
                    Growth {getSortIcon('dividendGrowthRate')}
                  </th>
                  <th onClick={() => handleSort('consecutiveYears')} style={{ padding: '1rem', textAlign: 'right', cursor: 'pointer', userSelect: 'none' }}>
                    Years {getSortIcon('consecutiveYears')}
                  </th>
                  <th onClick={() => handleSort('safetyScore')} style={{ padding: '1rem', textAlign: 'right', cursor: 'pointer', userSelect: 'none' }}>
                    Score {getSortIcon('safetyScore')}
                  </th>
                  <th style={{ padding: '1rem', textAlign: 'center' }}>Actions</th>
                </tr>
              </thead>
              <tbody>
                {getFilteredAndSortedStocks().map((stock, idx) => (
                  <tr key={idx} style={{
                    borderBottom: '1px solid #e9ecef',
                    transition: 'background 0.2s',
                    background: idx % 2 === 0 ? 'white' : '#f8f9fa'
                  }}
                  onMouseEnter={(e) => e.currentTarget.style.background = '#e3f2fd'}
                  onMouseLeave={(e) => e.currentTarget.style.background = idx % 2 === 0 ? 'white' : '#f8f9fa'}
                  >
                    <td style={{ padding: '0.75rem', fontWeight: '600', color: '#2c3e50' }}>{stock.symbol}</td>
                    <td style={{ padding: '0.75rem', maxWidth: '200px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                      {stock.companyName}
                    </td>
                    <td style={{ padding: '0.75rem' }}>
                      <span style={{
                        padding: '0.25rem 0.5rem',
                        background: '#e9ecef',
                        borderRadius: '4px',
                        fontSize: '0.8rem'
                      }}>
                        {stock.sector || 'N/A'}
                      </span>
                    </td>
                    <td style={{ padding: '0.75rem', textAlign: 'right' }}>
                      ${stock.currentPrice ? stock.currentPrice.toFixed(2) : 'N/A'}
                    </td>
                    <td style={{ padding: '0.75rem', textAlign: 'right', fontWeight: '600', color: '#27ae60' }}>
                      {stock.dividendYield ? stock.dividendYield.toFixed(2) + '%' : 'N/A'}
                    </td>
                    <td style={{ padding: '0.75rem', textAlign: 'right' }}>
                      {stock.payoutRatio ? stock.payoutRatio.toFixed(1) + '%' : 'N/A'}
                    </td>
                    <td style={{ padding: '0.75rem', textAlign: 'right', color: stock.dividendGrowthRate > 0 ? '#27ae60' : '#e74c3c' }}>
                      {stock.dividendGrowthRate ? stock.dividendGrowthRate.toFixed(1) + '%' : 'N/A'}
                    </td>
                    <td style={{ padding: '0.75rem', textAlign: 'right' }}>{stock.consecutiveYears}</td>
                    <td style={{ padding: '0.75rem', textAlign: 'right' }}>
                      <span style={{
                        padding: '0.25rem 0.5rem',
                        background: getSafetyColor(stock.safetyRating) + '20',
                        color: getSafetyColor(stock.safetyRating),
                        borderRadius: '4px',
                        fontWeight: '600'
                      }}>
                        {stock.safetyScore.toFixed(1)}
                      </span>
                    </td>
                    <td style={{ padding: '0.75rem' }}>
                      <div style={{ display: 'flex', gap: '0.5rem', justifyContent: 'center' }}>
                        <button
                          onClick={() => handleSelectCached(stock.symbol)}
                          style={{
                            padding: '0.4rem 0.8rem',
                            background: '#3498db',
                            color: 'white',
                            border: 'none',
                            borderRadius: '4px',
                            cursor: 'pointer',
                            fontSize: '0.8rem'
                          }}
                        >
                          View
                        </button>
                        <button
                          onClick={() => handleRefreshCached(stock.symbol)}
                          style={{
                            padding: '0.4rem 0.8rem',
                            background: '#95a5a6',
                            color: 'white',
                            border: 'none',
                            borderRadius: '4px',
                            cursor: 'pointer',
                            fontSize: '0.8rem'
                          }}
                        >
                          ↻
                        </button>
                        <button
                          onClick={() => handleDeleteCached(stock.symbol)}
                          style={{
                            padding: '0.4rem 0.8rem',
                            background: '#e74c3c',
                            color: 'white',
                            border: 'none',
                            borderRadius: '4px',
                            cursor: 'pointer',
                            fontSize: '0.8rem'
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

          {getFilteredAndSortedStocks().length === 0 && (
            <div style={{ padding: '2rem', textAlign: 'center', color: '#6c757d' }}>
              No stocks match your filters
            </div>
          )}
        </div>
      )}

      {analysis && (
        <>
          <div className="card">
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

            <div style={{ marginTop: '1.5rem' }}>
              <h4>Analysis</h4>
              <p style={{ color: '#555', lineHeight: '1.6' }}>{analysis.safetyAnalysis.recommendation}</p>
            </div>
          </div>

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
    </div>
  );
}

export default DividendAnalysis;
