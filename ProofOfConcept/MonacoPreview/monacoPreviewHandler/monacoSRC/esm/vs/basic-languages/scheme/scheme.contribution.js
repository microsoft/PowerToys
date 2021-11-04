/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { registerLanguage } from '../_.contribution.js';
registerLanguage({
    id: 'scheme',
    extensions: ['.scm', '.ss', '.sch', '.rkt'],
    aliases: ['scheme', 'Scheme'],
    loader: function () { return import('./scheme.js'); }
});
