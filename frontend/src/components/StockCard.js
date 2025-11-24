import React from 'react';
import './StockCard.css';

function StockCard({ stock, onDelete, onRefresh }) {
  const formatDate = (dateString) => {
    const date = new Date(dateString);
    return date.toLocaleString();
  };

  const getStatusBadge = () => {
    if (stock.isStale) {
      return <span className="badge warning">Stale ({stock.minutesOld}m old)</span>;
    }
    return <span className="badge success">Fresh</span>;
  };

  return (
    <div className="stock-card">
      <div className="stock-header">
        <div>
          <h3>{stock.symbol}</h3>
          <p className="company-name">{stock.companyName}</p>
        </div>
        {getStatusBadge()}
      </div>

      <div className="stock-price">
        <span className="price">${stock.currentPrice?.toFixed(2) || '0.00'}</span>
      </div>

      {stock.dividendYield && (
        <div className="stock-info">
          <span className="label">Dividend Yield:</span>
          <span className="value">{stock.dividendYield.toFixed(2)}%</span>
        </div>
      )}

      <div className="stock-info">
        <span className="label">Last Updated:</span>
        <span className="value">{formatDate(stock.lastUpdated)}</span>
      </div>

      <div className="stock-actions">
        <button className="success" onClick={onRefresh}>
          Refresh
        </button>
        <button className="danger" onClick={onDelete}>
          Delete
        </button>
      </div>
    </div>
  );
}

export default StockCard;
