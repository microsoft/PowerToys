// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import type {
  IContentPage,
  Content,
  Details,
  ContextItem,
  IconInfo,
  OptionalColor,
} from '../types';

/**
 * Base class for content pages that display rich content (markdown, forms, images, etc.).
 *
 * @example
 * ```typescript
 * import { ContentPageBase } from '@microsoft/cmdpal-sdk';
 *
 * class ReadmePage extends ContentPageBase {
 *   id = 'readme';
 *   name = 'README';
 *   title = 'About This Extension';
 *
 *   getContent() {
 *     return [
 *       { type: 'markdown', body: '# Hello World\n\nThis is my extension.' }
 *     ];
 *   }
 * }
 * ```
 */
export abstract class ContentPageBase implements IContentPage {
  abstract id: string;
  abstract name: string;
  abstract title: string;

  icon?: IconInfo | null = null;
  isLoading?: boolean = false;
  accentColor?: OptionalColor | null = null;
  details?: Details | null = null;
  commands?: ContextItem[] = [];

  abstract getContent(): Promise<Content[]> | Content[];
}
