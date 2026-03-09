/**
 * Raycast AI compatibility stub.
 *
 * Raycast extensions can import `AI` from `@raycast/api` for
 * LLM features. CmdPal doesn't expose an AI API yet, so these
 * stubs throw clear errors so extensions degrade gracefully.
 */
export declare const AI: {
    ask(prompt: string, options?: {
        model?: string;
        creativity?: number;
        signal?: AbortSignal;
    }): Promise<string>;
};
/**
 * Raycast's `useAI` hook equivalent — for extensions that use the hook pattern.
 * Returns a static "not supported" state.
 */
export declare function useAI(_prompt: string, _options?: Record<string, unknown>): {
    data: string;
    isLoading: boolean;
    error?: Error;
};
//# sourceMappingURL=ai.d.ts.map