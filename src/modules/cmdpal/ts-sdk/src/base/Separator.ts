// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import type { ICommand, IListItem } from '../types.js';

/**
 * A visual divider for list pages. With no title it renders as a horizontal
 * line; with a title it renders as a section header.
 *
 * @example
 * ```typescript
 * new Separator();              // horizontal line
 * new Separator('Recent');      // section header
 * ```
 */
export class Separator implements IListItem {
  /** Placeholder command; separators are not invokable. */
  readonly command: ICommand;
  /** Title text; empty for a plain divider. */
  title: string;
  /** Section header text when the separator names a group. */
  section?: string;

  /** Marker used by the runtime to serialize this item as a separator. */
  readonly isSeparator = true;

  /**
   * Creates a separator.
   *
   * @param title Optional section header text. When omitted the separator
   * renders as a plain horizontal line.
   */
  constructor(title = '') {
    this.title = title;
    this.section = title || undefined;
    this.command = { id: `separator:${title}`, name: '' };
  }
}
