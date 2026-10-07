import { useCallback, useEffect, useRef, useState } from 'react';
import { extractErrorMessage } from '../services/api';

function areEqual(left, right) {
  if (Object.is(left, right)) return true;
  if (left === null || right === null || typeof left !== 'object' || typeof right !== 'object') {
    return false;
  }

  if (left instanceof Date || right instanceof Date) {
    return left instanceof Date && right instanceof Date && left.getTime() === right.getTime();
  }

  if (Array.isArray(left) !== Array.isArray(right)) return false;

  const leftPrototype = Object.getPrototypeOf(left);
  const rightPrototype = Object.getPrototypeOf(right);
  if (leftPrototype !== rightPrototype || (leftPrototype !== Object.prototype && leftPrototype !== null && !Array.isArray(left))) {
    return false;
  }

  const leftKeys = Object.keys(left);
  const rightKeys = Object.keys(right);
  return leftKeys.length === rightKeys.length
    && leftKeys.every((key) => Object.hasOwn(right, key) && areEqual(left[key], right[key]));
}

/**
 * Runs a fetcher and exposes the three states every screen needs: loading, error and data,
 * plus a reload for the retry button. Keeps the loading and error handling identical
 * everywhere instead of repeating a try/catch in each page.
 *
 * @param fetcher the latest fetch function; its identity alone does not trigger a request.
 * @param deps    re-runs the fetcher when these values change, like useEffect's dependencies.
 */
export function useApiResource(fetcher, deps = []) {
  const [data, setData] = useState(null);
  const [error, setError] = useState(null);
  const [errorStatus, setErrorStatus] = useState(null);
  const [loading, setLoading] = useState(true);
  const [reloading, setReloading] = useState(false);

  // A late response from a superseded request must not overwrite the current one.
  const requestId = useRef(0);
  const fetcherRef = useRef(fetcher);
  const dataRef = useRef(data);
  fetcherRef.current = fetcher;

  const updateData = useCallback((nextData) => {
    const resolvedData = typeof nextData === 'function' ? nextData(dataRef.current) : nextData;
    if (areEqual(dataRef.current, resolvedData)) return;

    dataRef.current = resolvedData;
    setData(resolvedData);
  }, []);

  const run = useCallback(
    async ({ isReload = false } = {}) => {
      const id = ++requestId.current;

      if (isReload) setReloading(true);
      else setLoading(true);
      setError(null);
      setErrorStatus(null);

      try {
        const result = await fetcherRef.current();
        if (id === requestId.current) updateData(result);
        return result;
      } catch (err) {
        if (id === requestId.current) {
          setError(err.friendlyMessage ?? extractErrorMessage(err));
          setErrorStatus(err.status ?? err.response?.status ?? null);
        }
        return null;
      } finally {
        if (id === requestId.current) {
          setLoading(false);
          setReloading(false);
        }
      }
    },
    // fetcherRef tracks the latest function; deps are the explicit refetch triggers.
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [...deps, updateData],
  );

  useEffect(() => {
    run();
    // Cancels any in-flight result on unmount by invalidating the current request id.
    return () => { requestId.current += 1; };
  }, [run]);

  const reload = useCallback(() => run({ isReload: true }), [run]);

  return {
    data,
    error,
    errorStatus,
    loading,
    reloading,
    reload,
    setData: updateData,
  };
}
