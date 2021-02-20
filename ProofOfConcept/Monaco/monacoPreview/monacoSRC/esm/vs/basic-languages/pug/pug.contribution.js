/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { registerLanguage } from '../_.contribution.js';
registerLanguage({
    id: 'pug',
    extensions: ['.jade', '.pug'],
    aliases: ['Pug', 'Jade', 'jade'],
    loader: function () { return import('./pug.js'); }
});
