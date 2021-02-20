/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { registerLanguage } from '../_.contribution.js';
registerLanguage({
    id: 'ecl',
    extensions: ['.ecl'],
    aliases: ['ECL', 'Ecl', 'ecl'],
    loader: function () { return import('./ecl.js'); }
});
