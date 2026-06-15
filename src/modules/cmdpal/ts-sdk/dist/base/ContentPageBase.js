"use strict";
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
Object.defineProperty(exports, "__esModule", { value: true });
exports.ContentPageBase = void 0;
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
class ContentPageBase {
    icon = null;
    isLoading = false;
    accentColor = null;
    details = null;
    commands = [];
}
exports.ContentPageBase = ContentPageBase;
//# sourceMappingURL=ContentPageBase.js.map