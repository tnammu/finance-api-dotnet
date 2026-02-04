import React, { useState, useEffect } from 'react';
import axios from 'axios';
import { LineChart, Line, BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer, Cell } from 'recharts';
import './GrowthAnalysis.css';

const GrowthAnalysis = () => {
  const [symbol, setSymbol] = useState('');
  const [analysis, setAnalysis] = useState(null);
  const [allStocks, setAllStocks] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [sortConfig, setSortConfig] = useState({ key: 'growthScore', direction: 'desc' });
  const [etfHistory, setEtfHistory] = useState(null);
  const [etfLoading, setEtfLoading] = useState(false);
  const [etfHourly, setEtfHourly] = useState(null);
  const [hourlyLoading, setHourlyLoading] = useState(false);
  const [etfIntraday, setEtfIntraday] = useState(null);
  const [intradayLoading, setIntradayLoading] = useState(false);
  const [selectedPeriod, setSelectedPeriod] = useState('1d');
  const [strategy, setStrategy] = useState(null);
  const [strategyLoading, setStrategyLoading] = useState(false);
  const [showStrategyModal, setShowStrategyModal] = useState(false);
  const [strategyCapital, setStrategyCapital] = useState(1000);

  useEffect(() => {
    loadAllGrowthStocks();
    loadEtfHistory('XEG.TO', 5);
    loadEtfHourly('XEG.TO', 730);
    loadEtfIntraday('XEG.TO', '1d');
  }, []);

  const loadAllGrowthStocks = async () => {
    try {
      const response = await axios.get('http://localhost:5000/api/growth/all');
      setAllStocks(response.data.stocks || []);
    } catch (err) {
      console.error('Failed to load growth stocks:', err);
    }
  };

  const loadEtfHistory = async (symbol = 'XEG.TO', years = 5) => {
    setEtfLoading(true);
    try {
      const response = await axios.get(`http://localhost:5000/api/growth/etf-history/${symbol}?years=${years}`);
      setEtfHistory(response.data);
    } catch (err) {
      console.error('Failed to load ETF history:', err);
    } finally {
      setEtfLoading(false);
    }
  };

  const loadEtfHourly = async (symbol = 'XEG.TO', days = 730) => {
    setHourlyLoading(true);
    try {
      const response = await axios.get(`http://localhost:5000/api/growth/etf-hourly/${symbol}?days=${days}`);
      setEtfHourly(response.data);
    } catch (err) {
      console.error('Failed to load ETF hourly data:', err);
    } finally {
      setHourlyLoading(false);
    }
  };

  const loadEtfIntraday = async (symbol = 'XEG.TO', period = '1d') => {
    setIntradayLoading(true);
    setSelectedPeriod(period);
    try {
      const response = await axios.get(`http://localhost:5000/api/growth/etf-intraday/${symbol}?period=${period}`);
      setEtfIntraday(response.data);
    } catch (err) {
      console.error('Failed to load ETF intraday data:', err);
    } finally {
      setIntradayLoading(false);
    }
  };

  const loadStockStrategy = async (stockSymbol, capital = 1000) => {
    setStrategyLoading(true);
    try {
      const response = await axios.get(`http://localhost:5000/api/growth/strategy/${stockSymbol}?capital=${capital}&years=3`);
      setStrategy(response.data);
      setShowStrategyModal(true);
    } catch (err) {
      console.error('Failed to load strategy:', err);
      alert('Failed to load strategy. Please try again.');
    } finally {
      setStrategyLoading(false);
    }
  };

  const handleAnalyze = async (symbolToAnalyze = symbol, refresh = false) => {
    if (!symbolToAnalyze.trim()) {
      setError('Please enter a stock symbol');
      return;
    }

    setLoading(true);
    setError('');

    try {
      const response = await axios.get(
        `http://localhost:5000/api/growth/analyze/${symbolToAnalyze}?refresh=${refresh}`
      );
      setAnalysis(response.data);
      await loadAllGrowthStocks(); // Refresh the table
    } catch (err) {
      setError(err.response?.data?.error || 'Failed to analyze stock');
      setAnalysis(null);
    } finally {
      setLoading(false);
    }
  };

  const handleSort = (key) => {
    let direction = 'desc';
    if (sortConfig.key === key && sortConfig.direction === 'desc') {
      direction = 'asc';
    }
    setSortConfig({ key, direction });
  };

  const sortedStocks = [...allStocks].sort((a, b) => {
    const aValue = a[sortConfig.key] ?? 0;
    const bValue = b[sortConfig.key] ?? 0;

    if (aValue < bValue) return sortConfig.direction === 'asc' ? -1 : 1;
    if (aValue > bValue) return sortConfig.direction === 'asc' ? 1 : -1;
    return 0;
  });

  const formatNumber = (num, decimals = 2) => {
    if (num === null || num === undefined) return 'N/A';
    if (typeof num === 'number') {
      return num.toFixed(decimals);
    }
    return num;
  };

  const formatCurrency = (num) => {
    if (num === null || num === undefined) return 'N/A';
    if (Math.abs(num) >= 1_000_000_000) {
      return `$${(num / 1_000_000_000).toFixed(2)}B`;
    }
    if (Math.abs(num) >= 1_000_000) {
      return `$${(num / 1_000_000).toFixed(2)}M`;
    }
    return `$${num.toLocaleString()}`;
  };

  const getScoreClass = (score) => {
    if (score >= 80) return 'score-strong';
    if (score >= 60) return 'score-moderate';
    if (score >= 40) return 'score-weak';
    return 'score-none';
  };

  const getFilterIcon = (passed) => {
    return passed ? '✅' : '❌';
  };

  const getBarColor = (growth) => {
    if (growth > 0) return '#27ae60'; // Green for positive
    if (growth < 0) return '#e74c3c'; // Red for negative
    return '#95a5a6'; // Gray for zero
  };

  return (
    <div className="growth-container">
      <div className="growth-header">
        <h2>🚀 Growth Stock Analysis</h2>
        <p className="subtitle">5-Filter Growth Strategy: Revenue • EPS • PEG • Rule of 40 • FCF</p>
      </div>

      {/* Analysis Input */}
      <div className="analysis-input">
        <input
          type="text"
          value={symbol}
          onChange={(e) => setSymbol(e.target.value.toUpperCase())}
          onKeyPress={(e) => e.key === 'Enter' && handleAnalyze()}
          placeholder="Enter symbol (e.g., CNQ, SHOP.TO, AAPL)"
          className="symbol-input"
        />
        <button onClick={() => handleAnalyze()} disabled={loading} className="analyze-button">
          {loading ? 'Analyzing...' : 'Analyze'}
        </button>
      </div>

      {error && <div className="error-message">{error}</div>}

      {/* Individual Stock Analysis Results */}
      {analysis && (
        <div className="analysis-results">
          <div className="results-header">
            <div>
              <h3>{analysis.companyName} ({analysis.symbol})</h3>
              <p className="stock-info">{analysis.sector} • {analysis.industry}</p>
              <p className="stock-price">Current Price: ${formatNumber(analysis.currentPrice)}</p>
            </div>
            <div className={`growth-score-badge ${getScoreClass(analysis.growthMetrics.growthScore)}`}>
              <div className="score-value">{analysis.growthMetrics.growthScore}/100</div>
              <div className="score-label">{analysis.growthMetrics.growthRating}</div>
            </div>
          </div>

          <div className="metrics-grid">
            {/* Filter 1: Revenue Growth */}
            <div className="metric-card">
              <div className="metric-header">
                <span className="metric-icon">📈</span>
                <span className="metric-title">Revenue Growth</span>
              </div>
              <div className="metric-value">
                {analysis.growthMetrics.revenueGrowth !== null
                  ? `${formatNumber(analysis.growthMetrics.revenueGrowth, 1)}%`
                  : 'N/A'}
              </div>
              <div className="metric-status">
                {analysis.growthMetrics.revenueGrowth !== null && analysis.growthMetrics.revenueGrowth > 15
                  ? <span className="pass">✅ PASS (&gt; 15%)</span>
                  : <span className="fail">❌ FAIL</span>}
              </div>
            </div>

            {/* Filter 2: EPS Growth */}
            <div className="metric-card">
              <div className="metric-header">
                <span className="metric-icon">💰</span>
                <span className="metric-title">EPS Growth</span>
              </div>
              <div className="metric-value">
                {analysis.growthMetrics.epsGrowthRate !== null
                  ? `${formatNumber(analysis.growthMetrics.epsGrowthRate, 1)}%`
                  : 'N/A'}
              </div>
              <div className="metric-status">
                {analysis.growthMetrics.epsGrowthRate !== null && analysis.growthMetrics.epsGrowthRate > 0
                  ? <span className="pass">✅ PASS (Positive)</span>
                  : <span className="fail">❌ FAIL</span>}
              </div>
            </div>

            {/* Filter 3: PEG Ratio */}
            <div className="metric-card">
              <div className="metric-header">
                <span className="metric-icon">⚖️</span>
                <span className="metric-title">PEG Ratio</span>
              </div>
              <div className="metric-value">
                {analysis.growthMetrics.pegRatio !== null
                  ? formatNumber(analysis.growthMetrics.pegRatio, 2)
                  : 'N/A'}
              </div>
              <div className="metric-status">
                {analysis.growthMetrics.pegRatio !== null && analysis.growthMetrics.pegRatio < 1.5
                  ? <span className="pass">✅ PASS (&lt; 1.5)</span>
                  : <span className="fail">❌ FAIL</span>}
              </div>
            </div>

            {/* Filter 4: Rule of 40 */}
            <div className="metric-card">
              <div className="metric-header">
                <span className="metric-icon">🎯</span>
                <span className="metric-title">Rule of 40</span>
              </div>
              <div className="metric-value">
                {analysis.growthMetrics.ruleOf40Score !== null
                  ? formatNumber(analysis.growthMetrics.ruleOf40Score, 1)
                  : 'N/A'}
              </div>
              <div className="metric-status">
                {analysis.growthMetrics.ruleOf40Score !== null && analysis.growthMetrics.ruleOf40Score > 40
                  ? <span className="pass">✅ PASS (&gt; 40)</span>
                  : <span className="fail">❌ FAIL</span>}
              </div>
            </div>

            {/* Filter 5: Free Cash Flow */}
            <div className="metric-card">
              <div className="metric-header">
                <span className="metric-icon">💵</span>
                <span className="metric-title">Free Cash Flow</span>
              </div>
              <div className="metric-value">
                {analysis.growthMetrics.freeCashFlow !== null
                  ? formatCurrency(analysis.growthMetrics.freeCashFlow)
                  : 'N/A'}
              </div>
              <div className="metric-status">
                {analysis.growthMetrics.freeCashFlow !== null && analysis.growthMetrics.freeCashFlow > 0
                  ? <span className="pass">✅ PASS (Positive)</span>
                  : <span className="fail">❌ FAIL</span>}
              </div>
            </div>
          </div>

          {/* Dividend vs Growth Comparison */}
          {analysis.dividendMetrics.safetyScore > 0 && (
            <div className="comparison-section">
              <h4>📊 Dividend vs Growth Comparison</h4>
              <div className="comparison-grid">
                <div className="comparison-item">
                  <span className="label">Growth Score:</span>
                  <span className={`value ${getScoreClass(analysis.growthMetrics.growthScore)}`}>
                    {analysis.growthMetrics.growthScore}/100
                  </span>
                </div>
                <div className="comparison-item">
                  <span className="label">Dividend Safety Score:</span>
                  <span className="value">{formatNumber(analysis.dividendMetrics.safetyScore)}/100</span>
                </div>
                <div className="comparison-item">
                  <span className="label">Strategy Fit:</span>
                  <span className="value">
                    {analysis.growthMetrics.growthScore >= 60 && analysis.dividendMetrics.safetyScore >= 70
                      ? '🔥 Growth + Income Hybrid'
                      : analysis.growthMetrics.growthScore >= 60
                      ? '🚀 Pure Growth Play'
                      : analysis.dividendMetrics.safetyScore >= 70
                      ? '💰 Income Focus'
                      : '⚠️ Review Required'}
                  </span>
                </div>
              </div>
            </div>
          )}
        </div>
      )}

      {/* Growth Stocks Portfolio Table */}
      {allStocks.length > 0 && (
        <div className="growth-table-section">
          <h3>📋 Your Growth Stock Portfolio ({allStocks.length} stocks)</h3>
          <div className="table-container">
            <table className="growth-table">
              <thead>
                <tr>
                  <th onClick={() => handleSort('symbol')}>Symbol {sortConfig.key === 'symbol' && (sortConfig.direction === 'asc' ? '↑' : '↓')}</th>
                  <th onClick={() => handleSort('companyName')}>Company</th>
                  <th onClick={() => handleSort('growthScore')}>Growth Score {sortConfig.key === 'growthScore' && (sortConfig.direction === 'asc' ? '↑' : '↓')}</th>
                  <th onClick={() => handleSort('revenueGrowth')}>Revenue Growth {sortConfig.key === 'revenueGrowth' && (sortConfig.direction === 'asc' ? '↑' : '↓')}</th>
                  <th onClick={() => handleSort('epsGrowthRate')}>EPS Growth {sortConfig.key === 'epsGrowthRate' && (sortConfig.direction === 'asc' ? '↑' : '↓')}</th>
                  <th onClick={() => handleSort('pegRatio')}>PEG {sortConfig.key === 'pegRatio' && (sortConfig.direction === 'asc' ? '↑' : '↓')}</th>
                  <th onClick={() => handleSort('ruleOf40Score')}>Rule 40 {sortConfig.key === 'ruleOf40Score' && (sortConfig.direction === 'asc' ? '↑' : '↓')}</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {sortedStocks.map((stock) => (
                  <tr key={stock.symbol}>
                    <td className="symbol-cell">{stock.symbol}</td>
                    <td>{stock.companyName}</td>
                    <td className={`score-cell ${getScoreClass(stock.growthScore)}`}>
                      {stock.growthScore}/100
                    </td>
                    <td className={stock.revenueGrowth > 15 ? 'positive' : 'negative'}>
                      {formatNumber(stock.revenueGrowth, 1)}%
                    </td>
                    <td className={stock.epsGrowthRate > 0 ? 'positive' : 'negative'}>
                      {formatNumber(stock.epsGrowthRate, 1)}%
                    </td>
                    <td className={stock.pegRatio < 1.5 ? 'positive' : 'negative'}>
                      {formatNumber(stock.pegRatio, 2)}
                    </td>
                    <td className={stock.ruleOf40Score > 40 ? 'positive' : 'negative'}>
                      {formatNumber(stock.ruleOf40Score, 1)}
                    </td>
                    <td>
                      <button
                        onClick={() => handleAnalyze(stock.symbol, true)}
                        className="refresh-button"
                        title="Refresh growth data"
                      >
                        🔄
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {allStocks.length === 0 && !loading && (
        <div className="empty-state">
          <p>No growth stocks analyzed yet. Try analyzing CNQ, SHOP.TO, or AAPL!</p>
        </div>
      )}

      {/* ETF Historical Chart */}
      <div className="etf-history-section">
        <div className="section-header">
          <h3>📊 XEG.TO - 5-Year Historical Performance</h3>
          {etfHistory && (
            <div className="etf-stats">
              <span className="stat-item">
                <strong>Start:</strong> ${etfHistory.startPrice} ({etfHistory.startDate})
              </span>
              <span className="stat-item">
                <strong>Current:</strong> ${etfHistory.currentPrice} ({etfHistory.endDate})
              </span>
              <span className={`stat-item ${etfHistory.totalReturn >= 0 ? 'positive' : 'negative'}`}>
                <strong>Total Return:</strong> {etfHistory.totalReturn >= 0 ? '+' : ''}{etfHistory.totalReturn}%
              </span>
            </div>
          )}
        </div>

        {etfLoading && <div className="loading-message">Loading ETF historical data...</div>}

        {etfHistory && !etfLoading && (
          <div className="chart-container">
            <ResponsiveContainer width="100%" height={400}>
              <LineChart data={etfHistory.history} margin={{ top: 5, right: 30, left: 20, bottom: 5 }}>
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis
                  dataKey="date"
                  tick={{ fontSize: 12 }}
                  tickFormatter={(value) => {
                    const date = new Date(value);
                    return `${date.getMonth() + 1}/${date.getFullYear().toString().substr(2)}`;
                  }}
                  interval="preserveStartEnd"
                  minTickGap={50}
                />
                <YAxis
                  tick={{ fontSize: 12 }}
                  domain={['auto', 'auto']}
                  tickFormatter={(value) => `$${value.toFixed(2)}`}
                />
                <Tooltip
                  formatter={(value) => [`$${value.toFixed(2)}`, 'Price']}
                  labelFormatter={(label) => `Date: ${label}`}
                />
                <Legend />
                <Line
                  type="monotone"
                  dataKey="close"
                  stroke="#2563eb"
                  strokeWidth={2}
                  dot={false}
                  name="Closing Price"
                />
              </LineChart>
            </ResponsiveContainer>

            {/* Yearly Returns */}
            {etfHistory.yearlyReturns && etfHistory.yearlyReturns.length > 0 && (
              <div className="yearly-returns">
                <h4>Annual Returns</h4>
                <div className="returns-grid">
                  {etfHistory.yearlyReturns.map((yearData) => (
                    <div key={yearData.year} className="return-item">
                      <span className="year">{yearData.year}</span>
                      <span className={`return ${yearData.return >= 0 ? 'positive' : 'negative'}`}>
                        {yearData.return >= 0 ? '+' : ''}{yearData.return}%
                      </span>
                    </div>
                  ))}
                </div>
              </div>
            )}
          </div>
        )}

        {/* Consolidated Monthly Data */}
        {etfHistory && etfHistory.consolidatedMonthlyData && etfHistory.consolidatedMonthlyData.length > 0 && (
          <div className="consolidated-section">
            <div className="section-header">
              <h3>📅 Average Growth by Calendar Month</h3>
              <p className="section-subtitle">Historical performance across all years</p>
            </div>

            {/* Consolidated Monthly Bar Chart */}
            <div className="chart-container">
              <ResponsiveContainer width="100%" height={350}>
                <BarChart data={etfHistory.consolidatedMonthlyData} margin={{ top: 20, right: 30, left: 20, bottom: 40 }}>
                  <CartesianGrid strokeDasharray="3 3" />
                  <XAxis
                    dataKey="month"
                    tick={{ fontSize: 12 }}
                    tickFormatter={(value) => value.substring(0, 3)}
                  />
                  <YAxis label={{ value: 'Avg Growth (%)', angle: -90, position: 'insideLeft' }} />
                  <Tooltip
                    content={({ active, payload }) => {
                      if (active && payload && payload.length) {
                        const data = payload[0].payload;
                        return (
                          <div className="etf-tooltip">
                            <p className="tooltip-month"><strong>{data.month}</strong></p>
                            <p className={`tooltip-growth ${data.avgGrowth >= 0 ? 'positive' : 'negative'}`}>
                              Avg Growth: {data.avgGrowth >= 0 ? '+' : ''}{data.avgGrowth}%
                            </p>
                            <p className="tooltip-info">Win Rate: {data.positivePercentage}%</p>
                            <p className="tooltip-info">Positive: {data.positiveCount}/{data.occurrences}</p>
                          </div>
                        );
                      }
                      return null;
                    }}
                  />
                  <Bar dataKey="avgGrowth" radius={[4, 4, 0, 0]}>
                    {etfHistory.consolidatedMonthlyData.map((entry, index) => (
                      <Cell key={`cell-${index}`} fill={getBarColor(entry.avgGrowth)} />
                    ))}
                  </Bar>
                </BarChart>
              </ResponsiveContainer>
            </div>

            {/* Consolidated Monthly Data Table */}
            <div className="consolidated-table">
              <h4>Detailed Monthly Breakdown</h4>
              <table>
                <thead>
                  <tr>
                    <th>Month</th>
                    <th>Avg Growth</th>
                    <th>Positive Months</th>
                    <th>Negative Months</th>
                    <th>Win Rate</th>
                    <th>Total Occurrences</th>
                  </tr>
                </thead>
                <tbody>
                  {etfHistory.consolidatedMonthlyData.map((month, idx) => (
                    <tr key={idx} className={month.avgGrowth >= 0 ? 'positive-row' : 'negative-row'}>
                      <td className="month-name"><strong>{month.month}</strong></td>
                      <td className={`growth-cell ${month.avgGrowth >= 0 ? 'positive' : 'negative'}`}>
                        {month.avgGrowth >= 0 ? '+' : ''}{month.avgGrowth}%
                      </td>
                      <td className="positive-count">{month.positiveCount}</td>
                      <td className="negative-count">{month.negativeCount}</td>
                      <td className="win-rate">
                        <span className={`win-rate-badge ${month.positivePercentage >= 60 ? 'high' : month.positivePercentage >= 40 ? 'medium' : 'low'}`}>
                          {month.positivePercentage}%
                        </span>
                      </td>
                      <td className="occurrences">{month.occurrences}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            {/* Summary Statistics */}
            <div className="consolidated-summary">
              <div className="summary-stats">
                <div className="stat-item">
                  <span className="stat-label">Best Month:</span>
                  <span className="stat-value positive">
                    {etfHistory.consolidatedMonthlyData.reduce((best, month) =>
                      month.avgGrowth > best.avgGrowth ? month : best
                    ).month} ({etfHistory.consolidatedMonthlyData.reduce((best, month) =>
                      month.avgGrowth > best.avgGrowth ? month : best
                    ).avgGrowth >= 0 ? '+' : ''}{etfHistory.consolidatedMonthlyData.reduce((best, month) =>
                      month.avgGrowth > best.avgGrowth ? month : best
                    ).avgGrowth}%)
                  </span>
                </div>
                <div className="stat-item">
                  <span className="stat-label">Worst Month:</span>
                  <span className="stat-value negative">
                    {etfHistory.consolidatedMonthlyData.reduce((worst, month) =>
                      month.avgGrowth < worst.avgGrowth ? month : worst
                    ).month} ({etfHistory.consolidatedMonthlyData.reduce((worst, month) =>
                      month.avgGrowth < worst.avgGrowth ? month : worst
                    ).avgGrowth >= 0 ? '+' : ''}{etfHistory.consolidatedMonthlyData.reduce((worst, month) =>
                      month.avgGrowth < worst.avgGrowth ? month : worst
                    ).avgGrowth}%)
                  </span>
                </div>
                <div className="stat-item">
                  <span className="stat-label">Most Consistent:</span>
                  <span className="stat-value">
                    {etfHistory.consolidatedMonthlyData.reduce((best, month) =>
                      month.positivePercentage > best.positivePercentage ? month : best
                    ).month} ({etfHistory.consolidatedMonthlyData.reduce((best, month) =>
                      month.positivePercentage > best.positivePercentage ? month : best
                    ).positivePercentage}% win rate)
                  </span>
                </div>
              </div>
            </div>
          </div>
        )}

        {/* Hourly Analysis Section */}
        {etfHourly && etfHourly.hourlyAnalysis && etfHourly.hourlyAnalysis.length > 0 && (
          <div className="hourly-section">
            <div className="section-header">
              <h3>⏰ Hourly Performance Analysis</h3>
              <p className="section-subtitle">Average price & volume by hour of day (last 730 days)</p>
              {etfHourly.summary && (
                <div className="hourly-stats">
                  <span className="stat-item">
                    <strong>Highest Price Hour:</strong> {etfHourly.summary.highestPriceHour?.timeLabel} (${etfHourly.summary.highestPriceHour?.avgPrice})
                  </span>
                  <span className="stat-item">
                    <strong>Highest Volume Hour:</strong> {etfHourly.summary.highestVolumeHour?.timeLabel} ({etfHourly.summary.highestVolumeHour?.avgVolume?.toLocaleString()})
                  </span>
                  <span className="stat-item">
                    <strong>Most Volatile Hour:</strong> {etfHourly.summary.mostVolatileHour?.timeLabel} (σ=${etfHourly.summary.mostVolatileHour?.priceStdDev})
                  </span>
                </div>
              )}
            </div>

            {hourlyLoading && <div className="loading-message">Loading hourly data...</div>}

            {!hourlyLoading && (
              <>
                {/* Dual-axis chart: Price and Volume */}
                <div className="chart-container">
                  <ResponsiveContainer width="100%" height={400}>
                    <LineChart data={etfHourly.hourlyAnalysis} margin={{ top: 20, right: 60, left: 20, bottom: 40 }}>
                      <CartesianGrid strokeDasharray="3 3" />
                      <XAxis
                        dataKey="timeLabel"
                        label={{ value: 'Hour of Day', position: 'insideBottom', offset: -10 }}
                        tick={{ fontSize: 12 }}
                      />
                      <YAxis
                        yAxisId="left"
                        label={{ value: 'Average Price ($)', angle: -90, position: 'insideLeft' }}
                        tickFormatter={(value) => `$${value.toFixed(2)}`}
                      />
                      <YAxis
                        yAxisId="right"
                        orientation="right"
                        label={{ value: 'Average Volume', angle: 90, position: 'insideRight' }}
                        tickFormatter={(value) => value.toLocaleString()}
                      />
                      <Tooltip
                        formatter={(value, name) => {
                          if (name === 'Average Price') return [`$${value.toFixed(2)}`, name];
                          if (name === 'Average Volume') return [value.toLocaleString(), name];
                          return [value, name];
                        }}
                        labelFormatter={(label) => `Time: ${label}`}
                      />
                      <Legend />
                      <Line
                        yAxisId="left"
                        type="monotone"
                        dataKey="avgPrice"
                        stroke="#2563eb"
                        strokeWidth={2}
                        dot={{ r: 3 }}
                        name="Average Price"
                      />
                      <Line
                        yAxisId="right"
                        type="monotone"
                        dataKey="avgVolume"
                        stroke="#10b981"
                        strokeWidth={2}
                        dot={{ r: 3 }}
                        name="Average Volume"
                      />
                    </LineChart>
                  </ResponsiveContainer>
                </div>

                {/* Hourly Data Table */}
                <div className="hourly-table">
                  <h4>Detailed Hourly Breakdown</h4>
                  <table>
                    <thead>
                      <tr>
                        <th>Hour</th>
                        <th>Avg Price</th>
                        <th>Price Range</th>
                        <th>Avg Volume</th>
                        <th>Volatility (σ)</th>
                        <th>Data Points</th>
                      </tr>
                    </thead>
                    <tbody>
                      {etfHourly.hourlyAnalysis.map((hour, idx) => (
                        <tr key={idx}>
                          <td className="hour-label"><strong>{hour.timeLabel}</strong></td>
                          <td className="price-cell">${hour.avgPrice}</td>
                          <td className="range-cell">${hour.minPrice} - ${hour.maxPrice}</td>
                          <td className="volume-cell">{hour.avgVolume.toLocaleString()}</td>
                          <td className="volatility-cell">${hour.priceStdDev}</td>
                          <td className="occurrences">{hour.occurrences}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </>
            )}
          </div>
        )}

        {/* Today's Intraday Section */}
        {etfIntraday && etfIntraday.intradayData && etfIntraday.intradayData.length > 0 && (
          <div className="intraday-section">
            <div className="section-header">
              <h3>📈 {etfIntraday.periodLabel} Performance</h3>
              <p className="section-subtitle">{etfIntraday.interval} interval data</p>

              {/* Time Frame Selector */}
              <div className="time-frame-selector">
                {[
                  { value: '1d', label: '1 Day' },
                  { value: '5d', label: '5 Days' },
                  { value: '1wk', label: '1 Week' },
                  { value: '1mo', label: '1 Month' },
                  { value: 'ytd', label: 'YTD' },
                  { value: '1y', label: '1 Year' },
                  { value: '5y', label: '5 Years' }
                ].map(period => (
                  <button
                    key={period.value}
                    className={`period-btn ${selectedPeriod === period.value ? 'active' : ''}`}
                    onClick={() => loadEtfIntraday('XEG.TO', period.value)}
                    disabled={intradayLoading}
                  >
                    {period.label}
                  </button>
                ))}
              </div>

              {etfIntraday && (
                <div className="intraday-stats">
                  <span className="stat-item">
                    <strong>Period Start:</strong> ${etfIntraday.openingPrice}
                  </span>
                  <span className="stat-item">
                    <strong>Period End:</strong> ${etfIntraday.currentPrice}
                  </span>
                  <span className={`stat-item ${etfIntraday.dayChangePercent >= 0 ? 'positive' : 'negative'}`}>
                    <strong>Period Change:</strong> {etfIntraday.dayChangePercent >= 0 ? '+' : ''}{etfIntraday.dayChangePercent}% (${etfIntraday.dayChange >= 0 ? '+' : ''}{etfIntraday.dayChange})
                  </span>
                  <span className="stat-item">
                    <strong>{etfIntraday.periodLabel} Range:</strong> ${etfIntraday.lowOfDay} - ${etfIntraday.highOfDay}
                  </span>
                  <span className="stat-item">
                    <strong>Total Volume ({etfIntraday.periodLabel}):</strong> {etfIntraday.totalVolume?.toLocaleString()}
                  </span>
                  <span className="stat-item">
                    <strong>Data Points:</strong> {etfIntraday.dataPoints} hours
                  </span>
                </div>
              )}
            </div>

            {intradayLoading && <div className="loading-message">Loading today's intraday data...</div>}

            {!intradayLoading && (
              <>
                <div className="intraday-charts-grid">
                  {/* Volume Chart - Buy/Sell Volume */}
                  <div className="intraday-chart">
                    <h4>Average Hourly Volume (Buy/Sell) - {etfIntraday.periodLabel}</h4>
                    <ResponsiveContainer width="100%" height={300}>
                      <BarChart
                        data={etfIntraday.intradayData.map(d => ({
                          ...d,
                          signedVolume: d.avgPriceChange >= 0 ? d.avgVolume : -d.avgVolume
                        }))}
                        margin={{ top: 20, right: 30, left: 20, bottom: 40 }}
                      >
                        <CartesianGrid strokeDasharray="3 3" />
                        <XAxis
                          dataKey="time"
                          label={{ value: 'Hour of Day', position: 'insideBottom', offset: -10 }}
                          tick={{ fontSize: 12 }}
                        />
                        <YAxis
                          label={{ value: 'Avg Volume (Buy +ve / Sell -ve)', angle: -90, position: 'insideLeft' }}
                          tickFormatter={(value) => Math.abs(value).toLocaleString()}
                        />
                        <Tooltip
                          formatter={(value) => [
                            Math.abs(value).toLocaleString(),
                            value >= 0 ? 'Buy Volume' : 'Sell Volume'
                          ]}
                          labelFormatter={(label) => `Hour: ${label}`}
                        />
                        <Bar dataKey="signedVolume">
                          {etfIntraday.intradayData.map((entry, index) => (
                            <Cell key={`cell-${index}`} fill={entry.avgPriceChange >= 0 ? '#27ae60' : '#e74c3c'} />
                          ))}
                        </Bar>
                      </BarChart>
                    </ResponsiveContainer>
                  </div>

                  {/* Price Change Chart - Histogram */}
                  <div className="intraday-chart">
                    <h4>Average Hourly Price Change (%) - {etfIntraday.periodLabel}</h4>
                    <ResponsiveContainer width="100%" height={300}>
                      <BarChart data={etfIntraday.intradayData} margin={{ top: 20, right: 30, left: 20, bottom: 40 }}>
                        <CartesianGrid strokeDasharray="3 3" />
                        <XAxis
                          dataKey="time"
                          label={{ value: 'Hour of Day', position: 'insideBottom', offset: -10 }}
                          tick={{ fontSize: 12 }}
                        />
                        <YAxis
                          label={{ value: 'Avg Change (%)', angle: -90, position: 'insideLeft' }}
                          tickFormatter={(value) => `${value >= 0 ? '+' : ''}${value}%`}
                        />
                        <Tooltip
                          formatter={(value) => [`${value >= 0 ? '+' : ''}${value}%`, 'Avg Price Change']}
                          labelFormatter={(label) => `Hour: ${label}`}
                        />
                        <Bar dataKey="avgPriceChange">
                          {etfIntraday.intradayData.map((entry, index) => (
                            <Cell key={`cell-${index}`} fill={entry.avgPriceChange >= 0 ? '#27ae60' : '#e74c3c'} />
                          ))}
                        </Bar>
                      </BarChart>
                    </ResponsiveContainer>
                  </div>
                </div>

                {/* Hourly Average Data Table */}
                <div className="intraday-table">
                  <h4>Hourly Average Breakdown ({etfIntraday.periodLabel})</h4>
                  <table>
                    <thead>
                      <tr>
                        <th>Hour</th>
                        <th>Avg Price</th>
                        <th>Price Range</th>
                        <th>Avg Volume</th>
                        <th>Avg Change %</th>
                        <th>Volatility (σ)</th>
                        <th>Data Points</th>
                      </tr>
                    </thead>
                    <tbody>
                      {etfIntraday.intradayData.map((data, idx) => (
                        <tr key={idx}>
                          <td className="time-label"><strong>{data.time}</strong></td>
                          <td className="price-cell">${data.avgPrice}</td>
                          <td className="range-cell">${data.minPrice} - ${data.maxPrice}</td>
                          <td className="volume-cell">{data.avgVolume.toLocaleString()}</td>
                          <td className={`change-cell ${data.avgPriceChange >= 0 ? 'positive' : 'negative'}`}>
                            {data.avgPriceChange >= 0 ? '+' : ''}{data.avgPriceChange}%
                          </td>
                          <td className="volatility-cell">${data.priceStdDev}</td>
                          <td className="occurrences">{data.occurrences}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </>
            )}
          </div>
        )}
      </div>
    </div>
  );
};

export default GrowthAnalysis;