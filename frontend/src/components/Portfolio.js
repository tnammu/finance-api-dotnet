import React, { useState, useEffect } from 'react';
import { portfolioAPI } from '../services/api';

const styles = {
  container: { padding: '24px', maxWidth: '1200px', margin: '0 auto', color: '#e2e8f0' },
  title: { fontSize: '24px', fontWeight: 'bold', marginBottom: '4px', color: '#f1f5f9' },
  subtitle: { color: '#94a3b8', marginBottom: '24px', fontSize: '14px' },

  summaryGrid: { display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '16px', marginBottom: '28px' },
  summaryCard: { background: '#1e293b', borderRadius: '12px', padding: '20px', border: '1px solid #334155' },
  summaryLabel: { fontSize: '12px', color: '#94a3b8', textTransform: 'uppercase', letterSpacing: '0.05em', marginBottom: '6px' },
  summaryValue: { fontSize: '22px', fontWeight: 'bold', color: '#f1f5f9' },

  card: { background: '#1e293b', borderRadius: '12px', padding: '20px', border: '1px solid #334155', marginBottom: '20px' },
  cardTitle: { fontSize: '16px', fontWeight: '600', marginBottom: '16px', color: '#f1f5f9' },

  // Form
  formGrid: { display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(160px, 1fr))', gap: '12px', marginBottom: '16px' },
  label: { display: 'block', fontSize: '12px', color: '#94a3b8', marginBottom: '4px' },
  input: {
    width: '100%', padding: '8px 12px', background: '#0f172a', border: '1px solid #334155',
    borderRadius: '8px', color: '#e2e8f0', fontSize: '14px', boxSizing: 'border-box'
  },
  textarea: {
    width: '100%', padding: '8px 12px', background: '#0f172a', border: '1px solid #334155',
    borderRadius: '8px', color: '#e2e8f0', fontSize: '14px', resize: 'none', boxSizing: 'border-box'
  },
  btnRow: { display: 'flex', gap: '10px', flexWrap: 'wrap' },
  btnPrimary: {
    padding: '9px 20px', background: '#3b82f6', color: '#fff', border: 'none',
    borderRadius: '8px', cursor: 'pointer', fontWeight: '600', fontSize: '14px'
  },
  btnSecondary: {
    padding: '9px 20px', background: '#334155', color: '#e2e8f0', border: 'none',
    borderRadius: '8px', cursor: 'pointer', fontSize: '14px'
  },
  btnDanger: {
    padding: '6px 14px', background: 'transparent', color: '#f87171', border: '1px solid #f87171',
    borderRadius: '6px', cursor: 'pointer', fontSize: '13px'
  },
  btnEdit: {
    padding: '6px 14px', background: 'transparent', color: '#60a5fa', border: '1px solid #60a5fa',
    borderRadius: '6px', cursor: 'pointer', fontSize: '13px', marginRight: '6px'
  },

  // Table
  tableWrap: { overflowX: 'auto' },
  table: { width: '100%', borderCollapse: 'collapse', fontSize: '14px' },
  th: { padding: '10px 12px', textAlign: 'left', color: '#94a3b8', fontWeight: '600', borderBottom: '1px solid #334155', whiteSpace: 'nowrap' },
  td: { padding: '10px 12px', borderBottom: '1px solid #1e293b', whiteSpace: 'nowrap' },
  trHover: { background: '#263548' },

  gain: { color: '#4ade80', fontWeight: '600' },
  loss: { color: '#f87171', fontWeight: '600' },
  neutral: { color: '#94a3b8' },

  error: { background: '#450a0a', border: '1px solid #dc2626', borderRadius: '8px', padding: '12px 16px', color: '#fca5a5', marginBottom: '16px' },
  success: { background: '#052e16', border: '1px solid #16a34a', borderRadius: '8px', padding: '12px 16px', color: '#86efac', marginBottom: '16px' },
  empty: { textAlign: 'center', color: '#64748b', padding: '40px 0' },
};

const emptyForm = { symbol: '', shares: '', buyPrice: '', buyDate: new Date().toISOString().slice(0, 10), notes: '' };

