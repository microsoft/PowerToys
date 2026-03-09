import { useCachedPromise, useFetch } from './api-stubs/hooks';
export { useCachedPromise, useFetch };
/**
 * Similar to useCachedPromise but without caching semantics.
 * Delegates to useCachedPromise internally.
 */
export declare function usePromise<T>(fn: (...args: unknown[]) => Promise<T>, args?: unknown[], options?: {
    execute?: boolean;
}): {
    data: T | undefined;
    isLoading: boolean;
    error: Error | undefined;
    revalidate: () => void;
};
/**
 * Persisted state hook. Simplified: wraps useState (no cross-session
 * persistence in CmdPal compat).
 */
export declare function useCachedState<T>(_key: string, initialValue: T): [T, (value: T | ((prev: T) => T)) => void];
/**
 * Execute a shell command. Stub — shell execution is not supported
 * in the CmdPal compat layer for security.
 */
export declare function useExec(command: string, _args?: string[], _options?: {
    execute?: boolean;
}): {
    data: string | undefined;
    isLoading: boolean;
    error: Error | undefined;
    revalidate: () => void;
};
/**
 * Execute SQL queries. Stub — not supported in CmdPal.
 */
export declare function useSQL<T = unknown>(_databasePath: string, _query: string): {
    data: T[] | undefined;
    isLoading: boolean;
    error: Error | undefined;
    revalidate: () => void;
};
/**
 * Convenience wrapper for error toasts.
 */
export declare function showFailureToast(error: unknown, options?: {
    title?: string;
    message?: string;
}): Promise<void>;
/**
 * Returns a favicon URL for a given domain.
 */
export declare function getFavicon(url: string, options?: {
    size?: number;
    fallback?: string;
}): string;
/**
 * Returns a progress indicator character.
 */
export declare function getProgressIcon(progress: number, _color?: string): string;
/**
 * Sort items by frequency + recency of access.
 * Stub: returns data as-is with no-op tracking functions.
 */
export declare function useFrecencySorting<T>(data: T[] | undefined, options?: {
    key?: (item: T) => string;
    sortUnvisited?: (a: T, b: T) => number;
    namespace?: string;
}): {
    data: T[];
    visitItem: (item: T) => void;
    resetRanking: (item: T) => void;
};
/**
 * Form state management hook.
 * Stub: provides basic form value tracking with no validation.
 */
export declare function useForm<T extends Record<string, unknown>>(options?: {
    initialValues?: Partial<T>;
    validation?: Record<string, (value: unknown) => string | undefined>;
    onSubmit?: (values: T) => void | Promise<void>;
}): {
    handleSubmit: (values: T) => void;
    itemProps: Record<string, {
        value: unknown;
        onChange: (v: unknown) => void;
        error: string | undefined;
    }>;
    values: T;
    setValidationError: (id: string, error: string | undefined) => void;
    setValue: (id: string, value: unknown) => void;
    reset: (initialValues?: Partial<T>) => void;
    focus: (id: string) => void;
};
/**
 * Persistent local storage hook.
 * Delegates to useCachedState (no true persistence across sessions).
 */
export declare function useLocalStorage<T>(key: string, initialValue?: T): {
    value: T | undefined;
    setValue: (value: T) => void;
    removeValue: () => void;
    isLoading: boolean;
};
/**
 * Run an AppleScript. Stub: not supported on Windows.
 */
export declare function runAppleScript(script: string, _options?: {
    humanReadableOutput?: boolean;
    timeout?: number;
}): Promise<string>;
//# sourceMappingURL=utils-shim.d.ts.map