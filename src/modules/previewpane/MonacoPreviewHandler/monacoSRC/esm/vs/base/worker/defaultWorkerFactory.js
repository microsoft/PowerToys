/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
var _a;
import { globals } from '../common/platform.js';
import { logOnceWebWorkerWarning } from '../common/worker/simpleWorker.js';
const ttPolicy = (_a = window.trustedTypes) === null || _a === void 0 ? void 0 : _a.createPolicy('defaultWorkerFactory', { createScriptURL: value => value });
function getWorker(workerId, label) {
    // Option for hosts to overwrite the worker script (used in the standalone editor)
    if (globals.MonacoEnvironment) {
        if (typeof globals.MonacoEnvironment.getWorker === 'function') {
            return globals.MonacoEnvironment.getWorker(workerId, label);
        }
        if (typeof globals.MonacoEnvironment.getWorkerUrl === 'function') {
            const wokerUrl = globals.MonacoEnvironment.getWorkerUrl(workerId, label);
            return new Worker(ttPolicy ? ttPolicy.createScriptURL(wokerUrl) : wokerUrl, { name: label });
        }
    }
    // ESM-comment-begin
    // 	if (typeof require === 'function') {
    // 		// check if the JS lives on a different origin
    // 		const workerMain = require.toUrl('./' + workerId); // explicitly using require.toUrl(), see https://github.com/microsoft/vscode/issues/107440#issuecomment-698982321
    // 		const workerUrl = getWorkerBootstrapUrl(workerMain, label);
    // 		return new Worker(ttPolicy ? ttPolicy.createScriptURL(workerUrl) as unknown as string : workerUrl, { name: label });
    // 	}
    // ESM-comment-end
    throw new Error(`You must define a function MonacoEnvironment.getWorkerUrl or MonacoEnvironment.getWorker`);
}
// ESM-comment-begin
// export function getWorkerBootstrapUrl(scriptPath: string, label: string, forceDataUri: boolean = false): string {
// 	if (forceDataUri || /^((http:)|(https:)|(file:))/.test(scriptPath)) {
// 		const currentUrl = String(window.location);
// 		const currentOrigin = currentUrl.substr(0, currentUrl.length - window.location.hash.length - window.location.search.length - window.location.pathname.length);
// 		if (forceDataUri || scriptPath.substring(0, currentOrigin.length) !== currentOrigin) {
// 			// this is the cross-origin case
// 			// i.e. the webpage is running at a different origin than where the scripts are loaded from
// 			const myPath = 'vs/base/worker/defaultWorkerFactory.js';
// 			const workerBaseUrl = require.toUrl(myPath).slice(0, -myPath.length); // explicitly using require.toUrl(), see https://github.com/microsoft/vscode/issues/107440#issuecomment-698982321
// 			const js = `/*${label}*/self.MonacoEnvironment={baseUrl: '${workerBaseUrl}'};importScripts('${scriptPath}');/*${label}*/`;
// 			if (forceDataUri) {
// 				const url = `data:text/javascript;charset=utf-8,${encodeURIComponent(js)}`;
// 				return url;
// 			}
// 			const blob = new Blob([js], { type: 'application/javascript' });
// 			return URL.createObjectURL(blob);
// 		}
// 	}
// 	return scriptPath + '#' + label;
// }
// ESM-comment-end
function isPromiseLike(obj) {
    if (typeof obj.then === 'function') {
        return true;
    }
    return false;
}
/**
 * A worker that uses HTML5 web workers so that is has
 * its own global scope and its own thread.
 */
class WebWorker {
    constructor(moduleId, id, label, onMessageCallback, onErrorCallback) {
        this.id = id;
        const workerOrPromise = getWorker('workerMain.js', label);
        if (isPromiseLike(workerOrPromise)) {
            this.worker = workerOrPromise;
        }
        else {
            this.worker = Promise.resolve(workerOrPromise);
        }
        this.postMessage(moduleId, []);
        this.worker.then((w) => {
            w.onmessage = function (ev) {
                onMessageCallback(ev.data);
            };
            w.onmessageerror = onErrorCallback;
            if (typeof w.addEventListener === 'function') {
                w.addEventListener('error', onErrorCallback);
            }
        });
    }
    getId() {
        return this.id;
    }
    postMessage(message, transfer) {
        if (this.worker) {
            this.worker.then(w => w.postMessage(message, transfer));
        }
    }
    dispose() {
        if (this.worker) {
            this.worker.then(w => w.terminate());
        }
        this.worker = null;
    }
}
export class DefaultWorkerFactory {
    constructor(label) {
        this._label = label;
        this._webWorkerFailedBeforeError = false;
    }
    create(moduleId, onMessageCallback, onErrorCallback) {
        let workerId = (++DefaultWorkerFactory.LAST_WORKER_ID);
        if (this._webWorkerFailedBeforeError) {
            throw this._webWorkerFailedBeforeError;
        }
        return new WebWorker(moduleId, workerId, this._label || 'anonymous' + workerId, onMessageCallback, (err) => {
            logOnceWebWorkerWarning(err);
            this._webWorkerFailedBeforeError = err;
            onErrorCallback(err);
        });
    }
}
DefaultWorkerFactory.LAST_WORKER_ID = 0;
