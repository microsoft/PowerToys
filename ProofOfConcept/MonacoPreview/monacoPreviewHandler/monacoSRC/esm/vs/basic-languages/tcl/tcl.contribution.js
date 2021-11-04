/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { registerLanguage } from '../_.contribution.js';
registerLanguage({
    id: 'tcl',
    extensions: ['.tcl'],
    aliases: ['tcl', 'Tcl', 'tcltk', 'TclTk', 'tcl/tk', 'Tcl/Tk'],
    loader: function () { return import('./tcl.js'); }
});
