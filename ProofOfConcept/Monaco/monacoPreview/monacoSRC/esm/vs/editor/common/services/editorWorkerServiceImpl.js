/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
var __decorate = (this && this.__decorate) || function (decorators, target, key, desc) {
    var c = arguments.length, r = c < 3 ? target : desc === null ? desc = Object.getOwnPropertyDescriptor(target, key) : desc, d;
    if (typeof Reflect === "object" && typeof Reflect.decorate === "function") r = Reflect.decorate(decorators, target, key, desc);
    else for (var i = decorators.length - 1; i >= 0; i--) if (d = decorators[i]) r = (c < 3 ? d(r) : c > 3 ? d(target, key, r) : d(target, key)) || r;
    return c > 3 && r && Object.defineProperty(target, key, r), r;
};
var __param = (this && this.__param) || function (paramIndex, decorator) {
    return function (target, key) { decorator(target, key, paramIndex); }
};
var __awaiter = (this && this.__awaiter) || function (thisArg, _arguments, P, generator) {
    function adopt(value) { return value instanceof P ? value : new P(function (resolve) { resolve(value); }); }
    return new (P || (P = Promise))(function (resolve, reject) {
        function fulfilled(value) { try { step(generator.next(value)); } catch (e) { reject(e); } }
        function rejected(value) { try { step(generator["throw"](value)); } catch (e) { reject(e); } }
        function step(result) { result.done ? resolve(result.value) : adopt(result.value).then(fulfilled, rejected); }
        step((generator = generator.apply(thisArg, _arguments || [])).next());
    });
};
import { IntervalTimer, timeout } from '../../../base/common/async.js';
import { Disposable, dispose, toDisposable, DisposableStore } from '../../../base/common/lifecycle.js';
import { SimpleWorkerClient, logOnceWebWorkerWarning } from '../../../base/common/worker/simpleWorker.js';
import { DefaultWorkerFactory } from '../../../base/worker/defaultWorkerFactory.js';
import { Range } from '../core/range.js';
import * as modes from '../modes.js';
import { LanguageConfigurationRegistry } from '../modes/languageConfigurationRegistry.js';
import { EditorSimpleWorker } from './editorSimpleWorker.js';
import { IModelService } from './modelService.js';
import { ITextResourceConfigurationService } from './textResourceConfigurationService.js';
import { regExpFlags } from '../../../base/common/strings.js';
import { isNonEmptyArray } from '../../../base/common/arrays.js';
import { ILogService } from '../../../platform/log/common/log.js';
import { StopWatch } from '../../../base/common/stopwatch.js';
import { canceled } from '../../../base/common/errors.js';
/**
 * Stop syncing a model to the worker if it was not needed for 1 min.
 */
const STOP_SYNC_MODEL_DELTA_TIME_MS = 60 * 1000;
/**
 * Stop the worker if it was not needed for 5 min.
 */
