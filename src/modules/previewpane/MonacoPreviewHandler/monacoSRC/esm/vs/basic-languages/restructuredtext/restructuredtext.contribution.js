/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { registerLanguage } from '../_.contribution.js';
registerLanguage({
    id: 'restructuredtext',
    extensions: ['.rst'],
    aliases: ['reStructuredText', 'restructuredtext'],
    loader: function () { return import('./restructuredtext.js'); }
});
