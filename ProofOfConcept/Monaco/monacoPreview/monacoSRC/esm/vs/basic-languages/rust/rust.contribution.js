/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { registerLanguage } from '../_.contribution.js';
registerLanguage({
    id: 'rust',
    extensions: ['.rs', '.rlib'],
    aliases: ['Rust', 'rust'],
    loader: function () { return import('./rust.js'); }
});
