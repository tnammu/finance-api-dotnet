import React, { useState, useEffect } from 'react';
import { dividendsAPI } from '../services/api';
import { LineChart, Line, BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer } from 'recharts';
import './DividendAnalysis.css';

function DividendAnalysis() {
  const [symbol, setSymbol] = useState('');
  const [analysis, setAnalysis] = useState(null);
  const [cachedStocks, setCachedStocks] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const [stats, setStats] = useState(null);

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

  const handleAnalyze = async (symbolToAnalyze = symbol, refresh = false) => {
    if (!symbolToAnalyze.trim()) {
      setError('Please enter a stock symbol');
      return;
    }

    try {
      setLoading(true);
      setError(null);
      const response = await dividendsAPI.analyze(symbolToAnalyze.toUpperCase(), refresh);
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

  return (
    <div className="dividend-analysis">
      <div className="card">
        <h2>Dividend Analysis</h2>
        <p style={{ color: '#7f8c8d', marginBottom: '1.5rem' }}>
          Analyze dividend stocks with historical data and safety ratings
        </p>

        <div className="search-section">
          <div style={{ display: 'flex', gap: '0.75rem' }}>
            <input
              type="text"
              value={symbol}
              onChange={(e) => setSymbol(e.target.value)}
              placeholder="Enter stock symbol (e.g., AAPL, TD.TO)"
              onKeyPress={(e) => e.key === 'Enter' && handleAnalyze()}
              disabled={loading}
            />
            <button
              className="primary"
              onClick={() => handleAnalyze()}
              disabled={loading || !symbol.trim()}
            >
              {loading ? 'Analyzing...' : 'Analyze'}
            </button>
          </div>
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
            <h3>Recently Analyzed ({cachedStocks.length})</h3>
            <div style={{ display: 'flex', gap: '0.5rem' }}>
              <button className="secondary" onClick={handleExportDividends} style={{ fontSize: '0.875rem', padding: '0.5rem 1rem' }}>
                Export All
              </button>
              <button className="secondary" onClick={handleExportPayments} style={{ fontSize: '0.875rem', padding: '0.5rem 1rem' }}>
                Export Payments
              </button>
            </div>
          </div>
          <div className="cached-stocks">
            {cachedStocks.map((stock, idx) => (
              <div key={idx} className="cached-stock-item">
                <div>
                  <strong>{stock.symbol}</strong>
                  <p>{stock.companyName}</p>
                  <span className="badge" style={{ background: getSafetyColor(stock.safetyRating) + '20', color: getSafetyColor(stock.safetyRating) }}>
                    {stock.safetyRating}
                  </span>
                </div>
                <div>
                  <div className="metric">
                    <span>Yield:</span>
                    <strong>{stock.dividendYield ? (stock.dividendYield * 100).toFixed(2) + '%' : 'N/A'}</strong>
                  </div>
                  <div className="metric">
                    <span>Score:</span>
                    <strong style={{ color: getSafetyColor(stock.safetyRating) }}>
                      {stock.safetyScore.toFixed(1)}
                    </strong>
                  </div>
                  <div className="metric">
                    <span>Years:</span>
                    <strong>{stock.consecutiveYears}</strong>
                  </div>
                </div>
                <div className="cached-actions">
                  <button className="primary" onClick={() => handleSelectCached(stock.symbol)}>
                    View
                  </button>
                  <button className="secondary" onClick={() => handleRefreshCached(stock.symbol)}>
                    Refresh
                  </button>
                </div>
              </div>
            ))}
          </div>
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
                  {analysis.currentMetrics.dividendYield ? (analysis.currentMetrics.dividendYield * 100).toFixed(2) + '%' : 'N/A'}
                </div>
              </div>
              <div className="stat-card">
                <h4>Payout Ratio</h4>
                <div className="value">
                  {analysis.currentMetrics.payoutRatio ? (analysis.currentMetrics.payoutRatio * 100).toFixed(1) + '%' : 'N/A'}
                </div>
              </div>
              <div className="stat-card">
                <h4>Consecutive Years</h4>
                <div className="value">{analysis.historicalAnalysis.consecutiveYearsOfPayments}</div>
              </div>
              <div className="stat-card">
                <h4>Growth Rate</h4>
                <div className="value">
                  {analysis.historicalAnalysis.dividendGrowthRate ? (analysis.historicalAnalysis.dividendGrowthRate * 100).toFixed(1) + '%' : 'N/A'}
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
        </>
      )}
    </div>
  );
}

export default DividendAnalysis;
