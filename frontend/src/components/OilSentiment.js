import React, { useState, useEffect, useCallback } from 'react';
import { oilAPI } from '../services/api';

const DIRECTION_STYLE = {
  'BUY':            { color: '#22c55e', bg: '#052e16', border: '#16a34a', emoji: '🟢' },
  'SELL':           { color: '#ef4444', bg: '#2d0000', border: '#b91c1c', emoji: '🔴' },
  'NEUTRAL / WAIT': { color: '#eab308', bg: '#1c1400', border: '#a16207', emoji: '🟡' },
};

function ForecastBar({ label, low, high, mean_pct, prob_up, current }) {
  const probDown = 100 - prob_up;
  const upColor  = prob_up >= 50 ? '#22c55e' : '#ef4444';
  const chg      = mean_pct >= 0 ? `+${mean_pct}%` : `${mean_pct}%`;
  return (
    <div style={{ marginBottom: 14 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 4 }}>
        <span style={{ color: '#94a3b8', fontSize: 13, fontWeight: 600 }}>{label}</span>
        <span style={{ color: '#64748b', fontSize: 12 }}>
          Avg {chg} &nbsp;|&nbsp;
          <span style={{ color: upColor }}>{prob_up}% chance up</span>
          <span style={{ color: '#64748b' }}> / {probDown}% down</span>
        </span>
      </div>
      <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
        <span style={{ color: '#ef4444', fontWeight: 700, fontSize: 14, minWidth: 60 }}>${low}</span>
        <div style={{ flex: 1, height: 8, background: '#1e293b', borderRadius: 4, position: 'relative' }}>
          {/* Probability fill */}
          <div style={{
            position: 'absolute', left: 0, top: 0, height: '100%',
            width: `${prob_up}%`,
            background: `linear-gradient(90deg, #22c55e44, #22c55e)`,
            borderRadius: 4,
          }} />
        </div>
        <span style={{ color: '#22c55e', fontWeight: 700, fontSize: 14, minWidth: 60, textAlign: 'right' }}>${high}</span>
      </div>
      <div style={{ color: '#475569', fontSize: 11, marginTop: 2, textAlign: 'center' }}>
        Expected range (80% probability band)
      </div>
    </div>
  );
}

function ForecastCard({ forecast }) {
  if (!forecast) return null;
  const probUp   = forecast.prob_up_pct;
  const probDown = forecast.prob_down_pct;
  const mrChg    = forecast.mean_reversion_chg_pct;
  const upColor  = probUp >= 50 ? '#22c55e' : '#ef4444';
  const downColor= probDown > probUp ? '#ef4444' : '#64748b';

  return (
    <div style={{
      background: '#0f172a',
      border: '1px solid #334155',
      borderRadius: 10,
      padding: 18,
      marginTop: 16,
    }}>
      <h4 style={{ margin: '0 0 14px', color: '#94a3b8', fontSize: 13, textTransform: 'uppercase', letterSpacing: 1 }}>
        Price Range Forecast
      </h4>

      {/* Direction probability */}
      <div style={{
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        marginBottom: 16,
        padding: '10px 14px',
        background: '#1e293b',
        borderRadius: 8,
      }}>
        <div style={{ textAlign: 'center' }}>
          <div style={{ color: upColor, fontSize: 22, fontWeight: 700 }}>{probUp}%</div>
          <div style={{ color: '#64748b', fontSize: 12 }}>Probability UP</div>
        </div>
        <div style={{ color: '#334155', fontSize: 24 }}>vs</div>
        <div style={{ textAlign: 'center' }}>
          <div style={{ color: downColor, fontSize: 22, fontWeight: 700 }}>{probDown}%</div>
          <div style={{ color: '#64748b', fontSize: 12 }}>Probability DOWN</div>
        </div>
        <div style={{ textAlign: 'center', borderLeft: '1px solid #334155', paddingLeft: 16 }}>
          <div style={{ color: mrChg < 0 ? '#ef4444' : '#22c55e', fontSize: 16, fontWeight: 700 }}>
            ${forecast.mean_reversion_target}
          </div>
          <div style={{ color: '#64748b', fontSize: 12 }}>Mean revert target</div>
          <div style={{ color: mrChg < 0 ? '#ef4444' : '#22c55e', fontSize: 12 }}>
            ({mrChg >= 0 ? '+' : ''}{mrChg}%)
          </div>
        </div>
      </div>

      <ForecastBar label="Next 1 Day"  {...forecast.forecast_1d} current={forecast.current_price} />
      <ForecastBar label="Next 3 Days" {...forecast.forecast_3d} current={forecast.current_price} />
      <ForecastBar label="Next 5 Days" {...forecast.forecast_5d} current={forecast.current_price} />

      <div style={{ color: '#475569', fontSize: 11, marginTop: 8 }}>
        {forecast.note}
      </div>
    </div>
  );
}

