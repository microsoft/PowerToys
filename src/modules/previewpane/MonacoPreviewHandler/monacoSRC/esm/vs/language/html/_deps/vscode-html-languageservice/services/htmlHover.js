/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { createScanner } from '../parser/htmlScanner.js';
import { TokenType, Range, Position, MarkupKind } from '../htmlLanguageTypes.js';
import { isDefined } from '../utils/object.js';
import { generateDocumentation } from '../languageFacts/dataProvider.js';
import { entities } from '../parser/htmlEntities.js';
import { isLetterOrDigit } from '../utils/strings.js';
import * as nls from './../../../fillers/vscode-nls.js';
var localize = nls.loadMessageBundle();
var HTMLHover = /** @class */ (function () {
    function HTMLHover(lsOptions, dataManager) {
        this.lsOptions = lsOptions;
        this.dataManager = dataManager;
    }
    HTMLHover.prototype.doHover = function (document, position, htmlDocument, options) {
        var convertContents = this.convertContents.bind(this);
        var doesSupportMarkdown = this.doesSupportMarkdown();
        var offset = document.offsetAt(position);
        var node = htmlDocument.findNodeAt(offset);
        var text = document.getText();
        if (!node || !node.tag) {
            return null;
        }
        var dataProviders = this.dataManager.getDataProviders().filter(function (p) { return p.isApplicable(document.languageId); });
        function getTagHover(currTag, range, open) {
            var _loop_1 = function (provider) {
                var hover = null;
                provider.provideTags().forEach(function (tag) {
                    if (tag.name.toLowerCase() === currTag.toLowerCase()) {
                        var markupContent = generateDocumentation(tag, options, doesSupportMarkdown);
                        if (!markupContent) {
                            markupContent = {
                                kind: doesSupportMarkdown ? 'markdown' : 'plaintext',
                                value: ''
                            };
                        }
                        hover = { contents: markupContent, range: range };
                    }
                });
                if (hover) {
                    hover.contents = convertContents(hover.contents);
                    return { value: hover };
                }
            };
            for (var _i = 0, dataProviders_1 = dataProviders; _i < dataProviders_1.length; _i++) {
                var provider = dataProviders_1[_i];
                var state_1 = _loop_1(provider);
                if (typeof state_1 === "object")
                    return state_1.value;
            }
            return null;
        }
        function getAttrHover(currTag, currAttr, range) {
            var _loop_2 = function (provider) {
                var hover = null;
                provider.provideAttributes(currTag).forEach(function (attr) {
                    if (currAttr === attr.name && attr.description) {
                        var contentsDoc = generateDocumentation(attr, options, doesSupportMarkdown);
                        if (contentsDoc) {
                            hover = { contents: contentsDoc, range: range };
                        }
                        else {
                            hover = null;
                        }
                    }
                });
                if (hover) {
                    hover.contents = convertContents(hover.contents);
                    return { value: hover };
                }
            };
            for (var _i = 0, dataProviders_2 = dataProviders; _i < dataProviders_2.length; _i++) {
                var provider = dataProviders_2[_i];
                var state_2 = _loop_2(provider);
                if (typeof state_2 === "object")
                    return state_2.value;
            }
            return null;
        }
        function getAttrValueHover(currTag, currAttr, currAttrValue, range) {
            var _loop_3 = function (provider) {
                var hover = null;
                provider.provideValues(currTag, currAttr).forEach(function (attrValue) {
                    if (currAttrValue === attrValue.name && attrValue.description) {
                        var contentsDoc = generateDocumentation(attrValue, options, doesSupportMarkdown);
                        if (contentsDoc) {
                            hover = { contents: contentsDoc, range: range };
                        }
                        else {
                            hover = null;
                        }
                    }
                });
                if (hover) {
                    hover.contents = convertContents(hover.contents);
                    return { value: hover };
                }
            };
            for (var _i = 0, dataProviders_3 = dataProviders; _i < dataProviders_3.length; _i++) {
                var provider = dataProviders_3[_i];
                var state_3 = _loop_3(provider);
                if (typeof state_3 === "object")
                    return state_3.value;
            }
            return null;
        }
        function getEntityHover(text, range) {
            var currEntity = filterEntity(text);
            for (var entity in entities) {
                var hover = null;
                var label = '&' + entity;
                if (currEntity === label) {
                    var code = entities[entity].charCodeAt(0).toString(16).toUpperCase();
                    var hex = 'U+';
                    if (code.length < 4) {
                        var zeroes = 4 - code.length;
                        var k = 0;
                        while (k < zeroes) {
                            hex += '0';
                            k += 1;
                        }
                    }
                    hex += code;
                    var contentsDoc = localize('entity.propose', "Character entity representing '" + entities[entity] + "', unicode equivalent '" + hex + "'");
                    if (contentsDoc) {
                        hover = { contents: contentsDoc, range: range };
                    }
                    else {
                        hover = null;
                    }
                }
                if (hover) {
                    hover.contents = convertContents(hover.contents);
                    return hover;
                }
            }
            return null;
        }
        function getTagNameRange(tokenType, startOffset) {
            var scanner = createScanner(document.getText(), startOffset);
            var token = scanner.scan();
            while (token !== TokenType.EOS && (scanner.getTokenEnd() < offset || scanner.getTokenEnd() === offset && token !== tokenType)) {
                token = scanner.scan();
            }
            if (token === tokenType && offset <= scanner.getTokenEnd()) {
                return { start: document.positionAt(scanner.getTokenOffset()), end: document.positionAt(scanner.getTokenEnd()) };
            }
            return null;
        }
        function getEntityRange() {
            var k = offset - 1;
            var characterStart = position.character;
            while (k >= 0 && isLetterOrDigit(text, k)) {
                k--;
                characterStart--;
            }
            var n = k + 1;
            var characterEnd = characterStart;
            while (isLetterOrDigit(text, n)) {
                n++;
                characterEnd++;
            }
            if (k >= 0 && text[k] === '&') {
                var range = null;
                if (text[n] === ';') {
                    range = Range.create(Position.create(position.line, characterStart), Position.create(position.line, characterEnd + 1));
                }
                else {
                    range = Range.create(Position.create(position.line, characterStart), Position.create(position.line, characterEnd));
                }
                return range;
            }
            return null;
        }
        function filterEntity(text) {
            var k = offset - 1;
            var newText = '&';
            while (k >= 0 && isLetterOrDigit(text, k)) {
                k--;
            }
            k = k + 1;
            while (isLetterOrDigit(text, k)) {
                newText += text[k];
                k += 1;
            }
            newText += ';';
            return newText;
        }
        if (node.endTagStart && offset >= node.endTagStart) {
            var tagRange_1 = getTagNameRange(TokenType.EndTag, node.endTagStart);
            if (tagRange_1) {
                return getTagHover(node.tag, tagRange_1, false);
            }
            return null;
        }
        var tagRange = getTagNameRange(TokenType.StartTag, node.start);
        if (tagRange) {
            return getTagHover(node.tag, tagRange, true);
        }
        var attrRange = getTagNameRange(TokenType.AttributeName, node.start);
        if (attrRange) {
            var tag = node.tag;
            var attr = document.getText(attrRange);
            return getAttrHover(tag, attr, attrRange);
        }
        var entityRange = getEntityRange();
        if (entityRange) {
            return getEntityHover(text, entityRange);
        }
        function scanAttrAndAttrValue(nodeStart, attrValueStart) {
            var scanner = createScanner(document.getText(), nodeStart);
            var token = scanner.scan();
            var prevAttr = undefined;
            while (token !== TokenType.EOS && (scanner.getTokenEnd() <= attrValueStart)) {
                token = scanner.scan();
                if (token === TokenType.AttributeName) {
                    prevAttr = scanner.getTokenText();
                }
            }
            return prevAttr;
        }
        var attrValueRange = getTagNameRange(TokenType.AttributeValue, node.start);
        if (attrValueRange) {
            var tag = node.tag;
            var attrValue = trimQuotes(document.getText(attrValueRange));
            var matchAttr = scanAttrAndAttrValue(node.start, document.offsetAt(attrValueRange.start));
            if (matchAttr) {
                return getAttrValueHover(tag, matchAttr, attrValue, attrValueRange);
            }
        }
        return null;
    };
    HTMLHover.prototype.convertContents = function (contents) {
        if (!this.doesSupportMarkdown()) {
            if (typeof contents === 'string') {
                return contents;
            }
            // MarkupContent
            else if ('kind' in contents) {
                return {
                    kind: 'plaintext',
                    value: contents.value
                };
            }
            // MarkedString[]
            else if (Array.isArray(contents)) {
                contents.map(function (c) {
                    return typeof c === 'string' ? c : c.value;
                });
            }
            // MarkedString
            else {
                return contents.value;
            }
        }
        return contents;
    };
    HTMLHover.prototype.doesSupportMarkdown = function () {
        var _a, _b, _c;
        if (!isDefined(this.supportsMarkdown)) {
            if (!isDefined(this.lsOptions.clientCapabilities)) {
                this.supportsMarkdown = true;
                return this.supportsMarkdown;
            }
            var contentFormat = (_c = (_b = (_a = this.lsOptions.clientCapabilities) === null || _a === void 0 ? void 0 : _a.textDocument) === null || _b === void 0 ? void 0 : _b.hover) === null || _c === void 0 ? void 0 : _c.contentFormat;
            this.supportsMarkdown = Array.isArray(contentFormat) && contentFormat.indexOf(MarkupKind.Markdown) !== -1;
        }
        return this.supportsMarkdown;
    };
    return HTMLHover;
}());
export { HTMLHover };
function trimQuotes(s) {
    if (s.length <= 1) {
        return s.replace(/['"]/, '');
    }
    if (s[0] === "'" || s[0] === "\"") {
        s = s.slice(1);
    }
    if (s[s.length - 1] === "'" || s[s.length - 1] === "\"") {
        s = s.slice(0, -1);
    }
    return s;
}
