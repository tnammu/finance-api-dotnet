import axios from 'axios';

const API_BASE_URL = 'http://localhost:5000/api';

const api = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
});

// Stocks API
export const stocksAPI = {
  getAll: () => api.get('/stocks'),
  getById: (id) => api.get(`/stocks/${id}`),
  getLive: (symbol) => api.get(`/stocks/live/${symbol}`),
  add: (symbol) => api.post('/stocks', { symbol }),
  delete: (id) => api.delete(`/stocks/${id}`),
  refresh: (id) => api.post(`/stocks/refresh/${id}`),
  refreshAll: () => api.post('/stocks/refresh'),
};

// Dividends API
export const dividendsAPI = {
  analyze: (symbol, refresh = false) => api.get(`/dividends/analyze/${symbol}?refresh=${refresh}`),
  add: (symbol) => api.post('/dividends', { symbol }),
  getCharts: (symbol) => api.get(`/dividends/${symbol}/charts`),
  getCached: () => api.get('/dividends'),
  getHistory: (symbol) => api.get(`/dividends/${symbol}/history`),
  screenCanadian: (symbols) => api.post('/dividends/screen', symbols, { params: { market: 'canadian' } }),
  getUsageToday: () => api.get('/dividends/api-usage'),
  getUsageHistory: (days) => api.get('/dividends/api-usage', { params: { days } }),
  getStats: () => api.get('/dividends/analytics'),
  deleteCached: (symbol) => api.delete(`/dividends/${symbol}`),
  bulkImportTSX: () => api.post('/dividends/bulk-import/tsx'),
};

// Strategy API
export const strategyAPI = {
  getList: () => api.get('/strategy/list'),
  analyzeAll: (symbol, capital = 100, years = 5, enforceBuyFirst = true) =>
    api.get(`/strategy/analyze/${symbol}?capital=${capital}&years=${years}&enforceBuyFirst=${enforceBuyFirst}`),
  analyzeSingle: (symbol, strategyType, capital = 100, years = 5) =>
    api.get(`/strategy/single/${symbol}/${strategyType}?capital=${capital}&years=${years}`),
  calculate: (symbol, strategyType, amounts, years = 5) =>
    api.post('/strategy/calculator', { symbol, strategyType, amounts, years }),
};

// Sector API
export const sectorAPI = {
  getPerformances: (refresh = false) => api.get(`/sector/performances?refresh=${refresh}`),
  getSectorPerformance: (sector, period = 'current') =>
    api.get(`/sector/performance/${sector}?period=${period}`),
  getStockComparison: (symbol) => api.get(`/sector/comparison/${symbol}`),
  refreshStockComparison: (symbol) => api.post(`/sector/comparison/${symbol}/refresh`),
  getTopPerformers: (sector, limit = 10) =>
    api.get(`/sector/top-performers/${sector}?limit=${limit}`),
  getSummary: () => api.get('/sector/summary'),
};

export default api;
