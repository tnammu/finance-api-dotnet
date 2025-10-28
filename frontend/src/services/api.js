import axios from 'axios';

const API_BASE_URL = 'http://localhost:5199/api';

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
  getCached: () => api.get('/dividends/cached'),
  getHistory: (symbol) => api.get(`/dividends/history/${symbol}`),
  screenCanadian: (symbols) => api.post('/dividends/screen/canadian', symbols),
  getUsageToday: () => api.get('/dividends/usage/today'),
  getUsageHistory: () => api.get('/dividends/usage/history'),
  getStats: () => api.get('/dividends/stats'),
  deleteCached: (symbol) => api.delete(`/dividends/cached/${symbol}`),
};

export default api;
