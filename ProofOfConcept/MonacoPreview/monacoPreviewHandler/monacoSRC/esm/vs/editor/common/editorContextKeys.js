/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { RawContextKey } from '../../platform/contextkey/common/contextkey.js';
export var EditorContextKeys;
(function (EditorContextKeys) {
    EditorContextKeys.editorSimpleInput = new RawContextKey('editorSimpleInput', false);
    /**
     * A context key that is set when the editor's text has focus (cursor is blinking).
     * Is false when focus is in simple editor widgets (repl input, scm commit input).
     */
    EditorContextKeys.editorTextFocus = new RawContextKey('editorTextFocus', false);
    /**
     * A context key that is set when the editor's text or an editor's widget has focus.
     */
    EditorContextKeys.focus = new RawContextKey('editorFocus', false);
    /**
     * A context key that is set when any editor input has focus (regular editor, repl input...).
     */
    EditorContextKeys.textInputFocus = new RawContextKey('textInputFocus', false);
    EditorContextKeys.readOnly = new RawContextKey('editorReadonly', false);
    EditorContextKeys.inDiffEditor = new RawContextKey('inDiffEditor', false);
    EditorContextKeys.columnSelection = new RawContextKey('editorColumnSelection', false);
    EditorContextKeys.writable = EditorContextKeys.readOnly.toNegated();
    EditorContextKeys.hasNonEmptySelection = new RawContextKey('editorHasSelection', false);
    EditorContextKeys.hasOnlyEmptySelection = EditorContextKeys.hasNonEmptySelection.toNegated();
    EditorContextKeys.hasMultipleSelections = new RawContextKey('editorHasMultipleSelections', false);
    EditorContextKeys.hasSingleSelection = EditorContextKeys.hasMultipleSelections.toNegated();
    EditorContextKeys.tabMovesFocus = new RawContextKey('editorTabMovesFocus', false);
    EditorContextKeys.tabDoesNotMoveFocus = EditorContextKeys.tabMovesFocus.toNegated();
    EditorContextKeys.isInWalkThroughSnippet = new RawContextKey('isInEmbeddedEditor', false);
    EditorContextKeys.canUndo = new RawContextKey('canUndo', false);
    EditorContextKeys.canRedo = new RawContextKey('canRedo', false);
    EditorContextKeys.hoverVisible = new RawContextKey('editorHoverVisible', false);
    /**
     * A context key that is set when an editor is part of a larger editor, like notebooks or
     * (future) a diff editor
     */
    EditorContextKeys.inCompositeEditor = new RawContextKey('inCompositeEditor', undefined);
    EditorContextKeys.notInCompositeEditor = EditorContextKeys.inCompositeEditor.toNegated();
    // -- mode context keys
    EditorContextKeys.languageId = new RawContextKey('editorLangId', '');
    EditorContextKeys.hasCompletionItemProvider = new RawContextKey('editorHasCompletionItemProvider', false);
    EditorContextKeys.hasCodeActionsProvider = new RawContextKey('editorHasCodeActionsProvider', false);
    EditorContextKeys.hasCodeLensProvider = new RawContextKey('editorHasCodeLensProvider', false);
    EditorContextKeys.hasDefinitionProvider = new RawContextKey('editorHasDefinitionProvider', false);
    EditorContextKeys.hasDeclarationProvider = new RawContextKey('editorHasDeclarationProvider', false);
    EditorContextKeys.hasImplementationProvider = new RawContextKey('editorHasImplementationProvider', false);
    EditorContextKeys.hasTypeDefinitionProvider = new RawContextKey('editorHasTypeDefinitionProvider', false);
    EditorContextKeys.hasHoverProvider = new RawContextKey('editorHasHoverProvider', false);
    EditorContextKeys.hasDocumentHighlightProvider = new RawContextKey('editorHasDocumentHighlightProvider', false);
    EditorContextKeys.hasDocumentSymbolProvider = new RawContextKey('editorHasDocumentSymbolProvider', false);
    EditorContextKeys.hasReferenceProvider = new RawContextKey('editorHasReferenceProvider', false);
    EditorContextKeys.hasRenameProvider = new RawContextKey('editorHasRenameProvider', false);
    EditorContextKeys.hasSignatureHelpProvider = new RawContextKey('editorHasSignatureHelpProvider', false);
    EditorContextKeys.hasInlineHintsProvider = new RawContextKey('editorHasInlineHintsProvider', false);
    // -- mode context keys: formatting
    EditorContextKeys.hasDocumentFormattingProvider = new RawContextKey('editorHasDocumentFormattingProvider', false);
    EditorContextKeys.hasDocumentSelectionFormattingProvider = new RawContextKey('editorHasDocumentSelectionFormattingProvider', false);
    EditorContextKeys.hasMultipleDocumentFormattingProvider = new RawContextKey('editorHasMultipleDocumentFormattingProvider', false);
    EditorContextKeys.hasMultipleDocumentSelectionFormattingProvider = new RawContextKey('editorHasMultipleDocumentSelectionFormattingProvider', false);
})(EditorContextKeys || (EditorContextKeys = {}));
