/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { registerLanguage } from '../_.contribution.js';
registerLanguage({
    id: 'csharp',
    extensions: ['.cs', '.csx', '.cake'],
    aliases: ['C#', 'csharp'],
    loader: function () { return import('./csharp.js'); }
});
