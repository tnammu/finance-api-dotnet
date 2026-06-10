import React, { useState } from 'react';
import axios from 'axios';

const API_BASE = 'http://localhost:5000/api';

export default function StockAlerts() {
  const [picks, setPicks] = useState(null);
  const [loading, setLoading] = useState(false);
  const [smsLoading, setSmsLoading] = useState(false);
  const [smsResult, setSmsResult] = useState(null);
  const [error, setError] = useState(null);

  const fetchTopPicks = async () => {
    setLoading(true);
    setError(null);
    setPicks(null);
    setSmsResult(null);
    try {
      const res = await axios.get(`${API_BASE}/alerts/top-picks`, { timeout: 600000 }); // 10 min max
      setPicks(res.data);
    } catch (err) {
      setError(err.response?.data?.error || 'Failed to fetch top picks. Is the API running?');
    } finally {
      setLoading(false);
    }
  };

  const sendSms = async () => {
    setSmsLoading(true);
    setSmsResult(null);
    try {
      const res = await axios.post(`${API_BASE}/alerts/send-sms`);
      setSmsResult({ success: true, message: `Notification sent! ${res.data.smsStatus}` });
    } catch (err) {
      setSmsResult({ success: false, message: err.response?.data?.error || 'Failed to send SMS' });
    } finally {
      setSmsLoading(false);
    }
  };

  const rsiColor = (rsi) => {
    if (rsi == null) return '#888';
    if (rsi < 30) return '#22c55e';
    if (rsi < 40) return '#86efac';
    if (rsi > 70) return '#ef4444';
    return '#94a3b8';
  };

  const scoreColor = (score) => {
    if (score >= 70) return '#22c55e';
    if (score >= 50) return '#f59e0b';
    return '#ef4444';
  };

  const returnColor = (val) => {
    if (val == null) return '#888';
    return val >= 0 ? '#22c55e' : '#ef4444';
  };

  return (
    <div style={styles.container}>
      <div style={styles.header}>
        <h2 style={styles.title}>Top 10 Canadian Stock Picks</h2>
        <p style={styles.subtitle}>RSI Oversold · Swing Trade · Seasonal Pattern</p>
      </div>

      <div style={styles.buttonRow}>
        <button onClick={fetchTopPicks} disabled={loading} style={styles.analyzeBtn}>
          {loading ? 'Analyzing...' : 'Analyze Stocks'}
        </button>
        <button
          onClick={sendSms}
          disabled={smsLoading || !picks}
          style={{ ...styles.smsBtn, opacity: !picks ? 0.5 : 1 }}
        >
          {smsLoading ? 'Sending...' : 'Send Notification'}
        </button>
      </div>

      {smsResult && (
        <div style={{ ...styles.smsStatus, background: smsResult.success ? '#14532d' : '#7f1d1d' }}>
          {smsResult.success ? '✓ ' : '✗ '}{smsResult.message}
        </div>
      )}

      {error && <div style={styles.errorBox}>{error}</div>}

      {picks && (
        <>
          <p style={styles.meta}>
            Analyzed <strong>{picks.totalAnalyzed}</strong> Canadian stocks ·{' '}
            {new Date(picks.generatedAt).toLocaleString()}
          </p>

          <div style={styles.grid}>
            {/* RSI Section */}
            <div style={styles.card}>
              <div style={styles.cardHeader}>
                <span style={styles.cardIcon}>📉</span>
                <h3 style={styles.cardTitle}>RSI Oversold</h3>
              </div>
              <p style={styles.cardDesc}>Genuinely oversold (RSI &lt; 35) · volume rising · near support</p>
              {picks.strategies.rsi?.map((s, i) => (
                <div key={s.symbol} style={styles.row}>
                  <span style={styles.rank}>#{i + 1}</span>
                  <div style={styles.stockInfo}>
                    <span style={styles.symbol}>{s.symbol}</span>
                    <span style={styles.price}>${s.price?.toFixed(2)}</span>
                  </div>
                  <div style={styles.badge(rsiColor(s.rsi))}>
                    RSI {s.rsi?.toFixed(0) ?? 'N/A'}
                  </div>
                  <span style={{ ...styles.signal, color: s.trend === 'Uptrend' ? '#4caf50' : s.trend === 'Downtrend' ? '#f44336' : '#ff9800' }}>
                    {s.trend ?? 'N/A'}
                  </span>
                  <span style={styles.meta2}>
                    Vol {s.vol_ratio?.toFixed(1)}x · +{s.pct_from_52w_low?.toFixed(0)}% from low
                  </span>
                </div>
              ))}
            </div>

            {/* Swing Trade Section */}
            <div style={styles.card}>
              <div style={styles.cardHeader}>
                <span style={styles.cardIcon}>📊</span>
                <h3 style={styles.cardTitle}>Swing Trade</h3>
              </div>
              <p style={styles.cardDesc}>Score ≥ 45 · not downtrend · RSI not overbought</p>
              {picks.strategies.swing?.map((s, i) => (
                <div key={s.symbol} style={styles.row}>
                  <span style={styles.rank}>#{i + 1}</span>
                  <div style={styles.stockInfo}>
                    <span style={styles.symbol}>{s.symbol}</span>
                    <span style={styles.price}>${s.price?.toFixed(2)}</span>
                  </div>
                  <div style={styles.badge(scoreColor(s.swing_score))}>
                    Score {s.swing_score?.toFixed(0)}
                  </div>
                  <div style={styles.badge(rsiColor(s.rsi))}>
                    RSI {s.rsi?.toFixed(0)}
                  </div>
                  <span style={{ ...styles.signal, color: s.trend === 'Uptrend' ? '#4caf50' : s.trend === 'Downtrend' ? '#f44336' : '#ff9800' }}>
                    {s.trend ?? 'N/A'}
                  </span>
                  <span style={styles.meta2}>{s.bb_position}</span>
                </div>
              ))}
            </div>

            {/* Seasonal Section */}
            <div style={styles.card}>
              <div style={styles.cardHeader}>
                <span style={styles.cardIcon}>📅</span>
                <h3 style={styles.cardTitle}>Seasonal — {picks.month}</h3>
              </div>
              <p style={styles.cardDesc}>Good to buy in {picks.month} — shows avg return &amp; best month to sell</p>
              {picks.strategies.seasonal?.map((s, i) => (
                <div key={s.symbol} style={styles.row}>
                  <span style={styles.rank}>#{i + 1}</span>
                  <div style={styles.stockInfo}>
                    <span style={styles.symbol}>{s.symbol}</span>
                    <span style={styles.price}>${s.price?.toFixed(2)}</span>
                  </div>
                  <div style={styles.badge(returnColor(s.month_avg_return))}>
                    Buy {picks.month?.slice(0, 3)}{' '}
                    {s.month_avg_return != null
                      ? `${s.month_avg_return >= 0 ? '+' : ''}${s.month_avg_return.toFixed(1)}%`
                      : 'N/A'}
                  </div>
                  {s.best_sell_month && (
                    <div style={styles.badge(returnColor(s.best_sell_month_return))}>
                      Sell {s.best_sell_month.slice(0, 3)}{' '}
                      {s.best_sell_month_return != null
                        ? `${s.best_sell_month_return >= 0 ? '+' : ''}${s.best_sell_month_return.toFixed(1)}%`
                        : ''}
                    </div>
                  )}
                  <span style={styles.meta2}>{s.seasonal_years}yr avg</span>
                </div>
              ))}
            </div>
          </div>

          <p style={styles.disclaimer}>
            ⚠ Not financial advice. Past performance does not guarantee future results.
          </p>
        </>
      )}
    </div>
  );
}

