/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { transformErrorForSerialization } from '../errors.js';
import { Disposable } from '../lifecycle.js';
import { isWeb } from '../platform.js';
import * as types from '../types.js';
const INITIALIZE = '$initialize';
let webWorkerWarningLogged = false;
export function logOnceWebWorkerWarning(err) {
    if (!isWeb) {
        // running tests
        return;
    }
    if (!webWorkerWarningLogged) {
        webWorkerWarningLogged = true;
        console.warn('Could not create web worker(s). Falling back to loading web worker code in main thread, which might cause UI freezes. Please see https://github.com/microsoft/monaco-editor#faq');
    }
    console.warn(err.message);
}
class SimpleWorkerProtocol {
    constructor(handler) {
        this._workerId = -1;
        this._handler = handler;
        this._lastSentReq = 0;
        this._pendingReplies = Object.create(null);
    }
    setWorkerId(workerId) {
        this._workerId = workerId;
    }
    sendMessage(method, args) {
        let req = String(++this._lastSentReq);
        return new Promise((resolve, reject) => {
            this._pendingReplies[req] = {
                resolve: resolve,
                reject: reject
            };
            this._send({
                vsWorker: this._workerId,
                req: req,
                method: method,
                args: args
            });
        });
    }
    handleMessage(message) {
        if (!message || !message.vsWorker) {
            return;
        }
        if (this._workerId !== -1 && message.vsWorker !== this._workerId) {
            return;
        }
        this._handleMessage(message);
    }
    _handleMessage(msg) {
        if (msg.seq) {
            let replyMessage = msg;
            if (!this._pendingReplies[replyMessage.seq]) {
                console.warn('Got reply to unknown seq');
                return;
            }
            let reply = this._pendingReplies[replyMessage.seq];
            delete this._pendingReplies[replyMessage.seq];
            if (replyMessage.err) {
                let err = replyMessage.err;
                if (replyMessage.err.$isError) {
                    err = new Error();
                    err.name = replyMessage.err.name;
                    err.message = replyMessage.err.message;
                    err.stack = replyMessage.err.stack;
                }
                reply.reject(err);
                return;
            }
            reply.resolve(replyMessage.res);
            return;
        }
        let requestMessage = msg;
        let req = requestMessage.req;
        let result = this._handler.handleMessage(requestMessage.method, requestMessage.args);
        result.then((r) => {
            this._send({
                vsWorker: this._workerId,
                seq: req,
                res: r,
                err: undefined
            });
        }, (e) => {
            if (e.detail instanceof Error) {
                // Loading errors have a detail property that points to the actual error
                e.detail = transformErrorForSerialization(e.detail);
            }
            this._send({
                vsWorker: this._workerId,
                seq: req,
                res: undefined,
                err: transformErrorForSerialization(e)
            });
        });
    }
    _send(msg) {
        let transfer = [];
        if (msg.req) {
            const m = msg;
            for (let i = 0; i < m.args.length; i++) {
                if (m.args[i] instanceof ArrayBuffer) {
                    transfer.push(m.args[i]);
                }
            }
        }
        else {
            const m = msg;
            if (m.res instanceof ArrayBuffer) {
                transfer.push(m.res);
            }
        }
        this._handler.sendMessage(msg, transfer);
    }
}
/**
 * Main thread side
 */
