/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { registerLanguage } from '../_.contribution.js';
registerLanguage({
    id: 'powershell',
    extensions: ['.ps1', '.psm1', '.psd1'],
    aliases: ['PowerShell', 'powershell', 'ps', 'ps1'],
    loader: function () { return import('./powershell.js'); }
});
