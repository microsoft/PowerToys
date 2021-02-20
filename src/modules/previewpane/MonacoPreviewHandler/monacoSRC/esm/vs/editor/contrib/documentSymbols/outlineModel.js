/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { equals } from '../../../base/common/arrays.js';
import { CancellationTokenSource } from '../../../base/common/cancellation.js';
import { onUnexpectedExternalError } from '../../../base/common/errors.js';
import { LRUCache } from '../../../base/common/map.js';
import { Range } from '../../common/core/range.js';
import { DocumentSymbolProviderRegistry } from '../../common/modes.js';
import { Iterable } from '../../../base/common/iterator.js';
import { LanguageFeatureRequestDelays } from '../../common/modes/languageFeatureRegistry.js';
export class TreeElement {
    remove() {
        if (this.parent) {
            this.parent.children.delete(this.id);
        }
    }
    static findId(candidate, container) {
        // complex id-computation which contains the origin/extension,
        // the parent path, and some dedupe logic when names collide
        let candidateId;
        if (typeof candidate === 'string') {
            candidateId = `${container.id}/${candidate}`;
        }
        else {
            candidateId = `${container.id}/${candidate.name}`;
            if (container.children.get(candidateId) !== undefined) {
                candidateId = `${container.id}/${candidate.name}_${candidate.range.startLineNumber}_${candidate.range.startColumn}`;
            }
        }
        let id = candidateId;
        for (let i = 0; container.children.get(id) !== undefined; i++) {
            id = `${candidateId}_${i}`;
        }
        return id;
    }
    static empty(element) {
        return element.children.size === 0;
    }
}
export class OutlineElement extends TreeElement {
    constructor(id, parent, symbol) {
        super();
        this.id = id;
        this.parent = parent;
        this.symbol = symbol;
        this.children = new Map();
    }
}
export class OutlineGroup extends TreeElement {
    constructor(id, parent, label, order) {
        super();
        this.id = id;
        this.parent = parent;
        this.label = label;
        this.order = order;
        this.children = new Map();
    }
}
export class OutlineModel extends TreeElement {
    constructor(uri) {
        super();
        this.uri = uri;
        this.id = 'root';
        this.parent = undefined;
        this._groups = new Map();
        this.children = new Map();
        this.id = 'root';
        this.parent = undefined;
    }
    static create(textModel, token) {
        let key = this._keys.for(textModel, true);
        let data = OutlineModel._requests.get(key);
        if (!data) {
            let source = new CancellationTokenSource();
            data = {
                promiseCnt: 0,
                source,
                promise: OutlineModel._create(textModel, source.token),
                model: undefined,
            };
            OutlineModel._requests.set(key, data);
            // keep moving average of request durations
            const now = Date.now();
            data.promise.then(() => {
                this._requestDurations.update(textModel, Date.now() - now);
            });
        }
        if (data.model) {
            // resolved -> return data
            return Promise.resolve(data.model);
        }
        // increase usage counter
        data.promiseCnt += 1;
        token.onCancellationRequested(() => {
            // last -> cancel provider request, remove cached promise
            if (--data.promiseCnt === 0) {
                data.source.cancel();
                OutlineModel._requests.delete(key);
            }
        });
        return new Promise((resolve, reject) => {
            data.promise.then(model => {
                data.model = model;
                resolve(model);
            }, err => {
                OutlineModel._requests.delete(key);
                reject(err);
            });
        });
    }
    static _create(textModel, token) {
        const cts = new CancellationTokenSource(token);
        const result = new OutlineModel(textModel.uri);
        const provider = DocumentSymbolProviderRegistry.ordered(textModel);
        const promises = provider.map((provider, index) => {
            var _a;
            let id = TreeElement.findId(`provider_${index}`, result);
            let group = new OutlineGroup(id, result, (_a = provider.displayName) !== null && _a !== void 0 ? _a : 'Unknown Outline Provider', index);
            return Promise.resolve(provider.provideDocumentSymbols(textModel, cts.token)).then(result => {
                for (const info of result || []) {
                    OutlineModel._makeOutlineElement(info, group);
                }
                return group;
            }, err => {
                onUnexpectedExternalError(err);
                return group;
            }).then(group => {
                if (!TreeElement.empty(group)) {
                    result._groups.set(id, group);
                }
                else {
                    group.remove();
                }
            });
        });
        const listener = DocumentSymbolProviderRegistry.onDidChange(() => {
            const newProvider = DocumentSymbolProviderRegistry.ordered(textModel);
            if (!equals(newProvider, provider)) {
                cts.cancel();
            }
        });
        return Promise.all(promises).then(() => {
            if (cts.token.isCancellationRequested && !token.isCancellationRequested) {
                return OutlineModel._create(textModel, token);
            }
            else {
                return result._compact();
            }
        }).finally(() => {
            listener.dispose();
        });
    }
    static _makeOutlineElement(info, container) {
        let id = TreeElement.findId(info, container);
        let res = new OutlineElement(id, container, info);
        if (info.children) {
            for (const childInfo of info.children) {
                OutlineModel._makeOutlineElement(childInfo, res);
            }
        }
        container.children.set(res.id, res);
    }
    _compact() {
        let count = 0;
        for (const [key, group] of this._groups) {
            if (group.children.size === 0) { // empty
                this._groups.delete(key);
            }
            else {
                count += 1;
            }
        }
        if (count !== 1) {
            //
            this.children = this._groups;
        }
        else {
            // adopt all elements of the first group
            let group = Iterable.first(this._groups.values());
            for (let [, child] of group.children) {
                child.parent = this;
                this.children.set(child.id, child);
            }
        }
        return this;
    }
    getTopLevelSymbols() {
        const roots = [];
        for (const child of this.children.values()) {
            if (child instanceof OutlineElement) {
                roots.push(child.symbol);
            }
            else {
                roots.push(...Iterable.map(child.children.values(), child => child.symbol));
            }
        }
        return roots.sort((a, b) => Range.compareRangesUsingStarts(a.range, b.range));
    }
    asListOfDocumentSymbols() {
        const roots = this.getTopLevelSymbols();
        const bucket = [];
        OutlineModel._flattenDocumentSymbols(bucket, roots, '');
        return bucket.sort((a, b) => Range.compareRangesUsingStarts(a.range, b.range));
    }
    static _flattenDocumentSymbols(bucket, entries, overrideContainerLabel) {
        for (const entry of entries) {
            bucket.push({
                kind: entry.kind,
                tags: entry.tags,
                name: entry.name,
                detail: entry.detail,
                containerName: entry.containerName || overrideContainerLabel,
                range: entry.range,
                selectionRange: entry.selectionRange,
                children: undefined, // we flatten it...
            });
            // Recurse over children
            if (entry.children) {
                OutlineModel._flattenDocumentSymbols(bucket, entry.children, entry.name);
            }
        }
    }
}
OutlineModel._requestDurations = new LanguageFeatureRequestDelays(DocumentSymbolProviderRegistry, 350);
OutlineModel._requests = new LRUCache(9, 0.75);
OutlineModel._keys = new class {
    constructor() {
        this._counter = 1;
        this._data = new WeakMap();
    }
    for(textModel, version) {
        return `${textModel.id}/${version ? textModel.getVersionId() : ''}/${this._hash(DocumentSymbolProviderRegistry.all(textModel))}`;
    }
    _hash(providers) {
        let result = '';
        for (const provider of providers) {
            let n = this._data.get(provider);
            if (typeof n === 'undefined') {
                n = this._counter++;
                this._data.set(provider, n);
            }
            result += n;
        }
        return result;
    }
};
