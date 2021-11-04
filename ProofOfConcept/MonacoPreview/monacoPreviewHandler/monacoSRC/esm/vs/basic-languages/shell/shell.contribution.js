/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { registerLanguage } from '../_.contribution.js';
registerLanguage({
    id: 'shell',
    extensions: ['.sh', '.bash'],
    aliases: ['Shell', 'sh'],
    loader: function () { return import('./shell.js'); }
});
