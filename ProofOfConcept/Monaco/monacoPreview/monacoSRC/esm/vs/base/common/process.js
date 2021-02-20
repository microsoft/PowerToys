/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { isWindows, isMacintosh, setImmediate, globals } from './platform.js';
let safeProcess;
// Native node.js environment
if (typeof process !== 'undefined') {
    safeProcess = process;
}
// Native sandbox environment
else if (typeof globals.vscode !== 'undefined') {
    safeProcess = {
        // Supported
        get platform() { return globals.vscode.process.platform; },
        get env() { return globals.vscode.process.env; },
        nextTick(callback) { return setImmediate(callback); },
        // Unsupported
        cwd() { return globals.vscode.process.env['VSCODE_CWD'] || globals.vscode.process.execPath.substr(0, globals.vscode.process.execPath.lastIndexOf(globals.vscode.process.platform === 'win32' ? '\\' : '/')); }
    };
}
// Web environment
else {
    safeProcess = {
        // Supported
        get platform() { return isWindows ? 'win32' : isMacintosh ? 'darwin' : 'linux'; },
        nextTick(callback) { return setImmediate(callback); },
        // Unsupported
        get env() { return Object.create(null); },
        cwd() { return '/'; }
    };
}
export const cwd = safeProcess.cwd;
export const env = safeProcess.env;
export const platform = safeProcess.platform;
