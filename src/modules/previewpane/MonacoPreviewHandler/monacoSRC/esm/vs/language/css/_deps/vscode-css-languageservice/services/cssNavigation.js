/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
'use strict';
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
import { DocumentHighlightKind, Location, Range, SymbolKind, TextEdit, FileType } from '../cssLanguageTypes.js';
import * as nls from './../../../fillers/vscode-nls.js';
import * as nodes from '../parser/cssNodes.js';
import { Symbols } from '../parser/cssSymbolScope.js';
import { getColorValue, hslFromColor } from '../languageFacts/facts.js';
import { startsWith } from '../utils/strings.js';
import { dirname, joinPath } from '../utils/resources.js';
var localize = nls.loadMessageBundle();
var CSSNavigation = /** @class */ (function () {
    function CSSNavigation(fileSystemProvider) {
        this.fileSystemProvider = fileSystemProvider;
    }
    CSSNavigation.prototype.findDefinition = function (document, position, stylesheet) {
        var symbols = new Symbols(stylesheet);
        var offset = document.offsetAt(position);
        var node = nodes.getNodeAtOffset(stylesheet, offset);
        if (!node) {
            return null;
        }
        var symbol = symbols.findSymbolFromNode(node);
        if (!symbol) {
            return null;
        }
        return {
            uri: document.uri,
            range: getRange(symbol.node, document)
        };
    };
    CSSNavigation.prototype.findReferences = function (document, position, stylesheet) {
        var highlights = this.findDocumentHighlights(document, position, stylesheet);
        return highlights.map(function (h) {
            return {
                uri: document.uri,
                range: h.range
            };
        });
    };
    CSSNavigation.prototype.findDocumentHighlights = function (document, position, stylesheet) {
        var result = [];
        var offset = document.offsetAt(position);
        var node = nodes.getNodeAtOffset(stylesheet, offset);
        if (!node || node.type === nodes.NodeType.Stylesheet || node.type === nodes.NodeType.Declarations) {
            return result;
        }
        if (node.type === nodes.NodeType.Identifier && node.parent && node.parent.type === nodes.NodeType.ClassSelector) {
            node = node.parent;
        }
        var symbols = new Symbols(stylesheet);
        var symbol = symbols.findSymbolFromNode(node);
        var name = node.getText();
        stylesheet.accept(function (candidate) {
            if (symbol) {
                if (symbols.matchesSymbol(candidate, symbol)) {
                    result.push({
                        kind: getHighlightKind(candidate),
                        range: getRange(candidate, document)
                    });
                    return false;
                }
            }
            else if (node && node.type === candidate.type && candidate.matches(name)) {
                // Same node type and data
                result.push({
                    kind: getHighlightKind(candidate),
                    range: getRange(candidate, document)
                });
            }
            return true;
        });
        return result;
    };
    CSSNavigation.prototype.isRawStringDocumentLinkNode = function (node) {
        return node.type === nodes.NodeType.Import;
    };
    CSSNavigation.prototype.findDocumentLinks = function (document, stylesheet, documentContext) {
        var links = this.findUnresolvedLinks(document, stylesheet);
        for (var i = 0; i < links.length; i++) {
            var target = links[i].target;
            if (target && !(/^\w+:\/\//g.test(target))) {
                var resolved = documentContext.resolveReference(target, document.uri);
                if (resolved) {
                    links[i].target = resolved;
                }
            }
        }
        return links;
    };
    CSSNavigation.prototype.findDocumentLinks2 = function (document, stylesheet, documentContext) {
        return __awaiter(this, void 0, void 0, function () {
            var links, resolvedLinks, _i, links_1, link, target, resolvedTarget;
            return __generator(this, function (_a) {
                switch (_a.label) {
                    case 0:
                        links = this.findUnresolvedLinks(document, stylesheet);
                        resolvedLinks = [];
                        _i = 0, links_1 = links;
                        _a.label = 1;
                    case 1:
                        if (!(_i < links_1.length)) return [3 /*break*/, 5];
                        link = links_1[_i];
                        target = link.target;
                        if (!(target && !(/^\w+:\/\//g.test(target)))) return [3 /*break*/, 3];
                        return [4 /*yield*/, this.resolveRelativeReference(target, document.uri, documentContext)];
                    case 2:
                        resolvedTarget = _a.sent();
                        if (resolvedTarget !== undefined) {
                            link.target = resolvedTarget;
                            resolvedLinks.push(link);
                        }
                        return [3 /*break*/, 4];
                    case 3:
                        resolvedLinks.push(link);
                        _a.label = 4;
                    case 4:
                        _i++;
                        return [3 /*break*/, 1];
                    case 5: return [2 /*return*/, resolvedLinks];
                }
            });
        });
    };
    CSSNavigation.prototype.findUnresolvedLinks = function (document, stylesheet) {
        var _this = this;
        var result = [];
        var collect = function (uriStringNode) {
            var rawUri = uriStringNode.getText();
            var range = getRange(uriStringNode, document);
            // Make sure the range is not empty
            if (range.start.line === range.end.line && range.start.character === range.end.character) {
                return;
            }
            if (startsWith(rawUri, "'") || startsWith(rawUri, "\"")) {
                rawUri = rawUri.slice(1, -1);
            }
            result.push({ target: rawUri, range: range });
        };
        stylesheet.accept(function (candidate) {
            if (candidate.type === nodes.NodeType.URILiteral) {
                var first = candidate.getChild(0);
                if (first) {
                    collect(first);
                }
                return false;
            }
            /**
             * In @import, it is possible to include links that do not use `url()`
             * For example, `@import 'foo.css';`
             */
            if (candidate.parent && _this.isRawStringDocumentLinkNode(candidate.parent)) {
                var rawText = candidate.getText();
                if (startsWith(rawText, "'") || startsWith(rawText, "\"")) {
                    collect(candidate);
                }
                return false;
            }
            return true;
        });
        return result;
    };
    CSSNavigation.prototype.findDocumentSymbols = function (document, stylesheet) {
        var result = [];
        stylesheet.accept(function (node) {
            var entry = {
                name: null,
                kind: SymbolKind.Class,
                location: null
            };
            var locationNode = node;
            if (node instanceof nodes.Selector) {
                entry.name = node.getText();
                locationNode = node.findAParent(nodes.NodeType.Ruleset, nodes.NodeType.ExtendsReference);
                if (locationNode) {
                    entry.location = Location.create(document.uri, getRange(locationNode, document));
                    result.push(entry);
                }
                return false;
            }
            else if (node instanceof nodes.VariableDeclaration) {
                entry.name = node.getName();
                entry.kind = SymbolKind.Variable;
            }
            else if (node instanceof nodes.MixinDeclaration) {
                entry.name = node.getName();
                entry.kind = SymbolKind.Method;
            }
            else if (node instanceof nodes.FunctionDeclaration) {
                entry.name = node.getName();
                entry.kind = SymbolKind.Function;
            }
            else if (node instanceof nodes.Keyframe) {
                entry.name = localize('literal.keyframes', "@keyframes {0}", node.getName());
            }
            else if (node instanceof nodes.FontFace) {
                entry.name = localize('literal.fontface', "@font-face");
            }
            else if (node instanceof nodes.Media) {
                var mediaList = node.getChild(0);
                if (mediaList instanceof nodes.Medialist) {
                    entry.name = '@media ' + mediaList.getText();
                    entry.kind = SymbolKind.Module;
                }
            }
            if (entry.name) {
                entry.location = Location.create(document.uri, getRange(locationNode, document));
                result.push(entry);
            }
            return true;
        });
        return result;
    };
    CSSNavigation.prototype.findDocumentColors = function (document, stylesheet) {
        var result = [];
        stylesheet.accept(function (node) {
            var colorInfo = getColorInformation(node, document);
            if (colorInfo) {
                result.push(colorInfo);
            }
            return true;
        });
        return result;
    };
    CSSNavigation.prototype.getColorPresentations = function (document, stylesheet, color, range) {
        var result = [];
        var red256 = Math.round(color.red * 255), green256 = Math.round(color.green * 255), blue256 = Math.round(color.blue * 255);
        var label;
        if (color.alpha === 1) {
            label = "rgb(" + red256 + ", " + green256 + ", " + blue256 + ")";
        }
        else {
            label = "rgba(" + red256 + ", " + green256 + ", " + blue256 + ", " + color.alpha + ")";
        }
        result.push({ label: label, textEdit: TextEdit.replace(range, label) });
        if (color.alpha === 1) {
            label = "#" + toTwoDigitHex(red256) + toTwoDigitHex(green256) + toTwoDigitHex(blue256);
        }
        else {
            label = "#" + toTwoDigitHex(red256) + toTwoDigitHex(green256) + toTwoDigitHex(blue256) + toTwoDigitHex(Math.round(color.alpha * 255));
        }
        result.push({ label: label, textEdit: TextEdit.replace(range, label) });
        var hsl = hslFromColor(color);
        if (hsl.a === 1) {
            label = "hsl(" + hsl.h + ", " + Math.round(hsl.s * 100) + "%, " + Math.round(hsl.l * 100) + "%)";
        }
        else {
            label = "hsla(" + hsl.h + ", " + Math.round(hsl.s * 100) + "%, " + Math.round(hsl.l * 100) + "%, " + hsl.a + ")";
        }
        result.push({ label: label, textEdit: TextEdit.replace(range, label) });
        return result;
    };
    CSSNavigation.prototype.doRename = function (document, position, newName, stylesheet) {
        var _a;
        var highlights = this.findDocumentHighlights(document, position, stylesheet);
        var edits = highlights.map(function (h) { return TextEdit.replace(h.range, newName); });
        return {
            changes: (_a = {}, _a[document.uri] = edits, _a)
        };
    };
    CSSNavigation.prototype.resolveRelativeReference = function (ref, documentUri, documentContext) {
        return __awaiter(this, void 0, void 0, function () {
            var moduleName, rootFolderUri, documentFolderUri, modulePath, pathWithinModule;
            return __generator(this, function (_a) {
                switch (_a.label) {
                    case 0:
                        if (!(ref[0] === '~' && ref[1] !== '/' && this.fileSystemProvider)) return [3 /*break*/, 3];
                        ref = ref.substring(1);
                        if (!startsWith(documentUri, 'file://')) return [3 /*break*/, 2];
                        moduleName = getModuleNameFromPath(ref);
                        rootFolderUri = documentContext.resolveReference('/', documentUri);
                        documentFolderUri = dirname(documentUri);
                        return [4 /*yield*/, this.resolvePathToModule(moduleName, documentFolderUri, rootFolderUri)];
                    case 1:
                        modulePath = _a.sent();
                        if (modulePath) {
                            pathWithinModule = ref.substring(moduleName.length + 1);
                            return [2 /*return*/, joinPath(modulePath, pathWithinModule)];
                        }
                        _a.label = 2;
                    case 2: return [2 /*return*/, documentContext.resolveReference(ref, documentUri)];
                    case 3: return [2 /*return*/, documentContext.resolveReference(ref, documentUri)];
                }
            });
        });
    };
    CSSNavigation.prototype.resolvePathToModule = function (_moduleName, documentFolderUri, rootFolderUri) {
        return __awaiter(this, void 0, void 0, function () {
            var packPath;
            return __generator(this, function (_a) {
                switch (_a.label) {
                    case 0:
                        packPath = joinPath(documentFolderUri, 'node_modules', _moduleName, 'package.json');
                        return [4 /*yield*/, this.fileExists(packPath)];
                    case 1:
                        if (_a.sent()) {
                            return [2 /*return*/, dirname(packPath)];
                        }
                        else if (rootFolderUri && documentFolderUri.startsWith(rootFolderUri) && (documentFolderUri.length !== rootFolderUri.length)) {
                            return [2 /*return*/, this.resolvePathToModule(_moduleName, dirname(documentFolderUri), rootFolderUri)];
                        }
                        return [2 /*return*/, undefined];
                }
            });
        });
    };
    CSSNavigation.prototype.fileExists = function (uri) {
        return __awaiter(this, void 0, void 0, function () {
            var stat, err_1;
            return __generator(this, function (_a) {
                switch (_a.label) {
                    case 0:
                        if (!this.fileSystemProvider) {
                            return [2 /*return*/, false];
                        }
                        _a.label = 1;
                    case 1:
                        _a.trys.push([1, 3, , 4]);
                        return [4 /*yield*/, this.fileSystemProvider.stat(uri)];
                    case 2:
                        stat = _a.sent();
                        if (stat.type === FileType.Unknown && stat.size === -1) {
                            return [2 /*return*/, false];
                        }
                        return [2 /*return*/, true];
                    case 3:
                        err_1 = _a.sent();
                        return [2 /*return*/, false];
                    case 4: return [2 /*return*/];
                }
            });
        });
    };
    return CSSNavigation;
}());
export { CSSNavigation };
function getColorInformation(node, document) {
    var color = getColorValue(node);
    if (color) {
        var range = getRange(node, document);
        return { color: color, range: range };
    }
    return null;
}
function getRange(node, document) {
    return Range.create(document.positionAt(node.offset), document.positionAt(node.end));
}
function getHighlightKind(node) {
    if (node.type === nodes.NodeType.Selector) {
        return DocumentHighlightKind.Write;
    }
    if (node instanceof nodes.Identifier) {
        if (node.parent && node.parent instanceof nodes.Property) {
            if (node.isCustomProperty) {
                return DocumentHighlightKind.Write;
            }
        }
    }
    if (node.parent) {
        switch (node.parent.type) {
            case nodes.NodeType.FunctionDeclaration:
            case nodes.NodeType.MixinDeclaration:
            case nodes.NodeType.Keyframe:
            case nodes.NodeType.VariableDeclaration:
            case nodes.NodeType.FunctionParameter:
                return DocumentHighlightKind.Write;
        }
    }
    return DocumentHighlightKind.Read;
}
function toTwoDigitHex(n) {
    var r = n.toString(16);
    return r.length !== 2 ? '0' + r : r;
}
function getModuleNameFromPath(path) {
    // If a scoped module (starts with @) then get up until second instance of '/', otherwise get until first instance of '/'
    if (path[0] === '@') {
        return path.substring(0, path.indexOf('/', path.indexOf('/') + 1));
    }
    return path.substring(0, path.indexOf('/'));
}
