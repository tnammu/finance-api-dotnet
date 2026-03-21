import React, { useState, useEffect } from 'react';
import { portfolioAPI } from '../services/api';

const S = {
  container: { padding: '24px 28px', maxWidth: '1500px', margin: '0 auto', color: '#e2e8f0' },

  pageHeader: { display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: '20px' },
  title: { fontSize: '22px', fontWeight: '700', color: '#f1f5f9', marginBottom: '2px' },
  subtitle: { color: '#64748b', fontSize: '13px' },

  // Summary strip
  summaryStrip: {
    display: 'grid',
    gridTemplateColumns: 'repeat(5, 1fr)',
    gap: '12px',
    marginBottom: '20px',
  },
  summaryCard: (accent) => ({
    background: '#1e293b', borderRadius: '10px', padding: '16px 18px',
    border: '1px solid #334155', borderLeft: `3px solid ${accent}`,
  }),
  summaryLabel: { fontSize: '11px', color: '#64748b', textTransform: 'uppercase', letterSpacing: '0.06em', marginBottom: '5px' },
  summaryValue: (color) => ({ fontSize: '20px', fontWeight: '700', color }),
  summarySubValue: { fontSize: '12px', color: '#64748b', marginTop: '2px' },

  // Table card
  tableCard: { background: '#1e293b', borderRadius: '10px', border: '1px solid #334155', overflow: 'hidden' },
  tableHeader: {
    display: 'flex', justifyContent: 'space-between', alignItems: 'center',
    padding: '14px 18px', borderBottom: '1px solid #334155',
  },
  tableTitle: { fontSize: '15px', fontWeight: '600', color: '#f1f5f9' },
  holdingCount: { fontSize: '12px', color: '#64748b', marginLeft: '8px' },

  tableWrap: { overflowX: 'auto' },
  table: { width: '100%', borderCollapse: 'collapse', fontSize: '13px' },
  th: {
    padding: '9px 11px', textAlign: 'right', color: '#64748b', fontWeight: '600',
    borderBottom: '1px solid #334155', whiteSpace: 'nowrap',
    position: 'sticky', top: 0, background: '#1e293b', zIndex: 1,
  },
  thLeft: {
    padding: '9px 11px', textAlign: 'left', color: '#64748b', fontWeight: '600',
    borderBottom: '1px solid #334155', whiteSpace: 'nowrap',
    position: 'sticky', top: 0, background: '#1e293b', zIndex: 1,
  },
  td: { padding: '8px 11px', borderBottom: '1px solid #0f172a', whiteSpace: 'nowrap', textAlign: 'right' },
  tdLeft: { padding: '8px 11px', borderBottom: '1px solid #0f172a', whiteSpace: 'nowrap', textAlign: 'left' },

  gain: { color: '#4ade80', fontWeight: '600' },
  loss: { color: '#f87171', fontWeight: '600' },
  neutral: { color: '#64748b' },

  // Buttons
  btnAdd: {
    padding: '8px 18px', background: '#3b82f6', color: '#fff', border: 'none',
    borderRadius: '8px', cursor: 'pointer', fontWeight: '600', fontSize: '13px',
  },
  btnPrimary: {
    padding: '9px 20px', background: '#3b82f6', color: '#fff', border: 'none',
    borderRadius: '8px', cursor: 'pointer', fontWeight: '600', fontSize: '14px',
  },
  btnSecondary: {
    padding: '9px 20px', background: '#334155', color: '#e2e8f0', border: 'none',
    borderRadius: '8px', cursor: 'pointer', fontSize: '14px',
  },
  btnEdit: {
    padding: '4px 12px', background: 'transparent', color: '#60a5fa',
    border: '1px solid #3b5c8a', borderRadius: '6px', cursor: 'pointer',
    fontSize: '12px', marginRight: '5px',
  },
  btnDanger: {
    padding: '4px 12px', background: 'transparent', color: '#f87171',
    border: '1px solid #7a2e2e', borderRadius: '6px', cursor: 'pointer', fontSize: '12px',
  },

  // Modal
  overlay: {
    position: 'fixed', top: 0, left: 0, right: 0, bottom: 0,
    background: 'rgba(0,0,0,0.75)', zIndex: 1000,
    display: 'flex', alignItems: 'center', justifyContent: 'center', padding: '16px',
  },
  modal: {
    background: '#1e293b', borderRadius: '12px', border: '1px solid #334155',
    padding: '28px', width: '100%', maxWidth: '540px', position: 'relative',
  },
  modalTitle: { fontSize: '17px', fontWeight: '700', color: '#f1f5f9', marginBottom: '20px' },
  modalClose: {
    position: 'absolute', top: '14px', right: '16px',
    background: 'transparent', border: 'none', color: '#64748b',
    fontSize: '22px', cursor: 'pointer', lineHeight: 1, padding: '4px 8px',
  },
  formGrid: { display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px', marginBottom: '12px' },
  formFull: { marginBottom: '16px' },
  label: { display: 'block', fontSize: '11px', color: '#64748b', marginBottom: '4px', textTransform: 'uppercase', letterSpacing: '0.05em' },
  input: {
    width: '100%', padding: '8px 11px', background: '#0f172a', border: '1px solid #334155',
    borderRadius: '7px', color: '#e2e8f0', fontSize: '14px', boxSizing: 'border-box',
  },
  textarea: {
    width: '100%', padding: '8px 11px', background: '#0f172a', border: '1px solid #334155',
    borderRadius: '7px', color: '#e2e8f0', fontSize: '14px', resize: 'none', boxSizing: 'border-box',
  },
  btnRow: { display: 'flex', gap: '10px' },

  alert: (type) => ({
    background: type === 'error' ? '#450a0a' : '#052e16',
    border: `1px solid ${type === 'error' ? '#dc2626' : '#16a34a'}`,
    borderRadius: '8px', padding: '10px 14px',
    color: type === 'error' ? '#fca5a5' : '#86efac',
    marginBottom: '14px', fontSize: '13px',
  }),
  empty: { textAlign: 'center', color: '#475569', padding: '48px 0', fontSize: '14px' },
};

const emptyForm = { symbol: '', shares: '', buyPrice: '', buyDate: new Date().toISOString().slice(0, 10), notes: '' };

export default function Portfolio() {
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [successMsg, setSuccessMsg] = useState(null);
  const [showModal, setShowModal] = useState(false);
  const [form, setForm] = useState(emptyForm);
  const [editId, setEditId] = useState(null);
  const [saving, setSaving] = useState(null);

  useEffect(() => { load(); }, []);

  async function load() {
    setLoading(true); setError(null);
    try {
      const res = await portfolioAPI.getAll();
      setData(res.data);
    } catch (err) {
      setError(err.response?.data?.error || 'Failed to load portfolio');
    } finally { setLoading(false); }
  }

  function flash(msg) { setSuccessMsg(msg); setTimeout(() => setSuccessMsg(null), 3000); }

  function openAdd() { setEditId(null); setForm(emptyForm); setShowModal(true); }

  function openEdit(h) {
    setEditId(h.id);
    setForm({ symbol: h.symbol, shares: String(h.shares), buyPrice: String(h.buyPrice), buyDate: h.buyDate, notes: h.notes || '' });
    setShowModal(true);
  }

  function closeModal() { setShowModal(false); setEditId(null); setForm(emptyForm); setError(null); }

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
      if (editId) { await portfolioAPI.update(editId, payload); flash('Holding updated'); }
      else { await portfolioAPI.add(payload); flash('Holding added'); }
      closeModal(); await load();
    } catch (err) {
      setError(err.response?.data?.error || 'Failed to save holding');
    } finally { setSaving(null); }
  }

  async function handleDelete(id, symbol) {
    if (!window.confirm(`Remove ${symbol} from your portfolio?`)) return;
    setSaving(id);
    try { await portfolioAPI.remove(id); flash(`${symbol} removed`); await load(); }
    catch (err) { setError(err.response?.data?.error || 'Failed to delete'); }
    finally { setSaving(null); }
  }

  const fmt = (n, dec = 2) => n != null ? Number(n).toLocaleString('en-CA', { minimumFractionDigits: dec, maximumFractionDigits: dec }) : 'N/A';
  const fmtC = (n) => n != null ? `$${fmt(n)}` : 'N/A';
  const glStyle = (n) => n == null ? S.neutral : n >= 0 ? S.gain : S.loss;
  const glSign = (n) => (n != null && n >= 0) ? '+' : '';

  const sm = data?.summary;

  return (
    <div style={S.container}>
      {/* Page header */}
      <div style={S.pageHeader}>
        <div>
          <div style={S.title}>My Portfolio</div>
          <div style={S.subtitle}>Holdings, cost basis &amp; dividend income</div>
        </div>
      </div>

      {error && !showModal && <div style={S.alert('error')}>{error}</div>}
      {successMsg && <div style={S.alert('success')}>{successMsg}</div>}

      {/* Summary strip */}
      {sm && (
        <div style={S.summaryStrip}>
          <div style={S.summaryCard('#3b82f6')}>
            <div style={S.summaryLabel}>Total Invested</div>
            <div style={S.summaryValue('#f1f5f9')}>{fmtC(sm.totalInvested)}</div>
            <div style={S.summarySubValue}>{sm.totalHoldings} positions</div>
          </div>
          <div style={S.summaryCard('#60a5fa')}>
            <div style={S.summaryLabel}>Current Value</div>
            <div style={S.summaryValue('#60a5fa')}>{fmtC(sm.currentValue)}</div>
          </div>
          <div style={S.summaryCard(sm.gainLoss >= 0 ? '#4ade80' : '#f87171')}>
            <div style={S.summaryLabel}>Total Gain / Loss</div>
            <div style={S.summaryValue(sm.gainLoss >= 0 ? '#4ade80' : '#f87171')}>
              {glSign(sm.gainLoss)}{fmtC(sm.gainLoss)}
            </div>
            <div style={S.summarySubValue}>{glSign(sm.gainLossPct)}{fmt(sm.gainLossPct)}%</div>
          </div>
          <div style={S.summaryCard('#a78bfa')}>
            <div style={S.summaryLabel}>Annual Dividends</div>
            <div style={S.summaryValue('#a78bfa')}>{fmtC(sm.annualDividendIncome)}</div>
            <div style={S.summarySubValue}>{fmtC(sm.annualDividendIncome != null ? sm.annualDividendIncome / 12 : null)}/mo</div>
          </div>
          <div style={S.summaryCard('#34d399')}>
            <div style={S.summaryLabel}>Yield on Cost</div>
            <div style={S.summaryValue('#34d399')}>
              {sm.totalInvested > 0 && sm.annualDividendIncome != null
                ? `${fmt(sm.annualDividendIncome / sm.totalInvested * 100)}%`
                : 'N/A'}
            </div>
          </div>
        </div>
      )}

      {/* Holdings table */}
      <div style={S.tableCard}>
        <div style={S.tableHeader}>
          <div>
            <span style={S.tableTitle}>Holdings</span>
            {data?.holdings && <span style={S.holdingCount}>{data.holdings.length} positions</span>}
          </div>
          <button style={S.btnAdd} onClick={openAdd}>+ Add</button>
        </div>

        {loading ? (
          <div style={S.empty}>Loading...</div>
        ) : !data?.holdings?.length ? (
          <div style={S.empty}>No holdings yet.</div>
        ) : (
          <div style={S.tableWrap}>
            <table style={S.table}>
              <thead>
                <tr>
                  <th style={S.thLeft}>Symbol</th>
                  <th style={S.thLeft}>Company</th>
                  <th style={S.th}>Shares</th>
                  <th style={S.th}>Avg Cost</th>
                  <th style={S.th}>Cost Basis</th>
                  <th style={S.th}>Price</th>
                  <th style={S.th}>Mkt Value</th>
                  <th style={S.th}>Gain / Loss</th>
                  <th style={S.th}>G/L %</th>
                  <th style={S.th}>Yield</th>
                  <th style={S.th}>Ann. Div.</th>
                  <th style={{ ...S.th, textAlign: 'center' }}>Actions</th>
                </tr>
              </thead>
              <tbody>
                {data.holdings.map((h, i) => (
                  <tr key={h.id} style={{ background: i % 2 === 0 ? 'transparent' : 'rgba(255,255,255,0.02)' }}>
                    <td style={{ ...S.tdLeft, fontWeight: '700', color: '#60a5fa' }}>{h.symbol}</td>
                    <td style={{ ...S.tdLeft, color: '#94a3b8', maxWidth: '150px', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                      {h.companyName}
                    </td>
                    <td style={S.td}>{fmt(h.shares, 4).replace(/\.?0+$/, '')}</td>
                    <td style={S.td}>{fmtC(h.buyPrice)}</td>
                    <td style={S.td}>{fmtC(h.totalCost)}</td>
                    <td style={S.td}>{fmtC(h.currentPrice)}</td>
                    <td style={{ ...S.td, fontWeight: '600' }}>{fmtC(h.currentValue)}</td>
                    <td style={{ ...S.td, ...glStyle(h.gainLossDollar) }}>
                      {h.gainLossDollar != null ? `${glSign(h.gainLossDollar)}${fmtC(h.gainLossDollar)}` : 'N/A'}
                    </td>
                    <td style={{ ...S.td, ...glStyle(h.gainLossPct) }}>
                      {h.gainLossPct != null ? `${glSign(h.gainLossPct)}${fmt(h.gainLossPct)}%` : 'N/A'}
                    </td>
                    <td style={S.td}>{h.dividendYield != null ? `${fmt(h.dividendYield)}%` : 'N/A'}</td>
                    <td style={{ ...S.td, color: '#a78bfa' }}>
                      {h.annualDividendIncome != null ? fmtC(h.annualDividendIncome) : 'N/A'}
                    </td>
                    <td style={{ ...S.td, textAlign: 'center' }}>
                      <button style={S.btnEdit} onClick={() => openEdit(h)}>Edit</button>
                      <button style={S.btnDanger} onClick={() => handleDelete(h.id, h.symbol)} disabled={saving === h.id}>
                        {saving === h.id ? '…' : 'Del'}
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* Modal */}
      {showModal && (
        <div style={S.overlay} onClick={e => { if (e.target === e.currentTarget) closeModal(); }}>
          <div style={S.modal}>
            <button style={S.modalClose} onClick={closeModal}>&times;</button>
            <div style={S.modalTitle}>{editId ? 'Edit Holding' : 'Add Holding'}</div>
            {error && <div style={S.alert('error')}>{error}</div>}
            <form onSubmit={handleSubmit}>
              <div style={S.formGrid}>
                <div>
                  <label style={S.label}>Symbol *</label>
                  <input style={S.input} placeholder="e.g. TD.TO" value={form.symbol}
                    onChange={e => setForm(f => ({ ...f, symbol: e.target.value }))} required autoFocus />
                </div>
                <div>
                  <label style={S.label}>Shares *</label>
                  <input style={S.input} type="number" step="0.0001" min="0.0001" placeholder="e.g. 50"
                    value={form.shares} onChange={e => setForm(f => ({ ...f, shares: e.target.value }))} required />
                </div>
                <div>
                  <label style={S.label}>Avg Buy Price (CAD) *</label>
                  <input style={S.input} type="number" step="0.0001" min="0.0001" placeholder="e.g. 78.50"
                    value={form.buyPrice} onChange={e => setForm(f => ({ ...f, buyPrice: e.target.value }))} required />
                </div>
                <div>
                  <label style={S.label}>Buy Date *</label>
                  <input style={S.input} type="date" value={form.buyDate}
                    onChange={e => setForm(f => ({ ...f, buyDate: e.target.value }))} required />
                </div>
              </div>
              <div style={S.formFull}>
                <label style={S.label}>Notes</label>
                <textarea style={{ ...S.textarea, height: '56px' }} placeholder="Optional"
                  value={form.notes} onChange={e => setForm(f => ({ ...f, notes: e.target.value }))} />
              </div>
              <div style={S.btnRow}>
                <button style={S.btnPrimary} type="submit" disabled={!!saving}>
                  {saving ? 'Saving…' : editId ? 'Update' : 'Add Holding'}
                </button>
                <button style={S.btnSecondary} type="button" onClick={closeModal}>Cancel</button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
