/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { registerLanguage } from '../_.contribution.js';
registerLanguage({
    id: 'vb',
    extensions: ['.vb'],
    aliases: ['Visual Basic', 'vb'],
    loader: function () { return import('./vb.js'); }
});
