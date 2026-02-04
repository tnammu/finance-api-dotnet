import React, { useState } from 'react';
import axios from 'axios';
import { Treemap, ResponsiveContainer, Tooltip } from 'recharts';
import './EtfAnalysis.css';

const EtfAnalysis = () => {
  const [inputSymbol, setInputSymbol] = useState('');
  const [etfList, setEtfList] = useState([]);
  const [loading, setLoading] = useState(false);
  const [etfData, setEtfData] = useState([]);
  const [error, setError] = useState('');

  const addEtf = () => {
    if (!inputSymbol.trim()) {
      setError('Please enter an ETF symbol');
      return;
    }

    // Support both single symbol and comma-separated symbols
    const symbols = inputSymbol
      .split(',')
      .map(s => s.trim().toUpperCase())
      .filter(s => s.length > 0);

    const newSymbols = symbols.filter(s => !etfList.includes(s));
    const duplicates = symbols.filter(s => etfList.includes(s));

    if (newSymbols.length > 0) {
      setEtfList([...etfList, ...newSymbols]);
      setInputSymbol('');
      setError('');

      if (duplicates.length > 0) {
        setError(`Added ${newSymbols.join(', ')}. Already in list: ${duplicates.join(', ')}`);
      }
    } else {
      setError(`${symbols.join(', ')} already in the list`);
    }
  };

  const removeEtf = (symbol) => {
    setEtfList(etfList.filter(s => s !== symbol));
  };

  const analyzeEtfs = async () => {
    if (etfList.length === 0) {
      setError('Please add at least one ETF symbol');
      return;
    }

    setLoading(true);
    setError('');

    try {
      const response = await axios.post('http://localhost:5000/api/etf/analyze', {
        symbols: etfList
      });

      if (response.data.success) {
        setEtfData(response.data.etfs);
      } else {
        setError('Failed to fetch ETF data');
      }
    } catch (err) {
      setError(err.response?.data?.error || 'Error fetching ETF data');
      console.error('Error:', err);
    } finally {
      setLoading(false);
    }
  };

  const handleKeyDown = (e) => {
    if (e.key === 'Enter') {
      addEtf();
    }
  };

  // Transform ETF data into treemap format
  const getTreemapData = () => {
    if (!etfData || etfData.length === 0) return [];

    const children = [];

    etfData.forEach(etf => {
      if (etf.success && etf.holdings && etf.holdings.length > 0) {
        // Group holdings by ETF
        etf.holdings.forEach(holding => {
          children.push({
            name: holding.symbol,
            fullName: holding.name,
            size: holding.weight,
            etf: etf.symbol,
            etfName: etf.name,
            weight: holding.weight,
            sector: holding.sector || 'Unknown',
            currentPrice: holding.currentPrice,
            priceChange: holding.priceChange,
            percentChange: holding.percentChange
          });
        });
      }
    });

    return children.length > 0 ? [{ name: 'ETF Holdings', children }] : [];
  };

  // Generate unique color for each ETF based on its symbol
  const getColor = (etfSymbol) => {
    // Safety check for undefined/null values
    if (!etfSymbol) {
      return '#17becf'; // Default color
    }

    // Better hash function to avoid collisions
    const stringToHash = (str) => {
      let hash = 0;
      for (let i = 0; i < str.length; i++) {
        const char = str.charCodeAt(i);
        hash = ((hash << 5) - hash) + char;
        hash = hash & hash; // Convert to 32bit integer
      }
      return Math.abs(hash);
    };

    // Generate multiple hash values for better distribution
    const hash1 = stringToHash(etfSymbol);
    const hash2 = stringToHash(etfSymbol + 'salt');
    const hash3 = stringToHash(etfSymbol.split('').reverse().join(''));

    // Use different hashes for hue, saturation, lightness to maximize uniqueness
    const hue = hash1 % 360;
    const saturation = 65 + (hash2 % 20); // 65-85%
    const lightness = 45 + (hash3 % 15);  // 45-60%

    return `hsl(${hue}, ${saturation}%, ${lightness}%)`;
  };

  const CustomizedContent = (props) => {
    const { x, y, width, height, name, etf, weight } = props;

    if (width < 30 || height < 30) return null;

    return (
      <g>
        <rect
          x={x}
          y={y}
          width={width}
          height={height}
          style={{
            fill: getColor(etf),
            stroke: '#fff',
            strokeWidth: 2,
            opacity: 0.8
          }}
        />
        {width > 50 && height > 30 && (
          <>
            <text
              x={x + width / 2}
              y={y + height / 2 - 5}
              textAnchor="middle"
              fill="#fff"
              fontSize={Math.min(width / 6, height / 4, 14)}
              fontWeight="bold"
            >
              {name}
            </text>
            <text
              x={x + width / 2}
              y={y + height / 2 + 15}
              textAnchor="middle"
              fill="#fff"
              fontSize={Math.min(width / 8, height / 5, 11)}
            >
              {weight?.toFixed(2)}%
            </text>
          </>
        )}
      </g>
    );
  };

  const CustomTooltip = ({ active, payload }) => {
    if (active && payload && payload.length) {
      const data = payload[0].payload;
      return (
        <div className="treemap-tooltip">
          <p className="stock-symbol">{data.name}</p>
          <p className="stock-name">{data.fullName}</p>
          <p className="stock-etf">ETF: {data.etf} ({data.etfName})</p>
          <p className="stock-weight">Weight: {data.weight?.toFixed(2)}%</p>
          {data.priceChange !== undefined && data.percentChange !== undefined ? (
            <p className={`stock-price-change ${data.priceChange >= 0 ? 'positive' : 'negative'}`}>
              Today: {data.priceChange >= 0 ? '+' : ''}{data.priceChange.toFixed(2)} ({data.percentChange >= 0 ? '+' : ''}{data.percentChange.toFixed(2)}%)
            </p>
          ) : null}
          <p className="stock-sector">Sector: {data.sector}</p>
        </div>
      );
    }
    return null;
  };

  // Get summary statistics
  const getSummary = () => {
    const totalEtfs = etfData.filter(e => e.success).length;
    const totalHoldings = etfData.reduce((sum, etf) =>
      etf.success ? sum + (etf.holdings?.length || 0) : sum, 0);
    const uniqueStocks = new Set();

    etfData.forEach(etf => {
      if (etf.success && etf.holdings) {
        etf.holdings.forEach(h => uniqueStocks.add(h.symbol));
      }
    });

    return { totalEtfs, totalHoldings, uniqueStocks: uniqueStocks.size };
  };

  // Find common holdings across multiple ETFs
  const getCommonHoldings = () => {
    if (!etfData || etfData.length < 2) return [];

    const successfulEtfs = etfData.filter(e => e.success && e.holdings && e.holdings.length > 0);
    if (successfulEtfs.length < 2) return [];

    // Create a map of stock symbol to ETFs that hold it
    const stockToEtfs = new Map();

    successfulEtfs.forEach(etf => {
      etf.holdings.forEach(holding => {
        if (!stockToEtfs.has(holding.symbol)) {
          stockToEtfs.set(holding.symbol, []);
        }
        stockToEtfs.get(holding.symbol).push({
          etfSymbol: etf.symbol,
          etfName: etf.name,
          weight: holding.weight,
          stockName: holding.name,
          currentPrice: holding.currentPrice,
          priceChange: holding.priceChange,
          percentChange: holding.percentChange
        });
      });
    });

    // Filter to only stocks held by 2+ ETFs
    const commonHoldings = [];
    stockToEtfs.forEach((etfs, stockSymbol) => {
      if (etfs.length >= 2) {
        // Calculate average weight across ETFs
        const avgWeight = etfs.reduce((sum, e) => sum + e.weight, 0) / etfs.length;

        commonHoldings.push({
          symbol: stockSymbol,
          name: etfs[0].stockName,
          etfs: etfs,
          etfCount: etfs.length,
          avgWeight: avgWeight,
          currentPrice: etfs[0].currentPrice,
          priceChange: etfs[0].priceChange,
          percentChange: etfs[0].percentChange
        });
      }
    });

    // Sort by number of ETFs holding it, then by average weight
    commonHoldings.sort((a, b) => {
      if (b.etfCount !== a.etfCount) {
        return b.etfCount - a.etfCount;
      }
      return b.avgWeight - a.avgWeight;
    });

    return commonHoldings;
  };

  // Get individual stocks that appear in only ONE ETF (not in common holdings)
  const getAllStocks = () => {
    if (!etfData || etfData.length === 0) return [];

    const successfulEtfs = etfData.filter(e => e.success && e.holdings && e.holdings.length > 0);
    const stockToEtfs = new Map();

    successfulEtfs.forEach(etf => {
      etf.holdings.forEach(holding => {
        if (!stockToEtfs.has(holding.symbol)) {
          stockToEtfs.set(holding.symbol, []);
        }
        stockToEtfs.get(holding.symbol).push({
          etfSymbol: etf.symbol,
          etfName: etf.name,
          weight: holding.weight,
          stockName: holding.name,
          currentPrice: holding.currentPrice,
          priceChange: holding.priceChange,
          percentChange: holding.percentChange
        });
      });
    });

    // Only include stocks that appear in exactly ONE ETF
    const individualStocks = [];
    stockToEtfs.forEach((etfs, stockSymbol) => {
      if (etfs.length === 1) {
        individualStocks.push({
          symbol: stockSymbol,
          name: etfs[0].stockName,
          etfs: etfs,
          etfCount: 1,
          totalWeight: etfs[0].weight,
          avgWeight: etfs[0].weight,
          currentPrice: etfs[0].currentPrice,
          priceChange: etfs[0].priceChange,
          percentChange: etfs[0].percentChange
        });
      }
    });

    // Sort by weight descending
    individualStocks.sort((a, b) => b.totalWeight - a.totalWeight);

    return individualStocks;
  };

  const summary = getSummary();
  const treemapData = getTreemapData();
  const commonHoldings = getCommonHoldings();
  const allStocks = getAllStocks();

  return (
    <div className="etf-analysis-container">
      <h2>ETF Holdings Analysis</h2>
      <p className="subtitle">Visualize which stocks your ETFs are investing in</p>

      <div className="input-section">
        <div className="input-group">
          <input
            type="text"
            placeholder="Enter ETF symbol (e.g., QQQ or QQQ, VGT, DIA)"
            value={inputSymbol}
            onChange={(e) => setInputSymbol(e.target.value)}
            onKeyDown={handleKeyDown}
            className="etf-input"
          />
          <button
            onClick={addEtf}
            className="add-button"
          >
            Add
          </button>
          <button
            onClick={analyzeEtfs}
            disabled={loading || etfList.length === 0}
            className="analyze-button"
          >
            {loading ? 'Analyzing...' : 'Analyze'}
          </button>
        </div>
        <p className="hint">Enter one symbol or comma-separated symbols, then click Add. Press Enter to add quickly.</p>

        {etfList.length > 0 && (
          <div className="etf-list">
            <div className="etf-list-header">ETFs to analyze ({etfList.length}):</div>
            <div className="etf-chips">
              {etfList.map((symbol, idx) => (
                <div key={idx} className="etf-chip">
                  <span className="etf-chip-symbol">{symbol}</span>
                  <button
                    onClick={() => removeEtf(symbol)}
                    className="etf-chip-remove"
                    title={`Remove ${symbol}`}
                  >
                    ×
                  </button>
                </div>
              ))}
            </div>
          </div>
        )}
      </div>

      {error && (
        <div className="error-message">
          <span>⚠️ {error}</span>
        </div>
      )}

      {etfData.length > 0 && (
        <>
          <div className="summary-cards">
            <div className="summary-card">
              <div className="summary-value">{summary.totalEtfs}</div>
              <div className="summary-label">ETFs Analyzed</div>
            </div>
            <div className="summary-card">
              <div className="summary-value">{summary.uniqueStocks}</div>
              <div className="summary-label">Unique Stocks</div>
            </div>
            <div className="summary-card">
              <div className="summary-value">{summary.totalHoldings}</div>
              <div className="summary-label">Total Holdings</div>
            </div>
          </div>

          {treemapData.length > 0 ? (
            <div className="treemap-container">
              <h3>Holdings Treemap</h3>
              <ResponsiveContainer width="100%" height={600}>
                <Treemap
                  data={treemapData}
                  dataKey="size"
                  stroke="#fff"
                  fill="#8884d8"
                  content={<CustomizedContent />}
                >
                  <Tooltip content={<CustomTooltip />} />
                </Treemap>
              </ResponsiveContainer>

              <div className="legend">
                {etfData
                  .filter(e => e.success)
                  .map(etf => (
                    <div key={etf.symbol} className="legend-item">
                      <span
                        className="legend-color"
                        style={{ backgroundColor: getColor(etf.symbol) }}
                      />
                      <span className="legend-text">
                        {etf.symbol} - {etf.name} ({etf.holdings?.length || 0} holdings)
                      </span>
                    </div>
                  ))}
              </div>
            </div>
          ) : (
            <div className="no-data-message">
              <h3>⚠️ No Holdings Data Available</h3>
              <p>Unable to fetch holdings data for the selected ETFs.</p>
              <p><strong>Possible reasons:</strong></p>
              <ul>
                <li>ETF data not available in Yahoo Finance API (common for Canadian ETFs)</li>
                <li>Web scraping blocked by websites</li>
                <li>ETF symbol might be incorrect</li>
              </ul>
              <p><strong>Try these US ETFs instead:</strong> QQQ, SPY, VTI, VGT, DIA</p>
            </div>
          )}

          {commonHoldings.length > 0 && (
            <div className="common-holdings-section">
              <h3>Common Holdings Across ETFs</h3>
              <p className="section-subtitle">Stocks held by multiple ETFs in your selection</p>

              <div className="common-holdings-table">
                <table>
                  <thead>
                    <tr>
                      <th>Stock</th>
                      <th>Name</th>
                      <th>Today's Change</th>
                      <th>In {summary.totalEtfs} ETFs</th>
                      <th>Average Weight</th>
                      <th>Details</th>
                    </tr>
                  </thead>
                  <tbody>
                    {commonHoldings.map((holding, idx) => (
                      <tr key={idx}>
                        <td className="stock-symbol-cell">{holding.symbol}</td>
                        <td className="stock-name-cell">{holding.name}</td>
                        <td className="price-change-cell">
                          {holding.priceChange !== undefined && holding.percentChange !== undefined ? (
                            <div className={`price-change ${holding.priceChange >= 0 ? 'positive' : 'negative'}`}>
                              <div className="price-value">
                                {holding.priceChange >= 0 ? '+' : ''}{holding.priceChange.toFixed(2)}
                              </div>
                              <div className="percent-value">
                                ({holding.percentChange >= 0 ? '+' : ''}{holding.percentChange.toFixed(2)}%)
                              </div>
                            </div>
                          ) : (
                            <span className="no-data">N/A</span>
                          )}
                        </td>
                        <td className="etf-count-cell">
                          <span className="badge">{holding.etfCount} / {summary.totalEtfs}</span>
                        </td>
                        <td className="avg-weight-cell">{holding.avgWeight.toFixed(2)}%</td>
                        <td className="details-cell">
                          <div className="etf-weights">
                            {holding.etfs.map((etf, i) => (
                              <div key={i} className="etf-weight-item">
                                <span
                                  className="etf-color-indicator"
                                  style={{ backgroundColor: getColor(etf.etfSymbol) }}
                                />
                                <span className="etf-symbol-text">{etf.etfSymbol}:</span>
                                <span className="weight-text">{etf.weight.toFixed(2)}%</span>
                              </div>
                            ))}
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )}

          {allStocks.length > 0 && (
            <div className="all-stocks-section">
              <h3>Individual Holdings</h3>
              <p className="section-subtitle">Stocks that appear in only one ETF (sorted by weight)</p>

              <div className="all-stocks-table">
                <table>
                  <thead>
                    <tr>
                      <th>Stock</th>
                      <th>Name</th>
                      <th>Today's Change</th>
                      <th>Weight</th>
                      <th>ETF</th>
                    </tr>
                  </thead>
                  <tbody>
                    {allStocks.map((stock, idx) => (
                      <tr key={idx}>
                        <td className="stock-symbol-cell">{stock.symbol}</td>
                        <td className="stock-name-cell">{stock.name}</td>
                        <td className="price-change-cell">
                          {stock.priceChange !== undefined && stock.percentChange !== undefined ? (
                            <div className={`price-change ${stock.priceChange >= 0 ? 'positive' : 'negative'}`}>
                              <div className="price-value">
                                {stock.priceChange >= 0 ? '+' : ''}{stock.priceChange.toFixed(2)}
                              </div>
                              <div className="percent-value">
                                ({stock.percentChange >= 0 ? '+' : ''}{stock.percentChange.toFixed(2)}%)
                              </div>
                            </div>
                          ) : (
                            <span className="no-data">N/A</span>
                          )}
                        </td>
                        <td className="total-weight-cell">{stock.totalWeight.toFixed(2)}%</td>
                        <td className="details-cell">
                          <div className="etf-weights">
                            {stock.etfs.map((etf, i) => (
                              <div key={i} className="etf-weight-item">
                                <span
                                  className="etf-color-indicator"
                                  style={{ backgroundColor: getColor(etf.etfSymbol) }}
                                />
                                <span className="etf-symbol-text">{etf.etfSymbol}</span>
                              </div>
                            ))}
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )}

          {etfData.some(e => !e.success) && (
            <div className="error-section">
              <h4>Errors:</h4>
              {etfData
                .filter(e => !e.success)
                .map((etf, idx) => (
                  <div key={idx} className="error-item">
                    {etf.symbol}: {etf.error}
                  </div>
                ))}
            </div>
          )}
        </>
      )}
    </div>
  );
};

export default EtfAnalysis;
