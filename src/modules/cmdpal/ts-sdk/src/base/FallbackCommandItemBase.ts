// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import type { IFallbackCommandItem, IFallbackHandler, ICommand, ContextItem, IconInfo } from '../types'

/**
 * Base class for fallback command items that appear when the user types
 * from the home page. These provide search/filter results as the user types.
 *
 * @example
 * ```typescript
 * class WebSearchFallback extends FallbackCommandItemBase {
 *   command = new WebSearchCommand()
 *   title = 'Search the web'
 *
 *   updateQuery(query: string) {
 *     this.displayTitle = `Search for "${query}"`
 *   }
 * }
 * ```
 */
export abstract class FallbackCommandItemBase implements IFallbackCommandItem, IFallbackHandler {
  abstract command: ICommand
  abstract title: string

  subtitle?: string
  icon?: IconInfo | null
  moreCommands?: ContextItem[]
  displayTitle?: string

  get fallbackHandler(): IFallbackHandler {
    return this
  }

  abstract updateQuery(query: string): void
}