export class SimpleWorkerClient extends Disposable {
    constructor(workerFactory, moduleId, host) {
        super();
        let lazyProxyReject = null;
        this._worker = this._register(workerFactory.create('vs/base/common/worker/simpleWorker', (msg) => {
            this._protocol.handleMessage(msg);
        }, (err) => {
            // in Firefox, web workers fail lazily :(
            // we will reject the proxy
            if (lazyProxyReject) {
                lazyProxyReject(err);
            }
        }));
        this._protocol = new SimpleWorkerProtocol({
            sendMessage: (msg, transfer) => {
                this._worker.postMessage(msg, transfer);
            },
            handleMessage: (method, args) => {
                if (typeof host[method] !== 'function') {
                    return Promise.reject(new Error('Missing method ' + method + ' on main thread host.'));
                }
                try {
                    return Promise.resolve(host[method].apply(host, args));
                }
                catch (e) {
                    return Promise.reject(e);
                }
            }
        });
        this._protocol.setWorkerId(this._worker.getId());
        // Gather loader configuration
        let loaderConfiguration = null;
        if (typeof self.require !== 'undefined' && typeof self.require.getConfig === 'function') {
            // Get the configuration from the Monaco AMD Loader
            loaderConfiguration = self.require.getConfig();
        }
        else if (typeof self.requirejs !== 'undefined') {
            // Get the configuration from requirejs
            loaderConfiguration = self.requirejs.s.contexts._.config;
        }
        const hostMethods = types.getAllMethodNames(host);
        // Send initialize message
        this._onModuleLoaded = this._protocol.sendMessage(INITIALIZE, [
            this._worker.getId(),
            JSON.parse(JSON.stringify(loaderConfiguration)),
            moduleId,
            hostMethods,
        ]);
        // Create proxy to loaded code
        const proxyMethodRequest = (method, args) => {
            return this._request(method, args);
        };
        this._lazyProxy = new Promise((resolve, reject) => {
            lazyProxyReject = reject;
            this._onModuleLoaded.then((availableMethods) => {
                resolve(types.createProxyObject(availableMethods, proxyMethodRequest));
            }, (e) => {
                reject(e);
                this._onError('Worker failed to load ' + moduleId, e);
            });
        });
    }
    getProxyObject() {
        return this._lazyProxy;
    }
    _request(method, args) {
        return new Promise((resolve, reject) => {
            this._onModuleLoaded.then(() => {
                this._protocol.sendMessage(method, args).then(resolve, reject);
            }, reject);
        });
    }
    _onError(message, error) {
        console.error(message);
        console.info(error);
    }
}
/**
 * Worker side
 */
export class SimpleWorkerServer {
    constructor(postMessage, requestHandlerFactory) {
        this._requestHandlerFactory = requestHandlerFactory;
        this._requestHandler = null;
        this._protocol = new SimpleWorkerProtocol({
            sendMessage: (msg, transfer) => {
                postMessage(msg, transfer);
            },
            handleMessage: (method, args) => this._handleMessage(method, args)
        });
    }
    onmessage(msg) {
        this._protocol.handleMessage(msg);
    }
    _handleMessage(method, args) {
        if (method === INITIALIZE) {
            return this.initialize(args[0], args[1], args[2], args[3]);
        }
        if (!this._requestHandler || typeof this._requestHandler[method] !== 'function') {
            return Promise.reject(new Error('Missing requestHandler or method: ' + method));
        }
        try {
            return Promise.resolve(this._requestHandler[method].apply(this._requestHandler, args));
        }
        catch (e) {
            return Promise.reject(e);
        }
    }
    initialize(workerId, loaderConfig, moduleId, hostMethods) {
        this._protocol.setWorkerId(workerId);
        const proxyMethodRequest = (method, args) => {
            return this._protocol.sendMessage(method, args);
        };
        const hostProxy = types.createProxyObject(hostMethods, proxyMethodRequest);
        if (this._requestHandlerFactory) {
            // static request handler
            this._requestHandler = this._requestHandlerFactory(hostProxy);
            return Promise.resolve(types.getAllMethodNames(this._requestHandler));
        }
        if (loaderConfig) {
            // Remove 'baseUrl', handling it is beyond scope for now
            if (typeof loaderConfig.baseUrl !== 'undefined') {
                delete loaderConfig['baseUrl'];
            }
            if (typeof loaderConfig.paths !== 'undefined') {
                if (typeof loaderConfig.paths.vs !== 'undefined') {
                    delete loaderConfig.paths['vs'];
                }
            }
            if (typeof loaderConfig.trustedTypesPolicy !== undefined) {
                // don't use, it has been destroyed during serialize
                delete loaderConfig['trustedTypesPolicy'];
            }
            // Since this is in a web worker, enable catching errors
            loaderConfig.catchError = true;
            self.require.config(loaderConfig);
        }
        return new Promise((resolve, reject) => {
            // Use the global require to be sure to get the global config
            self.require([moduleId], (module) => {
                this._requestHandler = module.create(hostProxy);
                if (!this._requestHandler) {
                    reject(new Error(`No RequestHandler!`));
                    return;
                }
                resolve(types.getAllMethodNames(this._requestHandler));
            }, reject);
        });
    }
}
/**
 * Called on the worker side
 */
export function create(postMessage) {
    return new SimpleWorkerServer(postMessage, null);
}
