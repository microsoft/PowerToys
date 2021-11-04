/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { registerLanguage } from '../_.contribution.js';
registerLanguage({
    id: 'html',
    extensions: ['.html', '.htm', '.shtml', '.xhtml', '.mdoc', '.jsp', '.asp', '.aspx', '.jshtm'],
    aliases: ['HTML', 'htm', 'html', 'xhtml'],
    mimetypes: ['text/html', 'text/x-jshtm', 'text/template', 'text/ng-template'],
    loader: function () { return import('./html.js'); }
});
