// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * Raycast React hooks compatibility stubs.
 *
 * These are hooks that Raycast extensions commonly import from `@raycast/api`.
 * They're not part of the React reconciler but rather imperative utilities
 * that extensions call from within their components.
 */

import { useState, useCallback, useEffect, useRef } from 'react';

/**
 * Raycast's useNavigation hook — provides push/pop navigation.
 * In CmdPal, navigation is handled declaratively. This stub provides
 * the interface shape so extensions don't crash.
 */
export function useNavigation(): { push: (component: React.ReactNode) => void; pop: () => void } {
  return {
    push: (_component: React.ReactNode) => {
      console.warn('[useNavigation] push() is not yet supported in CmdPal compat layer');
    },
    pop: () => {
      console.warn('[useNavigation] pop() is not yet supported in CmdPal compat layer');
    },
  };
}

/**
 * Raycast's useCachedPromise — executes an async function and caches the result.
 * Simplified implementation for compatibility.
 */
export function useCachedPromise<T>(
  fn: (...args: unknown[]) => Promise<T>,
  args?: unknown[],
  options?: { initialData?: T; keepPreviousData?: boolean; execute?: boolean },
): { data: T | undefined; isLoading: boolean; error: Error | undefined; revalidate: () => void; mutate: (data?: T) => void; pagination?: { hasMore: boolean; pageSize: number } } {
  const [data, setData] = useState<T | undefined>(options?.initialData);
  const [isLoading, setIsLoading] = useState(options?.execute !== false);
  const [error, setError] = useState<Error | undefined>();
  const mountedRef = useRef(true);
  // Use refs for fn/args to avoid re-render loops from changing identity
  const fnRef = useRef(fn);
  fnRef.current = fn;
  const argsRef = useRef(args);
  argsRef.current = args;
  // Serialize args to a stable string for the effect dependency
  const argsKey = JSON.stringify(args ?? []);
  const shouldExecute = options?.execute !== false;

  const execute = useCallback(async () => {
    if (!mountedRef.current) return;
    setIsLoading(true);
    setError(undefined);
    try {
      const fnResult = await fnRef.current(...(argsRef.current ?? []));
      let finalData: T;
      // Raycast pagination pattern: fn(args) returns an async function
      // that expects { cursor, page }. Call it with initial page params.
      if (typeof fnResult === 'function') {
        const pageResult = await (fnResult as (opts: { cursor?: string; page: number }) => Promise<unknown>)({ cursor: undefined, page: 0 });
        if (pageResult && typeof pageResult === 'object' && 'data' in (pageResult as Record<string, unknown>)) {
          finalData = (pageResult as { data: T }).data;
        } else {
          finalData = pageResult as T;
        }
      } else {
        finalData = fnResult as T;
      }
      if (mountedRef.current) {
        setData(finalData);
      }
    } catch (err) {
      if (mountedRef.current) {
        setError(err instanceof Error ? err : new Error(String(err)));
      }
    } finally {
      if (mountedRef.current) {
        setIsLoading(false);
      }
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [argsKey]);

  useEffect(() => {
    mountedRef.current = true;
    if (shouldExecute) {
      void execute();
    }
    return () => { mountedRef.current = false; };
  }, [execute, shouldExecute]);

  return {
    data,
    isLoading,
    error,
    revalidate: () => void execute(),
    mutate: (newData?: T) => { if (newData !== undefined) setData(newData); },
    pagination: { hasMore: false, pageSize: 20 },
  };
}

/**
 * Raycast's useFetch — simplified fetch with caching.
 * Delegates to useCachedPromise with a fetch wrapper.
 */
export function useFetch<T = unknown>(
  url: string | ((...args: unknown[]) => string),
  options?: {
    method?: string;
    headers?: Record<string, string>;
    body?: string;
    execute?: boolean;
    initialData?: T;
    parseResponse?: (response: Response) => Promise<T>;
    keepPreviousData?: boolean;
  },
): { data: T | undefined; isLoading: boolean; error: Error | undefined; revalidate: () => void; mutate: (data?: T) => void } {
  const resolvedUrl = typeof url === 'function' ? url() : url;

  return useCachedPromise(
    async () => {
      const response = await fetch(resolvedUrl, {
        method: options?.method ?? 'GET',
        headers: options?.headers,
        body: options?.body,
      });

      if (!response.ok) {
        throw new Error(`HTTP ${response.status}: ${response.statusText}`);
      }

      if (options?.parseResponse) {
        return options.parseResponse(response);
      }

      return response.json() as Promise<T>;
    },
    [resolvedUrl],
    { initialData: options?.initialData, execute: options?.execute, keepPreviousData: options?.keepPreviousData },
  );
}
