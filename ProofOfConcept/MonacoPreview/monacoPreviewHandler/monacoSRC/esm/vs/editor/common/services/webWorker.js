/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { EditorWorkerClient } from './editorWorkerServiceImpl.js';
import * as types from '../../../base/common/types.js';
/**
 * Create a new web worker that has model syncing capabilities built in.
 * Specify an AMD module to load that will `create` an object that will be proxied.
 */
export function createWebWorker(modelService, opts) {
    return new MonacoWebWorkerImpl(modelService, opts);
}
class MonacoWebWorkerImpl extends EditorWorkerClient {
    constructor(modelService, opts) {
        super(modelService, opts.keepIdleModels || false, opts.label);
        this._foreignModuleId = opts.moduleId;
        this._foreignModuleCreateData = opts.createData || null;
        this._foreignModuleHost = opts.host || null;
        this._foreignProxy = null;
    }
    // foreign host request
    fhr(method, args) {
        if (!this._foreignModuleHost || typeof this._foreignModuleHost[method] !== 'function') {
            return Promise.reject(new Error('Missing method ' + method + ' or missing main thread foreign host.'));
        }
        try {
            return Promise.resolve(this._foreignModuleHost[method].apply(this._foreignModuleHost, args));
        }
        catch (e) {
            return Promise.reject(e);
        }
    }
    _getForeignProxy() {
        if (!this._foreignProxy) {
            this._foreignProxy = this._getProxy().then((proxy) => {
                const foreignHostMethods = this._foreignModuleHost ? types.getAllMethodNames(this._foreignModuleHost) : [];
                return proxy.loadForeignModule(this._foreignModuleId, this._foreignModuleCreateData, foreignHostMethods).then((foreignMethods) => {
                    this._foreignModuleCreateData = null;
                    const proxyMethodRequest = (method, args) => {
                        return proxy.fmr(method, args);
                    };
                    const createProxyMethod = (method, proxyMethodRequest) => {
                        return function () {
                            const args = Array.prototype.slice.call(arguments, 0);
                            return proxyMethodRequest(method, args);
                        };
                    };
                    let foreignProxy = {};
                    for (const foreignMethod of foreignMethods) {
                        foreignProxy[foreignMethod] = createProxyMethod(foreignMethod, proxyMethodRequest);
                    }
                    return foreignProxy;
                });
            });
        }
        return this._foreignProxy;
    }
    getProxy() {
        return this._getForeignProxy();
    }
    withSyncedResources(resources) {
        return this._withSyncedResources(resources).then(_ => this.getProxy());
    }
}
