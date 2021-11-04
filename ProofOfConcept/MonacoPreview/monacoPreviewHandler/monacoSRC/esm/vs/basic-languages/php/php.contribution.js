/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { registerLanguage } from '../_.contribution.js';
registerLanguage({
    id: 'php',
    extensions: ['.php', '.php4', '.php5', '.phtml', '.ctp'],
    aliases: ['PHP', 'php'],
    mimetypes: ['application/x-php'],
    loader: function () { return import('./php.js'); }
});
