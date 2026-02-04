import React, { useState, useEffect } from 'react';
import { BarChart, Bar, LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer, Cell } from 'recharts';
import axios from 'axios';
import './SP500Analysis.css';

const SP500Analysis = () => {
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [chartType, setChartType] = useState('bar'); // 'bar' or 'line'
  const [period, setPeriod] = useState(5);
  const [customPeriod, setCustomPeriod] = useState('');

  useEffect(() => {
    fetchSP500Data();
  }, [period]);

  const handlePeriodChange = (years) => {
    // Validate: only integers between 1-20
    const yearNum = parseInt(years, 10);
    if (!isNaN(yearNum) && yearNum >= 1 && yearNum <= 20) {
      setPeriod(yearNum);
      setCustomPeriod('');
    }
  };

  const handleCustomPeriodSubmit = (e) => {
    e.preventDefault();
    const yearNum = parseInt(customPeriod, 10);
    if (!isNaN(yearNum) && yearNum >= 1 && yearNum <= 20) {
      setPeriod(yearNum);
    } else {
      alert('Please enter a number between 1 and 20');
    }
  };

  const fetchSP500Data = async () => {
    setLoading(true);
    setError('');
    try {
      const response = await axios.get(`http://localhost:5000/api/sp500/monthly-growth?years=${period}`);
      console.log('S&P 500 Response:', response.data);

      if (response.data.success) {
        setData(response.data);
      } else {
        setError(response.data.error || 'Failed to fetch S&P 500 data');
      }
    } catch (err) {
      console.error('Error fetching S&P 500 data:', err);
      setError(err.response?.data?.error || 'Failed to fetch S&P 500 data. Please try again.');
    } finally {
      setLoading(false);
    }
  };

  const getBarColor = (growth) => {
    if (growth > 0) return '#27ae60'; // Green for positive
    if (growth < 0) return '#e74c3c'; // Red for negative
    return '#95a5a6'; // Gray for zero
  };

  const CustomTooltip = ({ active, payload }) => {
    if (active && payload && payload.length) {
      const data = payload[0].payload;
      return (
        <div className="sp500-tooltip">
          <p className="tooltip-date">{data.date}</p>
          <p className="tooltip-close">Close: ${data.close.toLocaleString()}</p>
          <p className={`tooltip-growth ${data.growth >= 0 ? 'positive' : 'negative'}`}>
            Growth: {data.growth >= 0 ? '+' : ''}{data.growth}%
          </p>
        </div>
      );
    }
    return null;
  };

  if (loading) {
    return (
      <div className="sp500-container">
        <div className="loading-message">
          <h2>Loading S&P 500 Data...</h2>
          <p>Fetching {period} year{period > 1 ? 's' : ''} of monthly data...</p>
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="sp500-container">
        <div className="error-message">
          <span>{error}</span>
          <button onClick={fetchSP500Data} className="retry-button">Retry</button>
        </div>
      </div>
    );
  }

  if (!data || !data.monthlyData) {
    return (
      <div className="sp500-container">
        <div className="no-data-message">
          <h3>No data available</h3>
        </div>
      </div>
    );
  }

  const { monthlyData, statistics, consolidatedMonthlyData } = data;

  return (
    <div className="sp500-container">
      <div className="sp500-header">
        <div className="header-content">
          <h2>S&P 500 Index Monthly Growth</h2>
          <p className="subtitle">Past {period} Year{period > 1 ? 's' : ''} Performance Analysis</p>
        </div>

        <div className="period-selector">
          <button
            className={period === 1 ? 'active' : ''}
            onClick={() => handlePeriodChange(1)}
          >
            1 Year
          </button>
          <button
            className={period === 3 ? 'active' : ''}
            onClick={() => handlePeriodChange(3)}
          >
            3 Years
          </button>
          <button
            className={period === 5 ? 'active' : ''}
            onClick={() => handlePeriodChange(5)}
          >
            5 Years
          </button>
          <button
            className={period === 10 ? 'active' : ''}
            onClick={() => handlePeriodChange(10)}
          >
            10 Years
          </button>
          <form onSubmit={handleCustomPeriodSubmit} className="custom-period-form">
            <input
              type="number"
              min="1"
              max="20"
              step="1"
              placeholder="Custom (1-20)"
              value={customPeriod}
              onChange={(e) => setCustomPeriod(e.target.value)}
              className="custom-period-input"
            />
            <button type="submit" className="custom-period-button">Go</button>
          </form>
        </div>
      </div>

      {/* Summary Statistics Cards */}
      <div className="summary-cards">
        <div className="summary-card">
          <div className="summary-value">{statistics.totalReturn >= 0 ? '+' : ''}{statistics.totalReturn}%</div>
          <div className="summary-label">{period}-Year Total Return</div>
        </div>

        <div className="summary-card">
          <div className="summary-value">{statistics.avgMonthlyGrowth >= 0 ? '+' : ''}{statistics.avgMonthlyGrowth}%</div>
          <div className="summary-label">Avg Monthly Growth</div>
        </div>

        <div className="summary-card">
          <div className="summary-value">{statistics.positiveMonths}</div>
          <div className="summary-label">Positive Months</div>
        </div>

        <div className="summary-card">
          <div className="summary-value">{statistics.negativeMonths}</div>
          <div className="summary-label">Negative Months</div>
        </div>
      </div>

      {/* Best and Worst Months */}
      <div className="extremes-section">
        <div className="extreme-card best-month">
          <h4>Best Month</h4>
          <p className="extreme-date">{statistics.bestMonth?.date}</p>
          <p className="extreme-value positive">+{statistics.bestMonth?.growth}%</p>
        </div>

        <div className="extreme-card worst-month">
          <h4>Worst Month</h4>
          <p className="extreme-date">{statistics.worstMonth?.date}</p>
          <p className="extreme-value negative">{statistics.worstMonth?.growth}%</p>
        </div>
      </div>

      {/* Chart Type Toggle */}
      <div className="chart-controls">
        <button
          className={`chart-toggle ${chartType === 'bar' ? 'active' : ''}`}
          onClick={() => setChartType('bar')}
        >
          Bar Chart
        </button>
        <button
          className={`chart-toggle ${chartType === 'line' ? 'active' : ''}`}
          onClick={() => setChartType('line')}
        >
          Line Chart
        </button>
      </div>

      {/* Monthly Growth Chart */}
      <div className="chart-container">
        <h3>Monthly Growth Rate</h3>
        <ResponsiveContainer width="100%" height={400}>
          {chartType === 'bar' ? (
            <BarChart data={monthlyData} margin={{ top: 20, right: 30, left: 20, bottom: 60 }}>
              <CartesianGrid strokeDasharray="3 3" />
              <XAxis
                dataKey="date"
                angle={-45}
                textAnchor="end"
                height={100}
                interval={0}
                tick={{ fontSize: 10 }}
              />
              <YAxis label={{ value: 'Growth (%)', angle: -90, position: 'insideLeft' }} />
              <Tooltip content={<CustomTooltip />} />
              <Bar dataKey="growth" radius={[4, 4, 0, 0]}>
                {monthlyData.map((entry, index) => (
                  <Cell key={`cell-${index}`} fill={getBarColor(entry.growth)} />
                ))}
              </Bar>
            </BarChart>
          ) : (
            <LineChart data={monthlyData} margin={{ top: 20, right: 30, left: 20, bottom: 60 }}>
              <CartesianGrid strokeDasharray="3 3" />
              <XAxis
                dataKey="date"
                angle={-45}
                textAnchor="end"
                height={100}
                interval={0}
                tick={{ fontSize: 10 }}
              />
              <YAxis label={{ value: 'Growth (%)', angle: -90, position: 'insideLeft' }} />
              <Tooltip content={<CustomTooltip />} />
              <Line type="monotone" dataKey="growth" stroke="#3498db" strokeWidth={2} dot={{ r: 3 }} />
            </LineChart>
          )}
        </ResponsiveContainer>
      </div>

      {/* S&P 500 Index Value Chart */}
      <div className="chart-container">
        <h3>S&P 500 Index Value</h3>
        <ResponsiveContainer width="100%" height={400}>
          <LineChart data={monthlyData} margin={{ top: 20, right: 30, left: 20, bottom: 60 }}>
            <CartesianGrid strokeDasharray="3 3" />
            <XAxis
              dataKey="date"
              angle={-45}
              textAnchor="end"
              height={100}
              interval={0}
              tick={{ fontSize: 10 }}
            />
            <YAxis label={{ value: 'Index Value ($)', angle: -90, position: 'insideLeft' }} />
            <Tooltip content={<CustomTooltip />} />
            <Line type="monotone" dataKey="close" stroke="#27ae60" strokeWidth={2} dot={{ r: 3 }} />
          </LineChart>
        </ResponsiveContainer>
      </div>

      {/* Consolidated Monthly Breakdown */}
      {consolidatedMonthlyData && consolidatedMonthlyData.length > 0 && (
        <div className="consolidated-section">
          <div className="section-header">
            <h3>Average Growth by Calendar Month</h3>
            <p className="section-subtitle">Historical performance across all years</p>
          </div>

          {/* Consolidated Monthly Bar Chart */}
          <div className="chart-container">
            <ResponsiveContainer width="100%" height={350}>
              <BarChart data={consolidatedMonthlyData} margin={{ top: 20, right: 30, left: 20, bottom: 40 }}>
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
                        <div className="sp500-tooltip">
                          <p className="tooltip-date"><strong>{data.month}</strong></p>
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
                  {consolidatedMonthlyData.map((entry, index) => (
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
                {consolidatedMonthlyData.map((month, idx) => (
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
                  {consolidatedMonthlyData.reduce((best, month) =>
                    month.avgGrowth > best.avgGrowth ? month : best
                  ).month} ({consolidatedMonthlyData.reduce((best, month) =>
                    month.avgGrowth > best.avgGrowth ? month : best
                  ).avgGrowth >= 0 ? '+' : ''}{consolidatedMonthlyData.reduce((best, month) =>
                    month.avgGrowth > best.avgGrowth ? month : best
                  ).avgGrowth}%)
                </span>
              </div>
              <div className="stat-item">
                <span className="stat-label">Worst Month:</span>
                <span className="stat-value negative">
                  {consolidatedMonthlyData.reduce((worst, month) =>
                    month.avgGrowth < worst.avgGrowth ? month : worst
                  ).month} ({consolidatedMonthlyData.reduce((worst, month) =>
                    month.avgGrowth < worst.avgGrowth ? month : worst
                  ).avgGrowth >= 0 ? '+' : ''}{consolidatedMonthlyData.reduce((worst, month) =>
                    month.avgGrowth < worst.avgGrowth ? month : worst
                  ).avgGrowth}%)
                </span>
              </div>
              <div className="stat-item">
                <span className="stat-label">Most Consistent:</span>
                <span className="stat-value">
                  {consolidatedMonthlyData.reduce((best, month) =>
                    month.positivePercentage > best.positivePercentage ? month : best
                  ).month} ({consolidatedMonthlyData.reduce((best, month) =>
                    month.positivePercentage > best.positivePercentage ? month : best
                  ).positivePercentage}% win rate)
                </span>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Monthly Data Table */}
      <div className="monthly-table-section">
        <h3>All Monthly Data (Chronological)</h3>
        <div className="monthly-table">
          <table>
            <thead>
              <tr>
                <th>Month</th>
                <th>Close Price</th>
                <th>Monthly Growth</th>
              </tr>
            </thead>
            <tbody>
              {monthlyData.map((month, idx) => (
                <tr key={idx}>
                  <td className="month-cell">{month.date}</td>
                  <td className="price-cell">${month.close.toLocaleString()}</td>
                  <td className={`growth-cell ${month.growth >= 0 ? 'positive' : 'negative'}`}>
                    {month.growth >= 0 ? '+' : ''}{month.growth}%
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
};

export default SP500Analysis;
