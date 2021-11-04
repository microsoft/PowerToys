/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
'use strict';
export var browserNames = {
    E: 'Edge',
    FF: 'Firefox',
    S: 'Safari',
    C: 'Chrome',
    IE: 'IE',
    O: 'Opera'
};
function getEntryStatus(status) {
    switch (status) {
        case 'experimental':
            return '⚠️ Property is experimental. Be cautious when using it.️\n\n';
        case 'nonstandard':
            return '🚨️ Property is nonstandard. Avoid using it.\n\n';
        case 'obsolete':
            return '🚨️️️ Property is obsolete. Avoid using it.\n\n';
        default:
            return '';
    }
}
export function getEntryDescription(entry, doesSupportMarkdown, settings) {
    var result;
    if (doesSupportMarkdown) {
        result = {
            kind: 'markdown',
            value: getEntryMarkdownDescription(entry, settings)
        };
    }
    else {
        result = {
            kind: 'plaintext',
            value: getEntryStringDescription(entry, settings)
        };
    }
    if (result.value === '') {
        return undefined;
    }
    return result;
}
export function textToMarkedString(text) {
    text = text.replace(/[\\`*_{}[\]()#+\-.!]/g, '\\$&'); // escape markdown syntax tokens: http://daringfireball.net/projects/markdown/syntax#backslash
    return text.replace(/</g, '&lt;').replace(/>/g, '&gt;');
}
function getEntryStringDescription(entry, settings) {
    if (!entry.description || entry.description === '') {
        return '';
    }
    if (typeof entry.description !== 'string') {
        return entry.description.value;
    }
    var result = '';
    if ((settings === null || settings === void 0 ? void 0 : settings.documentation) !== false) {
        if (entry.status) {
            result += getEntryStatus(entry.status);
        }
        result += entry.description;
        var browserLabel = getBrowserLabel(entry.browsers);
        if (browserLabel) {
            result += '\n(' + browserLabel + ')';
        }
        if ('syntax' in entry) {
            result += "\n\nSyntax: " + entry.syntax;
        }
    }
    if (entry.references && entry.references.length > 0 && (settings === null || settings === void 0 ? void 0 : settings.references) !== false) {
        if (result.length > 0) {
            result += '\n\n';
        }
        result += entry.references.map(function (r) {
            return r.name + ": " + r.url;
        }).join(' | ');
    }
    return result;
}
function getEntryMarkdownDescription(entry, settings) {
    if (!entry.description || entry.description === '') {
        return '';
    }
    var result = '';
    if ((settings === null || settings === void 0 ? void 0 : settings.documentation) !== false) {
        if (entry.status) {
            result += getEntryStatus(entry.status);
        }
        var description = typeof entry.description === 'string' ? entry.description : entry.description.value;
        result += textToMarkedString(description);
        var browserLabel = getBrowserLabel(entry.browsers);
        if (browserLabel) {
            result += '\n\n(' + textToMarkedString(browserLabel) + ')';
        }
        if ('syntax' in entry && entry.syntax) {
            result += "\n\nSyntax: " + textToMarkedString(entry.syntax);
        }
    }
    if (entry.references && entry.references.length > 0 && (settings === null || settings === void 0 ? void 0 : settings.references) !== false) {
        if (result.length > 0) {
            result += '\n\n';
        }
        result += entry.references.map(function (r) {
            return "[" + r.name + "](" + r.url + ")";
        }).join(' | ');
    }
    return result;
}
/**
 * Input is like `["E12","FF49","C47","IE","O"]`
 * Output is like `Edge 12, Firefox 49, Chrome 47, IE, Opera`
 */
export function getBrowserLabel(browsers) {
    if (browsers === void 0) { browsers = []; }
    if (browsers.length === 0) {
        return null;
    }
    return browsers
        .map(function (b) {
        var result = '';
        var matches = b.match(/([A-Z]+)(\d+)?/);
        var name = matches[1];
        var version = matches[2];
        if (name in browserNames) {
            result += browserNames[name];
        }
        if (version) {
            result += ' ' + version;
        }
        return result;
    })
        .join(', ');
}
