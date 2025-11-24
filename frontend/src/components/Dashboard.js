import React, { useState, useEffect } from 'react';
import { stocksAPI } from '../services/api';
import StockCard from './StockCard';
import AddStockModal from './AddStockModal';
import './Dashboard.css';

function Dashboard() {
  const [stocks, setStocks] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [showAddModal, setShowAddModal] = useState(false);
  const [refreshing, setRefreshing] = useState(false);

  useEffect(() => {
    loadStocks();
  }, []);

  const loadStocks = async () => {
    try {
      setLoading(true);
      setError(null);
      const response = await stocksAPI.getAll();
      setStocks(response.data);
    } catch (err) {
      setError('Failed to load stocks: ' + err.message);
    } finally {
      setLoading(false);
    }
  };

  const handleAddStock = async (symbol) => {
    try {
      await stocksAPI.add(symbol);
      await loadStocks();
      setShowAddModal(false);
    } catch (err) {
      throw new Error(err.response?.data?.message || 'Failed to add stock');
    }
  };

  const handleDeleteStock = async (id) => {
    if (!window.confirm('Are you sure you want to delete this stock?')) {
      return;
    }

    try {
      await stocksAPI.delete(id);
      await loadStocks();
    } catch (err) {
      setError('Failed to delete stock: ' + err.message);
    }
  };

  const handleRefreshStock = async (id) => {
    try {
      await stocksAPI.refresh(id);
      await loadStocks();
    } catch (err) {
      setError('Failed to refresh stock: ' + err.message);
    }
  };

  const handleRefreshAll = async () => {
    if (!window.confirm('This will refresh all stocks. It may take a while. Continue?')) {
      return;
    }

    try {
      setRefreshing(true);
      setError(null);
      await stocksAPI.refreshAll();
      await loadStocks();
    } catch (err) {
      setError('Failed to refresh stocks: ' + err.message);
    } finally {
      setRefreshing(false);
    }
  };

  const handleExportCSV = () => {
    const API_BASE_URL = 'http://localhost:5000/api';
    window.open(`${API_BASE_URL}/stocks/export/csv`, '_blank');
  };

  const totalValue = stocks.reduce((sum, stock) => sum + (stock.currentPrice || 0), 0);
  const staleCount = stocks.filter(s => s.isStale).length;

  if (loading) {
    return <div className="loading">Loading stocks...</div>;
  }

  return (
    <div className="dashboard">
      <div className="dashboard-header">
        <div>
          <h2>Stock Portfolio</h2>
          <p className="subtitle">{stocks.length} stocks tracked</p>
        </div>
        <div className="actions">
          <button className="primary" onClick={() => setShowAddModal(true)}>
            + Add Stock
          </button>
          <button
            className="secondary"
            onClick={handleRefreshAll}
            disabled={refreshing || stocks.length === 0}
          >
            {refreshing ? 'Refreshing...' : 'Refresh All'}
          </button>
          <button
            className="secondary"
            onClick={handleExportCSV}
            disabled={stocks.length === 0}
          >
            Export CSV
          </button>
        </div>
      </div>

      {error && <div className="error">{error}</div>}

      <div className="stats-grid">
        <div className="stat-card">
          <h4>Total Stocks</h4>
          <div className="value">{stocks.length}</div>
        </div>
        <div className="stat-card">
          <h4>Total Value</h4>
          <div className="value">${totalValue.toFixed(2)}</div>
        </div>
        <div className="stat-card">
          <h4>Stale Data</h4>
          <div className="value">{staleCount}</div>
        </div>
        <div className="stat-card">
          <h4>Avg Price</h4>
          <div className="value">
            ${stocks.length > 0 ? (totalValue / stocks.length).toFixed(2) : '0.00'}
          </div>
        </div>
      </div>

      {stocks.length === 0 ? (
        <div className="card empty-state">
          <p>No stocks yet. Add your first stock to get started!</p>
        </div>
      ) : (
        <div className="stocks-grid">
          {stocks.map(stock => (
            <StockCard
              key={stock.id}
              stock={stock}
              onDelete={() => handleDeleteStock(stock.id)}
              onRefresh={() => handleRefreshStock(stock.id)}
            />
          ))}
        </div>
      )}

      {showAddModal && (
        <AddStockModal
          onClose={() => setShowAddModal(false)}
          onAdd={handleAddStock}
        />
      )}
    </div>
  );
}

export default Dashboard;
