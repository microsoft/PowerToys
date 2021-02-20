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
var __generator = (this && this.__generator) || function (thisArg, body) {
    var _ = { label: 0, sent: function() { if (t[0] & 1) throw t[1]; return t[1]; }, trys: [], ops: [] }, f, y, t, g;
    return g = { next: verb(0), "throw": verb(1), "return": verb(2) }, typeof Symbol === "function" && (g[Symbol.iterator] = function() { return this; }), g;
    function verb(n) { return function (v) { return step([n, v]); }; }
    function step(op) {
        if (f) throw new TypeError("Generator is already executing.");
        while (_) try {
            if (f = 1, y && (t = op[0] & 2 ? y["return"] : op[0] ? y["throw"] || ((t = y["return"]) && t.call(y), 0) : y.next) && !(t = t.call(y, op[1])).done) return t;
            if (y = 0, t) op = [op[0] & 2, t.value];
            switch (op[0]) {
                case 0: case 1: t = op; break;
                case 4: _.label++; return { value: op[1], done: false };
                case 5: _.label++; y = op[1]; op = [0]; continue;
                case 7: op = _.ops.pop(); _.trys.pop(); continue;
                default:
                    if (!(t = _.trys, t = t.length > 0 && t[t.length - 1]) && (op[0] === 6 || op[0] === 2)) { _ = 0; continue; }
                    if (op[0] === 3 && (!t || (op[1] > t[0] && op[1] < t[3]))) { _.label = op[1]; break; }
                    if (op[0] === 6 && _.label < t[1]) { _.label = t[1]; t = op; break; }
                    if (t && _.label < t[2]) { _.label = t[2]; _.ops.push(op); break; }
                    if (t[2]) _.ops.pop();
                    _.trys.pop(); continue;
            }
            op = body.call(thisArg, _);
        } catch (e) { op = [6, e]; y = 0; } finally { f = t = 0; }
        if (op[0] & 5) throw op[1]; return { value: op[0] ? op[1] : void 0, done: true };
    }
};
import { createScanner } from '../parser/htmlScanner.js';
import { ScannerState, TokenType, Position, CompletionItemKind, Range, TextEdit, InsertTextFormat, MarkupKind } from '../htmlLanguageTypes.js';
import { entities } from '../parser/htmlEntities.js';
import * as nls from './../../../fillers/vscode-nls.js';
import { isLetterOrDigit, endsWith, startsWith } from '../utils/strings.js';
import { isVoidElement } from '../languageFacts/fact.js';
import { isDefined } from '../utils/object.js';
import { generateDocumentation } from '../languageFacts/dataProvider.js';
import { PathCompletionParticipant } from './pathCompletion.js';
var localize = nls.loadMessageBundle();
var HTMLCompletion = /** @class */ (function () {
    function HTMLCompletion(lsOptions, dataManager) {
        this.lsOptions = lsOptions;
        this.dataManager = dataManager;
        this.completionParticipants = [];
    }
    HTMLCompletion.prototype.setCompletionParticipants = function (registeredCompletionParticipants) {
        this.completionParticipants = registeredCompletionParticipants || [];
    };
    HTMLCompletion.prototype.doComplete2 = function (document, position, htmlDocument, documentContext, settings) {
        return __awaiter(this, void 0, void 0, function () {
            var participant, contributedParticipants, result, pathCompletionResult;
            return __generator(this, function (_a) {
                switch (_a.label) {
                    case 0:
                        if (!this.lsOptions.fileSystemProvider || !this.lsOptions.fileSystemProvider.readDirectory) {
                            return [2 /*return*/, this.doComplete(document, position, htmlDocument, settings)];
                        }
                        participant = new PathCompletionParticipant(this.lsOptions.fileSystemProvider.readDirectory);
                        contributedParticipants = this.completionParticipants;
                        this.completionParticipants = [participant].concat(contributedParticipants);
                        result = this.doComplete(document, position, htmlDocument, settings);
                        _a.label = 1;
                    case 1:
                        _a.trys.push([1, , 3, 4]);
                        return [4 /*yield*/, participant.computeCompletions(document, documentContext)];
                    case 2:
                        pathCompletionResult = _a.sent();
                        return [2 /*return*/, {
                                isIncomplete: result.isIncomplete || pathCompletionResult.isIncomplete,
                                items: pathCompletionResult.items.concat(result.items)
                            }];
                    case 3:
                        this.completionParticipants = contributedParticipants;
                        return [7 /*endfinally*/];
                    case 4: return [2 /*return*/];
                }
            });
        });
    };
    HTMLCompletion.prototype.doComplete = function (document, position, htmlDocument, settings) {
        var result = this._doComplete(document, position, htmlDocument, settings);
        return this.convertCompletionList(result);
    };
    HTMLCompletion.prototype._doComplete = function (document, position, htmlDocument, settings) {
        var result = {
            isIncomplete: false,
            items: []
        };
        var completionParticipants = this.completionParticipants;
        var dataProviders = this.dataManager.getDataProviders().filter(function (p) { return p.isApplicable(document.languageId) && (!settings || settings[p.getId()] !== false); });
        var doesSupportMarkdown = this.doesSupportMarkdown();
        var text = document.getText();
        var offset = document.offsetAt(position);
        var node = htmlDocument.findNodeBefore(offset);
        if (!node) {
            return result;
        }
        var scanner = createScanner(text, node.start);
        var currentTag = '';
        var currentAttributeName;
        function getReplaceRange(replaceStart, replaceEnd) {
            if (replaceEnd === void 0) { replaceEnd = offset; }
            if (replaceStart > offset) {
                replaceStart = offset;
            }
            return { start: document.positionAt(replaceStart), end: document.positionAt(replaceEnd) };
        }
        function collectOpenTagSuggestions(afterOpenBracket, tagNameEnd) {
            var range = getReplaceRange(afterOpenBracket, tagNameEnd);
            dataProviders.forEach(function (provider) {
                provider.provideTags().forEach(function (tag) {
                    result.items.push({
                        label: tag.name,
                        kind: CompletionItemKind.Property,
                        documentation: generateDocumentation(tag, undefined, doesSupportMarkdown),
                        textEdit: TextEdit.replace(range, tag.name),
                        insertTextFormat: InsertTextFormat.PlainText
                    });
                });
            });
            return result;
        }
        function getLineIndent(offset) {
            var start = offset;
            while (start > 0) {
                var ch = text.charAt(start - 1);
                if ("\n\r".indexOf(ch) >= 0) {
                    return text.substring(start, offset);
                }
                if (!isWhiteSpace(ch)) {
                    return null;
                }
                start--;
            }
            return text.substring(0, offset);
        }
        function collectCloseTagSuggestions(afterOpenBracket, inOpenTag, tagNameEnd) {
            if (tagNameEnd === void 0) { tagNameEnd = offset; }
            var range = getReplaceRange(afterOpenBracket, tagNameEnd);
            var closeTag = isFollowedBy(text, tagNameEnd, ScannerState.WithinEndTag, TokenType.EndTagClose) ? '' : '>';
            var curr = node;
            if (inOpenTag) {
                curr = curr.parent; // don't suggest the own tag, it's not yet open
            }
            while (curr) {
                var tag = curr.tag;
                if (tag && (!curr.closed || curr.endTagStart && (curr.endTagStart > offset))) {
                    var item = {
                        label: '/' + tag,
                        kind: CompletionItemKind.Property,
                        filterText: '/' + tag,
                        textEdit: TextEdit.replace(range, '/' + tag + closeTag),
                        insertTextFormat: InsertTextFormat.PlainText
                    };
                    var startIndent = getLineIndent(curr.start);
                    var endIndent = getLineIndent(afterOpenBracket - 1);
                    if (startIndent !== null && endIndent !== null && startIndent !== endIndent) {
                        var insertText = startIndent + '</' + tag + closeTag;
                        item.textEdit = TextEdit.replace(getReplaceRange(afterOpenBracket - 1 - endIndent.length), insertText);
                        item.filterText = endIndent + '</' + tag;
                    }
                    result.items.push(item);
                    return result;
                }
                curr = curr.parent;
            }
            if (inOpenTag) {
                return result;
            }
            dataProviders.forEach(function (provider) {
                provider.provideTags().forEach(function (tag) {
                    result.items.push({
                        label: '/' + tag.name,
                        kind: CompletionItemKind.Property,
                        documentation: generateDocumentation(tag, undefined, doesSupportMarkdown),
                        filterText: '/' + tag.name + closeTag,
                        textEdit: TextEdit.replace(range, '/' + tag.name + closeTag),
                        insertTextFormat: InsertTextFormat.PlainText
                    });
                });
            });
            return result;
        }
        function collectAutoCloseTagSuggestion(tagCloseEnd, tag) {
            if (settings && settings.hideAutoCompleteProposals) {
                return result;
            }
            if (!isVoidElement(tag)) {
                var pos = document.positionAt(tagCloseEnd);
                result.items.push({
                    label: '</' + tag + '>',
                    kind: CompletionItemKind.Property,
                    filterText: '</' + tag + '>',
                    textEdit: TextEdit.insert(pos, '$0</' + tag + '>'),
                    insertTextFormat: InsertTextFormat.Snippet
                });
            }
            return result;
        }
        function collectTagSuggestions(tagStart, tagEnd) {
            collectOpenTagSuggestions(tagStart, tagEnd);
            collectCloseTagSuggestions(tagStart, true, tagEnd);
            return result;
        }
        function collectAttributeNameSuggestions(nameStart, nameEnd) {
            if (nameEnd === void 0) { nameEnd = offset; }
            var replaceEnd = offset;
            while (replaceEnd < nameEnd && text[replaceEnd] !== '<') { // < is a valid attribute name character, but we rather assume the attribute name ends. See #23236.
                replaceEnd++;
            }
            var range = getReplaceRange(nameStart, replaceEnd);
            var value = isFollowedBy(text, nameEnd, ScannerState.AfterAttributeName, TokenType.DelimiterAssign) ? '' : '="$1"';
            var seenAttributes = Object.create(null);
            dataProviders.forEach(function (provider) {
                provider.provideAttributes(currentTag).forEach(function (attr) {
                    if (seenAttributes[attr.name]) {
                        return;
                    }
                    seenAttributes[attr.name] = true;
                    var codeSnippet = attr.name;
                    var command;
                    if (attr.valueSet !== 'v' && value.length) {
                        codeSnippet = codeSnippet + value;
                        if (attr.valueSet || attr.name === 'style') {
                            command = {
                                title: 'Suggest',
                                command: 'editor.action.triggerSuggest'
                            };
                        }
                    }
                    result.items.push({
                        label: attr.name,
                        kind: attr.valueSet === 'handler' ? CompletionItemKind.Function : CompletionItemKind.Value,
                        documentation: generateDocumentation(attr, undefined, doesSupportMarkdown),
                        textEdit: TextEdit.replace(range, codeSnippet),
                        insertTextFormat: InsertTextFormat.Snippet,
                        command: command
                    });
                });
            });
            collectDataAttributesSuggestions(range, seenAttributes);
            return result;
        }
        function collectDataAttributesSuggestions(range, seenAttributes) {
            var dataAttr = 'data-';
            var dataAttributes = {};
            dataAttributes[dataAttr] = dataAttr + "$1=\"$2\"";
            function addNodeDataAttributes(node) {
                node.attributeNames.forEach(function (attr) {
                    if (startsWith(attr, dataAttr) && !dataAttributes[attr] && !seenAttributes[attr]) {
                        dataAttributes[attr] = attr + '="$1"';
                    }
                });
                node.children.forEach(function (child) { return addNodeDataAttributes(child); });
            }
            if (htmlDocument) {
                htmlDocument.roots.forEach(function (root) { return addNodeDataAttributes(root); });
            }
            Object.keys(dataAttributes).forEach(function (attr) { return result.items.push({
                label: attr,
                kind: CompletionItemKind.Value,
                textEdit: TextEdit.replace(range, dataAttributes[attr]),
                insertTextFormat: InsertTextFormat.Snippet
            }); });
        }
        function collectAttributeValueSuggestions(valueStart, valueEnd) {
            if (valueEnd === void 0) { valueEnd = offset; }
            var range;
            var addQuotes;
            var valuePrefix;
            if (offset > valueStart && offset <= valueEnd && isQuote(text[valueStart])) {
                // inside quoted attribute
                var valueContentStart = valueStart + 1;
                var valueContentEnd = valueEnd;
                // valueEnd points to the char after quote, which encloses the replace range
                if (valueEnd > valueStart && text[valueEnd - 1] === text[valueStart]) {
                    valueContentEnd--;
                }
                var wsBefore = getWordStart(text, offset, valueContentStart);
                var wsAfter = getWordEnd(text, offset, valueContentEnd);
                range = getReplaceRange(wsBefore, wsAfter);
                valuePrefix = offset >= valueContentStart && offset <= valueContentEnd ? text.substring(valueContentStart, offset) : '';
                addQuotes = false;
            }
            else {
                range = getReplaceRange(valueStart, valueEnd);
                valuePrefix = text.substring(valueStart, offset);
                addQuotes = true;
            }
            if (completionParticipants.length > 0) {
                var tag = currentTag.toLowerCase();
                var attribute = currentAttributeName.toLowerCase();
                var fullRange = getReplaceRange(valueStart, valueEnd);
                for (var _i = 0, completionParticipants_1 = completionParticipants; _i < completionParticipants_1.length; _i++) {
                    var participant = completionParticipants_1[_i];
                    if (participant.onHtmlAttributeValue) {
                        participant.onHtmlAttributeValue({ document: document, position: position, tag: tag, attribute: attribute, value: valuePrefix, range: fullRange });
                    }
                }
            }
            dataProviders.forEach(function (provider) {
                provider.provideValues(currentTag, currentAttributeName).forEach(function (value) {
                    var insertText = addQuotes ? '"' + value.name + '"' : value.name;
                    result.items.push({
                        label: value.name,
                        filterText: insertText,
                        kind: CompletionItemKind.Unit,
                        documentation: generateDocumentation(value, undefined, doesSupportMarkdown),
                        textEdit: TextEdit.replace(range, insertText),
                        insertTextFormat: InsertTextFormat.PlainText
                    });
                });
            });
            collectCharacterEntityProposals();
            return result;
        }
        function scanNextForEndPos(nextToken) {
            if (offset === scanner.getTokenEnd()) {
                token = scanner.scan();
                if (token === nextToken && scanner.getTokenOffset() === offset) {
                    return scanner.getTokenEnd();
                }
            }
            return offset;
        }
        function collectInsideContent() {
            for (var _i = 0, completionParticipants_2 = completionParticipants; _i < completionParticipants_2.length; _i++) {
                var participant = completionParticipants_2[_i];
                if (participant.onHtmlContent) {
                    participant.onHtmlContent({ document: document, position: position });
                }
            }
            return collectCharacterEntityProposals();
        }
        function collectCharacterEntityProposals() {
            // character entities
            var k = offset - 1;
            var characterStart = position.character;
            while (k >= 0 && isLetterOrDigit(text, k)) {
                k--;
                characterStart--;
            }
            if (k >= 0 && text[k] === '&') {
                var range = Range.create(Position.create(position.line, characterStart - 1), position);
                for (var entity in entities) {
                    if (endsWith(entity, ';')) {
                        var label = '&' + entity;
                        result.items.push({
                            label: label,
                            kind: CompletionItemKind.Keyword,
                            documentation: localize('entity.propose', "Character entity representing '" + entities[entity] + "'"),
                            textEdit: TextEdit.replace(range, label),
                            insertTextFormat: InsertTextFormat.PlainText
                        });
                    }
                }
            }
            return result;
        }
        function suggestDoctype(replaceStart, replaceEnd) {
            var range = getReplaceRange(replaceStart, replaceEnd);
            result.items.push({
                label: '!DOCTYPE',
                kind: CompletionItemKind.Property,
                documentation: 'A preamble for an HTML document.',
                textEdit: TextEdit.replace(range, '!DOCTYPE html>'),
                insertTextFormat: InsertTextFormat.PlainText
            });
        }
        var token = scanner.scan();
        while (token !== TokenType.EOS && scanner.getTokenOffset() <= offset) {
            switch (token) {
                case TokenType.StartTagOpen:
                    if (scanner.getTokenEnd() === offset) {
                        var endPos = scanNextForEndPos(TokenType.StartTag);
                        if (position.line === 0) {
                            suggestDoctype(offset, endPos);
                        }
                        return collectTagSuggestions(offset, endPos);
                    }
                    break;
                case TokenType.StartTag:
                    if (scanner.getTokenOffset() <= offset && offset <= scanner.getTokenEnd()) {
                        return collectOpenTagSuggestions(scanner.getTokenOffset(), scanner.getTokenEnd());
                    }
                    currentTag = scanner.getTokenText();
                    break;
                case TokenType.AttributeName:
                    if (scanner.getTokenOffset() <= offset && offset <= scanner.getTokenEnd()) {
                        return collectAttributeNameSuggestions(scanner.getTokenOffset(), scanner.getTokenEnd());
                    }
                    currentAttributeName = scanner.getTokenText();
                    break;
                case TokenType.DelimiterAssign:
                    if (scanner.getTokenEnd() === offset) {
                        var endPos = scanNextForEndPos(TokenType.AttributeValue);
                        return collectAttributeValueSuggestions(offset, endPos);
                    }
                    break;
                case TokenType.AttributeValue:
                    if (scanner.getTokenOffset() <= offset && offset <= scanner.getTokenEnd()) {
                        return collectAttributeValueSuggestions(scanner.getTokenOffset(), scanner.getTokenEnd());
                    }
                    break;
                case TokenType.Whitespace:
                    if (offset <= scanner.getTokenEnd()) {
                        switch (scanner.getScannerState()) {
                            case ScannerState.AfterOpeningStartTag:
                                var startPos = scanner.getTokenOffset();
                                var endTagPos = scanNextForEndPos(TokenType.StartTag);
                                return collectTagSuggestions(startPos, endTagPos);
                            case ScannerState.WithinTag:
                            case ScannerState.AfterAttributeName:
                                return collectAttributeNameSuggestions(scanner.getTokenEnd());
                            case ScannerState.BeforeAttributeValue:
                                return collectAttributeValueSuggestions(scanner.getTokenEnd());
                            case ScannerState.AfterOpeningEndTag:
                                return collectCloseTagSuggestions(scanner.getTokenOffset() - 1, false);
                            case ScannerState.WithinContent:
                                return collectInsideContent();
                        }
                    }
                    break;
                case TokenType.EndTagOpen:
                    if (offset <= scanner.getTokenEnd()) {
                        var afterOpenBracket = scanner.getTokenOffset() + 1;
                        var endOffset = scanNextForEndPos(TokenType.EndTag);
                        return collectCloseTagSuggestions(afterOpenBracket, false, endOffset);
                    }
                    break;
                case TokenType.EndTag:
                    if (offset <= scanner.getTokenEnd()) {
                        var start = scanner.getTokenOffset() - 1;
                        while (start >= 0) {
                            var ch = text.charAt(start);
                            if (ch === '/') {
                                return collectCloseTagSuggestions(start, false, scanner.getTokenEnd());
                            }
                            else if (!isWhiteSpace(ch)) {
                                break;
                            }
                            start--;
                        }
                    }
                    break;
                case TokenType.StartTagClose:
                    if (offset <= scanner.getTokenEnd()) {
                        if (currentTag) {
                            return collectAutoCloseTagSuggestion(scanner.getTokenEnd(), currentTag);
                        }
                    }
                    break;
                case TokenType.Content:
                    if (offset <= scanner.getTokenEnd()) {
                        return collectInsideContent();
                    }
                    break;
                default:
                    if (offset <= scanner.getTokenEnd()) {
                        return result;
                    }
                    break;
            }
            token = scanner.scan();
        }
        return result;
    };
    HTMLCompletion.prototype.doTagComplete = function (document, position, htmlDocument) {
        var offset = document.offsetAt(position);
        if (offset <= 0) {
            return null;
        }
        var char = document.getText().charAt(offset - 1);
        if (char === '>') {
            var node = htmlDocument.findNodeBefore(offset);
            if (node && node.tag && !isVoidElement(node.tag) && node.start < offset && (!node.endTagStart || node.endTagStart > offset)) {
                var scanner = createScanner(document.getText(), node.start);
                var token = scanner.scan();
                while (token !== TokenType.EOS && scanner.getTokenEnd() <= offset) {
                    if (token === TokenType.StartTagClose && scanner.getTokenEnd() === offset) {
                        return "$0</" + node.tag + ">";
                    }
                    token = scanner.scan();
                }
            }
        }
        else if (char === '/') {
            var node = htmlDocument.findNodeBefore(offset);
            while (node && node.closed) {
                node = node.parent;
            }
            if (node && node.tag) {
                var scanner = createScanner(document.getText(), node.start);
                var token = scanner.scan();
                while (token !== TokenType.EOS && scanner.getTokenEnd() <= offset) {
                    if (token === TokenType.EndTagOpen && scanner.getTokenEnd() === offset) {
                        return node.tag + ">";
                    }
                    token = scanner.scan();
                }
            }
        }
        return null;
    };
    HTMLCompletion.prototype.convertCompletionList = function (list) {
        if (!this.doesSupportMarkdown()) {
            list.items.forEach(function (item) {
                if (item.documentation && typeof item.documentation !== 'string') {
                    item.documentation = {
                        kind: 'plaintext',
                        value: item.documentation.value
                    };
                }
            });
        }
        return list;
    };
    HTMLCompletion.prototype.doesSupportMarkdown = function () {
        var _a, _b, _c;
        if (!isDefined(this.supportsMarkdown)) {
            if (!isDefined(this.lsOptions.clientCapabilities)) {
                this.supportsMarkdown = true;
                return this.supportsMarkdown;
            }
            var documentationFormat = (_c = (_b = (_a = this.lsOptions.clientCapabilities.textDocument) === null || _a === void 0 ? void 0 : _a.completion) === null || _b === void 0 ? void 0 : _b.completionItem) === null || _c === void 0 ? void 0 : _c.documentationFormat;
            this.supportsMarkdown = Array.isArray(documentationFormat) && documentationFormat.indexOf(MarkupKind.Markdown) !== -1;
        }
        return this.supportsMarkdown;
    };
    return HTMLCompletion;
}());
export { HTMLCompletion };
function isQuote(s) {
    return /^["']*$/.test(s);
}
function isWhiteSpace(s) {
    return /^\s*$/.test(s);
}
function isFollowedBy(s, offset, intialState, expectedToken) {
    var scanner = createScanner(s, offset, intialState);
    var token = scanner.scan();
    while (token === TokenType.Whitespace) {
        token = scanner.scan();
    }
    return token === expectedToken;
}
function getWordStart(s, offset, limit) {
    while (offset > limit && !isWhiteSpace(s[offset - 1])) {
        offset--;
    }
    return offset;
}
function getWordEnd(s, offset, limit) {
    while (offset < limit && !isWhiteSpace(s[offset])) {
        offset++;
    }
    return offset;
}
