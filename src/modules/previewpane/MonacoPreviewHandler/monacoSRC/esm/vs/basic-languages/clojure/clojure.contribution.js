/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { registerLanguage } from '../_.contribution.js';
registerLanguage({
    id: 'clojure',
    extensions: ['.clj', '.cljs', '.cljc', '.edn'],
    aliases: ['clojure', 'Clojure'],
    loader: function () { return import('./clojure.js'); }
});
