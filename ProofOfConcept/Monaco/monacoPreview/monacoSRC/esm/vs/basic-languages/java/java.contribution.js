/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { registerLanguage } from '../_.contribution.js';
registerLanguage({
    id: 'java',
    extensions: ['.java', '.jav'],
    aliases: ['Java', 'java'],
    mimetypes: ['text/x-java-source', 'text/x-java'],
    loader: function () { return import('./java.js'); }
});
