// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * @raycast/utils compatibility shim.
 *
 * Raycast extensions import utility hooks from `@raycast/utils`.
 * This module re-exports compatible implementations from our api-stubs
 * and provides minimal stubs for the rest. The esbuild bundler aliases
 * `@raycast/utils` → this file.
 */

import { useState } from 'react';
import { useCachedPromise, useFetch } from './api-stubs/hooks';
import { showToast, Toast } from './api-stubs/toast';
import type { ToastOptions } from './api-stubs/toast';

// ── Re-exported hooks ──────────────────────────────────────────────────

export { useCachedPromise, useFetch };

// ── usePromise ─────────────────────────────────────────────────────────

/**
 * Similar to useCachedPromise but without caching semantics.
 * Delegates to useCachedPromise internally.
 */
export function usePromise<T>(
  fn: (...args: unknown[]) => Promise<T>,
  args?: unknown[],
  options?: { execute?: boolean },
): {
  data: T | undefined;
  isLoading: boolean;
  error: Error | undefined;
  revalidate: () => void;
} {
  return useCachedPromise(fn, args, options);
}

// ── useCachedState ─────────────────────────────────────────────────────

/**
 * Persisted state hook. Simplified: wraps useState (no cross-session
 * persistence in CmdPal compat).
 */
export function useCachedState<T>(
  _key: string,
  initialValue: T,
): [T, (value: T | ((prev: T) => T)) => void] {
  return useState<T>(initialValue);
}

// ── useExec ────────────────────────────────────────────────────────────

/**
 * Execute a shell command. Stub — shell execution is not supported
 * in the CmdPal compat layer for security.
 */
export function useExec(
  command: string,
  _args?: string[],
  _options?: { execute?: boolean },
): {
  data: string | undefined;
  isLoading: boolean;
  error: Error | undefined;
  revalidate: () => void;
} {
  return {
    data: undefined,
    isLoading: false,
    error: new Error(
      `[useExec] Shell execution not supported in CmdPal compat layer: ${command}`,
    ),
    revalidate: () => {},
  };
}

// ── useSQL ─────────────────────────────────────────────────────────────

/**
 * Execute SQL queries. Stub — not supported in CmdPal.
 */
export function useSQL<T = unknown>(
  _databasePath: string,
  _query: string,
): {
  data: T[] | undefined;
  isLoading: boolean;
  error: Error | undefined;
  revalidate: () => void;
} {
  return {
    data: undefined,
    isLoading: false,
    error: new Error('[useSQL] SQL queries not supported in CmdPal compat layer'),
    revalidate: () => {},
  };
}

// ── showFailureToast ───────────────────────────────────────────────────

/**
 * Convenience wrapper for error toasts.
 */
export async function showFailureToast(
  error: unknown,
  options?: { title?: string; message?: string },
): Promise<void> {
  const message = error instanceof Error ? error.message : String(error);
  await showToast({
    style: Toast.Style.Failure,
    title: options?.title ?? 'Error',
    message: options?.message ?? message,
  } as ToastOptions);
}

// ── getFavicon ─────────────────────────────────────────────────────────

/**
 * Returns a favicon URL for a given domain.
 */
export function getFavicon(
  url: string,
  options?: { size?: number; fallback?: string },
): string {
  try {
    const domain = new URL(url).hostname;
    return `https://www.google.com/s2/favicons?domain=${domain}&sz=${options?.size ?? 32}`;
  } catch {
    return options?.fallback ?? '';
  }
}

// ── getProgressIcon ────────────────────────────────────────────────────

/**
 * Returns a progress indicator character.
 */
export function getProgressIcon(progress: number, _color?: string): string {
  if (progress <= 0) return '○';
  if (progress >= 1) return '●';
  return '◐';
}

// ── useFrecencySorting ─────────────────────────────────────────────────

/**
 * Sort items by frequency + recency of access.
 * Stub: returns data as-is with no-op tracking functions.
 */
export function useFrecencySorting<T>(
  data: T[] | undefined,
  options?: {
    key?: (item: T) => string;
    sortUnvisited?: (a: T, b: T) => number;
    namespace?: string;
  },
): {
  data: T[];
  visitItem: (item: T) => void;
  resetRanking: (item: T) => void;
} {
  void options;
  return {
    data: data ?? [],
    visitItem: () => {},
    resetRanking: () => {},
  };
}

// ── useForm ────────────────────────────────────────────────────────────

/**
 * Form state management hook.
 * Stub: provides basic form value tracking with no validation.
 */
export function useForm<T extends Record<string, unknown>>(options?: {
  initialValues?: Partial<T>;
  validation?: Record<string, (value: unknown) => string | undefined>;
  onSubmit?: (values: T) => void | Promise<void>;
}): {
  handleSubmit: (values: T) => void;
  itemProps: Record<string, { value: unknown; onChange: (v: unknown) => void; error: string | undefined }>;
  values: T;
  setValidationError: (id: string, error: string | undefined) => void;
  setValue: (id: string, value: unknown) => void;
  reset: (initialValues?: Partial<T>) => void;
  focus: (id: string) => void;
} {
  const values = (options?.initialValues ?? {}) as T;
  const [formValues, setFormValues] = useState<T>(values);
  const [errors, setErrors] = useState<Record<string, string | undefined>>({});

  const itemProps = new Proxy({} as Record<string, { value: unknown; onChange: (v: unknown) => void; error: string | undefined }>, {
    get(_target, prop: string) {
      return {
        value: (formValues as Record<string, unknown>)[prop],
        onChange: (v: unknown) => {
          setFormValues((prev) => ({ ...prev, [prop]: v }));
        },
        error: errors[prop],
      };
    },
  });

  return {
    handleSubmit: (vals: T) => options?.onSubmit?.(vals),
    itemProps,
    values: formValues,
    setValidationError: (id: string, error: string | undefined) => {
      setErrors((prev) => ({ ...prev, [id]: error }));
    },
    setValue: (id: string, value: unknown) => {
      setFormValues((prev) => ({ ...prev, [id]: value }));
    },
    reset: (initial?: Partial<T>) => {
      setFormValues((initial ?? options?.initialValues ?? {}) as T);
      setErrors({});
    },
    focus: () => {},
  };
}

// ── useLocalStorage ────────────────────────────────────────────────────

/**
 * Persistent local storage hook.
 * Delegates to useCachedState (no true persistence across sessions).
 */
export function useLocalStorage<T>(
  key: string,
  initialValue?: T,
): {
  value: T | undefined;
  setValue: (value: T) => void;
  removeValue: () => void;
  isLoading: boolean;
} {
  const [value, setVal] = useCachedState<T | undefined>(key, initialValue);
  return {
    value,
    setValue: setVal as (v: T) => void,
    removeValue: () => setVal(undefined),
    isLoading: false,
  };
}

// ── runAppleScript ─────────────────────────────────────────────────────

/**
 * Run an AppleScript. Stub: not supported on Windows.
 */
export async function runAppleScript(
  script: string,
  _options?: { humanReadableOutput?: boolean; timeout?: number },
): Promise<string> {
  void script;
  console.warn('[Utils] runAppleScript() is not supported on Windows');
  return '';
}
