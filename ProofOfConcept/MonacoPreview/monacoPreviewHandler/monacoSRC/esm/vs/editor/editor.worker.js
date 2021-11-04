/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { SimpleWorkerServer } from '../base/common/worker/simpleWorker.js';
import { EditorSimpleWorker } from './common/services/editorSimpleWorker.js';
let initialized = false;
export function initialize(foreignModule) {
    if (initialized) {
        return;
    }
    initialized = true;
    const simpleWorker = new SimpleWorkerServer((msg) => {
        self.postMessage(msg);
    }, (host) => new EditorSimpleWorker(host, foreignModule));
    self.onmessage = (e) => {
        simpleWorker.onmessage(e.data);
    };
}
self.onmessage = (e) => {
    // Ignore first message in this case and initialize if not yet initialized
    if (!initialized) {
        initialize(null);
    }
};
