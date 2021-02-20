/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import * as worker from '../../editor/editor.worker.js';
import { JSONWorker } from './jsonWorker.js';
self.onmessage = function () {
    // ignore the first message
    worker.initialize(function (ctx, createData) {
        return new JSONWorker(ctx, createData);
    });
};