function SignalCard({ name, tech, signal }) {
  if (!tech || !signal) return null;
  const style = DIRECTION_STYLE[signal.direction] || DIRECTION_STYLE['NEUTRAL / WAIT'];
  const dayArrow = tech.day_chg_pct >= 0 ? '▲' : '▼';
  const dayColor = tech.day_chg_pct >= 0 ? '#22c55e' : '#ef4444';

  return (
    <div style={{
      background: '#1a1a2e',
      border: `1px solid ${style.border}`,
      borderRadius: 12,
      padding: '24px',
      flex: 1,
      minWidth: 320,
    }}>
      {/* Header */}
      <div style={{ marginBottom: 16 }}>
        <h2 style={{ margin: 0, color: '#e2e8f0', fontSize: 20 }}>{name}</h2>
        <p style={{ margin: '4px 0 0', color: '#94a3b8', fontSize: 13 }}>{tech.symbol}</p>
      </div>

      {/* Price */}
      <div style={{ marginBottom: 20 }}>
        <span style={{ fontSize: 36, fontWeight: 700, color: '#f1f5f9' }}>
          {tech.price != null ? `$${tech.price.toFixed(2)}` : 'N/A'}
        </span>
        <span style={{ marginLeft: 10, color: dayColor, fontWeight: 600 }}>
          {dayArrow} {tech.day_chg_pct != null ? `${Math.abs(tech.day_chg_pct)}%` : 'N/A'} today
        </span>
      </div>

      {/* Signal badge */}
      <div style={{
        background: style.bg,
        border: `1px solid ${style.border}`,
        borderRadius: 8,
        padding: '12px 16px',
        marginBottom: 20,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
      }}>
        <span style={{ fontSize: 18, fontWeight: 700, color: style.color }}>
          {style.emoji} {signal.direction}
        </span>
        <div style={{ textAlign: 'right' }}>
          <div style={{ color: '#94a3b8', fontSize: 12 }}>Score / Confidence</div>
          <div style={{ color: style.color, fontWeight: 700 }}>
            {signal.score > 0 ? '+' : ''}{signal.score} / {signal.confidence}%
          </div>
        </div>
      </div>

      {/* Technicals grid */}
      <div style={{
        display: 'grid',
        gridTemplateColumns: '1fr 1fr 1fr',
        gap: 8,
        marginBottom: 20,
      }}>
        {[
          { label: 'RSI', value: tech.rsi },
          { label: 'ATR', value: tech.atr ? `$${tech.atr.toFixed(2)}` : '—' },
          { label: 'ATR%', value: tech.atr_pct ? `${tech.atr_pct}%` : '—' },
          { label: 'Trend', value: tech.trend },
          { label: '1W', value: tech.week_chg_pct != null ? `${tech.week_chg_pct > 0 ? '+' : ''}${tech.week_chg_pct}%` : '—' },
          { label: '1M', value: tech.month_chg_pct != null ? `${tech.month_chg_pct > 0 ? '+' : ''}${tech.month_chg_pct}%` : '—' },
          { label: 'BB Low', value: tech.bb_low ? `$${tech.bb_low}` : '—' },
          { label: 'BB Mid', value: tech.bb_mid ? `$${tech.bb_mid}` : '—' },
          { label: 'BB High', value: tech.bb_high ? `$${tech.bb_high}` : '—' },
          { label: '52w Low', value: tech.low_52w ? `$${tech.low_52w}` : '—' },
          { label: '52w High', value: tech.high_52w ? `$${tech.high_52w}` : '—' },
          { label: 'Vol Ratio', value: tech.vol_ratio ? `${tech.vol_ratio}x` : '—' },
        ].map(({ label, value }) => (
          <div key={label} style={{ background: '#0f172a', borderRadius: 6, padding: '8px 10px' }}>
            <div style={{ color: '#64748b', fontSize: 11, marginBottom: 2 }}>{label}</div>
            <div style={{ color: '#e2e8f0', fontWeight: 600, fontSize: 13 }}>{value ?? '—'}</div>
          </div>
        ))}
      </div>

      {/* Order levels */}
      <div style={{ marginBottom: 20 }}>
        <h4 style={{ color: '#94a3b8', margin: '0 0 10px', fontSize: 13, textTransform: 'uppercase', letterSpacing: 1 }}>
          Suggested Order Levels (ATR = {tech.atr != null ? `$${tech.atr.toFixed(2)}` : 'N/A'})
        </h4>
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
          {/* BUY box */}
          <div style={{ background: '#052e16', border: '1px solid #16a34a', borderRadius: 8, padding: 12 }}>
            <div style={{ color: '#22c55e', fontWeight: 700, marginBottom: 8, fontSize: 13 }}>BUY ENTRIES</div>
            {[
              ['Aggressive', signal.orders?.buy?.aggressive],
              ['Moderate',   signal.orders?.buy?.moderate],
              ['Patient',    signal.orders?.buy?.patient],
              ['Stop Loss',  signal.orders?.buy?.stop_loss],
              ['TP1',        signal.orders?.buy?.tp1],
              ['TP2',        signal.orders?.buy?.tp2],
            ].map(([label, val]) => (
              <div key={label} style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 4, fontSize: 13 }}>
                <span style={{ color: '#86efac' }}>{label}</span>
                <span style={{ color: '#f0fdf4', fontWeight: 600 }}>${val?.toFixed(2) ?? '—'}</span>
              </div>
            ))}
            <div style={{ color: '#4ade80', fontSize: 11, marginTop: 4 }}>
              R:R  TP1 {signal.orders?.buy?.rr_tp1}  |  TP2 {signal.orders?.buy?.rr_tp2}
            </div>
          </div>

          {/* SELL box */}
          <div style={{ background: '#2d0000', border: '1px solid #b91c1c', borderRadius: 8, padding: 12 }}>
            <div style={{ color: '#ef4444', fontWeight: 700, marginBottom: 8, fontSize: 13 }}>SELL ENTRIES</div>
            {[
              ['Aggressive', signal.orders?.sell?.aggressive],
              ['Moderate',   signal.orders?.sell?.moderate],
              ['Patient',    signal.orders?.sell?.patient],
              ['Stop Loss',  signal.orders?.sell?.stop_loss],
              ['TP1',        signal.orders?.sell?.tp1],
              ['TP2',        signal.orders?.sell?.tp2],
            ].map(([label, val]) => (
              <div key={label} style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 4, fontSize: 13 }}>
                <span style={{ color: '#fca5a5' }}>{label}</span>
                <span style={{ color: '#fff1f2', fontWeight: 600 }}>${val?.toFixed(2) ?? '—'}</span>
              </div>
            ))}
            <div style={{ color: '#f87171', fontSize: 11, marginTop: 4 }}>
              R:R  TP1 {signal.orders?.sell?.rr_tp1}  |  TP2 {signal.orders?.sell?.rr_tp2}
            </div>
          </div>
        </div>
      </div>

      {/* Reasons */}
      <div>
        <h4 style={{ color: '#94a3b8', margin: '0 0 8px', fontSize: 13, textTransform: 'uppercase', letterSpacing: 1 }}>
          Signal Breakdown
        </h4>
        <ul style={{ margin: 0, padding: 0, listStyle: 'none' }}>
          {(signal.reasons || []).map((r, i) => (
            <li key={i} style={{ color: '#cbd5e1', fontSize: 13, padding: '3px 0', borderBottom: '1px solid #1e293b' }}>
              • {r}
            </li>
          ))}
        </ul>
      </div>

      {/* Forecast */}
      <ForecastCard forecast={tech?.forecast} />
    </div>
  );
}

