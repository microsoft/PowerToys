/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { registerLanguage } from '../_.contribution.js';
registerLanguage({
    id: 'sb',
    extensions: ['.sb'],
    aliases: ['Small Basic', 'sb'],
    loader: function () { return import('./sb.js'); }
});
