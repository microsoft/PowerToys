/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
var __awaiter = (this && this.__awaiter) || function (thisArg, _arguments, P, generator) {
    function adopt(value) { return value instanceof P ? value : new P(function (resolve) { resolve(value); }); }
    return new (P || (P = Promise))(function (resolve, reject) {
        function fulfilled(value) { try { step(generator.next(value)); } catch (e) { reject(e); } }
        function rejected(value) { try { step(generator["throw"](value)); } catch (e) { reject(e); } }
        function step(result) { result.done ? resolve(result.value) : adopt(result.value).then(fulfilled, rejected); }
        step((generator = generator.apply(thisArg, _arguments || [])).next());
    });
};
import { CancellationToken } from '../../../base/common/cancellation.js';
import { onUnexpectedExternalError } from '../../../base/common/errors.js';
import { URI } from '../../../base/common/uri.js';
import { Range } from '../../common/core/range.js';
import { LinkProviderRegistry } from '../../common/modes.js';
import { IModelService } from '../../common/services/modelService.js';
import { CommandsRegistry } from '../../../platform/commands/common/commands.js';
import { isDisposable, DisposableStore } from '../../../base/common/lifecycle.js';
import { coalesce } from '../../../base/common/arrays.js';
import { assertType } from '../../../base/common/types.js';
export class Link {
    constructor(link, provider) {
        this._link = link;
        this._provider = provider;
    }
    toJSON() {
        return {
            range: this.range,
            url: this.url,
            tooltip: this.tooltip
        };
    }
    get range() {
        return this._link.range;
    }
    get url() {
        return this._link.url;
    }
    get tooltip() {
        return this._link.tooltip;
    }
    resolve(token) {
        return __awaiter(this, void 0, void 0, function* () {
            if (this._link.url) {
                return this._link.url;
            }
            if (typeof this._provider.resolveLink === 'function') {
                return Promise.resolve(this._provider.resolveLink(this._link, token)).then(value => {
                    this._link = value || this._link;
                    if (this._link.url) {
                        // recurse
                        return this.resolve(token);
                    }
                    return Promise.reject(new Error('missing'));
                });
            }
            return Promise.reject(new Error('missing'));
        });
    }
}
export class LinksList {
    constructor(tuples) {
        this._disposables = new DisposableStore();
        let links = [];
        for (const [list, provider] of tuples) {
            // merge all links
            const newLinks = list.links.map(link => new Link(link, provider));
            links = LinksList._union(links, newLinks);
            // register disposables
            if (isDisposable(list)) {
                this._disposables.add(list);
            }
        }
        this.links = links;
    }
    dispose() {
        this._disposables.dispose();
        this.links.length = 0;
    }
    static _union(oldLinks, newLinks) {
        // reunite oldLinks with newLinks and remove duplicates
        let result = [];
        let oldIndex;
        let oldLen;
        let newIndex;
        let newLen;
        for (oldIndex = 0, newIndex = 0, oldLen = oldLinks.length, newLen = newLinks.length; oldIndex < oldLen && newIndex < newLen;) {
            const oldLink = oldLinks[oldIndex];
            const newLink = newLinks[newIndex];
            if (Range.areIntersectingOrTouching(oldLink.range, newLink.range)) {
                // Remove the oldLink
                oldIndex++;
                continue;
            }
            const comparisonResult = Range.compareRangesUsingStarts(oldLink.range, newLink.range);
            if (comparisonResult < 0) {
                // oldLink is before
                result.push(oldLink);
                oldIndex++;
            }
            else {
                // newLink is before
                result.push(newLink);
                newIndex++;
            }
        }
        for (; oldIndex < oldLen; oldIndex++) {
            result.push(oldLinks[oldIndex]);
        }
        for (; newIndex < newLen; newIndex++) {
            result.push(newLinks[newIndex]);
        }
        return result;
    }
}
export function getLinks(model, token) {
    const lists = [];
    // ask all providers for links in parallel
    const promises = LinkProviderRegistry.ordered(model).reverse().map((provider, i) => {
        return Promise.resolve(provider.provideLinks(model, token)).then(result => {
            if (result) {
                lists[i] = [result, provider];
            }
        }, onUnexpectedExternalError);
    });
    return Promise.all(promises).then(() => {
        const result = new LinksList(coalesce(lists));
        if (!token.isCancellationRequested) {
            return result;
        }
        result.dispose();
        return new LinksList([]);
    });
}
CommandsRegistry.registerCommand('_executeLinkProvider', (accessor, ...args) => __awaiter(void 0, void 0, void 0, function* () {
    let [uri, resolveCount] = args;
    assertType(uri instanceof URI);
    if (typeof resolveCount !== 'number') {
        resolveCount = 0;
    }
    const model = accessor.get(IModelService).getModel(uri);
    if (!model) {
        return [];
    }
    const list = yield getLinks(model, CancellationToken.None);
    if (!list) {
        return [];
    }
    // resolve links
    for (let i = 0; i < Math.min(resolveCount, list.links.length); i++) {
        yield list.links[i].resolve(CancellationToken.None);
    }
    const result = list.links.slice(0);
    list.dispose();
    return result;
}));
