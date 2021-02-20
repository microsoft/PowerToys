/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { Emitter } from '../../../base/common/event.js';
import { hash } from '../../../base/common/hash.js';
import { toDisposable } from '../../../base/common/lifecycle.js';
import { LRUCache } from '../../../base/common/map.js';
import { MovingAverage } from '../../../base/common/numbers.js';
import { score } from './languageSelector.js';
import { shouldSynchronizeModel } from '../services/modelService.js';
function isExclusive(selector) {
    if (typeof selector === 'string') {
        return false;
    }
    else if (Array.isArray(selector)) {
        return selector.every(isExclusive);
    }
    else {
        return !!selector.exclusive;
    }
}
export class LanguageFeatureRegistry {
    constructor() {
        this._clock = 0;
        this._entries = [];
        this._onDidChange = new Emitter();
    }
    get onDidChange() {
        return this._onDidChange.event;
    }
    register(selector, provider) {
        let entry = {
            selector,
            provider,
            _score: -1,
            _time: this._clock++
        };
        this._entries.push(entry);
        this._lastCandidate = undefined;
        this._onDidChange.fire(this._entries.length);
        return toDisposable(() => {
            if (entry) {
                let idx = this._entries.indexOf(entry);
                if (idx >= 0) {
                    this._entries.splice(idx, 1);
                    this._lastCandidate = undefined;
                    this._onDidChange.fire(this._entries.length);
                    entry = undefined;
                }
            }
        });
    }
    has(model) {
        return this.all(model).length > 0;
    }
    all(model) {
        if (!model) {
            return [];
        }
        this._updateScores(model);
        const result = [];
        // from registry
        for (let entry of this._entries) {
            if (entry._score > 0) {
                result.push(entry.provider);
            }
        }
        return result;
    }
    ordered(model) {
        const result = [];
        this._orderedForEach(model, entry => result.push(entry.provider));
        return result;
    }
    orderedGroups(model) {
        const result = [];
        let lastBucket;
        let lastBucketScore;
        this._orderedForEach(model, entry => {
            if (lastBucket && lastBucketScore === entry._score) {
                lastBucket.push(entry.provider);
            }
            else {
                lastBucketScore = entry._score;
                lastBucket = [entry.provider];
                result.push(lastBucket);
            }
        });
        return result;
    }
    _orderedForEach(model, callback) {
        if (!model) {
            return;
        }
        this._updateScores(model);
        for (const entry of this._entries) {
            if (entry._score > 0) {
                callback(entry);
            }
        }
    }
    _updateScores(model) {
        let candidate = {
            uri: model.uri.toString(),
            language: model.getLanguageIdentifier().language
        };
        if (this._lastCandidate
            && this._lastCandidate.language === candidate.language
            && this._lastCandidate.uri === candidate.uri) {
            // nothing has changed
            return;
        }
        this._lastCandidate = candidate;
        for (let entry of this._entries) {
            entry._score = score(entry.selector, model.uri, model.getLanguageIdentifier().language, shouldSynchronizeModel(model));
            if (isExclusive(entry.selector) && entry._score > 0) {
                // support for one exclusive selector that overwrites
                // any other selector
                for (let entry of this._entries) {
                    entry._score = 0;
                }
                entry._score = 1000;
                break;
            }
        }
        // needs sorting
        this._entries.sort(LanguageFeatureRegistry._compareByScoreAndTime);
    }
    static _compareByScoreAndTime(a, b) {
        if (a._score < b._score) {
            return 1;
        }
        else if (a._score > b._score) {
            return -1;
        }
        else if (a._time < b._time) {
            return 1;
        }
        else if (a._time > b._time) {
            return -1;
        }
        else {
            return 0;
        }
    }
}
/**
 * Keeps moving average per model and set of providers so that requests
 * can be debounce according to the provider performance
 */
export class LanguageFeatureRequestDelays {
    constructor(_registry, min, max = Number.MAX_SAFE_INTEGER) {
        this._registry = _registry;
        this.min = min;
        this.max = max;
        this._cache = new LRUCache(50, 0.7);
    }
    _key(model) {
        return model.id + hash(this._registry.all(model));
    }
    _clamp(value) {
        if (value === undefined) {
            return this.min;
        }
        else {
            return Math.min(this.max, Math.max(this.min, Math.floor(value * 1.3)));
        }
    }
    get(model) {
        const key = this._key(model);
        const avg = this._cache.get(key);
        return this._clamp(avg === null || avg === void 0 ? void 0 : avg.value);
    }
    update(model, value) {
        const key = this._key(model);
        let avg = this._cache.get(key);
        if (!avg) {
            avg = new MovingAverage();
            this._cache.set(key, avg);
        }
        avg.update(value);
        return this.get(model);
    }
}
