/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { registerLanguage } from '../_.contribution.js';
registerLanguage({
    id: 'handlebars',
    extensions: ['.handlebars', '.hbs'],
    aliases: ['Handlebars', 'handlebars'],
    mimetypes: ['text/x-handlebars-template'],
    loader: function () { return import('./handlebars.js'); }
});
