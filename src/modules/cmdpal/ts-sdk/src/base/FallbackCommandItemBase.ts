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
import { ObservableBase } from './ObservableBase.js';

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
export abstract class FallbackCommandItemBase
  extends ObservableBase
  implements IFallbackCommandItem, IFallbackHandler
{
  /** The command run when the item is activated. */
  abstract readonly command: ICommand;
  protected get notificationId(): string {
    return this.command.id;
  }
  /** Primary text shown for the item. */
  abstract title: string;

  /** Secondary text shown below the title. */
  subtitle?: string;
  /** Identity of the fallback item, defaulting to the nested command id. */
  id?: string;
  /** Icon shown next to the item. */
  icon?: IconInfo | null = null;
  /** Right-click / overflow menu actions. */
  moreCommands?: ContextItem[];
  /** Dynamic title that updates as the user types. */
  displayTitle?: string;

  /** Returns this instance as its own {@link IFallbackHandler}. */
  get fallbackHandler(): IFallbackHandler {
    return this;
  }

  /**
   * Called whenever the search query changes so the item can update itself,
   * usually by setting {@link FallbackCommandItemBase.displayTitle}.
   *
   * @param query The current text in the search box.
   */
  abstract updateQuery(query: string): void | Promise<void>;
}
