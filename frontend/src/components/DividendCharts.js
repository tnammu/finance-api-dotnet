import React, { useState, useEffect } from 'react';
import { dividendsAPI } from '../services/api';
import axios from 'axios';
import {
  LineChart, Line, BarChart, Bar, XAxis, YAxis, CartesianGrid,
  Tooltip, Legend, ResponsiveContainer, Area, AreaChart
} from 'recharts';
import './DividendCharts.css';

const API_BASE_URL = 'http://localhost:5000/api';

function DividendCharts({ symbol }) {
  const [chartData, setChartData] = useState(null);
  const [performanceData, setPerformanceData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [perfPeriod, setPerfPeriod] = useState(1);

  useEffect(() => {
    if (symbol) {
      loadChartData();
      loadPerformanceData();
    }
  }, [symbol, perfPeriod]);

  const loadChartData = async () => {
    try {
      setLoading(true);
      setError(null);
      const response = await dividendsAPI.getCharts(symbol);
      setChartData(response.data);
    } catch (err) {
      setError('Failed to load chart data: ' + err.message);
      console.error('Chart data error:', err);
    } finally {
      setLoading(false);
    }
  };

  const loadPerformanceData = async () => {
    try {
      const response = await axios.get(`${API_BASE_URL}/performance/python-portfolio?period=${perfPeriod}`, {
        timeout: 60000
      });

      // Find the performance data for this specific symbol
      const stockPerf = response.data.stocks.find(s => s.symbol === symbol);
      const benchmark = response.data.benchmark;

      if (stockPerf && benchmark) {
        setPerformanceData({
          stock: {
            totalReturn: stockPerf.total_return,
            annualizedReturn: stockPerf.annualized_return,
            volatility: stockPerf.volatility,
            sharpeRatio: stockPerf.sharpe_ratio,
            beta: stockPerf.beta,
            alpha: stockPerf.alpha,
            vsSpX: stockPerf.vs_benchmark
          },
          benchmark: {
            totalReturn: benchmark.total_return,
            annualizedReturn: benchmark.annualized_return,
            volatility: benchmark.volatility
          }
        });
      }
    } catch (err) {
      console.error('Performance data error:', err);
      // Don't set error, just don't show the performance chart
      setPerformanceData(null);
    }
  };

  if (loading) {
    return <div className="charts-loading">Loading charts...</div>;
  }

  if (error) {
    return <div className="charts-error">{error}</div>;
  }

  if (!chartData || !chartData.charts) {
    return <div className="charts-empty">No chart data available</div>;
  }

  const { charts, currentMetrics } = chartData;

  return (
    <div className="dividend-charts">
      <h2>📊 Dividend Analysis Charts</h2>

      {/* Current Metrics Summary */}
      {currentMetrics && (
        <div className="metrics-summary">
          <div className="metric-card">
            <span className="metric-label">Dividend Yield</span>
            <span className="metric-value">
              {currentMetrics.dividendYield ? `${currentMetrics.dividendYield.toFixed(2)}%` : 'N/A'}
            </span>
          </div>
          <div className="metric-card">
            <span className="metric-label">Annual Dividend</span>
            <span className="metric-value">
              {currentMetrics.annualDividend ? `$${currentMetrics.annualDividend.toFixed(2)}` : 'N/A'}
            </span>
          </div>
          <div className="metric-card">
            <span className="metric-label">Payout Ratio</span>
            <span className="metric-value">
              {currentMetrics.payoutRatio ? `${currentMetrics.payoutRatio.toFixed(2)}%` : 'N/A'}
            </span>
          </div>
          <div className="metric-card">
            <span className="metric-label">Trailing EPS</span>
            <span className="metric-value">
              {currentMetrics.trailingEps ? `$${currentMetrics.trailingEps.toFixed(2)}` : 'N/A'}
            </span>
          </div>
        </div>
      )}

      {/* Chart 1: Dividend Payment History */}
      {charts.dividendHistory && charts.dividendHistory.length > 0 && (
        <div className="chart-container">
          <h3>💰 Annual Dividend History</h3>
          <p className="chart-description">
            Shows total dividends paid per year. Consistent or increasing payments indicate reliability.
          </p>
          <ResponsiveContainer width="100%" height={300}>
            <BarChart data={charts.dividendHistory}>
              <CartesianGrid strokeDasharray="3 3" />
              <XAxis dataKey="year" />
              <YAxis />
              <Tooltip
                formatter={(value) => `$${value.toFixed(2)}`}
                labelFormatter={(year) => `Year ${year}`}
              />
              <Legend />
              <Bar dataKey="amount" fill="#4CAF50" name="Dividend per Share ($)" />
            </BarChart>
          </ResponsiveContainer>
        </div>
      )}

      {/* Chart 2: Dividend Growth Trend */}
      {charts.dividendGrowth && charts.dividendGrowth.length > 0 && (
        <div className="chart-container">
          <h3>📈 Dividend Growth Rate (Year-over-Year)</h3>
          <p className="chart-description">
            Tracks annual percentage growth in dividends. Positive growth shows management confidence.
          </p>
          <ResponsiveContainer width="100%" height={300}>
            <LineChart data={charts.dividendGrowth}>
              <CartesianGrid strokeDasharray="3 3" />
              <XAxis dataKey="year" />
              <YAxis />
              <Tooltip
                formatter={(value, name) => {
                  if (name === 'Growth Rate') return `${value.toFixed(2)}%`;
                  return `$${value.toFixed(2)}`;
                }}
              />
              <Legend />
              <Line
                type="monotone"
                dataKey="growthRate"
                stroke="#2196F3"
                name="Growth Rate (%)"
                strokeWidth={2}
              />
              <Line
                type="monotone"
                dataKey="amount"
                stroke="#4CAF50"
                name="Dividend Amount ($)"
                strokeWidth={2}
                dot={{ fill: '#4CAF50' }}
              />
            </LineChart>
          </ResponsiveContainer>
        </div>
      )}

      {/* Chart 3: Payout Ratio Trend */}
      {charts.payoutRatioTrend && charts.payoutRatioTrend.length > 0 && (
        <div className="chart-container">
          <h3>⚖️ Payout Ratio Trend</h3>
          <p className="chart-description">
            Shows percentage of earnings paid as dividends. 40-60% is typically sustainable; &gt;80% may be risky.
          </p>
          <ResponsiveContainer width="100%" height={300}>
            <AreaChart data={charts.payoutRatioTrend}>
              <CartesianGrid strokeDasharray="3 3" />
              <XAxis dataKey="year" />
              <YAxis />
              <Tooltip
                formatter={(value, name) => {
                  if (name === 'Payout Ratio') return `${value.toFixed(2)}%`;
                  return `$${value.toFixed(2)}`;
                }}
              />
              <Legend />
              <Area
                type="monotone"
                dataKey="payoutRatio"
                stroke="#FF9800"
                fill="#FFE0B2"
                name="Payout Ratio (%)"
              />
            </AreaChart>
          </ResponsiveContainer>
        </div>
      )}

      {/* Chart 4: EPS vs Dividends */}
      {charts.epsVsDividends && charts.epsVsDividends.length > 0 && (
        <div className="chart-container">
          <h3>💼 Earnings vs Dividends</h3>
          <p className="chart-description">
            Compares earnings per share (EPS) with dividends. Dividend should stay below EPS for sustainability.
          </p>
          <ResponsiveContainer width="100%" height={300}>
            <LineChart data={charts.epsVsDividends}>
              <CartesianGrid strokeDasharray="3 3" />
              <XAxis dataKey="year" />
              <YAxis />
              <Tooltip formatter={(value) => `$${value ? value.toFixed(2) : '0.00'}`} />
              <Legend />
              <Line
                type="monotone"
                dataKey="eps"
                stroke="#9C27B0"
                name="Earnings per Share ($)"
                strokeWidth={2}
                dot={{ fill: '#9C27B0' }}
              />
              <Line
                type="monotone"
                dataKey="dividend"
                stroke="#4CAF50"
                name="Dividend per Share ($)"
                strokeWidth={2}
                dot={{ fill: '#4CAF50' }}
              />
            </LineChart>
          </ResponsiveContainer>
        </div>
      )}

      {/* Chart 5: Performance vs S&P 500 */}
      {performanceData && (
        <div className="chart-container">
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1rem' }}>
            <div>
              <h3>📈 Performance vs S&P 500</h3>
              <p className="chart-description">
                Compares total return performance against the S&P 500 benchmark.
              </p>
            </div>
            <div className="period-selector" style={{ display: 'flex', gap: '0.5rem' }}>
              <button
                className={perfPeriod === 1 ? 'active' : ''}
                onClick={() => setPerfPeriod(1)}
                style={{
                  padding: '0.5rem 1rem',
                  border: '1px solid #ccc',
                  borderRadius: '4px',
                  background: perfPeriod === 1 ? '#3b82f6' : 'white',
                  color: perfPeriod === 1 ? 'white' : '#333',
                  cursor: 'pointer'
                }}
              >
                1 Year
              </button>
              <button
                className={perfPeriod === 3 ? 'active' : ''}
                onClick={() => setPerfPeriod(3)}
                style={{
                  padding: '0.5rem 1rem',
                  border: '1px solid #ccc',
                  borderRadius: '4px',
                  background: perfPeriod === 3 ? '#3b82f6' : 'white',
                  color: perfPeriod === 3 ? 'white' : '#333',
                  cursor: 'pointer'
                }}
              >
                3 Years
              </button>
              <button
                className={perfPeriod === 5 ? 'active' : ''}
                onClick={() => setPerfPeriod(5)}
                style={{
                  padding: '0.5rem 1rem',
                  border: '1px solid #ccc',
                  borderRadius: '4px',
                  background: perfPeriod === 5 ? '#3b82f6' : 'white',
                  color: perfPeriod === 5 ? 'white' : '#333',
                  cursor: 'pointer'
                }}
              >
                5 Years
              </button>
            </div>
          </div>

          {/* Performance Summary Cards */}
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '1rem', marginBottom: '1.5rem' }}>
            <div style={{ padding: '1rem', background: '#f0f9ff', borderRadius: '8px', border: '1px solid #bfdbfe' }}>
              <div style={{ fontSize: '0.85rem', color: '#64748b', marginBottom: '0.5rem' }}>Stock Return</div>
              <div style={{ fontSize: '1.5rem', fontWeight: 'bold', color: performanceData.stock.totalReturn >= 0 ? '#10b981' : '#ef4444' }}>
                {performanceData.stock.totalReturn.toFixed(2)}%
              </div>
            </div>
            <div style={{ padding: '1rem', background: '#f9fafb', borderRadius: '8px', border: '1px solid #e5e7eb' }}>
              <div style={{ fontSize: '0.85rem', color: '#64748b', marginBottom: '0.5rem' }}>S&P 500 Return</div>
              <div style={{ fontSize: '1.5rem', fontWeight: 'bold', color: performanceData.benchmark.totalReturn >= 0 ? '#10b981' : '#ef4444' }}>
                {performanceData.benchmark.totalReturn.toFixed(2)}%
              </div>
            </div>
            <div style={{ padding: '1rem', background: performanceData.stock.vsSpX >= 0 ? '#f0fdf4' : '#fef2f2', borderRadius: '8px', border: `1px solid ${performanceData.stock.vsSpX >= 0 ? '#bbf7d0' : '#fecaca'}` }}>
              <div style={{ fontSize: '0.85rem', color: '#64748b', marginBottom: '0.5rem' }}>vs S&P 500</div>
              <div style={{ fontSize: '1.5rem', fontWeight: 'bold', color: performanceData.stock.vsSpX >= 0 ? '#10b981' : '#ef4444' }}>
                {performanceData.stock.vsSpX >= 0 ? '+' : ''}{performanceData.stock.vsSpX.toFixed(2)}%
              </div>
            </div>
            <div style={{ padding: '1rem', background: '#faf5ff', borderRadius: '8px', border: '1px solid #e9d5ff' }}>
              <div style={{ fontSize: '0.85rem', color: '#64748b', marginBottom: '0.5rem' }}>Beta</div>
              <div style={{ fontSize: '1.5rem', fontWeight: 'bold', color: '#8b5cf6' }}>
                {performanceData.stock.beta ? performanceData.stock.beta.toFixed(2) : 'N/A'}
              </div>
              <div style={{ fontSize: '0.75rem', color: '#64748b', marginTop: '0.25rem' }}>
                {performanceData.stock.beta > 1 ? 'More volatile' : 'Less volatile'}
              </div>
            </div>
            <div style={{ padding: '1rem', background: '#ecfdf5', borderRadius: '8px', border: '1px solid #a7f3d0' }}>
              <div style={{ fontSize: '0.85rem', color: '#64748b', marginBottom: '0.5rem' }}>Alpha</div>
              <div style={{ fontSize: '1.5rem', fontWeight: 'bold', color: performanceData.stock.alpha >= 0 ? '#10b981' : '#ef4444' }}>
                {performanceData.stock.alpha ? performanceData.stock.alpha.toFixed(2) : 'N/A'}%
              </div>
              <div style={{ fontSize: '0.75rem', color: '#64748b', marginTop: '0.25rem' }}>Excess return</div>
            </div>
            <div style={{ padding: '1rem', background: '#fffbeb', borderRadius: '8px', border: '1px solid #fde68a' }}>
              <div style={{ fontSize: '0.85rem', color: '#64748b', marginBottom: '0.5rem' }}>Sharpe Ratio</div>
              <div style={{ fontSize: '1.5rem', fontWeight: 'bold', color: '#f59e0b' }}>
                {performanceData.stock.sharpeRatio.toFixed(2)}
              </div>
              <div style={{ fontSize: '0.75rem', color: '#64748b', marginTop: '0.25rem' }}>Risk-adjusted</div>
            </div>
          </div>

          {/* Bar Chart Comparison */}
          <ResponsiveContainer width="100%" height={300}>
            <BarChart data={[
              {
                name: symbol,
                'Stock Return': performanceData.stock.totalReturn,
                'S&P 500': performanceData.benchmark.totalReturn
              }
            ]}>
              <CartesianGrid strokeDasharray="3 3" />
              <XAxis dataKey="name" />
              <YAxis label={{ value: 'Total Return (%)', angle: -90, position: 'insideLeft' }} />
              <Tooltip formatter={(value) => `${value.toFixed(2)}%`} />
              <Legend />
              <Bar dataKey="Stock Return" fill="#3b82f6" />
              <Bar dataKey="S&P 500" fill="#6b7280" />
            </BarChart>
          </ResponsiveContainer>

          <div style={{ marginTop: '1rem', padding: '1rem', background: '#f9fafb', borderRadius: '6px', fontSize: '0.9rem', color: '#64748b' }}>
            <p style={{ margin: '0.5rem 0' }}>
              <strong>Beta:</strong> Measures volatility relative to the market. Beta &gt; 1 = more volatile, Beta &lt; 1 = less volatile.
            </p>
            <p style={{ margin: '0.5rem 0' }}>
              <strong>Alpha:</strong> Excess return over what's expected given the stock's beta. Positive = outperformance.
            </p>
            <p style={{ margin: '0.5rem 0' }}>
              <strong>Sharpe Ratio:</strong> Risk-adjusted return. Higher is better. &gt;1.0 is good, &gt;2.0 is excellent.
            </p>
          </div>
        </div>
      )}

      {/* No data message */}
      {(!charts.dividendHistory || charts.dividendHistory.length === 0) &&
       (!charts.dividendGrowth || charts.dividendGrowth.length === 0) && (
        <div className="no-charts">
          <p>No historical dividend data available for charting.</p>
          <p>This stock may be new or may not pay dividends regularly.</p>
        </div>
      )}
    </div>
  );
}

export default DividendCharts;
