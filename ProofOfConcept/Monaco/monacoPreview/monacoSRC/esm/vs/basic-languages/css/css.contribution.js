/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { registerLanguage } from '../_.contribution.js';
registerLanguage({
    id: 'css',
    extensions: ['.css'],
    aliases: ['CSS', 'css'],
    mimetypes: ['text/css'],
    loader: function () { return import('./css.js'); }
});
