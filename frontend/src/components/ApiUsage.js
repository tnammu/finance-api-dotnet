import React, { useState, useEffect } from 'react';
import { dividendsAPI } from '../services/api';
import { BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer } from 'recharts';
import './ApiUsage.css';

function ApiUsage() {
  const [todayUsage, setTodayUsage] = useState(null);
  const [history, setHistory] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    loadUsageData();
  }, []);

  const loadUsageData = async () => {
    try {
      setLoading(true);
      const [todayResponse, historyResponse] = await Promise.all([
        dividendsAPI.getUsageToday(),
        dividendsAPI.getUsageHistory()
      ]);
      setTodayUsage(todayResponse.data);
      setHistory(historyResponse.data);
    } catch (err) {
      console.error('Failed to load usage data:', err);
    } finally {
      setLoading(false);
    }
  };

  if (loading) {
    return <div className="loading">Loading API usage data...</div>;
  }

  const getStatusColor = (status) => {
    return status === 'OK' ? '#27ae60' : '#e74c3c';
  };

  const getUsageColor = (percent) => {
    if (percent < 50) return '#27ae60';
    if (percent < 80) return '#f39c12';
    return '#e74c3c';
  };

  const handleExportUsage = () => {
    const API_BASE_URL = 'http://localhost:7065/api';
    window.open(`${API_BASE_URL}/dividends/export/usage/csv`, '_blank');
  };

  return (
    <div className="api-usage">
      <div className="card">
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1rem' }}>
          <div>
            <h2 style={{ marginBottom: '0.25rem' }}>API Usage Dashboard</h2>
            <p style={{ color: '#7f8c8d', margin: 0 }}>
              Track your Alpha Vantage API usage and stay within limits
            </p>
          </div>
          <button className="secondary" onClick={handleExportUsage}>
            Export CSV
          </button>
        </div>

        {todayUsage && (
          <>
            <div className="usage-overview">
              <div className="usage-circle" style={{ borderColor: getUsageColor(todayUsage.percentUsed) }}>
                <div className="usage-percent" style={{ color: getUsageColor(todayUsage.percentUsed) }}>
                  {todayUsage.percentUsed.toFixed(0)}%
                </div>
                <div className="usage-label">Used Today</div>
              </div>
              <div className="usage-details">
                <div className="usage-stat">
                  <span className="label">API Calls Used:</span>
                  <span className="value">{todayUsage.callsUsed} / {todayUsage.dailyLimit}</span>
                </div>
                <div className="usage-stat">
                  <span className="label">Remaining:</span>
                  <span className="value" style={{ color: getStatusColor(todayUsage.status) }}>
                    {todayUsage.remaining}
                  </span>
                </div>
                <div className="usage-stat">
                  <span className="label">Status:</span>
                  <span className="badge" style={{ background: getStatusColor(todayUsage.status) + '20', color: getStatusColor(todayUsage.status) }}>
                    {todayUsage.status}
                  </span>
                </div>
                <div className="usage-stat">
                  <span className="label">Stocks Analyzable:</span>
                  <span className="value">{todayUsage.canAnalyzeStocks}</span>
                </div>
              </div>
            </div>

            <div className="progress-bar">
              <div
                className="progress-fill"
                style={{
                  width: `${todayUsage.percentUsed}%`,
                  background: getUsageColor(todayUsage.percentUsed)
                }}
              ></div>
            </div>

            <div className="info-banner" style={{ marginTop: '1.5rem' }}>
              <strong>Tip:</strong> Each stock analysis uses approximately 3 API calls.
              Data is cached for 7 days to minimize API usage.
            </div>
          </>
        )}
      </div>

      {history && history.dailyUsage && history.dailyUsage.length > 0 && (
        <div className="card">
          <h3>Usage History (Last 30 Days)</h3>

          <div className="stats-grid" style={{ marginBottom: '1.5rem' }}>
            <div className="stat-card">
              <h4>Total Days</h4>
              <div className="value">{history.totalDays}</div>
            </div>
            <div className="stat-card">
              <h4>Total API Calls</h4>
              <div className="value">{history.totalApiCalls}</div>
            </div>
            <div className="stat-card">
              <h4>Average Per Day</h4>
              <div className="value">{history.averagePerDay.toFixed(1)}</div>
            </div>
            <div className="stat-card">
              <h4>Peak Usage</h4>
              <div className="value">
                {Math.max(...history.dailyUsage.map(d => d.callsUsed))}
              </div>
            </div>
          </div>

          <ResponsiveContainer width="100%" height={300}>
            <BarChart data={history.dailyUsage.slice().reverse()}>
              <CartesianGrid strokeDasharray="3 3" />
              <XAxis dataKey="date" />
              <YAxis />
              <Tooltip />
              <Legend />
              <Bar dataKey="callsUsed" fill="#3498db" name="API Calls Used" />
              <Bar dataKey="remaining" fill="#27ae60" name="Remaining" />
            </BarChart>
          </ResponsiveContainer>

          <div className="usage-table">
            <h4>Daily Breakdown</h4>
            <table>
              <thead>
                <tr>
                  <th>Date</th>
                  <th>Calls Used</th>
                  <th>Remaining</th>
                  <th>Usage %</th>
                </tr>
              </thead>
              <tbody>
                {history.dailyUsage.slice(0, 10).map((day, idx) => (
                  <tr key={idx}>
                    <td>{day.date}</td>
                    <td>{day.callsUsed}</td>
                    <td>{day.remaining}</td>
                    <td>
                      <span style={{ color: getUsageColor((day.callsUsed / day.limit) * 100) }}>
                        {((day.callsUsed / day.limit) * 100).toFixed(1)}%
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      <div className="card">
        <h3>API Usage Tips</h3>
        <ul className="tips-list">
          <li>The free Alpha Vantage API has a limit of 25 calls per day</li>
          <li>Each dividend analysis requires 3 API calls (overview, dividends, time series)</li>
          <li>Analyzed data is cached for 7 days to reduce API calls</li>
          <li>Use the "View" button on cached stocks instead of refreshing</li>
          <li>Batch analyze stocks during off-peak hours to maximize daily limit</li>
          <li>Consider upgrading to a premium API key for higher limits</li>
        </ul>
      </div>
    </div>
  );
}

export default ApiUsage;
