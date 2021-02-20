/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { registerLanguage } from '../_.contribution.js';
registerLanguage({
    id: 'graphql',
    extensions: ['.graphql', '.gql'],
    aliases: ['GraphQL', 'graphql', 'gql'],
    mimetypes: ['application/graphql'],
    loader: function () { return import('./graphql.js'); }
});
