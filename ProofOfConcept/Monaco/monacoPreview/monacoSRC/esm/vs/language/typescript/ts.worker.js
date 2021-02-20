/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
'use strict';
import * as edworker from '../../editor/editor.worker.js';
import { TypeScriptWorker } from './tsWorker.js';
self.onmessage = function () {
    // ignore the first message
    edworker.initialize(function (ctx, createData) {
        return new TypeScriptWorker(ctx, createData);
    });
};