const styles = {
  container: {
    padding: '24px',
    maxWidth: '1100px',
    margin: '0 auto',
    color: '#e2e8f0',
  },
  header: { marginBottom: '20px' },
  title: { fontSize: '24px', fontWeight: '700', margin: 0, color: '#f1f5f9' },
  subtitle: { color: '#94a3b8', margin: '4px 0 0', fontSize: '14px' },
  buttonRow: { display: 'flex', gap: '12px', marginBottom: '16px' },
  analyzeBtn: {
    padding: '10px 24px',
    background: '#3b82f6',
    color: '#fff',
    border: 'none',
    borderRadius: '8px',
    fontSize: '14px',
    fontWeight: '600',
    cursor: 'pointer',
  },
  smsBtn: {
    padding: '10px 24px',
    background: '#22c55e',
    color: '#fff',
    border: 'none',
    borderRadius: '8px',
    fontSize: '14px',
    fontWeight: '600',
    cursor: 'pointer',
  },
  smsStatus: {
    padding: '10px 16px',
    borderRadius: '8px',
    marginBottom: '12px',
    fontSize: '14px',
    color: '#fff',
  },
  errorBox: {
    padding: '12px 16px',
    background: '#7f1d1d',
    borderRadius: '8px',
    color: '#fca5a5',
    fontSize: '14px',
    marginBottom: '16px',
  },
  meta: { color: '#94a3b8', fontSize: '13px', marginBottom: '20px' },
  grid: { display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(300px, 1fr))', gap: '20px' },
  card: {
    background: '#1e293b',
    borderRadius: '12px',
    padding: '20px',
    border: '1px solid #334155',
  },
  cardHeader: { display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '4px' },
  cardIcon: { fontSize: '20px' },
  cardTitle: { margin: 0, fontSize: '16px', fontWeight: '700', color: '#f1f5f9' },
  cardDesc: { color: '#64748b', fontSize: '12px', margin: '0 0 16px' },
  row: {
    display: 'flex',
    alignItems: 'center',
    gap: '8px',
    padding: '8px 0',
    borderBottom: '1px solid #1e293b',
  },
  rank: { color: '#64748b', fontSize: '12px', minWidth: '24px' },
  stockInfo: { display: 'flex', flexDirection: 'column', flex: 1 },
  symbol: { fontWeight: '700', fontSize: '14px', color: '#f1f5f9' },
  price: { fontSize: '12px', color: '#94a3b8' },
  badge: (color) => ({
    padding: '3px 8px',
    background: color + '22',
    color: color,
    borderRadius: '6px',
    fontSize: '12px',
    fontWeight: '700',
    whiteSpace: 'nowrap',
  }),
  signal: { fontSize: '12px', whiteSpace: 'nowrap' },
  meta2: { fontSize: '11px', color: '#64748b', whiteSpace: 'nowrap' },
  disclaimer: {
    color: '#475569',
    fontSize: '12px',
    marginTop: '24px',
    textAlign: 'center',
  },
};
