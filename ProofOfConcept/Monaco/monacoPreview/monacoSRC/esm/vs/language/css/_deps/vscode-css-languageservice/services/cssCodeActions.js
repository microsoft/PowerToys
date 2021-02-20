/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
'use strict';
import * as nodes from '../parser/cssNodes.js';
import { difference } from '../utils/strings.js';
import { Rules } from '../services/lintRules.js';
import { Command, TextEdit, CodeAction, CodeActionKind, TextDocumentEdit, VersionedTextDocumentIdentifier } from '../cssLanguageTypes.js';
import * as nls from './../../../fillers/vscode-nls.js';
var localize = nls.loadMessageBundle();
var CSSCodeActions = /** @class */ (function () {
    function CSSCodeActions(cssDataManager) {
        this.cssDataManager = cssDataManager;
    }
    CSSCodeActions.prototype.doCodeActions = function (document, range, context, stylesheet) {
        return this.doCodeActions2(document, range, context, stylesheet).map(function (ca) {
            var textDocumentEdit = ca.edit && ca.edit.documentChanges && ca.edit.documentChanges[0];
            return Command.create(ca.title, '_css.applyCodeAction', document.uri, document.version, textDocumentEdit && textDocumentEdit.edits);
        });
    };
    CSSCodeActions.prototype.doCodeActions2 = function (document, range, context, stylesheet) {
        var result = [];
        if (context.diagnostics) {
            for (var _i = 0, _a = context.diagnostics; _i < _a.length; _i++) {
                var diagnostic = _a[_i];
                this.appendFixesForMarker(document, stylesheet, diagnostic, result);
            }
        }
        return result;
    };
    CSSCodeActions.prototype.getFixesForUnknownProperty = function (document, property, marker, result) {
        var propertyName = property.getName();
        var candidates = [];
        this.cssDataManager.getProperties().forEach(function (p) {
            var score = difference(propertyName, p.name);
            if (score >= propertyName.length / 2 /*score_lim*/) {
                candidates.push({ property: p.name, score: score });
            }
        });
        // Sort in descending order.
        candidates.sort(function (a, b) {
            return b.score - a.score || a.property.localeCompare(b.property);
        });
        var maxActions = 3;
        for (var _i = 0, candidates_1 = candidates; _i < candidates_1.length; _i++) {
            var candidate = candidates_1[_i];
            var propertyName_1 = candidate.property;
            var title = localize('css.codeaction.rename', "Rename to '{0}'", propertyName_1);
            var edit = TextEdit.replace(marker.range, propertyName_1);
            var documentIdentifier = VersionedTextDocumentIdentifier.create(document.uri, document.version);
            var workspaceEdit = { documentChanges: [TextDocumentEdit.create(documentIdentifier, [edit])] };
            var codeAction = CodeAction.create(title, workspaceEdit, CodeActionKind.QuickFix);
            codeAction.diagnostics = [marker];
            result.push(codeAction);
            if (--maxActions <= 0) {
                return;
            }
        }
    };
    CSSCodeActions.prototype.appendFixesForMarker = function (document, stylesheet, marker, result) {
        if (marker.code !== Rules.UnknownProperty.id) {
            return;
        }
        var offset = document.offsetAt(marker.range.start);
        var end = document.offsetAt(marker.range.end);
        var nodepath = nodes.getNodePath(stylesheet, offset);
        for (var i = nodepath.length - 1; i >= 0; i--) {
            var node = nodepath[i];
            if (node instanceof nodes.Declaration) {
                var property = node.getProperty();
                if (property && property.offset === offset && property.end === end) {
                    this.getFixesForUnknownProperty(document, property, marker, result);
                    return;
                }
            }
        }
    };
    return CSSCodeActions;
}());
export { CSSCodeActions };