export default function Portfolio() {
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [successMsg, setSuccessMsg] = useState(null);

  const [showForm, setShowForm] = useState(false);
  const [form, setForm] = useState(emptyForm);
  const [editId, setEditId] = useState(null);
  const [saving, setSaving] = useState(null); // 'add' | 'edit' | id (delete)

  useEffect(() => { load(); }, []);

  async function load() {
    setLoading(true);
    setError(null);
    try {
      const res = await portfolioAPI.getAll();
      setData(res.data);
    } catch (err) {
      setError(err.response?.data?.error || 'Failed to load portfolio');
    } finally {
      setLoading(false);
    }
  }

  function flash(msg) {
    setSuccessMsg(msg);
    setTimeout(() => setSuccessMsg(null), 3000);
  }

  function startEdit(holding) {
    setEditId(holding.id);
    setForm({
      symbol: holding.symbol,
      shares: String(holding.shares),
      buyPrice: String(holding.buyPrice),
      buyDate: holding.buyDate,
      notes: holding.notes || '',
    });
    setShowForm(true);
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  function cancelForm() {
    setShowForm(false);
    setEditId(null);
    setForm(emptyForm);
  }

  async function handleSubmit(e) {
    e.preventDefault();
    setSaving(editId ? 'edit' : 'add');
    setError(null);
    try {
      const payload = {
        symbol: form.symbol.toUpperCase().trim(),
        shares: parseFloat(form.shares),
        buyPrice: parseFloat(form.buyPrice),
        buyDate: new Date(form.buyDate).toISOString(),
        notes: form.notes || null,
      };
      if (editId) {
        await portfolioAPI.update(editId, payload);
        flash('Holding updated');
      } else {
        await portfolioAPI.add(payload);
        flash('Holding added');
      }
      cancelForm();
      await load();
    } catch (err) {
      setError(err.response?.data?.error || 'Failed to save holding');
    } finally {
      setSaving(null);
    }
  }

  async function handleDelete(id, symbol) {
    if (!window.confirm(`Remove ${symbol} from your portfolio?`)) return;
    setSaving(id);
    try {
      await portfolioAPI.remove(id);
      flash(`${symbol} removed`);
      await load();
    } catch (err) {
      setError(err.response?.data?.error || 'Failed to delete holding');
    } finally {
      setSaving(null);
    }
  }

  const fmt = (n, dec = 2) => n != null ? Number(n).toLocaleString('en-CA', { minimumFractionDigits: dec, maximumFractionDigits: dec }) : 'N/A';
  const fmtC = (n) => n != null ? `$${fmt(n)}` : 'N/A';
  const glStyle = (n) => n == null ? styles.neutral : n >= 0 ? styles.gain : styles.loss;
  const glPrefix = (n) => n == null ? '' : n >= 0 ? '+' : '';

  const summary = data?.summary;

  return (
    <div style={styles.container}>
      <div style={styles.title}>My Portfolio</div>
      <div style={styles.subtitle}>Track your personal holdings, cost basis, and dividend income</div>

      {error && <div style={styles.error}>{error}</div>}
      {successMsg && <div style={styles.success}>{successMsg}</div>}

      {/* Summary cards */}
      {summary && (
        <div style={styles.summaryGrid}>
          <div style={styles.summaryCard}>
            <div style={styles.summaryLabel}>Total Invested</div>
            <div style={styles.summaryValue}>{fmtC(summary.totalInvested)}</div>
          </div>
          <div style={styles.summaryCard}>
            <div style={styles.summaryLabel}>Current Value</div>
            <div style={{ ...styles.summaryValue, color: '#60a5fa' }}>{fmtC(summary.currentValue)}</div>
          </div>
          <div style={styles.summaryCard}>
            <div style={styles.summaryLabel}>Total Gain / Loss</div>
            <div style={{ ...styles.summaryValue, ...glStyle(summary.gainLoss) }}>
              {glPrefix(summary.gainLoss)}{fmtC(summary.gainLoss)}
              {summary.gainLoss != null && (
                <span style={{ fontSize: '14px', marginLeft: '8px' }}>
                  ({glPrefix(summary.gainLossPct)}{fmt(summary.gainLossPct)}%)
                </span>
              )}
            </div>
          </div>
          <div style={styles.summaryCard}>
            <div style={styles.summaryLabel}>Annual Dividends</div>
            <div style={{ ...styles.summaryValue, color: '#a78bfa' }}>{fmtC(summary.annualDividendIncome)}</div>
          </div>
          <div style={styles.summaryCard}>
            <div style={styles.summaryLabel}>Holdings</div>
            <div style={styles.summaryValue}>{summary.totalHoldings}</div>
          </div>
        </div>
      )}

      {/* Add / Edit Form */}
      <div style={styles.card}>
        {!showForm ? (
          <button style={styles.btnPrimary} onClick={() => setShowForm(true)}>+ Add Holding</button>
        ) : (
          <>
            <div style={styles.cardTitle}>{editId ? 'Edit Holding' : 'Add Holding'}</div>
            <form onSubmit={handleSubmit}>
              <div style={styles.formGrid}>
                <div>
                  <label style={styles.label}>Symbol *</label>
                  <input
                    style={styles.input}
                    placeholder="e.g. TD.TO"
                    value={form.symbol}
                    onChange={e => setForm(f => ({ ...f, symbol: e.target.value }))}
                    required
                  />
                </div>
                <div>
                  <label style={styles.label}>Shares *</label>
                  <input
                    style={styles.input}
                    type="number"
                    step="0.001"
                    min="0.001"
                    placeholder="e.g. 50"
                    value={form.shares}
                    onChange={e => setForm(f => ({ ...f, shares: e.target.value }))}
                    required
                  />
                </div>
                <div>
                  <label style={styles.label}>Buy Price (CAD) *</label>
                  <input
                    style={styles.input}
                    type="number"
                    step="0.01"
                    min="0.01"
                    placeholder="e.g. 78.50"
                    value={form.buyPrice}
                    onChange={e => setForm(f => ({ ...f, buyPrice: e.target.value }))}
                    required
                  />
                </div>
                <div>
                  <label style={styles.label}>Buy Date *</label>
                  <input
                    style={styles.input}
                    type="date"
                    value={form.buyDate}
                    onChange={e => setForm(f => ({ ...f, buyDate: e.target.value }))}
                    required
                  />
                </div>
                <div>
                  <label style={styles.label}>Notes</label>
                  <textarea
                    style={{ ...styles.textarea, height: '38px' }}
                    placeholder="Optional"
                    value={form.notes}
                    onChange={e => setForm(f => ({ ...f, notes: e.target.value }))}
                  />
                </div>
              </div>
              <div style={styles.btnRow}>
                <button style={styles.btnPrimary} type="submit" disabled={!!saving}>
                  {saving ? 'Saving...' : editId ? 'Update' : 'Add Holding'}
                </button>
                <button style={styles.btnSecondary} type="button" onClick={cancelForm}>Cancel</button>
              </div>
            </form>
          </>
        )}
      </div>

      {/* Holdings Table */}
      <div style={styles.card}>
        <div style={styles.cardTitle}>Holdings</div>
        {loading ? (
          <div style={styles.empty}>Loading...</div>
        ) : !data?.holdings?.length ? (
          <div style={styles.empty}>No holdings yet. Add your first stock above.</div>
        ) : (
          <div style={styles.tableWrap}>
            <table style={styles.table}>
              <thead>
                <tr>
                  <th style={styles.th}>Symbol</th>
                  <th style={styles.th}>Company</th>
                  <th style={styles.th}>Shares</th>
                  <th style={styles.th}>Buy Price</th>
                  <th style={styles.th}>Cost Basis</th>
                  <th style={styles.th}>Current Price</th>
                  <th style={styles.th}>Current Value</th>
                  <th style={styles.th}>Gain / Loss</th>
                  <th style={styles.th}>G/L %</th>
                  <th style={styles.th}>Yield</th>
                  <th style={styles.th}>Annual Div.</th>
                  <th style={styles.th}>Buy Date</th>
                  <th style={styles.th}>Actions</th>
                </tr>
              </thead>
              <tbody>
                {data.holdings.map(h => (
                  <tr key={h.id} style={{ borderBottom: '1px solid #334155' }}>
                    <td style={{ ...styles.td, fontWeight: '700', color: '#60a5fa' }}>{h.symbol}</td>
                    <td style={{ ...styles.td, color: '#cbd5e1', maxWidth: '160px', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                      {h.companyName}
                    </td>
                    <td style={styles.td}>{fmt(h.shares, 3).replace(/\.?0+$/, '')}</td>
                    <td style={styles.td}>{fmtC(h.buyPrice)}</td>
                    <td style={styles.td}>{fmtC(h.totalCost)}</td>
                    <td style={styles.td}>{fmtC(h.currentPrice)}</td>
                    <td style={{ ...styles.td, fontWeight: '600' }}>{fmtC(h.currentValue)}</td>
                    <td style={{ ...styles.td, ...glStyle(h.gainLossDollar) }}>
                      {h.gainLossDollar != null ? `${glPrefix(h.gainLossDollar)}${fmtC(h.gainLossDollar)}` : 'N/A'}
                    </td>
                    <td style={{ ...styles.td, ...glStyle(h.gainLossPct) }}>
                      {h.gainLossPct != null ? `${glPrefix(h.gainLossPct)}${fmt(h.gainLossPct)}%` : 'N/A'}
                    </td>
                    <td style={styles.td}>
                      {h.dividendYield != null ? `${fmt(h.dividendYield)}%` : 'N/A'}
                    </td>
                    <td style={{ ...styles.td, color: '#a78bfa' }}>
                      {h.annualDividendIncome != null ? fmtC(h.annualDividendIncome) : 'N/A'}
                    </td>
                    <td style={{ ...styles.td, color: '#94a3b8' }}>{h.buyDate}</td>
                    <td style={styles.td}>
                      <button style={styles.btnEdit} onClick={() => startEdit(h)}>Edit</button>
                      <button
                        style={styles.btnDanger}
                        onClick={() => handleDelete(h.id, h.symbol)}
                        disabled={saving === h.id}
                      >
                        {saving === h.id ? '...' : 'Remove'}
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}
