/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { registerLanguage } from '../_.contribution.js';
registerLanguage({
    id: 'coffeescript',
    extensions: ['.coffee'],
    aliases: ['CoffeeScript', 'coffeescript', 'coffee'],
    mimetypes: ['text/x-coffeescript', 'text/coffeescript'],
    loader: function () { return import('./coffee.js'); }
});
