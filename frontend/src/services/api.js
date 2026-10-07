import axios from 'axios';

const BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5006';
export const API_BASE_URL = BASE_URL;

export const STORAGE_KEYS = {
  accessToken: 'halade.accessToken',
  refreshToken: 'halade.refreshToken',
  user: 'halade.user',
};

/* ---------------------------------------------------------------------------
   Token storage
   Kept in localStorage so a reload keeps the session. The refresh token is
   rotated by the API on every use, so a stolen copy stops working as soon as
   the legitimate client refreshes.
   --------------------------------------------------------------------------- */
export const tokenStore = {
  getAccessToken: () =>
    localStorage.getItem(STORAGE_KEYS.accessToken) ?? localStorage.getItem('token'),
  getRefreshToken: () => localStorage.getItem(STORAGE_KEYS.refreshToken),

  getUser() {
    const raw = localStorage.getItem(STORAGE_KEYS.user);
    if (!raw) return null;
    try {
      return JSON.parse(raw);
    } catch {
      return null;
    }
  },

  save({ accessToken, refreshToken, user }) {
    if (accessToken) localStorage.setItem(STORAGE_KEYS.accessToken, accessToken);
    if (refreshToken) localStorage.setItem(STORAGE_KEYS.refreshToken, refreshToken);
    if (user) localStorage.setItem(STORAGE_KEYS.user, JSON.stringify(user));
  },

  clear() {
    Object.values(STORAGE_KEYS).forEach((key) => localStorage.removeItem(key));
    localStorage.removeItem('token');
  },
};

/* ---------------------------------------------------------------------------
   Session expiry notification
   api.js cannot import the router or the auth context without creating a cycle,
   so AuthContext subscribes here and decides what to do when a session dies.
   --------------------------------------------------------------------------- */
const sessionExpiredHandlers = new Set();

export function onSessionExpired(handler) {
  sessionExpiredHandlers.add(handler);
  return () => sessionExpiredHandlers.delete(handler);
}

function notifySessionExpired() {
  sessionExpiredHandlers.forEach((handler) => {
    try {
      handler();
    } catch {
      // A misbehaving subscriber must not stop the others from being told.
    }
  });
}

const api = axios.create({
  baseURL: BASE_URL,
  headers: { 'Content-Type': 'application/json' },
  withCredentials: true,
  timeout: 30000,
});

/** Bare client for refreshing, so the interceptors below cannot recurse. */
const refreshClient = axios.create({ baseURL: BASE_URL, withCredentials: true, timeout: 30000 });

api.interceptors.request.use((config) => {
  const token = tokenStore.getAccessToken();
  if (token) {
    config.headers = config.headers ?? {};
    config.headers.Authorization = `Bearer ${token}`;
  }
  if (typeof FormData !== 'undefined' && config.data instanceof FormData) {
    if (typeof config.headers?.delete === 'function') {
      config.headers.delete('Content-Type');
    } else if (config.headers) {
      delete config.headers['Content-Type'];
      delete config.headers['content-type'];
    }
  }
  return config;
});

/* One refresh at a time. Without this, a dashboard that fires six requests on
   mount would send six refreshes, and rotation would invalidate five of them. */
let refreshPromise = null;

function refreshAccessToken() {
  if (refreshPromise) return refreshPromise;

  const refreshToken = tokenStore.getRefreshToken();
  if (!refreshToken) return Promise.reject(new Error('No refresh token stored.'));

  refreshPromise = refreshClient
    .post('/api/auth/refresh', { refreshToken })
    .then(({ data }) => {
      tokenStore.save({
        accessToken: data.accessToken,
        refreshToken: data.refreshToken,
        user: data.user,
      });
      return data.accessToken;
    })
    .finally(() => {
      refreshPromise = null;
    });

  return refreshPromise;
}

