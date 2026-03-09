"use strict";
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
Object.defineProperty(exports, "__esModule", { value: true });
exports.useNavigation = useNavigation;
exports.useCachedPromise = useCachedPromise;
exports.useFetch = useFetch;
/**
 * Raycast React hooks compatibility stubs.
 *
 * These are hooks that Raycast extensions commonly import from `@raycast/api`.
 * They're not part of the React reconciler but rather imperative utilities
 * that extensions call from within their components.
 */
const react_1 = require("react");
/**
 * Raycast's useNavigation hook — provides push/pop navigation.
 * In CmdPal, navigation is handled declaratively. This stub provides
 * the interface shape so extensions don't crash.
 */
function useNavigation() {
    return {
        push: (_component) => {
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
function useCachedPromise(fn, args, options) {
    const [data, setData] = (0, react_1.useState)(options?.initialData);
    const [isLoading, setIsLoading] = (0, react_1.useState)(options?.execute !== false);
    const [error, setError] = (0, react_1.useState)();
    const mountedRef = (0, react_1.useRef)(true);
    const execute = (0, react_1.useCallback)(async () => {
        if (!mountedRef.current)
            return;
        setIsLoading(true);
        setError(undefined);
        try {
            const result = await fn(...(args ?? []));
            if (mountedRef.current) {
                setData(result);
            }
        }
        catch (err) {
            if (mountedRef.current) {
                setError(err instanceof Error ? err : new Error(String(err)));
            }
        }
        finally {
            if (mountedRef.current) {
                setIsLoading(false);
            }
        }
    }, [fn, ...(args ?? [])]);
    (0, react_1.useEffect)(() => {
        mountedRef.current = true;
        if (options?.execute !== false) {
            void execute();
        }
        return () => { mountedRef.current = false; };
    }, [execute, options?.execute]);
    return {
        data,
        isLoading,
        error,
        revalidate: () => void execute(),
        mutate: (newData) => { if (newData !== undefined)
            setData(newData); },
    };
}
/**
 * Raycast's useFetch — simplified fetch with caching.
 * Delegates to useCachedPromise with a fetch wrapper.
 */
function useFetch(url, options) {
    const resolvedUrl = typeof url === 'function' ? url() : url;
    return useCachedPromise(async () => {
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
        return response.json();
    }, [resolvedUrl], { initialData: options?.initialData, execute: options?.execute, keepPreviousData: options?.keepPreviousData });
}
//# sourceMappingURL=hooks.js.map