function HeadlineRow({ h }) {
  const combined = h.combined || 0;
  const bias  = combined > 0.05 ? '▲' : combined < -0.05 ? '▼' : '→';
  const color = combined > 0.05 ? '#22c55e' : combined < -0.05 ? '#ef4444' : '#94a3b8';
  return (
    <div style={{
      display: 'flex',
      gap: 12,
      padding: '10px 0',
      borderBottom: '1px solid #1e293b',
      alignItems: 'flex-start',
    }}>
      <span style={{ color, fontWeight: 700, fontSize: 16, minWidth: 14 }}>{bias}</span>
      <div>
        <div style={{ color: '#e2e8f0', fontSize: 14 }}>{h.title}</div>
        <div style={{ color: '#64748b', fontSize: 12, marginTop: 2 }}>
          {h.source}
          {h.matches?.length > 0 && (
            <span style={{ marginLeft: 8, color: '#475569' }}>
              keywords: {h.matches.slice(0, 3).join(', ')}
            </span>
          )}
        </div>
      </div>
      <span style={{ marginLeft: 'auto', color, fontWeight: 700, fontSize: 13, whiteSpace: 'nowrap' }}>
        {combined >= 0 ? '+' : ''}{combined.toFixed(3)}
      </span>
    </div>
  );
}

