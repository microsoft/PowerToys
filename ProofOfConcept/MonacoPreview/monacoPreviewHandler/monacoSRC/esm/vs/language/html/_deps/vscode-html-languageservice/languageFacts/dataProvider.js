/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { normalizeMarkupContent } from '../utils/markup.js';
var HTMLDataProvider = /** @class */ (function () {
    /**
     * Currently, unversioned data uses the V1 implementation
     * In the future when the provider handles multiple versions of HTML custom data,
     * use the latest implementation for unversioned data
     */
    function HTMLDataProvider(id, customData) {
        var _this = this;
        this.id = id;
        this._tags = [];
        this._tagMap = {};
        this._valueSetMap = {};
        this._tags = customData.tags || [];
        this._globalAttributes = customData.globalAttributes || [];
        this._tags.forEach(function (t) {
            _this._tagMap[t.name.toLowerCase()] = t;
        });
        if (customData.valueSets) {
            customData.valueSets.forEach(function (vs) {
                _this._valueSetMap[vs.name] = vs.values;
            });
        }
    }
    HTMLDataProvider.prototype.isApplicable = function () {
        return true;
    };
    HTMLDataProvider.prototype.getId = function () {
        return this.id;
    };
    HTMLDataProvider.prototype.provideTags = function () {
        return this._tags;
    };
    HTMLDataProvider.prototype.provideAttributes = function (tag) {
        var attributes = [];
        var processAttribute = function (a) {
            attributes.push(a);
        };
        var tagEntry = this._tagMap[tag.toLowerCase()];
        if (tagEntry) {
            tagEntry.attributes.forEach(processAttribute);
        }
        this._globalAttributes.forEach(processAttribute);
        return attributes;
    };
    HTMLDataProvider.prototype.provideValues = function (tag, attribute) {
        var _this = this;
        var values = [];
        attribute = attribute.toLowerCase();
        var processAttributes = function (attributes) {
            attributes.forEach(function (a) {
                if (a.name.toLowerCase() === attribute) {
                    if (a.values) {
                        a.values.forEach(function (v) {
                            values.push(v);
                        });
                    }
                    if (a.valueSet) {
                        if (_this._valueSetMap[a.valueSet]) {
                            _this._valueSetMap[a.valueSet].forEach(function (v) {
                                values.push(v);
                            });
                        }
                    }
                }
            });
        };
        var tagEntry = this._tagMap[tag.toLowerCase()];
        if (!tagEntry) {
            return [];
        }
        processAttributes(tagEntry.attributes);
        processAttributes(this._globalAttributes);
        return values;
    };
    return HTMLDataProvider;
}());
export { HTMLDataProvider };
/**
 * Generate Documentation used in hover/complete
 * From `documentation` and `references`
 */
export function generateDocumentation(item, settings, doesSupportMarkdown) {
    if (settings === void 0) { settings = {}; }
    var result = {
        kind: doesSupportMarkdown ? 'markdown' : 'plaintext',
        value: ''
    };
    if (item.description && settings.documentation !== false) {
        var normalizedDescription = normalizeMarkupContent(item.description);
        if (normalizedDescription) {
            result.value += normalizedDescription.value;
        }
    }
    if (item.references && item.references.length > 0 && settings.references !== false) {
        if (result.value.length) {
            result.value += "\n\n";
        }
        if (doesSupportMarkdown) {
            result.value += item.references.map(function (r) {
                return "[" + r.name + "](" + r.url + ")";
            }).join(' | ');
        }
        else {
            result.value += item.references.map(function (r) {
                return r.name + ": " + r.url;
            }).join('\n');
        }
    }
    if (result.value === '') {
        return undefined;
    }
    return result;
}
