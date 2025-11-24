import React, { useState, useEffect } from 'react';
import axios from 'axios';
import {
  LineChart, Line, BarChart, Bar, XAxis, YAxis, CartesianGrid,
  Tooltip, Legend, ResponsiveContainer, ScatterChart, Scatter, ZAxis
} from 'recharts';
import './PerformanceDashboard.css';

const API_BASE_URL = 'http://localhost:5000/api';

function PerformanceDashboard() {
  const [portfolioData, setPortfolioData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [period, setPeriod] = useState(1);

  useEffect(() => {
    loadPortfolioPerformance();
  }, [period]);

  const loadPortfolioPerformance = async () => {
    try {
      setLoading(true);
      setError(null);

      const response = await axios.get(`${API_BASE_URL}/performance/python-portfolio?period=${period}`, {
        timeout: 60000 // 60 second timeout for Python script
      });

      // Transform Python snake_case to JavaScript camelCase
      const data = response.data;
      const transformedData = {
        benchmark: {
          totalReturn: data.benchmark.total_return,
          annualizedReturn: data.benchmark.annualized_return,
          volatility: data.benchmark.volatility,
          sharpeRatio: data.benchmark.sharpe_ratio,
          maxDrawdown: data.benchmark.max_drawdown_pct
        },
        stocks: data.stocks.map(stock => ({
          symbol: stock.symbol,
          totalReturn: stock.total_return,
          annualizedReturn: stock.annualized_return,
          volatility: stock.volatility,
          sharpeRatio: stock.sharpe_ratio,
          beta: stock.beta,
          alpha: stock.alpha,
          correlation: stock.correlation,
          maxDrawdown: stock.max_drawdown_pct,
          vsSpX: stock.vs_benchmark,
          daysAnalyzed: stock.days_analyzed
        }))
      };

      setPortfolioData(transformedData);
    } catch (err) {
      setError('Failed to load performance data: ' + (err.response?.data?.error || err.message));
      console.error('Performance error:', err);
    } finally {
      setLoading(false);
    }
  };

  if (loading) {
    return (
      <div className="performance-dashboard">
        <div className="loading">
          <div className="spinner"></div>
          <p>Loading performance data...</p>
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="performance-dashboard">
        <div className="error-message">
          <h3>⚠️ Error</h3>
          <p>{error}</p>
          <button onClick={loadPortfolioPerformance} className="retry-button">
            Retry
          </button>
        </div>
      </div>
    );
  }

  if (!portfolioData || !portfolioData.stocks || portfolioData.stocks.length === 0) {
    return (
      <div className="performance-dashboard">
        <div className="no-data">
          <h3>📊 No Performance Data</h3>
          <p>Add stocks to your portfolio to see performance comparison</p>
        </div>
      </div>
    );
  }

  const { benchmark, stocks } = portfolioData;

  // Calculate portfolio averages
  const avgReturn = stocks.reduce((sum, s) => sum + s.totalReturn, 0) / stocks.length;
  const avgBeta = stocks.reduce((sum, s) => sum + (s.beta || 0), 0) / stocks.length;
  const avgAlpha = stocks.reduce((sum, s) => sum + (s.alpha || 0), 0) / stocks.length;
  const outperformingCount = stocks.filter(s => s.vsSpX > 0).length;

  // Prepare data for charts
  const returnComparisonData = stocks.map(s => ({
    name: s.symbol,
    stockReturn: s.totalReturn,
    sp500Return: benchmark.totalReturn,
    difference: s.vsSpX
  })).sort((a, b) => b.stockReturn - a.stockReturn);

  const riskReturnData = stocks.map(s => ({
    symbol: s.symbol,
    return: s.totalReturn,
    volatility: s.volatility,
    sharpe: s.sharpeRatio
  }));

  const betaAlphaData = stocks.map(s => ({
    symbol: s.symbol,
    beta: s.beta || 0,
    alpha: s.alpha || 0
  }));

  return (
    <div className="performance-dashboard">
      <div className="dashboard-header">
        <h1>📈 Portfolio Performance vs S&P 500</h1>

        <div className="period-selector">
          <button
            className={period === 1 ? 'active' : ''}
            onClick={() => setPeriod(1)}
          >
            1 Year
          </button>
          <button
            className={period === 3 ? 'active' : ''}
            onClick={() => setPeriod(3)}
          >
            3 Years
          </button>
          <button
            className={period === 5 ? 'active' : ''}
            onClick={() => setPeriod(5)}
          >
            5 Years
          </button>
        </div>
      </div>

      {/* Summary Cards */}
      <div className="metrics-grid">
        <div className="metric-card benchmark">
          <div className="metric-label">S&P 500 Return</div>
          <div className="metric-value">{benchmark.totalReturn.toFixed(2)}%</div>
          <div className="metric-subtitle">{period} year{period > 1 ? 's' : ''}</div>
        </div>

        <div className="metric-card portfolio">
          <div className="metric-label">Portfolio Avg Return</div>
          <div className="metric-value">{avgReturn.toFixed(2)}%</div>
          <div className={`metric-subtitle ${avgReturn > benchmark.totalReturn ? 'positive' : 'negative'}`}>
            {avgReturn > benchmark.totalReturn ? '▲' : '▼'} {Math.abs(avgReturn - benchmark.totalReturn).toFixed(2)}% vs S&P 500
          </div>
        </div>

        <div className="metric-card">
          <div className="metric-label">Average Beta</div>
          <div className="metric-value">{avgBeta.toFixed(2)}</div>
          <div className="metric-subtitle">
            {avgBeta > 1 ? 'More volatile' : 'Less volatile'} than market
          </div>
        </div>

        <div className="metric-card">
          <div className="metric-label">Average Alpha</div>
          <div className="metric-value" style={{color: avgAlpha >= 0 ? '#10b981' : '#ef4444'}}>
            {avgAlpha.toFixed(2)}%
          </div>
          <div className="metric-subtitle">Excess return over expected</div>
        </div>

        <div className="metric-card">
          <div className="metric-label">Outperforming</div>
          <div className="metric-value">{outperformingCount}/{stocks.length}</div>
          <div className="metric-subtitle">
            {((outperformingCount / stocks.length) * 100).toFixed(0)}% beating S&P 500
          </div>
        </div>

        <div className="metric-card">
          <div className="metric-label">Market Volatility</div>
          <div className="metric-value">{benchmark.volatility.toFixed(2)}%</div>
          <div className="metric-subtitle">S&P 500 annualized volatility</div>
        </div>
      </div>

      {/* Stock Performance Table */}
      <div className="chart-container">
        <h2>📊 Individual Stock Performance</h2>
        <div className="performance-table">
          <table>
            <thead>
              <tr>
                <th>Symbol</th>
                <th>Name</th>
                <th>Total Return</th>
                <th>vs S&P 500</th>
                <th>Beta</th>
                <th>Alpha</th>
                <th>Sharpe Ratio</th>
                <th>Volatility</th>
              </tr>
            </thead>
            <tbody>
              {stocks.sort((a, b) => b.totalReturn - a.totalReturn).map((stock) => (
                <tr key={stock.symbol}>
                  <td className="symbol">{stock.symbol}</td>
                  <td className="name">{stock.name}</td>
                  <td className={stock.totalReturn >= 0 ? 'positive' : 'negative'}>
                    {stock.totalReturn.toFixed(2)}%
                  </td>
                  <td className={stock.vsSpX >= 0 ? 'positive' : 'negative'}>
                    {stock.vsSpX >= 0 ? '+' : ''}{stock.vsSpX.toFixed(2)}%
                  </td>
                  <td>{stock.beta ? stock.beta.toFixed(2) : 'N/A'}</td>
                  <td className={stock.alpha >= 0 ? 'positive' : 'negative'}>
                    {stock.alpha ? stock.alpha.toFixed(2) + '%' : 'N/A'}
                  </td>
                  <td>{stock.sharpeRatio.toFixed(2)}</td>
                  <td>{stock.volatility.toFixed(2)}%</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {/* Return Comparison Chart */}
      <div className="chart-container">
        <h2>📊 Returns Comparison: Stocks vs S&P 500</h2>
        <ResponsiveContainer width="100%" height={400}>
          <BarChart data={returnComparisonData}>
            <CartesianGrid strokeDasharray="3 3" />
            <XAxis dataKey="name" />
            <YAxis label={{ value: 'Return (%)', angle: -90, position: 'insideLeft' }} />
            <Tooltip formatter={(value) => `${value.toFixed(2)}%`} />
            <Legend />
            <Bar dataKey="stockReturn" fill="#3b82f6" name="Stock Return" />
            <Bar dataKey="sp500Return" fill="#6b7280" name="S&P 500 Return" />
          </BarChart>
        </ResponsiveContainer>
      </div>

      {/* Risk-Return Scatter Plot */}
      <div className="chart-container">
        <h2>💹 Risk vs Return Analysis</h2>
        <ResponsiveContainer width="100%" height={400}>
          <ScatterChart>
            <CartesianGrid strokeDasharray="3 3" />
            <XAxis
              dataKey="volatility"
              name="Volatility"
              label={{ value: 'Volatility (%)', position: 'bottom' }}
            />
            <YAxis
              dataKey="return"
              name="Return"
              label={{ value: 'Return (%)', angle: -90, position: 'insideLeft' }}
            />
            <ZAxis dataKey="sharpe" name="Sharpe Ratio" range={[50, 400]} />
            <Tooltip
              cursor={{ strokeDasharray: '3 3' }}
              content={({ active, payload }) => {
                if (active && payload && payload.length) {
                  const data = payload[0].payload;
                  return (
                    <div className="custom-tooltip">
                      <p className="label"><strong>{data.symbol}</strong></p>
                      <p>Return: {data.return.toFixed(2)}%</p>
                      <p>Volatility: {data.volatility.toFixed(2)}%</p>
                      <p>Sharpe: {data.sharpe.toFixed(2)}</p>
                    </div>
                  );
                }
                return null;
              }}
            />
            <Legend />
            <Scatter name="Stocks" data={riskReturnData} fill="#8b5cf6" />
          </ScatterChart>
        </ResponsiveContainer>
      </div>

      {/* Beta vs Alpha Chart */}
      <div className="chart-container">
        <h2>🎯 Beta vs Alpha Analysis</h2>
        <ResponsiveContainer width="100%" height={400}>
          <ScatterChart>
            <CartesianGrid strokeDasharray="3 3" />
            <XAxis
              dataKey="beta"
              name="Beta"
              label={{ value: 'Beta (Market Sensitivity)', position: 'bottom' }}
            />
            <YAxis
              dataKey="alpha"
              name="Alpha"
              label={{ value: 'Alpha (Excess Return %)', angle: -90, position: 'insideLeft' }}
            />
            <ZAxis range={[100, 300]} />
            <Tooltip
              cursor={{ strokeDasharray: '3 3' }}
              content={({ active, payload }) => {
                if (active && payload && payload.length) {
                  const data = payload[0].payload;
                  return (
                    <div className="custom-tooltip">
                      <p className="label"><strong>{data.symbol}</strong></p>
                      <p>Beta: {data.beta.toFixed(2)}</p>
                      <p>Alpha: {data.alpha.toFixed(2)}%</p>
                    </div>
                  );
                }
                return null;
              }}
            />
            <Legend />
            <Scatter name="Stocks" data={betaAlphaData} fill="#10b981" />
          </ScatterChart>
        </ResponsiveContainer>
        <div className="chart-explanation">
          <p><strong>Beta:</strong> Measures volatility relative to the market (S&P 500). Beta &gt; 1 means more volatile, Beta &lt; 1 means less volatile.</p>
          <p><strong>Alpha:</strong> Measures excess return over what's expected given the stock's beta. Positive alpha means outperformance.</p>
        </div>
      </div>

      {/* Definitions */}
      <div className="definitions-section">
        <h2>📚 Metric Definitions</h2>
        <div className="definitions-grid">
          <div className="definition">
            <h4>Total Return</h4>
            <p>Percentage gain/loss over the period</p>
          </div>
          <div className="definition">
            <h4>Beta</h4>
            <p>Sensitivity to market movements. 1.0 = moves with market, &gt;1.0 = more volatile, &lt;1.0 = less volatile</p>
          </div>
          <div className="definition">
            <h4>Alpha</h4>
            <p>Excess return above what's expected based on beta. Positive = outperformance</p>
          </div>
          <div className="definition">
            <h4>Sharpe Ratio</h4>
            <p>Risk-adjusted return. Higher is better. &gt;1.0 is good, &gt;2.0 is excellent</p>
          </div>
          <div className="definition">
            <h4>Volatility</h4>
            <p>Annualized standard deviation of returns. Higher = more price fluctuation</p>
          </div>
          <div className="definition">
            <h4>vs S&P 500</h4>
            <p>Difference between stock return and benchmark return</p>
          </div>
        </div>
      </div>
    </div>
  );
}

export default PerformanceDashboard;
