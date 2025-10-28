import React, { useState } from 'react';
import { BrowserRouter as Router } from 'react-router-dom';
import Dashboard from './components/Dashboard';
import DividendAnalysis from './components/DividendAnalysis';
import ApiUsage from './components/ApiUsage';
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
              className={activeTab === 'usage' ? 'active' : ''}
              onClick={() => setActiveTab('usage')}
            >
              API Usage
            </button>
          </nav>
        </header>

        <main className="main">
          {activeTab === 'stocks' && <Dashboard />}
          {activeTab === 'dividends' && <DividendAnalysis />}
          {activeTab === 'usage' && <ApiUsage />}
        </main>
      </div>
    </Router>
  );
}

export default App;
