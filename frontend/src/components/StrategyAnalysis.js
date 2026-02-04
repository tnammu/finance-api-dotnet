import React, { useState, useEffect } from 'react';
import { strategyAPI } from '../services/api';
import { BarChart, Bar, LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer, PieChart, Pie, Cell } from 'recharts';
import './StrategyAnalysis.css';

function StrategyAnalysis({ symbol }) {
  const [strategies, setStrategies] = useState(null);
  const [availableStrategies, setAvailableStrategies] = useState([]);
  const [selectedStrategy, setSelectedStrategy] = useState('buyHold');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  // Calculator state
  const [capital, setCapital] = useState(100);
  const [years, setYears] = useState(5);
  const [enforceBuyFirst, setEnforceBuyFirst] = useState(true);
  const [calculatorResults, setCalculatorResults] = useState(null);
  const [calculatingAmounts, setCalculatingAmounts] = useState(false);

  // Trades pagination state
  const [currentPage, setCurrentPage] = useState(1);
  const [tradesPerPage, setTradesPerPage] = useState(50);

  const COLORS = ['#0088FE', '#00C49F', '#FFBB28', '#FF8042', '#8884d8', '#82ca9d', '#ffc658'];

  useEffect(() => {
    loadAvailableStrategies();
  }, []);

  useEffect(() => {
    if (symbol) {
      analyzeStrategies();
    }
  }, [symbol, capital, years, enforceBuyFirst]);

  const loadAvailableStrategies = async () => {
    try {
      const response = await strategyAPI.getList();
      setAvailableStrategies(response.data.strategies || []);
    } catch (err) {
      console.error('Failed to load available strategies:', err);
    }
  };

  const analyzeStrategies = async () => {
    if (!symbol) return;

    try {
      setLoading(true);
      setError(null);

      const response = await strategyAPI.analyzeAll(symbol, capital, years, enforceBuyFirst);
      setStrategies(response.data);

      // Auto-select best strategy
      if (response.data.bestStrategy) {
        setSelectedStrategy(response.data.bestStrategy);
      }
    } catch (err) {
      setError(err.response?.data?.error || 'Failed to analyze strategies');
    } finally {
      setLoading(false);
    }
  };

  const calculateMultipleAmounts = async () => {
    if (!symbol || !selectedStrategy) return;

    try {
      setCalculatingAmounts(true);
      const amounts = [100, 500, 1000, 5000, 10000];

      const response = await strategyAPI.calculate(symbol, selectedStrategy, amounts, years);
      setCalculatorResults(response.data);
    } catch (err) {
      console.error('Failed to calculate amounts:', err);
    } finally {
      setCalculatingAmounts(false);
    }
  };

  const getRiskLevelColor = (riskLevel) => {
    switch (riskLevel) {
      case 'Low': return '#22c55e';
      case 'Medium': return '#eab308';
      case 'Medium-High': return '#f97316';
      case 'High': return '#ef4444';
      default: return '#6b7280';
    }
  };

  const getReturnColor = (value) => {
    if (value > 0) return '#22c55e';
    if (value < 0) return '#ef4444';
    return '#6b7280';
  };

  const getPaginatedTrades = (allTrades) => {
    if (!allTrades || allTrades.length === 0) return { trades: [], totalPages: 0, totalTrades: 0 };

    const totalTrades = allTrades.length;

    // If "All" is selected, return all trades
    if (tradesPerPage === -1) {
      return { trades: allTrades, totalPages: 1, totalTrades };
    }

    // Calculate pagination
    const totalPages = Math.ceil(totalTrades / tradesPerPage);
    const startIndex = (currentPage - 1) * tradesPerPage;
    const endIndex = startIndex + tradesPerPage;
    const paginatedTrades = allTrades.slice(startIndex, endIndex);

    return { trades: paginatedTrades, totalPages, totalTrades };
  };

  const handleTradesPerPageChange = (newPerPage) => {
    setTradesPerPage(newPerPage);
    setCurrentPage(1); // Reset to first page when changing items per page
  };

  const handlePageChange = (newPage) => {
    setCurrentPage(newPage);
  };

  if (!symbol) {
    return (
      <div className="strategy-analysis">
        <div className="strategy-placeholder">
          <h3>Trading Strategy Analysis</h3>
          <p>Select a stock to view detailed trading strategies and backtested results</p>
        </div>
      </div>
    );
  }

  if (loading) {
    return (
      <div className="strategy-analysis">
        <div className="strategy-loading">
          <div className="loading-spinner"></div>
          <p>Analyzing {years}-year trading strategies for {symbol}...</p>
          <small>This may take 10-30 seconds depending on data availability</small>
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="strategy-analysis">
        <div className="strategy-error">
          <h3>Error</h3>
          <p>{error}</p>
          <button onClick={analyzeStrategies}>Retry</button>
        </div>
      </div>
    );
  }

  if (!strategies) {
    return null;
  }

  const currentStrategy = strategies.strategies?.[selectedStrategy];

  // Prepare comparison chart data
  const comparisonData = Object.entries(strategies.strategies || {}).map(([key, strategy]) => ({
    name: strategy.name,
    return: strategy.totalReturn || 0,
    winRate: strategy.winRate || 0,
    trades: strategy.trades || 0
  }));

  return (
    <div className="strategy-analysis">
      <div className="strategy-header">
        <div className="strategy-title">
          <h2>Trading Strategies for {symbol}</h2>
          <p>{strategies.companyName} - ${strategies.currentPrice?.toFixed(2)}</p>
        </div>

        <div className="strategy-controls">
          <div className="control-group">
            <label>Capital:</label>
            <input
              type="number"
              value={capital}
              onChange={(e) => setCapital(Number(e.target.value))}
              min="10"
              step="10"
            />
          </div>
          <div className="control-group">
            <label>Years:</label>
            <select value={years} onChange={(e) => setYears(Number(e.target.value))}>
              <option value={1}>1 Year</option>
              <option value={2}>2 Years</option>
              <option value={3}>3 Years</option>
              <option value={5}>5 Years</option>
              <option value={7}>7 Years</option>
              <option value={10}>10 Years</option>
            </select>
          </div>
          <div className="control-group">
            <label title="Ensure stocks can only be sold after being bought">
              Buy-First Rule:
            </label>
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
              <label style={{ display: 'flex', alignItems: 'center', gap: '5px', cursor: 'pointer' }}>
                <input
                  type="checkbox"
                  checked={enforceBuyFirst}
                  onChange={(e) => setEnforceBuyFirst(e.target.checked)}
                  style={{ cursor: 'pointer' }}
                />
                <span style={{ fontSize: '13px', color: enforceBuyFirst ? '#10b981' : '#ef4444' }}>
                  {enforceBuyFirst ? '✓ Enforced' : '⚠️ Disabled'}
                </span>
              </label>
            </div>
          </div>
          <button className="btn-refresh" onClick={analyzeStrategies}>
            🔄 Recalculate
          </button>
        </div>
      </div>

      {/* Stock Performance Overview (No Strategy) */}
      {strategies.strategies?.buyHold && (
        <div className="stock-performance-overview">
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: '15px', flexWrap: 'wrap', gap: '15px' }}>
            <div>
              <h3 style={{ margin: '0 0 5px 0' }}>📈 Stock Performance Over {years} Year{years > 1 ? 's' : ''} (No Strategy)</h3>
              <p style={{ color: 'rgba(255, 255, 255, 0.9)', margin: 0 }}>
                Raw stock price appreciation without any trading strategy - pure buy and hold
              </p>
            </div>
            {strategies.valuation && (
              <div style={{
                padding: '10px 20px',
                background: strategies.valuation.status === 'discount' ? '#10b981' :
                           strategies.valuation.status === 'premium' ? '#ef4444' : '#f59e0b',
                borderRadius: '20px',
                fontWeight: '700',
                fontSize: '16px',
                color: 'white',
                textAlign: 'center',
                minWidth: '150px',
                boxShadow: '0 4px 12px rgba(0,0,0,0.2)'
              }}>
                {strategies.valuation.status === 'discount' && '💎 DISCOUNTED'}
                {strategies.valuation.status === 'premium' && '⚠️ OVERPRICED'}
                {strategies.valuation.status === 'fair' && '✓ FAIR VALUE'}
              </div>
            )}
          </div>
          <div className="performance-metrics">
            <div className="perf-metric-card highlight-card">
              <div className="perf-metric-label">Starting Price</div>
              <div className="perf-metric-value">
                ${strategies.strategies.buyHold.entryPrice?.toFixed(2)}
              </div>
              <div className="perf-metric-date">
                {new Date(new Date().setFullYear(new Date().getFullYear() - years)).toLocaleDateString()}
              </div>
            </div>
            <div className="perf-metric-card highlight-card">
              <div className="perf-metric-label">Current Price</div>
              <div className="perf-metric-value">
                ${strategies.strategies.buyHold.exitPrice?.toFixed(2)}
              </div>
              <div className="perf-metric-date">
                {new Date().toLocaleDateString()}
              </div>
            </div>
            <div className="perf-metric-card highlight-card main-highlight">
              <div className="perf-metric-label">Total Stock Growth</div>
              <div
                className="perf-metric-value-large"
                style={{ color: getReturnColor(strategies.strategies.buyHold.totalReturn) }}
              >
                {strategies.strategies.buyHold.totalReturn > 0 ? '+' : ''}
                {strategies.strategies.buyHold.totalReturn?.toFixed(2)}%
              </div>
              <div className="perf-metric-sublabel">
                {strategies.strategies.buyHold.annualReturn?.toFixed(2)}% per year
              </div>
            </div>
            <div className="perf-metric-card highlight-card">
              <div className="perf-metric-label">Max Drawdown</div>
              <div className="perf-metric-value" style={{ color: '#ef4444' }}>
                {strategies.strategies.buyHold.maxDrawdown?.toFixed(2)}%
              </div>
              <div className="perf-metric-sublabel">Worst decline from peak</div>
            </div>
            <div className="perf-metric-card highlight-card">
              <div className="perf-metric-label">$100 Investment</div>
              <div className="perf-metric-value" style={{ color: '#8b5cf6' }}>
                ${strategies.strategies.buyHold.finalValue?.toFixed(2)}
              </div>
              <div className="perf-metric-sublabel">
                {strategies.strategies.buyHold.totalReturn > 0 ? '+' : ''}
                ${(strategies.strategies.buyHold.finalValue - 100)?.toFixed(2)} profit
              </div>
            </div>
          </div>

          <div style={{
            marginTop: '15px',
            padding: '12px',
            background: 'rgba(255, 255, 255, 0.15)',
            borderRadius: '8px',
            fontSize: '14px',
            color: 'white'
          }}>
            💡 <strong>Baseline Reference:</strong> Use this as a baseline to compare trading strategies below.
            A good strategy should outperform the simple buy-and-hold approach.
          </div>
        </div>
      )}

      {/* Strategy Selector */}
      <div className="strategy-selector">
        <h3>Select Strategy</h3>
        <div className="strategy-cards">
          {availableStrategies.map((strat) => (
            <div
              key={strat.id}
              className={`strategy-card ${selectedStrategy === strat.id ? 'active' : ''}`}
              onClick={() => setSelectedStrategy(strat.id)}
            >
              <div className="strategy-card-header">
                <h4>{strat.name}</h4>
                <span
                  className="risk-badge"
                  style={{ backgroundColor: getRiskLevelColor(strat.riskLevel) }}
                >
                  {strat.riskLevel}
                </span>
              </div>
              <p className="strategy-description">{strat.description}</p>
              <div className="strategy-category">{strat.category}</div>
              {strategies.strategies?.[strat.id] && (
                <div className="strategy-performance">
                  <span
                    className="performance-return"
                    style={{ color: getReturnColor(strategies.strategies[strat.id].totalReturn) }}
                  >
                    {strategies.strategies[strat.id].totalReturn > 0 ? '+' : ''}
                    {strategies.strategies[strat.id].totalReturn?.toFixed(2)}%
                  </span>
                  {strategies.bestStrategy === strat.id && (
                    <span className="best-badge">⭐ BEST</span>
                  )}
                </div>
              )}
            </div>
          ))}
        </div>
      </div>

      {/* Strategy Details */}
      {currentStrategy && (
        <div className="strategy-details">
          <div className="strategy-metrics">
            <div className="metric-card">
              <div className="metric-label">Initial Capital</div>
              <div className="metric-value">${currentStrategy.initialValue?.toFixed(2)}</div>
            </div>
            <div className="metric-card">
              <div className="metric-label">Final Value</div>
              <div className="metric-value highlight">${currentStrategy.finalValue?.toFixed(2)}</div>
            </div>
            <div className="metric-card">
              <div className="metric-label">Total Return</div>
              <div
                className="metric-value"
                style={{ color: getReturnColor(currentStrategy.totalReturn) }}
              >
                {currentStrategy.totalReturn > 0 ? '+' : ''}
                {currentStrategy.totalReturn?.toFixed(2)}%
              </div>
            </div>
            <div className="metric-card">
              <div className="metric-label">Annual Return</div>
              <div className="metric-value">
                {currentStrategy.annualReturn > 0 ? '+' : ''}
                {currentStrategy.annualReturn?.toFixed(2)}%
              </div>
            </div>
            <div className="metric-card">
              <div className="metric-label">Win Rate</div>
              <div className="metric-value">{currentStrategy.winRate?.toFixed(1)}%</div>
            </div>
            <div className="metric-card">
              <div className="metric-label">Total Trades</div>
              <div className="metric-value">{currentStrategy.trades || 0}</div>
            </div>
            <div className="metric-card">
              <div className="metric-label">Avg Win</div>
              <div className="metric-value success">
                +{currentStrategy.avgWin?.toFixed(2)}%
              </div>
            </div>
            <div className="metric-card">
              <div className="metric-label">Avg Loss</div>
              <div className="metric-value danger">
                {currentStrategy.avgLoss?.toFixed(2)}%
              </div>
            </div>
            {currentStrategy.riskRewardRatio > 0 && (
              <div className="metric-card">
                <div className="metric-label">Risk/Reward</div>
                <div className="metric-value">{currentStrategy.riskRewardRatio?.toFixed(2)}:1</div>
              </div>
            )}
            {currentStrategy.maxDrawdown && (
              <div className="metric-card">
                <div className="metric-label">Max Drawdown</div>
                <div className="metric-value danger">
                  {currentStrategy.maxDrawdown?.toFixed(2)}%
                </div>
              </div>
            )}
          </div>

          {/* Calculator Section */}
          <div className="strategy-calculator">
            <h3>Returns Calculator</h3>
            <p>Calculate returns for different investment amounts</p>
            <button
              className="btn-calculate"
              onClick={calculateMultipleAmounts}
              disabled={calculatingAmounts}
            >
              {calculatingAmounts ? 'Calculating...' : 'Calculate for $100, $500, $1K, $5K, $10K'}
            </button>

            {calculatorResults && calculatorResults.calculations && (
              <div className="calculator-results">
                <table className="calculator-table">
                  <thead>
                    <tr>
                      <th>Capital</th>
                      <th>Final Value</th>
                      <th>Profit</th>
                      <th>Return</th>
                      <th>Win Rate</th>
                    </tr>
                  </thead>
                  <tbody>
                    {calculatorResults.calculations.map((calc, idx) => (
                      <tr key={idx}>
                        <td>${calc.capital.toFixed(2)}</td>
                        <td>${calc.finalValue.toFixed(2)}</td>
                        <td style={{ color: getReturnColor(calc.profit) }}>
                          {calc.profit > 0 ? '+' : ''}${calc.profit.toFixed(2)}
                        </td>
                        <td style={{ color: getReturnColor(calc.totalReturn) }}>
                          {calc.totalReturn > 0 ? '+' : ''}
                          {calc.totalReturn.toFixed(2)}%
                        </td>
                        <td>{calc.winRate.toFixed(1)}%</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>

          {/* All Trades with Pagination */}
          {currentStrategy.allTrades && currentStrategy.allTrades.length > 0 && (() => {
            const { trades, totalPages, totalTrades } = getPaginatedTrades(currentStrategy.allTrades);

            return (
              <div className="recent-trades">
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '15px' }}>
                  <h3 style={{ margin: 0 }}>All Trades ({totalTrades} total)</h3>
                  <div style={{ display: 'flex', gap: '10px', alignItems: 'center' }}>
                    <label style={{ fontSize: '14px', color: '#64748b' }}>Show:</label>
                    <select
                      value={tradesPerPage}
                      onChange={(e) => handleTradesPerPageChange(Number(e.target.value))}
                      style={{
                        padding: '6px 12px',
                        border: '1px solid #cbd5e1',
                        borderRadius: '6px',
                        fontSize: '14px',
                        cursor: 'pointer'
                      }}
                    >
                      <option value={50}>50 per page</option>
                      <option value={100}>100 per page</option>
                      <option value={1000}>1000 per page</option>
                      <option value={-1}>All</option>
                    </select>
                  </div>
                </div>

                <table className="trades-table">
                  <thead>
                    <tr>
                      <th>#</th>
                      <th>Date</th>
                      <th>Type</th>
                      <th>Price</th>
                      <th>Shares</th>
                      <th>Value</th>
                      <th>Profit</th>
                      <th>Reason</th>
                    </tr>
                  </thead>
                  <tbody>
                    {trades.map((trade, idx) => {
                      const globalIndex = tradesPerPage === -1
                        ? idx + 1
                        : (currentPage - 1) * tradesPerPage + idx + 1;

                      return (
                        <tr key={idx}>
                          <td style={{ color: '#94a3b8', fontWeight: '600' }}>{globalIndex}</td>
                          <td>{trade.date}</td>
                          <td className={`trade-type ${trade.type.toLowerCase()}`}>{trade.type}</td>
                          <td>${trade.price?.toFixed(2)}</td>
                          <td>{trade.shares?.toFixed(4)}</td>
                          <td>${trade.value?.toFixed(2) || '-'}</td>
                          <td
                            style={{
                              color: trade.profitPct
                                ? getReturnColor(trade.profitPct)
                                : '#6b7280'
                            }}
                          >
                            {trade.profitPct
                              ? `${trade.profitPct > 0 ? '+' : ''}${trade.profitPct.toFixed(2)}%`
                              : '-'}
                          </td>
                          <td className="trade-reason">{trade.reason}</td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>

                {/* Pagination Controls */}
                {totalPages > 1 && tradesPerPage !== -1 && (
                  <div style={{
                    display: 'flex',
                    justifyContent: 'center',
                    alignItems: 'center',
                    gap: '10px',
                    marginTop: '20px',
                    padding: '15px',
                    background: '#f8fafc',
                    borderRadius: '8px'
                  }}>
                    <button
                      onClick={() => handlePageChange(currentPage - 1)}
                      disabled={currentPage === 1}
                      style={{
                        padding: '8px 16px',
                        background: currentPage === 1 ? '#e2e8f0' : '#3b82f6',
                        color: currentPage === 1 ? '#94a3b8' : 'white',
                        border: 'none',
                        borderRadius: '6px',
                        cursor: currentPage === 1 ? 'not-allowed' : 'pointer',
                        fontWeight: '600',
                        fontSize: '14px'
                      }}
                    >
                      ← Previous
                    </button>

                    <div style={{ display: 'flex', gap: '5px', alignItems: 'center' }}>
                      {/* Show page numbers with ellipsis for large page counts */}
                      {(() => {
                        const pageNumbers = [];
                        const maxPagesToShow = 7;

                        if (totalPages <= maxPagesToShow) {
                          // Show all pages
                          for (let i = 1; i <= totalPages; i++) {
                            pageNumbers.push(i);
                          }
                        } else {
                          // Show first, last, current and surrounding pages with ellipsis
                          if (currentPage <= 3) {
                            for (let i = 1; i <= 5; i++) pageNumbers.push(i);
                            pageNumbers.push('...');
                            pageNumbers.push(totalPages);
                          } else if (currentPage >= totalPages - 2) {
                            pageNumbers.push(1);
                            pageNumbers.push('...');
                            for (let i = totalPages - 4; i <= totalPages; i++) pageNumbers.push(i);
                          } else {
                            pageNumbers.push(1);
                            pageNumbers.push('...');
                            for (let i = currentPage - 1; i <= currentPage + 1; i++) pageNumbers.push(i);
                            pageNumbers.push('...');
                            pageNumbers.push(totalPages);
                          }
                        }

                        return pageNumbers.map((page, idx) => (
                          page === '...' ? (
                            <span key={`ellipsis-${idx}`} style={{ padding: '0 8px', color: '#94a3b8' }}>...</span>
                          ) : (
                            <button
                              key={page}
                              onClick={() => handlePageChange(page)}
                              style={{
                                padding: '8px 12px',
                                background: currentPage === page ? '#3b82f6' : 'white',
                                color: currentPage === page ? 'white' : '#475569',
                                border: currentPage === page ? 'none' : '1px solid #cbd5e1',
                                borderRadius: '6px',
                                cursor: 'pointer',
                                fontWeight: currentPage === page ? '600' : '400',
                                fontSize: '14px',
                                minWidth: '40px'
                              }}
                            >
                              {page}
                            </button>
                          )
                        ));
                      })()}
                    </div>

                    <button
                      onClick={() => handlePageChange(currentPage + 1)}
                      disabled={currentPage === totalPages}
                      style={{
                        padding: '8px 16px',
                        background: currentPage === totalPages ? '#e2e8f0' : '#3b82f6',
                        color: currentPage === totalPages ? '#94a3b8' : 'white',
                        border: 'none',
                        borderRadius: '6px',
                        cursor: currentPage === totalPages ? 'not-allowed' : 'pointer',
                        fontWeight: '600',
                        fontSize: '14px'
                      }}
                    >
                      Next →
                    </button>

                    <span style={{ marginLeft: '15px', color: '#64748b', fontSize: '14px' }}>
                      Page {currentPage} of {totalPages}
                    </span>
                  </div>
                )}
              </div>
            );
          })()}

          {/* Monthly Stats for Seasonal Strategy */}
          {selectedStrategy === 'monthlySeasonal' && currentStrategy.monthlyStats && (
            <div className="monthly-stats">
              <h3>Monthly Performance Analysis</h3>
              <ResponsiveContainer width="100%" height={300}>
                <BarChart data={currentStrategy.monthlyStats}>
                  <CartesianGrid strokeDasharray="3 3" />
                  <XAxis dataKey="month" angle={-45} textAnchor="end" height={100} />
                  <YAxis label={{ value: 'Avg Return (%)', angle: -90, position: 'insideLeft' }} />
                  <Tooltip />
                  <Bar dataKey="avgReturn" fill="#8884d8" />
                </BarChart>
              </ResponsiveContainer>
              {currentStrategy.bestMonths && (
                <div className="best-months">
                  <strong>Best Months to Trade:</strong> {currentStrategy.bestMonths.join(', ')}
                </div>
              )}
            </div>
          )}
        </div>
      )}

      {/* Strategy Comparison Chart */}
      <div className="strategy-comparison">
        <h3>Strategy Comparison</h3>
        <ResponsiveContainer width="100%" height={400}>
          <BarChart data={comparisonData}>
            <CartesianGrid strokeDasharray="3 3" />
            <XAxis dataKey="name" angle={-45} textAnchor="end" height={150} />
            <YAxis label={{ value: 'Total Return (%)', angle: -90, position: 'insideLeft' }} />
            <Tooltip />
            <Legend />
            <Bar dataKey="return" fill="#8884d8" name="Total Return %" />
          </BarChart>
        </ResponsiveContainer>
      </div>

      {/* Win Rate Comparison */}
      <div className="winrate-comparison">
        <h3>Win Rate Comparison</h3>
        <ResponsiveContainer width="100%" height={300}>
          <PieChart>
            <Pie
              data={comparisonData}
              dataKey="winRate"
              nameKey="name"
              cx="50%"
              cy="50%"
              outerRadius={100}
              label={(entry) => `${entry.name}: ${entry.winRate.toFixed(1)}%`}
            >
              {comparisonData.map((entry, index) => (
                <Cell key={`cell-${index}`} fill={COLORS[index % COLORS.length]} />
              ))}
            </Pie>
            <Tooltip />
          </PieChart>
        </ResponsiveContainer>
      </div>

      {/* Valuation Analysis */}
      {strategies.valuation && (
        <div className="valuation-analysis">
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '20px' }}>
            <h3 style={{ margin: 0 }}>📊 Valuation Analysis</h3>
            <div style={{
              padding: '8px 16px',
              background: strategies.valuation.status === 'discount' ? '#10b981' :
                         strategies.valuation.status === 'premium' ? '#ef4444' : '#f59e0b',
              borderRadius: '20px',
              fontWeight: '700',
              fontSize: '14px',
              color: 'white',
              textAlign: 'center'
            }}>
              {strategies.valuation.overallAssessment}
            </div>
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '15px', marginBottom: '20px' }}>
            {strategies.valuation.peRatio && (
              <div className="valuation-metric-card">
                <div className="valuation-metric-label">P/E RATIO</div>
                <div className="valuation-metric-value">{strategies.valuation.peRatio}</div>
                <div className="valuation-metric-sublabel">
                  {strategies.valuation.peRatio < 15 ? '📉 Low (Value)' : strategies.valuation.peRatio > 30 ? '📈 High (Growth)' : '➡️ Moderate'}
                </div>
              </div>
            )}
            {strategies.valuation.pbRatio && (
              <div className="valuation-metric-card">
                <div className="valuation-metric-label">P/B RATIO</div>
                <div className="valuation-metric-value">{strategies.valuation.pbRatio}</div>
                <div className="valuation-metric-sublabel">
                  {strategies.valuation.pbRatio < 1 ? '💎 Undervalued' : strategies.valuation.pbRatio > 3 ? '💰 Expensive' : '✓ Fair'}
                </div>
              </div>
            )}
            {strategies.valuation.rangePosition !== null && (
              <div className="valuation-metric-card">
                <div className="valuation-metric-label">52-WEEK POSITION</div>
                <div className="valuation-metric-value">{strategies.valuation.rangePosition}%</div>
                <div className="valuation-metric-sublabel">
                  Range: ${strategies.valuation.fiftyTwoWeekLow} - ${strategies.valuation.fiftyTwoWeekHigh}
                </div>
              </div>
            )}
            {strategies.valuation.upsidePotential !== null && (
              <div className="valuation-metric-card">
                <div className="valuation-metric-label">ANALYST TARGET</div>
                <div className="valuation-metric-value" style={{
                  color: strategies.valuation.upsidePotential > 0 ? '#10b981' : '#ef4444'
                }}>
                  {strategies.valuation.upsidePotential > 0 ? '+' : ''}{strategies.valuation.upsidePotential}%
                </div>
                <div className="valuation-metric-sublabel">
                  Target Price: ${strategies.valuation.targetPrice}
                </div>
              </div>
            )}
          </div>

          {strategies.valuation.signals && strategies.valuation.signals.length > 0 && (
            <div style={{
              padding: '15px',
              background: '#f8fafc',
              borderRadius: '8px',
              border: '1px solid #e2e8f0'
            }}>
              <div style={{ fontSize: '14px', fontWeight: '600', color: '#64748b', marginBottom: '10px' }}>
                Valuation Signals:
              </div>
              <ul style={{ margin: 0, paddingLeft: '20px', fontSize: '14px', color: '#475569', lineHeight: '1.8' }}>
                {strategies.valuation.signals.map((signal, idx) => (
                  <li key={idx}>{signal}</li>
                ))}
              </ul>
            </div>
          )}
        </div>
      )}

      {/* Risk Disclosure */}
      <div className="risk-disclosure">
        <h3>⚠️ Risk Disclosure</h3>
        <ul>
          <li>Past performance does not guarantee future results</li>
          <li>All strategies involve risk of loss</li>
          <li>Backtested results may not reflect actual trading conditions</li>
          <li>Consider your risk tolerance and investment goals</li>
          <li>This is for educational purposes only - not investment advice</li>
          <li>Consult a financial advisor before making investment decisions</li>
        </ul>
      </div>
    </div>
  );
}

export default StrategyAnalysis;
