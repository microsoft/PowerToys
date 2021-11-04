/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { registerLanguage } from '../_.contribution.js';
registerLanguage({
    id: 'powerquery',
    extensions: ['.pq', '.pqm'],
    aliases: ['PQ', 'M', 'Power Query', 'Power Query M'],
    loader: function () { return import('./powerquery.js'); }
});
