// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import type {
  Content,
  ContextItem,
  Details,
  IContentPage,
  IconInfo,
  OptionalColor,
} from '../types.js';

/**
 * Base class for a page that displays rich content such as markdown, forms,
 * images, or trees.
 *
 * @example
 * ```typescript
 * class ReadmePage extends ContentPageBase {
 *   readonly id = 'readme';
 *   readonly name = 'README';
 *   readonly title = 'About This Extension';
 *
 *   getContent(): Content[] {
 *     return [{ type: 'markdown', body: '# Hello\n\nWelcome.' }];
 *   }
 * }
 * ```
 */
export abstract class ContentPageBase implements IContentPage {
  /** Unique identifier for the page. */
  abstract readonly id: string;
  /** Internal name of the page. */
  abstract readonly name: string;
  /** Title shown at the top of the page. */
  abstract readonly title: string;

  /** Icon shown for the page. */
  icon?: IconInfo | null = null;
  /** Whether the page is currently loading; shows a progress indicator. */
  isLoading?: boolean = false;
  /** Accent color applied to the page. Uses the host default when unset. */
  accentColor?: OptionalColor | null = null;
  /** Rich metadata shown alongside the content, or `null` for none. */
  details?: Details | null = null;
  /** Overflow menu actions offered for the page. */
  commands?: ContextItem[] = [];

  /**
   * Produces the content blocks to render.
   *
   * @returns The page's {@link Content} blocks, synchronously or as a promise.
   */
  abstract getContent(): Content[] | Promise<Content[]>;
}
