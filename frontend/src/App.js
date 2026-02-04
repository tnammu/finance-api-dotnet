import React, { useState } from 'react';
import { BrowserRouter as Router } from 'react-router-dom';
import Dashboard from './components/Dashboard';
import DividendAnalysis from './components/DividendAnalysis';
import GrowthAnalysis from './components/GrowthAnalysis';
import EtfAnalysis from './components/EtfAnalysis';
import SP500Analysis from './components/SP500Analysis';
import './App.css';

function App() {
  const [activeTab, setActiveTab] = useState('stocks');

  return (
    <Router>
      <div className="app">
        <header className="header">
          <h1>Finance Dashboard</h1>
          <nav className="nav">
            <button
              className={activeTab === 'stocks' ? 'active' : ''}
              onClick={() => setActiveTab('stocks')}
            >
              Stocks
            </button>
            <button
              className={activeTab === 'dividends' ? 'active' : ''}
              onClick={() => setActiveTab('dividends')}
            >
              Dividend Analysis
            </button>
            <button
              className={activeTab === 'growth' ? 'active' : ''}
              onClick={() => setActiveTab('growth')}
            >
              Growth Analysis
            </button>
            <button
              className={activeTab === 'etf' ? 'active' : ''}
              onClick={() => setActiveTab('etf')}
            >
              ETF Holdings
            </button>
            <button
              className={activeTab === 'sp500' ? 'active' : ''}
              onClick={() => setActiveTab('sp500')}
            >
              S&P 500 Index
            </button>
          </nav>
        </header>

        <main className="main">
          {activeTab === 'stocks' && <Dashboard />}
          {activeTab === 'dividends' && <DividendAnalysis />}
          {activeTab === 'growth' && <GrowthAnalysis />}
          {activeTab === 'etf' && <EtfAnalysis />}
          {activeTab === 'sp500' && <SP500Analysis />}
        </main>
      </div>
    </Router>
  );
}

export default App;