api.interceptors.response.use(
  (response) => response,
  async (error) => {
    const { response, config } = error;

    if (!response) {
      // No response at all: the API is down, blocked by CORS, or the request timed out.
      return Promise.reject(
        Object.assign(error, {
          friendlyMessage:
            'Cannot reach the server. Check that the API is running on ' + BASE_URL + '.',
        }),
      );
    }

    const problem = sanitizeProblemDetails(error);
    if (shouldLogProblem(response.status)) {
      console.error(formatProblemLog(config, problem));
    }

    // Login and other anonymous auth endpoints must surface their own 401. A protected
    // endpoint such as /api/auth/me must still use the refresh-and-expire flow.
    const anonymousAuthPaths = new Set([
      '/api/auth/login',
      '/api/auth/refresh',
      '/api/auth/forgot-password',
      '/api/auth/reset-password',
      '/api/auth/register-student',
      '/api/auth/register-student-with-photo',
    ]);
    const requestPath = config?.url?.split('?')[0];
    const isAnonymousAuthCall = anonymousAuthPaths.has(requestPath);

    if (response.status === 401 && !config._retried && !isAnonymousAuthCall) {
      config._retried = true;

      try {
        const token = await refreshAccessToken();
        config.headers = { ...config.headers, Authorization: `Bearer ${token}` };
        return api(config);
      } catch {
        tokenStore.clear();
        notifySessionExpired();
        return Promise.reject(
          Object.assign(error, { friendlyMessage: 'Your session has expired. Please sign in again.' }),
        );
      }
    }

    if (response.status === 401 && isAnonymousAuthCall) {
      return Promise.reject(
        Object.assign(error, { friendlyMessage: extractErrorMessage(error) }),
      );
    }

    if (response.status === 403) {
      return Promise.reject(
        { ...problem, friendlyMessage: problem.message ?? 'You do not have permission to perform this action.' },
      );
    }

    return Promise.reject(problem);
  },
);

function shouldLogProblem(status) {
  return status === 400 || status === 404 || status === 409 || status >= 500;
}

function getRequestUrl(config) {
  if (!config?.url) return 'unknown URL';
  if (!config.baseURL) return config.url;

  try {
    return new URL(config.url, config.baseURL).toString();
  } catch {
    return `${config.baseURL}${config.url}`;
  }
}

function getValidationMessages(data) {
  if (!data?.errors || typeof data.errors !== 'object') return [];

  return Object.entries(data.errors)
    .flatMap(([field, values]) => (Array.isArray(values) ? values : [values]).map((value) => ({ field, value })))
    .map(({ field, value }) => {
      const message = typeof value === 'string' ? value : value?.message;
      return message ? `${field}: ${message}` : null;
    })
    .filter(Boolean);
}

/** Converts ASP.NET ProblemDetails/ValidationProblemDetails into a safe UI error contract. */
export function sanitizeProblemDetails(error) {
  const data = error?.response?.data;
  const validationMessages = getValidationMessages(data);
  const detail = typeof data === 'string' ? data : data?.detail ?? null;
  const title = typeof data === 'object' ? data?.title ?? null : null;
  const message = detail ?? (validationMessages.length ? validationMessages.join(' ') : null) ?? title ?? error?.message ?? 'Something went wrong.';

  return {
    status: error?.response?.status ?? null,
    message,
    title,
    detail,
    traceId: typeof data === 'object' ? data?.traceId ?? null : null,
    friendlyMessage: message,
  };
}

function formatProblemLog(config, problem) {
  const method = (config?.method ?? 'unknown').toUpperCase();
  const url = getRequestUrl(config);
  const trace = problem.traceId ? ` [traceId: ${problem.traceId}]` : '';
  return `[API ${problem.status}] ${method} ${url} - ${problem.message}${trace}`;
}

/**
 * Flattens the API's ProblemDetails and ValidationProblemDetails shapes into one
 * readable sentence, so every screen can show `err.friendlyMessage` and be done.
 */
export function extractErrorMessage(error) {
  if (error?.message && !error.response) return error.message;
  const data = error?.response?.data;

  if (!data) return error?.message ?? 'Something went wrong.';
  if (typeof data === 'string') return data;

  if (data.errors && typeof data.errors === 'object') {
    const messages = Object.values(data.errors)
      .flatMap((value) => Array.isArray(value) ? value : [value])
      .filter(Boolean)
      .map((message) => typeof message === 'string' ? message : message.message)
      .filter(Boolean);
    if (messages.length) return messages.join(' ');
  }

  return data.detail ?? data.message ?? data.error ?? data.title ?? error.message ?? 'Something went wrong.';
}

export { BASE_URL };
export default api;
