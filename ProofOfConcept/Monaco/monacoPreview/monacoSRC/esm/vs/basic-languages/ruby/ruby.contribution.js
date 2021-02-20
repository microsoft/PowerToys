/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { registerLanguage } from '../_.contribution.js';
registerLanguage({
    id: 'ruby',
    extensions: ['.rb', '.rbx', '.rjs', '.gemspec', '.pp'],
    filenames: ['rakefile', 'Gemfile'],
    aliases: ['Ruby', 'rb'],
    loader: function () { return import('./ruby.js'); }
});