const STOP_WORKER_DELTA_TIME_MS = 5 * 60 * 1000;
function canSyncModel(modelService, resource) {
    let model = modelService.getModel(resource);
    if (!model) {
        return false;
    }
    if (model.isTooLargeForSyncing()) {
        return false;
    }
    return true;
}
let EditorWorkerServiceImpl = class EditorWorkerServiceImpl extends Disposable {
    constructor(modelService, configurationService, logService) {
        super();
        this._modelService = modelService;
        this._workerManager = this._register(new WorkerManager(this._modelService));
        this._logService = logService;
        // register default link-provider and default completions-provider
        this._register(modes.LinkProviderRegistry.register('*', {
            provideLinks: (model, token) => {
                if (!canSyncModel(this._modelService, model.uri)) {
                    return Promise.resolve({ links: [] }); // File too large
                }
                return this._workerManager.withWorker().then(client => client.computeLinks(model.uri)).then(links => {
                    return links && { links };
                });
            }
        }));
        this._register(modes.CompletionProviderRegistry.register('*', new WordBasedCompletionItemProvider(this._workerManager, configurationService, this._modelService)));
    }
    dispose() {
        super.dispose();
    }
    canComputeDiff(original, modified) {
        return (canSyncModel(this._modelService, original) && canSyncModel(this._modelService, modified));
    }
    computeDiff(original, modified, ignoreTrimWhitespace, maxComputationTime) {
        return this._workerManager.withWorker().then(client => client.computeDiff(original, modified, ignoreTrimWhitespace, maxComputationTime));
    }
    computeMoreMinimalEdits(resource, edits) {
        if (isNonEmptyArray(edits)) {
            if (!canSyncModel(this._modelService, resource)) {
                return Promise.resolve(edits); // File too large
            }
            const sw = StopWatch.create(true);
            const result = this._workerManager.withWorker().then(client => client.computeMoreMinimalEdits(resource, edits));
            result.finally(() => this._logService.trace('FORMAT#computeMoreMinimalEdits', resource.toString(true), sw.elapsed()));
            return Promise.race([result, timeout(1000).then(() => edits)]);
        }
        else {
            return Promise.resolve(undefined);
        }
    }
    canNavigateValueSet(resource) {
        return (canSyncModel(this._modelService, resource));
    }
    navigateValueSet(resource, range, up) {
        return this._workerManager.withWorker().then(client => client.navigateValueSet(resource, range, up));
    }
    canComputeWordRanges(resource) {
        return canSyncModel(this._modelService, resource);
    }
    computeWordRanges(resource, range) {
        return this._workerManager.withWorker().then(client => client.computeWordRanges(resource, range));
    }
};
EditorWorkerServiceImpl = __decorate([
    __param(0, IModelService),
    __param(1, ITextResourceConfigurationService),
    __param(2, ILogService)
], EditorWorkerServiceImpl);
export { EditorWorkerServiceImpl };
class WordBasedCompletionItemProvider {
    constructor(workerManager, configurationService, modelService) {
        this._debugDisplayName = 'wordbasedCompletions';
        this._workerManager = workerManager;
        this._configurationService = configurationService;
        this._modelService = modelService;
    }
    provideCompletionItems(model, position) {
        return __awaiter(this, void 0, void 0, function* () {
            const config = this._configurationService.getValue(model.uri, position, 'editor');
            if (!config.wordBasedSuggestions) {
                return undefined;
            }
            const models = [];
            if (config.wordBasedSuggestionsMode === 'currentDocument') {
                // only current file and only if not too large
                if (canSyncModel(this._modelService, model.uri)) {
                    models.push(model.uri);
                }
            }
            else {
                // either all files or files of same language
                for (const candidate of this._modelService.getModels()) {
                    if (!canSyncModel(this._modelService, candidate.uri)) {
                        continue;
                    }
                    if (candidate === model) {
                        models.unshift(candidate.uri);
                    }
                    else if (config.wordBasedSuggestionsMode === 'allDocuments' || candidate.getLanguageIdentifier().id === model.getLanguageIdentifier().id) {
                        models.push(candidate.uri);
                    }
                }
            }
            if (models.length === 0) {
                return undefined; // File too large, no other files
            }
            const wordDefRegExp = LanguageConfigurationRegistry.getWordDefinition(model.getLanguageIdentifier().id);
            const word = model.getWordAtPosition(position);
            const replace = !word ? Range.fromPositions(position) : new Range(position.lineNumber, word.startColumn, position.lineNumber, word.endColumn);
            const insert = replace.setEndPosition(position.lineNumber, position.column);
            const client = yield this._workerManager.withWorker();
            const data = yield client.textualSuggest(models, word === null || word === void 0 ? void 0 : word.word, wordDefRegExp);
            if (!data) {
                return undefined;
            }
            return {
                duration: data.duration,
                suggestions: data.words.map((word) => {
                    return {
                        kind: 18 /* Text */,
                        label: word,
                        insertText: word,
                        range: { insert, replace }
                    };
                }),
            };
        });
    }
}
class WorkerManager extends Disposable {
    constructor(modelService) {
        super();
        this._modelService = modelService;
        this._editorWorkerClient = null;
        this._lastWorkerUsedTime = (new Date()).getTime();
        let stopWorkerInterval = this._register(new IntervalTimer());
        stopWorkerInterval.cancelAndSet(() => this._checkStopIdleWorker(), Math.round(STOP_WORKER_DELTA_TIME_MS / 2));
        this._register(this._modelService.onModelRemoved(_ => this._checkStopEmptyWorker()));
    }
    dispose() {
        if (this._editorWorkerClient) {
            this._editorWorkerClient.dispose();
            this._editorWorkerClient = null;
        }
        super.dispose();
    }
    /**
     * Check if the model service has no more models and stop the worker if that is the case.
     */
    _checkStopEmptyWorker() {
        if (!this._editorWorkerClient) {
            return;
        }
        let models = this._modelService.getModels();
        if (models.length === 0) {
            // There are no more models => nothing possible for me to do
            this._editorWorkerClient.dispose();
            this._editorWorkerClient = null;
        }
    }
    /**
     * Check if the worker has been idle for a while and then stop it.
     */
    _checkStopIdleWorker() {
        if (!this._editorWorkerClient) {
            return;
        }
        let timeSinceLastWorkerUsedTime = (new Date()).getTime() - this._lastWorkerUsedTime;
        if (timeSinceLastWorkerUsedTime > STOP_WORKER_DELTA_TIME_MS) {
            this._editorWorkerClient.dispose();
            this._editorWorkerClient = null;
        }
    }
    withWorker() {
        this._lastWorkerUsedTime = (new Date()).getTime();
        if (!this._editorWorkerClient) {
            this._editorWorkerClient = new EditorWorkerClient(this._modelService, false, 'editorWorkerService');
        }
        return Promise.resolve(this._editorWorkerClient);
    }
}
class EditorModelManager extends Disposable {
    constructor(proxy, modelService, keepIdleModels) {
        super();
        this._syncedModels = Object.create(null);
        this._syncedModelsLastUsedTime = Object.create(null);
        this._proxy = proxy;
        this._modelService = modelService;
        if (!keepIdleModels) {
            let timer = new IntervalTimer();
            timer.cancelAndSet(() => this._checkStopModelSync(), Math.round(STOP_SYNC_MODEL_DELTA_TIME_MS / 2));
            this._register(timer);
        }
    }
    dispose() {
        for (let modelUrl in this._syncedModels) {
            dispose(this._syncedModels[modelUrl]);
        }
        this._syncedModels = Object.create(null);
        this._syncedModelsLastUsedTime = Object.create(null);
        super.dispose();
    }
    ensureSyncedResources(resources) {
        for (const resource of resources) {
            let resourceStr = resource.toString();
            if (!this._syncedModels[resourceStr]) {
                this._beginModelSync(resource);
            }
            if (this._syncedModels[resourceStr]) {
                this._syncedModelsLastUsedTime[resourceStr] = (new Date()).getTime();
            }
        }
    }
    _checkStopModelSync() {
        let currentTime = (new Date()).getTime();
        let toRemove = [];
        for (let modelUrl in this._syncedModelsLastUsedTime) {
            let elapsedTime = currentTime - this._syncedModelsLastUsedTime[modelUrl];
            if (elapsedTime > STOP_SYNC_MODEL_DELTA_TIME_MS) {
                toRemove.push(modelUrl);
            }
        }
        for (const e of toRemove) {
            this._stopModelSync(e);
        }
    }
    _beginModelSync(resource) {
        let model = this._modelService.getModel(resource);
        if (!model) {
            return;
        }
        if (model.isTooLargeForSyncing()) {
            return;
        }
        let modelUrl = resource.toString();
        this._proxy.acceptNewModel({
            url: model.uri.toString(),
            lines: model.getLinesContent(),
            EOL: model.getEOL(),
            versionId: model.getVersionId()
        });
        const toDispose = new DisposableStore();
        toDispose.add(model.onDidChangeContent((e) => {
            this._proxy.acceptModelChanged(modelUrl.toString(), e);
        }));
        toDispose.add(model.onWillDispose(() => {
            this._stopModelSync(modelUrl);
        }));
        toDispose.add(toDisposable(() => {
            this._proxy.acceptRemovedModel(modelUrl);
        }));
        this._syncedModels[modelUrl] = toDispose;
    }
    _stopModelSync(modelUrl) {
        let toDispose = this._syncedModels[modelUrl];
        delete this._syncedModels[modelUrl];
        delete this._syncedModelsLastUsedTime[modelUrl];
        dispose(toDispose);
    }
}
class SynchronousWorkerClient {
    constructor(instance) {
        this._instance = instance;
        this._proxyObj = Promise.resolve(this._instance);
    }
    dispose() {
        this._instance.dispose();
    }
    getProxyObject() {
        return this._proxyObj;
    }
}
export class EditorWorkerHost {
    constructor(workerClient) {
        this._workerClient = workerClient;
    }
    // foreign host request
    fhr(method, args) {
        return this._workerClient.fhr(method, args);
    }
}
export class EditorWorkerClient extends Disposable {
    constructor(modelService, keepIdleModels, label) {
        super();
        this._disposed = false;
        this._modelService = modelService;
        this._keepIdleModels = keepIdleModels;
        this._workerFactory = new DefaultWorkerFactory(label);
        this._worker = null;
        this._modelManager = null;
    }
    // foreign host request
    fhr(method, args) {
        throw new Error(`Not implemented!`);
    }
    _getOrCreateWorker() {
        if (!this._worker) {
            try {
                this._worker = this._register(new SimpleWorkerClient(this._workerFactory, 'vs/editor/common/services/editorSimpleWorker', new EditorWorkerHost(this)));
            }
            catch (err) {
                logOnceWebWorkerWarning(err);
                this._worker = new SynchronousWorkerClient(new EditorSimpleWorker(new EditorWorkerHost(this), null));
            }
        }
        return this._worker;
    }
    _getProxy() {
        return this._getOrCreateWorker().getProxyObject().then(undefined, (err) => {
            logOnceWebWorkerWarning(err);
            this._worker = new SynchronousWorkerClient(new EditorSimpleWorker(new EditorWorkerHost(this), null));
            return this._getOrCreateWorker().getProxyObject();
        });
    }
    _getOrCreateModelManager(proxy) {
        if (!this._modelManager) {
            this._modelManager = this._register(new EditorModelManager(proxy, this._modelService, this._keepIdleModels));
        }
        return this._modelManager;
    }
    _withSyncedResources(resources) {
        if (this._disposed) {
            return Promise.reject(canceled());
        }
        return this._getProxy().then((proxy) => {
            this._getOrCreateModelManager(proxy).ensureSyncedResources(resources);
            return proxy;
        });
    }
    computeDiff(original, modified, ignoreTrimWhitespace, maxComputationTime) {
        return this._withSyncedResources([original, modified]).then(proxy => {
            return proxy.computeDiff(original.toString(), modified.toString(), ignoreTrimWhitespace, maxComputationTime);
        });
    }
    computeMoreMinimalEdits(resource, edits) {
        return this._withSyncedResources([resource]).then(proxy => {
            return proxy.computeMoreMinimalEdits(resource.toString(), edits);
        });
    }
    computeLinks(resource) {
        return this._withSyncedResources([resource]).then(proxy => {
            return proxy.computeLinks(resource.toString());
        });
    }
    textualSuggest(resources, leadingWord, wordDefRegExp) {
        return __awaiter(this, void 0, void 0, function* () {
            const proxy = yield this._withSyncedResources(resources);
            const wordDef = wordDefRegExp.source;
            const wordDefFlags = regExpFlags(wordDefRegExp);
            return proxy.textualSuggest(resources.map(r => r.toString()), leadingWord, wordDef, wordDefFlags);
        });
    }
    computeWordRanges(resource, range) {
        return this._withSyncedResources([resource]).then(proxy => {
            let model = this._modelService.getModel(resource);
            if (!model) {
                return Promise.resolve(null);
            }
            let wordDefRegExp = LanguageConfigurationRegistry.getWordDefinition(model.getLanguageIdentifier().id);
            let wordDef = wordDefRegExp.source;
            let wordDefFlags = regExpFlags(wordDefRegExp);
            return proxy.computeWordRanges(resource.toString(), range, wordDef, wordDefFlags);
        });
    }
    navigateValueSet(resource, range, up) {
        return this._withSyncedResources([resource]).then(proxy => {
            let model = this._modelService.getModel(resource);
            if (!model) {
                return null;
            }
            let wordDefRegExp = LanguageConfigurationRegistry.getWordDefinition(model.getLanguageIdentifier().id);
            let wordDef = wordDefRegExp.source;
            let wordDefFlags = regExpFlags(wordDefRegExp);
            return proxy.navigateValueSet(resource.toString(), range, up, wordDef, wordDefFlags);
        });
    }
    dispose() {
        super.dispose();
        this._disposed = true;
    }
}
