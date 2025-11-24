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
};

export default api;
