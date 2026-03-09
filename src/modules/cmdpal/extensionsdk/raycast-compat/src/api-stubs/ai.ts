// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * Raycast AI compatibility stub.
 *
 * Raycast extensions can import `AI` from `@raycast/api` for
 * LLM features. CmdPal doesn't expose an AI API yet, so these
 * stubs throw clear errors so extensions degrade gracefully.
 */

export const AI = {
  async ask(prompt: string, options?: { model?: string; creativity?: number; signal?: AbortSignal }): Promise<string> {
    void options;
    console.warn(`[AI] AI.ask() is not supported in CmdPal. Prompt: "${prompt.slice(0, 80)}..."`);
    throw new Error('AI.ask() is not available in CmdPal. The extension requires Raycast AI features that are not supported.');
  },
};

/**
 * Raycast's `useAI` hook equivalent — for extensions that use the hook pattern.
 * Returns a static "not supported" state.
 */
export function useAI(_prompt: string, _options?: Record<string, unknown>): { data: string; isLoading: boolean; error?: Error } {
  return {
    data: '',
    isLoading: false,
    error: new Error('AI features are not available in CmdPal'),
  };
}
