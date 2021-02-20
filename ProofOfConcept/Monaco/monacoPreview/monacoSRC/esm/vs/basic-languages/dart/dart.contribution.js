/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { registerLanguage } from '../_.contribution.js';
registerLanguage({
    id: 'dart',
    extensions: ['.dart'],
    aliases: ['Dart', 'dart'],
    mimetypes: ['text/x-dart-source', 'text/x-dart'],
    loader: function () { return import('./dart.js'); }
});
