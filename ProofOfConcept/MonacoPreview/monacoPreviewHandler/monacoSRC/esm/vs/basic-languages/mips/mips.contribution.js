/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { registerLanguage } from '../_.contribution.js';
registerLanguage({
    id: 'mips',
    extensions: ['.s'],
    aliases: ['MIPS', 'MIPS-V'],
    mimetypes: ['text/x-mips', 'text/mips', 'text/plaintext'],
    loader: function () { return import('./mips.js'); }
});
