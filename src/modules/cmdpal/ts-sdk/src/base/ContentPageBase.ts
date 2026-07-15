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
  abstract readonly id: string;
  abstract readonly name: string;
  abstract readonly title: string;

  icon?: IconInfo | null = null;
  isLoading?: boolean = false;
  accentColor?: OptionalColor | null = null;
  details?: Details | null = null;
  commands?: ContextItem[] = [];

  abstract getContent(): Content[] | Promise<Content[]>;
}
