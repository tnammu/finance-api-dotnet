import React, { useState } from 'react';

function AddStockModal({ onClose, onAdd }) {
  const [symbol, setSymbol] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  const handleSubmit = async (e) => {
    e.preventDefault();

    if (!symbol.trim()) {
      setError('Please enter a stock symbol');
      return;
    }

    try {
      setLoading(true);
      setError(null);
      await onAdd(symbol.toUpperCase().trim());
    } catch (err) {
      setError(err.message);
      setLoading(false);
    }
  };

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal" onClick={(e) => e.stopPropagation()}>
        <h2>Add New Stock</h2>

        {error && <div className="error">{error}</div>}

        <form onSubmit={handleSubmit}>
          <div style={{ marginBottom: '1rem' }}>
            <label style={{ display: 'block', marginBottom: '0.5rem', color: '#555' }}>
              Stock Symbol
            </label>
            <input
              type="text"
              value={symbol}
              onChange={(e) => setSymbol(e.target.value)}
              placeholder="e.g., AAPL, GOOGL, MSFT"
              disabled={loading}
              autoFocus
            />
            <p style={{ fontSize: '0.85rem', color: '#7f8c8d', marginTop: '0.5rem' }}>
              Enter the stock ticker symbol (e.g., AAPL for Apple Inc.)
            </p>
          </div>

          <div className="actions">
            <button type="button" className="secondary" onClick={onClose} disabled={loading}>
              Cancel
            </button>
            <button type="submit" className="primary" disabled={loading}>
              {loading ? 'Adding...' : 'Add Stock'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

export default AddStockModal;