function OilSentiment() {
  const [data,       setData]       = useState(null);
  const [loading,    setLoading]    = useState(false);
  const [error,      setError]      = useState(null);
  const [lastRun,    setLastRun]    = useState(null);
  const [smsSending, setSmsSending] = useState(false);
  const [smsStatus,  setSmsStatus]  = useState(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await oilAPI.getSentiment();
      setData(res.data);
      setLastRun(new Date());
    } catch (err) {
      setError(err.response?.data?.error || err.message || 'Failed to load oil sentiment');
    } finally {
      setLoading(false);
    }
  }, []);

  const sendSms = async () => {
    setSmsSending(true);
    setSmsStatus(null);
    try {
      await oilAPI.sendSms();
      setSmsStatus({ ok: true, msg: 'SMS sent! Check your phone.' });
    } catch (err) {
      setSmsStatus({ ok: false, msg: err.response?.data?.error || 'Failed to send SMS' });
    } finally {
      setSmsSending(false);
    }
  };

  useEffect(() => { load(); }, [load]);

  const instruments = data?.instruments || {};
  const headlines   = data?.top_headlines || [];
  const newsDir     = data?.news_direction;
  const newsDirColor = newsDir === 'Bullish' ? '#22c55e' : newsDir === 'Bearish' ? '#ef4444' : '#eab308';

  return (
    <div style={{ padding: '24px', background: '#0f172a', minHeight: '100vh', color: '#e2e8f0' }}>

      {/* Header */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 24 }}>
        <div>
          <h1 style={{ margin: 0, fontSize: 24, color: '#f1f5f9' }}>Crude Oil Signal Analyzer</h1>
          <p style={{ margin: '4px 0 0', color: '#64748b', fontSize: 14 }}>
            WTI (CL=F) and Brent (BZ=F) — news sentiment + technical indicators
          </p>
        </div>
        <div style={{ display: 'flex', gap: 12, alignItems: 'center' }}>
          {lastRun && (
            <span style={{ color: '#475569', fontSize: 13 }}>
              Updated {lastRun.toLocaleTimeString()} · cached 30min
            </span>
          )}
          <button
            onClick={load}
            disabled={loading}
            style={{
              background: '#1d4ed8', color: '#fff', border: 'none',
              borderRadius: 8, padding: '10px 20px',
              cursor: loading ? 'not-allowed' : 'pointer',
              fontWeight: 600, opacity: loading ? 0.7 : 1,
            }}
          >
            {loading ? 'Analyzing...' : 'Refresh Analysis'}
          </button>
          <button
            onClick={sendSms}
            disabled={smsSending || !data}
            style={{
              background: '#7c3aed', color: '#fff', border: 'none',
              borderRadius: 8, padding: '10px 20px',
              cursor: (smsSending || !data) ? 'not-allowed' : 'pointer',
              fontWeight: 600, opacity: (smsSending || !data) ? 0.6 : 1,
            }}
          >
            {smsSending ? 'Sending...' : '📱 Send SMS'}
          </button>
        </div>
      </div>

      {/* SMS status */}
      {smsStatus && (
        <div style={{
          background: smsStatus.ok ? '#052e16' : '#2d0000',
          border: `1px solid ${smsStatus.ok ? '#16a34a' : '#b91c1c'}`,
          borderRadius: 8, padding: '10px 16px', marginBottom: 16,
          color: smsStatus.ok ? '#86efac' : '#fca5a5', fontSize: 14,
        }}>
          {smsStatus.ok ? '✅' : '❌'} {smsStatus.msg}
        </div>
      )}

      {/* News direction banner */}
      {data && (
        <div style={{
          background: '#1e293b',
          borderRadius: 10,
          padding: '14px 20px',
          marginBottom: 24,
          display: 'flex',
          gap: 32,
          alignItems: 'center',
        }}>
          <div>
            <span style={{ color: '#64748b', fontSize: 13 }}>Overall News Sentiment</span>
            <span style={{
              marginLeft: 12,
              color: newsDirColor,
              fontWeight: 700,
              fontSize: 16,
            }}>
              {newsDir}  ({data.news_sentiment >= 0 ? '+' : ''}{data.news_sentiment?.toFixed(3)})
            </span>
          </div>
          <div style={{ color: '#64748b', fontSize: 13 }}>
            {headlines.length} headlines scored from news + RSS feeds
          </div>
          {data.generated_at && (
            <div style={{ marginLeft: 'auto', color: '#475569', fontSize: 12 }}>
              Generated: {new Date(data.generated_at).toLocaleString()}
            </div>
          )}
        </div>
      )}

      {error && (
        <div style={{
          background: '#2d0000',
          border: '1px solid #b91c1c',
          borderRadius: 8,
          padding: 16,
          marginBottom: 24,
          color: '#fca5a5',
        }}>
          {error}
          <br /><span style={{ fontSize: 12, color: '#94a3b8' }}>
            Make sure the backend is running and oil_sentiment.py dependencies are installed (pip install yfinance vaderSentiment)
          </span>
        </div>
      )}

      {loading && !data && (
        <div style={{ textAlign: 'center', padding: 60, color: '#64748b' }}>
          <div style={{ fontSize: 40, marginBottom: 16 }}>⛽</div>
          <p style={{ fontSize: 16 }}>Fetching news, scoring sentiment, analyzing technicals...</p>
          <p style={{ fontSize: 13 }}>This takes 15–30 seconds on first load.</p>
        </div>
      )}

      {/* Signal cards */}
      {!loading && data && (
        <>
          <div style={{ display: 'flex', gap: 20, marginBottom: 32, flexWrap: 'wrap' }}>
            {Object.entries(instruments).map(([name, inst]) => (
              <SignalCard
                key={name}
                name={inst.info?.name || name}
                tech={inst.tech}
                signal={inst.signal}
              />
            ))}
          </div>

          {/* Top headlines */}
          <div style={{
            background: '#1a1a2e',
            border: '1px solid #1e293b',
            borderRadius: 12,
            padding: 24,
          }}>
            <h3 style={{ margin: '0 0 16px', color: '#94a3b8', fontSize: 15, textTransform: 'uppercase', letterSpacing: 1 }}>
              Top Headlines Driving Signal
            </h3>
            {headlines.slice(0, 10).map((h, i) => (
              <HeadlineRow key={i} h={h} />
            ))}
            {headlines.length === 0 && (
              <p style={{ color: '#475569', margin: 0 }}>No headlines available.</p>
            )}
          </div>

          {/* Disclaimer */}
          <div style={{
            marginTop: 20,
            padding: '12px 16px',
            background: '#1e293b',
            borderRadius: 8,
            color: '#64748b',
            fontSize: 12,
          }}>
            ⚠ This tool provides analytical signals based on news sentiment and technical indicators.
            It is not financial advice. Oil prices are highly sensitive to geopolitical events that cannot be
            predicted. Always use a stop-loss and manage position size relative to your account.
          </div>
        </>
      )}
    </div>
  );
}

export default OilSentiment;
