import React, { useState } from 'react';
import { BrowserRouter as Router } from 'react-router-dom';
import Dashboard from './components/Dashboard';
import DividendAnalysis from './components/DividendAnalysis';
import GrowthAnalysis from './components/GrowthAnalysis';
import EtfAnalysis from './components/EtfAnalysis';
import SP500Analysis from './components/SP500Analysis';
import StockAlerts from './components/StockAlerts';
import Portfolio from './components/Portfolio';
import OilSentiment from './components/OilSentiment';
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
            <button
              className={activeTab === 'alerts' ? 'active' : ''}
              onClick={() => setActiveTab('alerts')}
            >
              Stock Alerts
            </button>
            <button
              className={activeTab === 'portfolio' ? 'active' : ''}
              onClick={() => setActiveTab('portfolio')}
            >
              My Portfolio
            </button>
            <button
              className={activeTab === 'oil' ? 'active' : ''}
              onClick={() => setActiveTab('oil')}
            >
              Oil Signal
            </button>
          </nav>
        </header>

        <main className="main">
          {activeTab === 'stocks' && <Dashboard />}
          {activeTab === 'dividends' && <DividendAnalysis />}
          {activeTab === 'growth' && <GrowthAnalysis />}
          {activeTab === 'etf' && <EtfAnalysis />}
          {activeTab === 'sp500' && <SP500Analysis />}
          {activeTab === 'alerts' && <StockAlerts />}
          {activeTab === 'portfolio' && <Portfolio />}
          {activeTab === 'oil' && <OilSentiment />}
        </main>
      </div>
    </Router>
  );
}

export default App;
