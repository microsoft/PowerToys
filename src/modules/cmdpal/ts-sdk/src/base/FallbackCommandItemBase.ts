// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import type {
  ContextItem,
  ICommand,
  IFallbackCommandItem,
  IFallbackHandler,
  IconInfo,
} from '../types.js';

/**
 * Base class for a fallback command item. Fallback items receive the user's
 * search query in real time through {@link FallbackCommandItemBase.updateQuery}
 * and typically update {@link FallbackCommandItemBase.displayTitle} in response.
 *
 * @example
 * ```typescript
 * class WebSearchFallback extends FallbackCommandItemBase {
 *   readonly command = new OpenUrlCommand('https://example.com');
 *   title = 'Search the web';
 *
 *   updateQuery(query: string): void {
 *     this.displayTitle = query ? `Search for "${query}"` : 'Search the web';
 *   }
 * }
 * ```
 */
export abstract class FallbackCommandItemBase implements IFallbackCommandItem, IFallbackHandler {
  abstract readonly command: ICommand;
  abstract title: string;

  subtitle?: string;
  icon?: IconInfo | null = null;
  moreCommands?: ContextItem[];
  displayTitle?: string;

  get fallbackHandler(): IFallbackHandler {
    return this;
  }

  abstract updateQuery(query: string): void | Promise<void>;
}
