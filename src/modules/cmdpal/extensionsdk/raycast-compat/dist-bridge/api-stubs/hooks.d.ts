/**
 * Raycast's useNavigation hook — provides push/pop navigation.
 * In CmdPal, navigation is handled declaratively. This stub provides
 * the interface shape so extensions don't crash.
 */
export declare function useNavigation(): {
    push: (component: React.ReactNode) => void;
    pop: () => void;
};
/**
 * Raycast's useCachedPromise — executes an async function and caches the result.
 * Simplified implementation for compatibility.
 */
export declare function useCachedPromise<T>(fn: (...args: unknown[]) => Promise<T>, args?: unknown[], options?: {
    initialData?: T;
    keepPreviousData?: boolean;
    execute?: boolean;
}): {
    data: T | undefined;
    isLoading: boolean;
    error: Error | undefined;
    revalidate: () => void;
    mutate: (data?: T) => void;
    pagination?: {
        hasMore: boolean;
        pageSize: number;
    };
};
/**
 * Raycast's useFetch — simplified fetch with caching.
 * Delegates to useCachedPromise with a fetch wrapper.
 */
export declare function useFetch<T = unknown>(url: string | ((...args: unknown[]) => string), options?: {
    method?: string;
    headers?: Record<string, string>;
    body?: string;
    execute?: boolean;
    initialData?: T;
    parseResponse?: (response: Response) => Promise<T>;
    keepPreviousData?: boolean;
}): {
    data: T | undefined;
    isLoading: boolean;
    error: Error | undefined;
    revalidate: () => void;
    mutate: (data?: T) => void;
};
//# sourceMappingURL=hooks.d.ts.map