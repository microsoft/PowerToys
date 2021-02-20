/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
var __decorate = (this && this.__decorate) || function (decorators, target, key, desc) {
    var c = arguments.length, r = c < 3 ? target : desc === null ? desc = Object.getOwnPropertyDescriptor(target, key) : desc, d;
    if (typeof Reflect === "object" && typeof Reflect.decorate === "function") r = Reflect.decorate(decorators, target, key, desc);
    else for (var i = decorators.length - 1; i >= 0; i--) if (d = decorators[i]) r = (c < 3 ? d(r) : c > 3 ? d(target, key, r) : d(target, key)) || r;
    return c > 3 && r && Object.defineProperty(target, key, r), r;
};
var __param = (this && this.__param) || function (paramIndex, decorator) {
    return function (target, key) { decorator(target, key, paramIndex); }
};
import './media/editor.css';
import * as nls from '../../../nls.js';
import * as dom from '../../../base/browser/dom.js';
import { onUnexpectedError } from '../../../base/common/errors.js';
import { Emitter } from '../../../base/common/event.js';
import { Disposable, dispose } from '../../../base/common/lifecycle.js';
import { Schemas } from '../../../base/common/network.js';
import { Configuration } from '../config/configuration.js';
import { EditorExtensionsRegistry } from '../editorExtensions.js';
import { ICodeEditorService } from '../services/codeEditorService.js';
import { View } from '../view/viewImpl.js';
import { ViewUserInputEvents } from '../view/viewUserInputEvents.js';
import { filterValidationDecorations } from '../../common/config/editorOptions.js';
import { Cursor } from '../../common/controller/cursor.js';
import { CursorColumns } from '../../common/controller/cursorCommon.js';
import { Position } from '../../common/core/position.js';
import { Range } from '../../common/core/range.js';
import { Selection } from '../../common/core/selection.js';
import { InternalEditorAction } from '../../common/editorAction.js';
import * as editorCommon from '../../common/editorCommon.js';
import { EditorContextKeys } from '../../common/editorContextKeys.js';
import * as modes from '../../common/modes.js';
import { editorUnnecessaryCodeBorder, editorUnnecessaryCodeOpacity } from '../../common/view/editorColorRegistry.js';
import { editorErrorBorder, editorErrorForeground, editorHintBorder, editorHintForeground, editorInfoBorder, editorInfoForeground, editorWarningBorder, editorWarningForeground, editorForeground, editorErrorBackground, editorInfoBackground, editorWarningBackground } from '../../../platform/theme/common/colorRegistry.js';
import { ViewModel } from '../../common/viewModel/viewModelImpl.js';
import { ICommandService } from '../../../platform/commands/common/commands.js';
import { IContextKeyService } from '../../../platform/contextkey/common/contextkey.js';
import { IInstantiationService } from '../../../platform/instantiation/common/instantiation.js';
import { ServiceCollection } from '../../../platform/instantiation/common/serviceCollection.js';
import { INotificationService } from '../../../platform/notification/common/notification.js';
import { IThemeService, registerThemingParticipant } from '../../../platform/theme/common/themeService.js';
import { IAccessibilityService } from '../../../platform/accessibility/common/accessibility.js';
import { withNullAsUndefined } from '../../../base/common/types.js';
import { MonospaceLineBreaksComputerFactory } from '../../common/viewModel/monospaceLineBreaksComputer.js';
import { DOMLineBreaksComputerFactory } from '../view/domLineBreaksComputer.js';
import { WordOperations } from '../../common/controller/cursorWordOperations.js';
let EDITOR_ID = 0;
class ModelData {
    constructor(model, viewModel, view, hasRealView, listenersToRemove) {
        this.model = model;
        this.viewModel = viewModel;
        this.view = view;
        this.hasRealView = hasRealView;
        this.listenersToRemove = listenersToRemove;
    }
    dispose() {
        dispose(this.listenersToRemove);
        this.model.onBeforeDetached();
        if (this.hasRealView) {
            this.view.dispose();
        }
        this.viewModel.dispose();
    }
}
let CodeEditorWidget = class CodeEditorWidget extends Disposable {
    constructor(domElement, _options, codeEditorWidgetOptions, instantiationService, codeEditorService, commandService, contextKeyService, themeService, notificationService, accessibilityService) {
        super();
        //#region Eventing
        this._onDidDispose = this._register(new Emitter());
        this.onDidDispose = this._onDidDispose.event;
        this._onDidChangeModelContent = this._register(new Emitter());
        this.onDidChangeModelContent = this._onDidChangeModelContent.event;
        this._onDidChangeModelLanguage = this._register(new Emitter());
        this.onDidChangeModelLanguage = this._onDidChangeModelLanguage.event;
        this._onDidChangeModelLanguageConfiguration = this._register(new Emitter());
        this.onDidChangeModelLanguageConfiguration = this._onDidChangeModelLanguageConfiguration.event;
        this._onDidChangeModelOptions = this._register(new Emitter());
        this.onDidChangeModelOptions = this._onDidChangeModelOptions.event;
        this._onDidChangeModelDecorations = this._register(new Emitter());
        this.onDidChangeModelDecorations = this._onDidChangeModelDecorations.event;
        this._onDidChangeConfiguration = this._register(new Emitter());
        this.onDidChangeConfiguration = this._onDidChangeConfiguration.event;
        this._onDidChangeModel = this._register(new Emitter());
        this.onDidChangeModel = this._onDidChangeModel.event;
        this._onDidChangeCursorPosition = this._register(new Emitter());
        this.onDidChangeCursorPosition = this._onDidChangeCursorPosition.event;
        this._onDidChangeCursorSelection = this._register(new Emitter());
        this.onDidChangeCursorSelection = this._onDidChangeCursorSelection.event;
        this._onDidAttemptReadOnlyEdit = this._register(new Emitter());
        this.onDidAttemptReadOnlyEdit = this._onDidAttemptReadOnlyEdit.event;
        this._onDidLayoutChange = this._register(new Emitter());
        this.onDidLayoutChange = this._onDidLayoutChange.event;
        this._editorTextFocus = this._register(new BooleanEventEmitter());
        this.onDidFocusEditorText = this._editorTextFocus.onDidChangeToTrue;
        this.onDidBlurEditorText = this._editorTextFocus.onDidChangeToFalse;
        this._editorWidgetFocus = this._register(new BooleanEventEmitter());
        this.onDidFocusEditorWidget = this._editorWidgetFocus.onDidChangeToTrue;
        this.onDidBlurEditorWidget = this._editorWidgetFocus.onDidChangeToFalse;
        this._onWillType = this._register(new Emitter());
        this.onWillType = this._onWillType.event;
        this._onDidType = this._register(new Emitter());
        this.onDidType = this._onDidType.event;
        this._onDidCompositionStart = this._register(new Emitter());
        this.onDidCompositionStart = this._onDidCompositionStart.event;
        this._onDidCompositionEnd = this._register(new Emitter());
        this.onDidCompositionEnd = this._onDidCompositionEnd.event;
        this._onDidPaste = this._register(new Emitter());
        this.onDidPaste = this._onDidPaste.event;
        this._onMouseUp = this._register(new Emitter());
        this.onMouseUp = this._onMouseUp.event;
        this._onMouseDown = this._register(new Emitter());
        this.onMouseDown = this._onMouseDown.event;
        this._onMouseDrag = this._register(new Emitter());
        this.onMouseDrag = this._onMouseDrag.event;
        this._onMouseDrop = this._register(new Emitter());
        this.onMouseDrop = this._onMouseDrop.event;
        this._onMouseDropCanceled = this._register(new Emitter());
        this.onMouseDropCanceled = this._onMouseDropCanceled.event;
        this._onContextMenu = this._register(new Emitter());
        this.onContextMenu = this._onContextMenu.event;
        this._onMouseMove = this._register(new Emitter());
        this.onMouseMove = this._onMouseMove.event;
        this._onMouseLeave = this._register(new Emitter());
        this.onMouseLeave = this._onMouseLeave.event;
        this._onMouseWheel = this._register(new Emitter());
        this.onMouseWheel = this._onMouseWheel.event;
        this._onKeyUp = this._register(new Emitter());
        this.onKeyUp = this._onKeyUp.event;
        this._onKeyDown = this._register(new Emitter());
        this.onKeyDown = this._onKeyDown.event;
        this._onDidContentSizeChange = this._register(new Emitter());
        this.onDidContentSizeChange = this._onDidContentSizeChange.event;
        this._onDidScrollChange = this._register(new Emitter());
        this.onDidScrollChange = this._onDidScrollChange.event;
        this._onDidChangeViewZones = this._register(new Emitter());
        this.onDidChangeViewZones = this._onDidChangeViewZones.event;
        const options = Object.assign({}, _options);
        this._domElement = domElement;
        this._overflowWidgetsDomNode = options.overflowWidgetsDomNode;
        delete options.overflowWidgetsDomNode;
        this._id = (++EDITOR_ID);
        this._decorationTypeKeysToIds = {};
        this._decorationTypeSubtypes = {};
        this.isSimpleWidget = codeEditorWidgetOptions.isSimpleWidget || false;
        this._telemetryData = codeEditorWidgetOptions.telemetryData;
        this._configuration = this._register(this._createConfiguration(options, accessibilityService));
        this._register(this._configuration.onDidChange((e) => {
            this._onDidChangeConfiguration.fire(e);
            const options = this._configuration.options;
            if (e.hasChanged(124 /* layoutInfo */)) {
                const layoutInfo = options.get(124 /* layoutInfo */);
                this._onDidLayoutChange.fire(layoutInfo);
            }
        }));
        this._contextKeyService = this._register(contextKeyService.createScoped(this._domElement));
        this._notificationService = notificationService;
        this._codeEditorService = codeEditorService;
        this._commandService = commandService;
        this._themeService = themeService;
        this._register(new EditorContextKeysManager(this, this._contextKeyService));
        this._register(new EditorModeContext(this, this._contextKeyService));
        this._instantiationService = instantiationService.createChild(new ServiceCollection([IContextKeyService, this._contextKeyService]));
        this._modelData = null;
        this._contributions = {};
        this._actions = {};
        this._focusTracker = new CodeEditorWidgetFocusTracker(domElement);
        this._focusTracker.onChange(() => {
            this._editorWidgetFocus.setValue(this._focusTracker.hasFocus());
        });
        this._contentWidgets = {};
        this._overlayWidgets = {};
        let contributions;
        if (Array.isArray(codeEditorWidgetOptions.contributions)) {
            contributions = codeEditorWidgetOptions.contributions;
        }
        else {
            contributions = EditorExtensionsRegistry.getEditorContributions();
        }
        for (const desc of contributions) {
            try {
                const contribution = this._instantiationService.createInstance(desc.ctor, this);
                this._contributions[desc.id] = contribution;
            }
            catch (err) {
                onUnexpectedError(err);
            }
        }
        EditorExtensionsRegistry.getEditorActions().forEach((action) => {
            const internalAction = new InternalEditorAction(action.id, action.label, action.alias, withNullAsUndefined(action.precondition), () => {
                return this._instantiationService.invokeFunction((accessor) => {
                    return Promise.resolve(action.runEditorCommand(accessor, this, null));
                });
            }, this._contextKeyService);
            this._actions[internalAction.id] = internalAction;
        });
        this._codeEditorService.addCodeEditor(this);
    }
    _createConfiguration(options, accessibilityService) {
        return new Configuration(this.isSimpleWidget, options, this._domElement, accessibilityService);
    }
    getId() {
        return this.getEditorType() + ':' + this._id;
    }
    getEditorType() {
        return editorCommon.EditorType.ICodeEditor;
    }
    dispose() {
        this._codeEditorService.removeCodeEditor(this);
        this._focusTracker.dispose();
        const keys = Object.keys(this._contributions);
        for (let i = 0, len = keys.length; i < len; i++) {
            const contributionId = keys[i];
            this._contributions[contributionId].dispose();
        }
        this._removeDecorationTypes();
        this._postDetachModelCleanup(this._detachModel());
        this._onDidDispose.fire();
        super.dispose();
    }
    invokeWithinContext(fn) {
        return this._instantiationService.invokeFunction(fn);
    }
    updateOptions(newOptions) {
        this._configuration.updateOptions(newOptions);
    }
    getOptions() {
        return this._configuration.options;
    }
    getOption(id) {
        return this._configuration.options.get(id);
    }
    getRawOptions() {
        return this._configuration.getRawOptions();
    }
    getOverflowWidgetsDomNode() {
        return this._overflowWidgetsDomNode;
    }
    getConfiguredWordAtPosition(position) {
        if (!this._modelData) {
            return null;
        }
        return WordOperations.getWordAtPosition(this._modelData.model, this._configuration.options.get(110 /* wordSeparators */), position);
    }
    getValue(options = null) {
        if (!this._modelData) {
            return '';
        }
        const preserveBOM = (options && options.preserveBOM) ? true : false;
        let eolPreference = 0 /* TextDefined */;
        if (options && options.lineEnding && options.lineEnding === '\n') {
            eolPreference = 1 /* LF */;
        }
        else if (options && options.lineEnding && options.lineEnding === '\r\n') {
            eolPreference = 2 /* CRLF */;
        }
        return this._modelData.model.getValue(eolPreference, preserveBOM);
    }
    setValue(newValue) {
        if (!this._modelData) {
            return;
        }
        this._modelData.model.setValue(newValue);
    }
    getModel() {
        if (!this._modelData) {
            return null;
        }
        return this._modelData.model;
    }
    setModel(_model = null) {
        const model = _model;
        if (this._modelData === null && model === null) {
            // Current model is the new model
            return;
        }
        if (this._modelData && this._modelData.model === model) {
            // Current model is the new model
            return;
        }
        const hasTextFocus = this.hasTextFocus();
        const detachedModel = this._detachModel();
        this._attachModel(model);
        if (hasTextFocus && this.hasModel()) {
            this.focus();
        }
        const e = {
            oldModelUrl: detachedModel ? detachedModel.uri : null,
            newModelUrl: model ? model.uri : null
        };
        this._removeDecorationTypes();
        this._onDidChangeModel.fire(e);
        this._postDetachModelCleanup(detachedModel);
    }
    _removeDecorationTypes() {
        this._decorationTypeKeysToIds = {};
        if (this._decorationTypeSubtypes) {
            for (let decorationType in this._decorationTypeSubtypes) {
                const subTypes = this._decorationTypeSubtypes[decorationType];
                for (let subType in subTypes) {
                    this._removeDecorationType(decorationType + '-' + subType);
                }
            }
            this._decorationTypeSubtypes = {};
        }
    }
    getVisibleRanges() {
        if (!this._modelData) {
            return [];
        }
        return this._modelData.viewModel.getVisibleRanges();
    }
    getVisibleRangesPlusViewportAboveBelow() {
        if (!this._modelData) {
            return [];
        }
        return this._modelData.viewModel.getVisibleRangesPlusViewportAboveBelow();
    }
    getWhitespaces() {
        if (!this._modelData) {
            return [];
        }
        return this._modelData.viewModel.viewLayout.getWhitespaces();
    }
    static _getVerticalOffsetForPosition(modelData, modelLineNumber, modelColumn) {
        const modelPosition = modelData.model.validatePosition({
            lineNumber: modelLineNumber,
            column: modelColumn
        });
        const viewPosition = modelData.viewModel.coordinatesConverter.convertModelPositionToViewPosition(modelPosition);
        return modelData.viewModel.viewLayout.getVerticalOffsetForLineNumber(viewPosition.lineNumber);
    }
    getTopForLineNumber(lineNumber) {
        if (!this._modelData) {
            return -1;
        }
        return CodeEditorWidget._getVerticalOffsetForPosition(this._modelData, lineNumber, 1);
    }
    getTopForPosition(lineNumber, column) {
        if (!this._modelData) {
            return -1;
        }
        return CodeEditorWidget._getVerticalOffsetForPosition(this._modelData, lineNumber, column);
    }
    setHiddenAreas(ranges) {
        if (this._modelData) {
            this._modelData.viewModel.setHiddenAreas(ranges.map(r => Range.lift(r)));
        }
    }
    getVisibleColumnFromPosition(rawPosition) {
        if (!this._modelData) {
            return rawPosition.column;
        }
        const position = this._modelData.model.validatePosition(rawPosition);
        const tabSize = this._modelData.model.getOptions().tabSize;
        return CursorColumns.visibleColumnFromColumn(this._modelData.model.getLineContent(position.lineNumber), position.column, tabSize) + 1;
    }
    getPosition() {
        if (!this._modelData) {
            return null;
        }
        return this._modelData.viewModel.getPosition();
    }
    setPosition(position) {
        if (!this._modelData) {
            return;
        }
        if (!Position.isIPosition(position)) {
            throw new Error('Invalid arguments');
        }
        this._modelData.viewModel.setSelections('api', [{
                selectionStartLineNumber: position.lineNumber,
                selectionStartColumn: position.column,
                positionLineNumber: position.lineNumber,
                positionColumn: position.column
            }]);
    }
    _sendRevealRange(modelRange, verticalType, revealHorizontal, scrollType) {
        if (!this._modelData) {
            return;
        }
        if (!Range.isIRange(modelRange)) {
            throw new Error('Invalid arguments');
        }
        const validatedModelRange = this._modelData.model.validateRange(modelRange);
        const viewRange = this._modelData.viewModel.coordinatesConverter.convertModelRangeToViewRange(validatedModelRange);
        this._modelData.viewModel.revealRange('api', revealHorizontal, viewRange, verticalType, scrollType);
    }
    revealLine(lineNumber, scrollType = 0 /* Smooth */) {
        this._revealLine(lineNumber, 0 /* Simple */, scrollType);
    }
    revealLineInCenter(lineNumber, scrollType = 0 /* Smooth */) {
        this._revealLine(lineNumber, 1 /* Center */, scrollType);
    }
    revealLineInCenterIfOutsideViewport(lineNumber, scrollType = 0 /* Smooth */) {
        this._revealLine(lineNumber, 2 /* CenterIfOutsideViewport */, scrollType);
    }
    revealLineNearTop(lineNumber, scrollType = 0 /* Smooth */) {
        this._revealLine(lineNumber, 5 /* NearTop */, scrollType);
    }
    _revealLine(lineNumber, revealType, scrollType) {
        if (typeof lineNumber !== 'number') {
            throw new Error('Invalid arguments');
        }
        this._sendRevealRange(new Range(lineNumber, 1, lineNumber, 1), revealType, false, scrollType);
    }
    revealPosition(position, scrollType = 0 /* Smooth */) {
        this._revealPosition(position, 0 /* Simple */, true, scrollType);
    }
    revealPositionInCenter(position, scrollType = 0 /* Smooth */) {
        this._revealPosition(position, 1 /* Center */, true, scrollType);
    }
    revealPositionInCenterIfOutsideViewport(position, scrollType = 0 /* Smooth */) {
        this._revealPosition(position, 2 /* CenterIfOutsideViewport */, true, scrollType);
    }
    revealPositionNearTop(position, scrollType = 0 /* Smooth */) {
        this._revealPosition(position, 5 /* NearTop */, true, scrollType);
    }
    _revealPosition(position, verticalType, revealHorizontal, scrollType) {
        if (!Position.isIPosition(position)) {
            throw new Error('Invalid arguments');
        }
        this._sendRevealRange(new Range(position.lineNumber, position.column, position.lineNumber, position.column), verticalType, revealHorizontal, scrollType);
    }
    getSelection() {
        if (!this._modelData) {
            return null;
        }
        return this._modelData.viewModel.getSelection();
    }
    getSelections() {
        if (!this._modelData) {
            return null;
        }
        return this._modelData.viewModel.getSelections();
    }
    setSelection(something) {
        const isSelection = Selection.isISelection(something);
        const isRange = Range.isIRange(something);
        if (!isSelection && !isRange) {
            throw new Error('Invalid arguments');
        }
        if (isSelection) {
            this._setSelectionImpl(something);
        }
        else if (isRange) {
            // act as if it was an IRange
            const selection = {
                selectionStartLineNumber: something.startLineNumber,
                selectionStartColumn: something.startColumn,
                positionLineNumber: something.endLineNumber,
                positionColumn: something.endColumn
            };
            this._setSelectionImpl(selection);
        }
    }
    _setSelectionImpl(sel) {
        if (!this._modelData) {
            return;
        }
        const selection = new Selection(sel.selectionStartLineNumber, sel.selectionStartColumn, sel.positionLineNumber, sel.positionColumn);
        this._modelData.viewModel.setSelections('api', [selection]);
    }
    revealLines(startLineNumber, endLineNumber, scrollType = 0 /* Smooth */) {
        this._revealLines(startLineNumber, endLineNumber, 0 /* Simple */, scrollType);
    }
    revealLinesInCenter(startLineNumber, endLineNumber, scrollType = 0 /* Smooth */) {
        this._revealLines(startLineNumber, endLineNumber, 1 /* Center */, scrollType);
    }
    revealLinesInCenterIfOutsideViewport(startLineNumber, endLineNumber, scrollType = 0 /* Smooth */) {
        this._revealLines(startLineNumber, endLineNumber, 2 /* CenterIfOutsideViewport */, scrollType);
    }
    revealLinesNearTop(startLineNumber, endLineNumber, scrollType = 0 /* Smooth */) {
        this._revealLines(startLineNumber, endLineNumber, 5 /* NearTop */, scrollType);
    }
    _revealLines(startLineNumber, endLineNumber, verticalType, scrollType) {
        if (typeof startLineNumber !== 'number' || typeof endLineNumber !== 'number') {
            throw new Error('Invalid arguments');
        }
        this._sendRevealRange(new Range(startLineNumber, 1, endLineNumber, 1), verticalType, false, scrollType);
    }
    revealRange(range, scrollType = 0 /* Smooth */, revealVerticalInCenter = false, revealHorizontal = true) {
        this._revealRange(range, revealVerticalInCenter ? 1 /* Center */ : 0 /* Simple */, revealHorizontal, scrollType);
    }
    revealRangeInCenter(range, scrollType = 0 /* Smooth */) {
        this._revealRange(range, 1 /* Center */, true, scrollType);
    }
    revealRangeInCenterIfOutsideViewport(range, scrollType = 0 /* Smooth */) {
        this._revealRange(range, 2 /* CenterIfOutsideViewport */, true, scrollType);
    }
    revealRangeNearTop(range, scrollType = 0 /* Smooth */) {
        this._revealRange(range, 5 /* NearTop */, true, scrollType);
    }
    revealRangeNearTopIfOutsideViewport(range, scrollType = 0 /* Smooth */) {
        this._revealRange(range, 6 /* NearTopIfOutsideViewport */, true, scrollType);
    }
    revealRangeAtTop(range, scrollType = 0 /* Smooth */) {
        this._revealRange(range, 3 /* Top */, true, scrollType);
    }
    _revealRange(range, verticalType, revealHorizontal, scrollType) {
        if (!Range.isIRange(range)) {
            throw new Error('Invalid arguments');
        }
        this._sendRevealRange(Range.lift(range), verticalType, revealHorizontal, scrollType);
    }
    setSelections(ranges, source = 'api', reason = 0 /* NotSet */) {
        if (!this._modelData) {
            return;
        }
        if (!ranges || ranges.length === 0) {
            throw new Error('Invalid arguments');
        }
        for (let i = 0, len = ranges.length; i < len; i++) {
            if (!Selection.isISelection(ranges[i])) {
                throw new Error('Invalid arguments');
            }
        }
        this._modelData.viewModel.setSelections(source, ranges, reason);
    }
    getContentWidth() {
        if (!this._modelData) {
            return -1;
        }
        return this._modelData.viewModel.viewLayout.getContentWidth();
    }
    getScrollWidth() {
        if (!this._modelData) {
            return -1;
        }
        return this._modelData.viewModel.viewLayout.getScrollWidth();
    }
    getScrollLeft() {
        if (!this._modelData) {
            return -1;
        }
        return this._modelData.viewModel.viewLayout.getCurrentScrollLeft();
    }
    getContentHeight() {
        if (!this._modelData) {
            return -1;
        }
        return this._modelData.viewModel.viewLayout.getContentHeight();
    }
    getScrollHeight() {
        if (!this._modelData) {
            return -1;
        }
        return this._modelData.viewModel.viewLayout.getScrollHeight();
    }
    getScrollTop() {
        if (!this._modelData) {
            return -1;
        }
        return this._modelData.viewModel.viewLayout.getCurrentScrollTop();
    }
    setScrollLeft(newScrollLeft, scrollType = 1 /* Immediate */) {
        if (!this._modelData) {
            return;
        }
        if (typeof newScrollLeft !== 'number') {
            throw new Error('Invalid arguments');
        }
        this._modelData.viewModel.setScrollPosition({
            scrollLeft: newScrollLeft
        }, scrollType);
    }
    setScrollTop(newScrollTop, scrollType = 1 /* Immediate */) {
        if (!this._modelData) {
            return;
        }
        if (typeof newScrollTop !== 'number') {
            throw new Error('Invalid arguments');
        }
        this._modelData.viewModel.setScrollPosition({
            scrollTop: newScrollTop
        }, scrollType);
    }
    setScrollPosition(position, scrollType = 1 /* Immediate */) {
        if (!this._modelData) {
            return;
        }
        this._modelData.viewModel.setScrollPosition(position, scrollType);
    }
    saveViewState() {
        if (!this._modelData) {
            return null;
        }
        const contributionsState = {};
        const keys = Object.keys(this._contributions);
        for (const id of keys) {
            const contribution = this._contributions[id];
            if (typeof contribution.saveViewState === 'function') {
                contributionsState[id] = contribution.saveViewState();
            }
        }
        const cursorState = this._modelData.viewModel.saveCursorState();
        const viewState = this._modelData.viewModel.saveState();
        return {
            cursorState: cursorState,
            viewState: viewState,
            contributionsState: contributionsState
        };
    }
    restoreViewState(s) {
        if (!this._modelData || !this._modelData.hasRealView) {
            return;
        }
        const codeEditorState = s;
        if (codeEditorState && codeEditorState.cursorState && codeEditorState.viewState) {
            const cursorState = codeEditorState.cursorState;
            if (Array.isArray(cursorState)) {
                this._modelData.viewModel.restoreCursorState(cursorState);
            }
            else {
                // Backwards compatibility
                this._modelData.viewModel.restoreCursorState([cursorState]);
            }
            const contributionsState = codeEditorState.contributionsState || {};
            const keys = Object.keys(this._contributions);
            for (let i = 0, len = keys.length; i < len; i++) {
                const id = keys[i];
                const contribution = this._contributions[id];
                if (typeof contribution.restoreViewState === 'function') {
                    contribution.restoreViewState(contributionsState[id]);
                }
            }
            const reducedState = this._modelData.viewModel.reduceRestoreState(codeEditorState.viewState);
            this._modelData.view.restoreState(reducedState);
        }
    }
    getContribution(id) {
        return (this._contributions[id] || null);
    }
    getActions() {
        const result = [];
        const keys = Object.keys(this._actions);
        for (let i = 0, len = keys.length; i < len; i++) {
            const id = keys[i];
            result.push(this._actions[id]);
        }
        return result;
    }
    getSupportedActions() {
        let result = this.getActions();
        result = result.filter(action => action.isSupported());
        return result;
    }
    getAction(id) {
        return this._actions[id] || null;
    }
    trigger(source, handlerId, payload) {
        payload = payload || {};
        switch (handlerId) {
            case "compositionStart" /* CompositionStart */:
                this._startComposition();
                return;
            case "compositionEnd" /* CompositionEnd */:
                this._endComposition(source);
                return;
            case "type" /* Type */: {
                const args = payload;
                this._type(source, args.text || '');
                return;
            }
            case "replacePreviousChar" /* ReplacePreviousChar */: {
                const args = payload;
                this._replacePreviousChar(source, args.text || '', args.replaceCharCnt || 0);
                return;
            }
            case "paste" /* Paste */: {
                const args = payload;
                this._paste(source, args.text || '', args.pasteOnNewLine || false, args.multicursorText || null, args.mode || null);
                return;
            }
            case "cut" /* Cut */:
                this._cut(source);
                return;
        }
        const action = this.getAction(handlerId);
        if (action) {
            Promise.resolve(action.run()).then(undefined, onUnexpectedError);
            return;
        }
        if (!this._modelData) {
            return;
        }
        if (this._triggerEditorCommand(source, handlerId, payload)) {
            return;
        }
        this._commandService.executeCommand(handlerId, payload);
    }
    _startComposition() {
        if (!this._modelData) {
            return;
        }
        this._modelData.viewModel.startComposition();
        this._onDidCompositionStart.fire();
    }
    _endComposition(source) {
        if (!this._modelData) {
            return;
        }
        this._modelData.viewModel.endComposition(source);
        this._onDidCompositionEnd.fire();
    }
    _type(source, text) {
        if (!this._modelData || text.length === 0) {
            return;
        }
        if (source === 'keyboard') {
            this._onWillType.fire(text);
        }
        this._modelData.viewModel.type(text, source);
        if (source === 'keyboard') {
            this._onDidType.fire(text);
        }
    }
    _replacePreviousChar(source, text, replaceCharCnt) {
        if (!this._modelData) {
            return;
        }
        this._modelData.viewModel.replacePreviousChar(text, replaceCharCnt, source);
    }
    _paste(source, text, pasteOnNewLine, multicursorText, mode) {
        if (!this._modelData || text.length === 0) {
            return;
        }
        const startPosition = this._modelData.viewModel.getSelection().getStartPosition();
        this._modelData.viewModel.paste(text, pasteOnNewLine, multicursorText, source);
        const endPosition = this._modelData.viewModel.getSelection().getStartPosition();
        if (source === 'keyboard') {
            this._onDidPaste.fire({
                range: new Range(startPosition.lineNumber, startPosition.column, endPosition.lineNumber, endPosition.column),
                mode: mode
            });
        }
    }
    _cut(source) {
        if (!this._modelData) {
            return;
        }
        this._modelData.viewModel.cut(source);
    }
    _triggerEditorCommand(source, handlerId, payload) {
        const command = EditorExtensionsRegistry.getEditorCommand(handlerId);
        if (command) {
            payload = payload || {};
            payload.source = source;
            this._instantiationService.invokeFunction((accessor) => {
                Promise.resolve(command.runEditorCommand(accessor, this, payload)).then(undefined, onUnexpectedError);
            });
            return true;
        }
        return false;
    }
    _getViewModel() {
        if (!this._modelData) {
            return null;
        }
        return this._modelData.viewModel;
    }
    pushUndoStop() {
        if (!this._modelData) {
            return false;
        }
        if (this._configuration.options.get(75 /* readOnly */)) {
            // read only editor => sorry!
            return false;
        }
        this._modelData.model.pushStackElement();
        return true;
    }
    popUndoStop() {
        if (!this._modelData) {
            return false;
        }
        if (this._configuration.options.get(75 /* readOnly */)) {
            // read only editor => sorry!
            return false;
        }
        this._modelData.model.popStackElement();
        return true;
    }
    executeEdits(source, edits, endCursorState) {
        if (!this._modelData) {
            return false;
        }
        if (this._configuration.options.get(75 /* readOnly */)) {
            // read only editor => sorry!
            return false;
        }
        let cursorStateComputer;
        if (!endCursorState) {
            cursorStateComputer = () => null;
        }
        else if (Array.isArray(endCursorState)) {
            cursorStateComputer = () => endCursorState;
        }
        else {
            cursorStateComputer = endCursorState;
        }
        this._modelData.viewModel.executeEdits(source, edits, cursorStateComputer);
        return true;
    }
    executeCommand(source, command) {
        if (!this._modelData) {
            return;
        }
        this._modelData.viewModel.executeCommand(command, source);
    }
    executeCommands(source, commands) {
        if (!this._modelData) {
            return;
        }
        this._modelData.viewModel.executeCommands(commands, source);
    }
    changeDecorations(callback) {
        if (!this._modelData) {
            // callback will not be called
            return null;
        }
        return this._modelData.model.changeDecorations(callback, this._id);
    }
    getLineDecorations(lineNumber) {
        if (!this._modelData) {
            return null;
        }
        return this._modelData.model.getLineDecorations(lineNumber, this._id, filterValidationDecorations(this._configuration.options));
    }
    deltaDecorations(oldDecorations, newDecorations) {
        if (!this._modelData) {
            return [];
        }
        if (oldDecorations.length === 0 && newDecorations.length === 0) {
            return oldDecorations;
        }
        return this._modelData.model.deltaDecorations(oldDecorations, newDecorations, this._id);
    }
    removeDecorations(decorationTypeKey) {
        // remove decorations for type and sub type
        const oldDecorationsIds = this._decorationTypeKeysToIds[decorationTypeKey];
        if (oldDecorationsIds) {
            this.deltaDecorations(oldDecorationsIds, []);
        }
        if (this._decorationTypeKeysToIds.hasOwnProperty(decorationTypeKey)) {
            delete this._decorationTypeKeysToIds[decorationTypeKey];
        }
        if (this._decorationTypeSubtypes.hasOwnProperty(decorationTypeKey)) {
            delete this._decorationTypeSubtypes[decorationTypeKey];
        }
    }
    getLayoutInfo() {
        const options = this._configuration.options;
        const layoutInfo = options.get(124 /* layoutInfo */);
        return layoutInfo;
    }
    createOverviewRuler(cssClassName) {
        if (!this._modelData || !this._modelData.hasRealView) {
            return null;
        }
        return this._modelData.view.createOverviewRuler(cssClassName);
    }
    getContainerDomNode() {
        return this._domElement;
    }
    getDomNode() {
        if (!this._modelData || !this._modelData.hasRealView) {
            return null;
        }
        return this._modelData.view.domNode.domNode;
    }
    delegateVerticalScrollbarMouseDown(browserEvent) {
        if (!this._modelData || !this._modelData.hasRealView) {
            return;
        }
        this._modelData.view.delegateVerticalScrollbarMouseDown(browserEvent);
    }
    layout(dimension) {
        this._configuration.observeReferenceElement(dimension);
        this.render();
    }
    focus() {
        if (!this._modelData || !this._modelData.hasRealView) {
            return;
        }
        this._modelData.view.focus();
    }
    hasTextFocus() {
        if (!this._modelData || !this._modelData.hasRealView) {
            return false;
        }
        return this._modelData.view.isFocused();
    }
    hasWidgetFocus() {
        return this._focusTracker && this._focusTracker.hasFocus();
    }
    addContentWidget(widget) {
        const widgetData = {
            widget: widget,
            position: widget.getPosition()
        };
        if (this._contentWidgets.hasOwnProperty(widget.getId())) {
            console.warn('Overwriting a content widget with the same id.');
        }
        this._contentWidgets[widget.getId()] = widgetData;
        if (this._modelData && this._modelData.hasRealView) {
            this._modelData.view.addContentWidget(widgetData);
        }
    }
    layoutContentWidget(widget) {
        const widgetId = widget.getId();
        if (this._contentWidgets.hasOwnProperty(widgetId)) {
            const widgetData = this._contentWidgets[widgetId];
            widgetData.position = widget.getPosition();
            if (this._modelData && this._modelData.hasRealView) {
                this._modelData.view.layoutContentWidget(widgetData);
            }
        }
    }
    removeContentWidget(widget) {
        const widgetId = widget.getId();
        if (this._contentWidgets.hasOwnProperty(widgetId)) {
            const widgetData = this._contentWidgets[widgetId];
            delete this._contentWidgets[widgetId];
            if (this._modelData && this._modelData.hasRealView) {
                this._modelData.view.removeContentWidget(widgetData);
            }
        }
    }
    addOverlayWidget(widget) {
        const widgetData = {
            widget: widget,
            position: widget.getPosition()
        };
        if (this._overlayWidgets.hasOwnProperty(widget.getId())) {
            console.warn('Overwriting an overlay widget with the same id.');
        }
        this._overlayWidgets[widget.getId()] = widgetData;
        if (this._modelData && this._modelData.hasRealView) {
            this._modelData.view.addOverlayWidget(widgetData);
        }
    }
    layoutOverlayWidget(widget) {
        const widgetId = widget.getId();
        if (this._overlayWidgets.hasOwnProperty(widgetId)) {
            const widgetData = this._overlayWidgets[widgetId];
            widgetData.position = widget.getPosition();
            if (this._modelData && this._modelData.hasRealView) {
                this._modelData.view.layoutOverlayWidget(widgetData);
            }
        }
    }
    removeOverlayWidget(widget) {
        const widgetId = widget.getId();
        if (this._overlayWidgets.hasOwnProperty(widgetId)) {
            const widgetData = this._overlayWidgets[widgetId];
            delete this._overlayWidgets[widgetId];
            if (this._modelData && this._modelData.hasRealView) {
                this._modelData.view.removeOverlayWidget(widgetData);
            }
        }
    }
    changeViewZones(callback) {
        if (!this._modelData || !this._modelData.hasRealView) {
            return;
        }
        this._modelData.view.change(callback);
    }
    getTargetAtClientPoint(clientX, clientY) {
        if (!this._modelData || !this._modelData.hasRealView) {
            return null;
        }
        return this._modelData.view.getTargetAtClientPoint(clientX, clientY);
    }
    getScrolledVisiblePosition(rawPosition) {
        if (!this._modelData || !this._modelData.hasRealView) {
            return null;
        }
        const position = this._modelData.model.validatePosition(rawPosition);
        const options = this._configuration.options;
        const layoutInfo = options.get(124 /* layoutInfo */);
        const top = CodeEditorWidget._getVerticalOffsetForPosition(this._modelData, position.lineNumber, position.column) - this.getScrollTop();
        const left = this._modelData.view.getOffsetForColumn(position.lineNumber, position.column) + layoutInfo.glyphMarginWidth + layoutInfo.lineNumbersWidth + layoutInfo.decorationsWidth - this.getScrollLeft();
        return {
            top: top,
            left: left,
            height: options.get(53 /* lineHeight */)
        };
    }
    getOffsetForColumn(lineNumber, column) {
        if (!this._modelData || !this._modelData.hasRealView) {
            return -1;
        }
        return this._modelData.view.getOffsetForColumn(lineNumber, column);
    }
    render(forceRedraw = false) {
        if (!this._modelData || !this._modelData.hasRealView) {
            return;
        }
        this._modelData.view.render(true, forceRedraw);
    }
    setAriaOptions(options) {
        if (!this._modelData || !this._modelData.hasRealView) {
            return;
        }
        this._modelData.view.setAriaOptions(options);
    }
    applyFontInfo(target) {
        Configuration.applyFontInfoSlow(target, this._configuration.options.get(38 /* fontInfo */));
    }
    _attachModel(model) {
        if (!model) {
            this._modelData = null;
            return;
        }
        const listenersToRemove = [];
        this._domElement.setAttribute('data-mode-id', model.getLanguageIdentifier().language);
        this._configuration.setIsDominatedByLongLines(model.isDominatedByLongLines());
        this._configuration.setMaxLineNumber(model.getLineCount());
        model.onBeforeAttached();
        const viewModel = new ViewModel(this._id, this._configuration, model, DOMLineBreaksComputerFactory.create(), MonospaceLineBreaksComputerFactory.create(this._configuration.options), (callback) => dom.scheduleAtNextAnimationFrame(callback));
        listenersToRemove.push(model.onDidChangeDecorations((e) => this._onDidChangeModelDecorations.fire(e)));
        listenersToRemove.push(model.onDidChangeLanguage((e) => {
            this._domElement.setAttribute('data-mode-id', model.getLanguageIdentifier().language);
            this._onDidChangeModelLanguage.fire(e);
        }));
        listenersToRemove.push(model.onDidChangeLanguageConfiguration((e) => this._onDidChangeModelLanguageConfiguration.fire(e)));
        listenersToRemove.push(model.onDidChangeContent((e) => this._onDidChangeModelContent.fire(e)));
        listenersToRemove.push(model.onDidChangeOptions((e) => this._onDidChangeModelOptions.fire(e)));
        // Someone might destroy the model from under the editor, so prevent any exceptions by setting a null model
        listenersToRemove.push(model.onWillDispose(() => this.setModel(null)));
        listenersToRemove.push(viewModel.onEvent((e) => {
            switch (e.kind) {
                case 0 /* ContentSizeChanged */:
                    this._onDidContentSizeChange.fire(e);
                    break;
                case 1 /* FocusChanged */:
                    this._editorTextFocus.setValue(e.hasFocus);
                    break;
                case 2 /* ScrollChanged */:
                    this._onDidScrollChange.fire(e);
                    break;
                case 3 /* ViewZonesChanged */:
                    this._onDidChangeViewZones.fire();
                    break;
                case 4 /* ReadOnlyEditAttempt */:
                    this._onDidAttemptReadOnlyEdit.fire();
                    break;
                case 5 /* CursorStateChanged */: {
                    if (e.reachedMaxCursorCount) {
                        this._notificationService.warn(nls.localize('cursors.maximum', "The number of cursors has been limited to {0}.", Cursor.MAX_CURSOR_COUNT));
                    }
                    const positions = [];
                    for (let i = 0, len = e.selections.length; i < len; i++) {
                        positions[i] = e.selections[i].getPosition();
                    }
                    const e1 = {
                        position: positions[0],
                        secondaryPositions: positions.slice(1),
                        reason: e.reason,
                        source: e.source
                    };
                    this._onDidChangeCursorPosition.fire(e1);
                    const e2 = {
                        selection: e.selections[0],
                        secondarySelections: e.selections.slice(1),
                        modelVersionId: e.modelVersionId,
                        oldSelections: e.oldSelections,
                        oldModelVersionId: e.oldModelVersionId,
                        source: e.source,
                        reason: e.reason
                    };
                    this._onDidChangeCursorSelection.fire(e2);
                    break;
                }
            }
        }));
        const [view, hasRealView] = this._createView(viewModel);
        if (hasRealView) {
            this._domElement.appendChild(view.domNode.domNode);
            let keys = Object.keys(this._contentWidgets);
            for (let i = 0, len = keys.length; i < len; i++) {
                const widgetId = keys[i];
                view.addContentWidget(this._contentWidgets[widgetId]);
            }
            keys = Object.keys(this._overlayWidgets);
            for (let i = 0, len = keys.length; i < len; i++) {
                const widgetId = keys[i];
                view.addOverlayWidget(this._overlayWidgets[widgetId]);
            }
            view.render(false, true);
            view.domNode.domNode.setAttribute('data-uri', model.uri.toString());
        }
        this._modelData = new ModelData(model, viewModel, view, hasRealView, listenersToRemove);
    }
    _createView(viewModel) {
        let commandDelegate;
        if (this.isSimpleWidget) {
            commandDelegate = {
                paste: (text, pasteOnNewLine, multicursorText, mode) => {
                    this._paste('keyboard', text, pasteOnNewLine, multicursorText, mode);
                },
                type: (text) => {
                    this._type('keyboard', text);
                },
                replacePreviousChar: (text, replaceCharCnt) => {
                    this._replacePreviousChar('keyboard', text, replaceCharCnt);
                },
                startComposition: () => {
                    this._startComposition();
                },
                endComposition: () => {
                    this._endComposition('keyboard');
                },
                cut: () => {
                    this._cut('keyboard');
                }
            };
        }
        else {
            commandDelegate = {
                paste: (text, pasteOnNewLine, multicursorText, mode) => {
                    const payload = { text, pasteOnNewLine, multicursorText, mode };
                    this._commandService.executeCommand("paste" /* Paste */, payload);
                },
                type: (text) => {
                    const payload = { text };
                    this._commandService.executeCommand("type" /* Type */, payload);
                },
                replacePreviousChar: (text, replaceCharCnt) => {
                    const payload = { text, replaceCharCnt };
                    this._commandService.executeCommand("replacePreviousChar" /* ReplacePreviousChar */, payload);
                },
                startComposition: () => {
                    this._commandService.executeCommand("compositionStart" /* CompositionStart */, {});
                },
                endComposition: () => {
                    this._commandService.executeCommand("compositionEnd" /* CompositionEnd */, {});
                },
                cut: () => {
                    this._commandService.executeCommand("cut" /* Cut */, {});
                }
            };
        }
        const viewUserInputEvents = new ViewUserInputEvents(viewModel.coordinatesConverter);
        viewUserInputEvents.onKeyDown = (e) => this._onKeyDown.fire(e);
        viewUserInputEvents.onKeyUp = (e) => this._onKeyUp.fire(e);
        viewUserInputEvents.onContextMenu = (e) => this._onContextMenu.fire(e);
        viewUserInputEvents.onMouseMove = (e) => this._onMouseMove.fire(e);
        viewUserInputEvents.onMouseLeave = (e) => this._onMouseLeave.fire(e);
        viewUserInputEvents.onMouseDown = (e) => this._onMouseDown.fire(e);
        viewUserInputEvents.onMouseUp = (e) => this._onMouseUp.fire(e);
        viewUserInputEvents.onMouseDrag = (e) => this._onMouseDrag.fire(e);
        viewUserInputEvents.onMouseDrop = (e) => this._onMouseDrop.fire(e);
        viewUserInputEvents.onMouseDropCanceled = (e) => this._onMouseDropCanceled.fire(e);
        viewUserInputEvents.onMouseWheel = (e) => this._onMouseWheel.fire(e);
        const view = new View(commandDelegate, this._configuration, this._themeService, viewModel, viewUserInputEvents, this._overflowWidgetsDomNode);
        return [view, true];
    }
    _postDetachModelCleanup(detachedModel) {
        if (detachedModel) {
            detachedModel.removeAllDecorationsWithOwnerId(this._id);
        }
    }
    _detachModel() {
        if (!this._modelData) {
            return null;
        }
        const model = this._modelData.model;
        const removeDomNode = this._modelData.hasRealView ? this._modelData.view.domNode.domNode : null;
        this._modelData.dispose();
        this._modelData = null;
        this._domElement.removeAttribute('data-mode-id');
        if (removeDomNode && this._domElement.contains(removeDomNode)) {
            this._domElement.removeChild(removeDomNode);
        }
        return model;
    }
    _removeDecorationType(key) {
        this._codeEditorService.removeDecorationType(key);
    }
    hasModel() {
        return (this._modelData !== null);
    }
};
CodeEditorWidget = __decorate([
    __param(3, IInstantiationService),
    __param(4, ICodeEditorService),
    __param(5, ICommandService),
    __param(6, IContextKeyService),
    __param(7, IThemeService),
    __param(8, INotificationService),
    __param(9, IAccessibilityService)
], CodeEditorWidget);
export { CodeEditorWidget };
export class BooleanEventEmitter extends Disposable {
    constructor() {
        super();
        this._onDidChangeToTrue = this._register(new Emitter());
        this.onDidChangeToTrue = this._onDidChangeToTrue.event;
        this._onDidChangeToFalse = this._register(new Emitter());
        this.onDidChangeToFalse = this._onDidChangeToFalse.event;
        this._value = 0 /* NotSet */;
    }
    setValue(_value) {
        const value = (_value ? 2 /* True */ : 1 /* False */);
        if (this._value === value) {
            return;
        }
        this._value = value;
        if (this._value === 2 /* True */) {
            this._onDidChangeToTrue.fire();
        }
        else if (this._value === 1 /* False */) {
            this._onDidChangeToFalse.fire();
        }
    }
}
class EditorContextKeysManager extends Disposable {
    constructor(editor, contextKeyService) {
        super();
        this._editor = editor;
        contextKeyService.createKey('editorId', editor.getId());
        this._editorSimpleInput = EditorContextKeys.editorSimpleInput.bindTo(contextKeyService);
        this._editorFocus = EditorContextKeys.focus.bindTo(contextKeyService);
        this._textInputFocus = EditorContextKeys.textInputFocus.bindTo(contextKeyService);
        this._editorTextFocus = EditorContextKeys.editorTextFocus.bindTo(contextKeyService);
        this._editorTabMovesFocus = EditorContextKeys.tabMovesFocus.bindTo(contextKeyService);
        this._editorReadonly = EditorContextKeys.readOnly.bindTo(contextKeyService);
        this._inDiffEditor = EditorContextKeys.inDiffEditor.bindTo(contextKeyService);
        this._editorColumnSelection = EditorContextKeys.columnSelection.bindTo(contextKeyService);
        this._hasMultipleSelections = EditorContextKeys.hasMultipleSelections.bindTo(contextKeyService);
        this._hasNonEmptySelection = EditorContextKeys.hasNonEmptySelection.bindTo(contextKeyService);
        this._canUndo = EditorContextKeys.canUndo.bindTo(contextKeyService);
        this._canRedo = EditorContextKeys.canRedo.bindTo(contextKeyService);
        this._register(this._editor.onDidChangeConfiguration(() => this._updateFromConfig()));
        this._register(this._editor.onDidChangeCursorSelection(() => this._updateFromSelection()));
        this._register(this._editor.onDidFocusEditorWidget(() => this._updateFromFocus()));
        this._register(this._editor.onDidBlurEditorWidget(() => this._updateFromFocus()));
        this._register(this._editor.onDidFocusEditorText(() => this._updateFromFocus()));
        this._register(this._editor.onDidBlurEditorText(() => this._updateFromFocus()));
        this._register(this._editor.onDidChangeModel(() => this._updateFromModel()));
        this._register(this._editor.onDidChangeConfiguration(() => this._updateFromModel()));
        this._updateFromConfig();
        this._updateFromSelection();
        this._updateFromFocus();
        this._updateFromModel();
        this._editorSimpleInput.set(this._editor.isSimpleWidget);
    }
    _updateFromConfig() {
        const options = this._editor.getOptions();
        this._editorTabMovesFocus.set(options.get(123 /* tabFocusMode */));
        this._editorReadonly.set(options.get(75 /* readOnly */));
        this._inDiffEditor.set(options.get(49 /* inDiffEditor */));
        this._editorColumnSelection.set(options.get(15 /* columnSelection */));
    }
    _updateFromSelection() {
        const selections = this._editor.getSelections();
        if (!selections) {
            this._hasMultipleSelections.reset();
            this._hasNonEmptySelection.reset();
        }
        else {
            this._hasMultipleSelections.set(selections.length > 1);
            this._hasNonEmptySelection.set(selections.some(s => !s.isEmpty()));
        }
    }
    _updateFromFocus() {
        this._editorFocus.set(this._editor.hasWidgetFocus() && !this._editor.isSimpleWidget);
        this._editorTextFocus.set(this._editor.hasTextFocus() && !this._editor.isSimpleWidget);
        this._textInputFocus.set(this._editor.hasTextFocus());
    }
    _updateFromModel() {
        const model = this._editor.getModel();
        this._canUndo.set(Boolean(model && model.canUndo()));
        this._canRedo.set(Boolean(model && model.canRedo()));
    }
}
export class EditorModeContext extends Disposable {
    constructor(_editor, _contextKeyService) {
        super();
        this._editor = _editor;
        this._contextKeyService = _contextKeyService;
        this._langId = EditorContextKeys.languageId.bindTo(_contextKeyService);
        this._hasCompletionItemProvider = EditorContextKeys.hasCompletionItemProvider.bindTo(_contextKeyService);
        this._hasCodeActionsProvider = EditorContextKeys.hasCodeActionsProvider.bindTo(_contextKeyService);
        this._hasCodeLensProvider = EditorContextKeys.hasCodeLensProvider.bindTo(_contextKeyService);
        this._hasDefinitionProvider = EditorContextKeys.hasDefinitionProvider.bindTo(_contextKeyService);
        this._hasDeclarationProvider = EditorContextKeys.hasDeclarationProvider.bindTo(_contextKeyService);
        this._hasImplementationProvider = EditorContextKeys.hasImplementationProvider.bindTo(_contextKeyService);
        this._hasTypeDefinitionProvider = EditorContextKeys.hasTypeDefinitionProvider.bindTo(_contextKeyService);
        this._hasHoverProvider = EditorContextKeys.hasHoverProvider.bindTo(_contextKeyService);
        this._hasDocumentHighlightProvider = EditorContextKeys.hasDocumentHighlightProvider.bindTo(_contextKeyService);
        this._hasDocumentSymbolProvider = EditorContextKeys.hasDocumentSymbolProvider.bindTo(_contextKeyService);
        this._hasReferenceProvider = EditorContextKeys.hasReferenceProvider.bindTo(_contextKeyService);
        this._hasRenameProvider = EditorContextKeys.hasRenameProvider.bindTo(_contextKeyService);
        this._hasSignatureHelpProvider = EditorContextKeys.hasSignatureHelpProvider.bindTo(_contextKeyService);
        this._hasInlineHintsProvider = EditorContextKeys.hasInlineHintsProvider.bindTo(_contextKeyService);
        this._hasDocumentFormattingProvider = EditorContextKeys.hasDocumentFormattingProvider.bindTo(_contextKeyService);
        this._hasDocumentSelectionFormattingProvider = EditorContextKeys.hasDocumentSelectionFormattingProvider.bindTo(_contextKeyService);
        this._hasMultipleDocumentFormattingProvider = EditorContextKeys.hasMultipleDocumentFormattingProvider.bindTo(_contextKeyService);
        this._hasMultipleDocumentSelectionFormattingProvider = EditorContextKeys.hasMultipleDocumentSelectionFormattingProvider.bindTo(_contextKeyService);
        this._isInWalkThrough = EditorContextKeys.isInWalkThroughSnippet.bindTo(_contextKeyService);
        const update = () => this._update();
        // update when model/mode changes
        this._register(_editor.onDidChangeModel(update));
        this._register(_editor.onDidChangeModelLanguage(update));
        // update when registries change
        this._register(modes.CompletionProviderRegistry.onDidChange(update));
        this._register(modes.CodeActionProviderRegistry.onDidChange(update));
        this._register(modes.CodeLensProviderRegistry.onDidChange(update));
        this._register(modes.DefinitionProviderRegistry.onDidChange(update));
        this._register(modes.DeclarationProviderRegistry.onDidChange(update));
        this._register(modes.ImplementationProviderRegistry.onDidChange(update));
        this._register(modes.TypeDefinitionProviderRegistry.onDidChange(update));
        this._register(modes.HoverProviderRegistry.onDidChange(update));
        this._register(modes.DocumentHighlightProviderRegistry.onDidChange(update));
        this._register(modes.DocumentSymbolProviderRegistry.onDidChange(update));
        this._register(modes.ReferenceProviderRegistry.onDidChange(update));
        this._register(modes.RenameProviderRegistry.onDidChange(update));
        this._register(modes.DocumentFormattingEditProviderRegistry.onDidChange(update));
        this._register(modes.DocumentRangeFormattingEditProviderRegistry.onDidChange(update));
        this._register(modes.SignatureHelpProviderRegistry.onDidChange(update));
        this._register(modes.InlineHintsProviderRegistry.onDidChange(update));
        update();
    }
    dispose() {
        super.dispose();
    }
    reset() {
        this._contextKeyService.bufferChangeEvents(() => {
            this._langId.reset();
            this._hasCompletionItemProvider.reset();
            this._hasCodeActionsProvider.reset();
            this._hasCodeLensProvider.reset();
            this._hasDefinitionProvider.reset();
            this._hasDeclarationProvider.reset();
            this._hasImplementationProvider.reset();
            this._hasTypeDefinitionProvider.reset();
            this._hasHoverProvider.reset();
            this._hasDocumentHighlightProvider.reset();
            this._hasDocumentSymbolProvider.reset();
            this._hasReferenceProvider.reset();
            this._hasRenameProvider.reset();
            this._hasDocumentFormattingProvider.reset();
            this._hasDocumentSelectionFormattingProvider.reset();
            this._hasSignatureHelpProvider.reset();
            this._isInWalkThrough.reset();
        });
    }
    _update() {
        const model = this._editor.getModel();
        if (!model) {
            this.reset();
            return;
        }
        this._contextKeyService.bufferChangeEvents(() => {
            this._langId.set(model.getLanguageIdentifier().language);
            this._hasCompletionItemProvider.set(modes.CompletionProviderRegistry.has(model));
            this._hasCodeActionsProvider.set(modes.CodeActionProviderRegistry.has(model));
            this._hasCodeLensProvider.set(modes.CodeLensProviderRegistry.has(model));
            this._hasDefinitionProvider.set(modes.DefinitionProviderRegistry.has(model));
            this._hasDeclarationProvider.set(modes.DeclarationProviderRegistry.has(model));
            this._hasImplementationProvider.set(modes.ImplementationProviderRegistry.has(model));
            this._hasTypeDefinitionProvider.set(modes.TypeDefinitionProviderRegistry.has(model));
            this._hasHoverProvider.set(modes.HoverProviderRegistry.has(model));
            this._hasDocumentHighlightProvider.set(modes.DocumentHighlightProviderRegistry.has(model));
            this._hasDocumentSymbolProvider.set(modes.DocumentSymbolProviderRegistry.has(model));
            this._hasReferenceProvider.set(modes.ReferenceProviderRegistry.has(model));
            this._hasRenameProvider.set(modes.RenameProviderRegistry.has(model));
            this._hasSignatureHelpProvider.set(modes.SignatureHelpProviderRegistry.has(model));
            this._hasInlineHintsProvider.set(modes.InlineHintsProviderRegistry.has(model));
            this._hasDocumentFormattingProvider.set(modes.DocumentFormattingEditProviderRegistry.has(model) || modes.DocumentRangeFormattingEditProviderRegistry.has(model));
            this._hasDocumentSelectionFormattingProvider.set(modes.DocumentRangeFormattingEditProviderRegistry.has(model));
            this._hasMultipleDocumentFormattingProvider.set(modes.DocumentFormattingEditProviderRegistry.all(model).length + modes.DocumentRangeFormattingEditProviderRegistry.all(model).length > 1);
            this._hasMultipleDocumentSelectionFormattingProvider.set(modes.DocumentRangeFormattingEditProviderRegistry.all(model).length > 1);
            this._isInWalkThrough.set(model.uri.scheme === Schemas.walkThroughSnippet);
        });
    }
}
class CodeEditorWidgetFocusTracker extends Disposable {
    constructor(domElement) {
        super();
        this._onChange = this._register(new Emitter());
        this.onChange = this._onChange.event;
        this._hasFocus = false;
        this._domFocusTracker = this._register(dom.trackFocus(domElement));
        this._register(this._domFocusTracker.onDidFocus(() => {
            this._hasFocus = true;
            this._onChange.fire(undefined);
        }));
        this._register(this._domFocusTracker.onDidBlur(() => {
            this._hasFocus = false;
            this._onChange.fire(undefined);
        }));
    }
    hasFocus() {
        return this._hasFocus;
    }
}
const squigglyStart = encodeURIComponent(`<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 6 3' enable-background='new 0 0 6 3' height='3' width='6'><g fill='`);
const squigglyEnd = encodeURIComponent(`'><polygon points='5.5,0 2.5,3 1.1,3 4.1,0'/><polygon points='4,0 6,2 6,0.6 5.4,0'/><polygon points='0,2 1,3 2.4,3 0,0.6'/></g></svg>`);
function getSquigglySVGData(color) {
    return squigglyStart + encodeURIComponent(color.toString()) + squigglyEnd;
}
const dotdotdotStart = encodeURIComponent(`<svg xmlns="http://www.w3.org/2000/svg" height="3" width="12"><g fill="`);
const dotdotdotEnd = encodeURIComponent(`"><circle cx="1" cy="1" r="1"/><circle cx="5" cy="1" r="1"/><circle cx="9" cy="1" r="1"/></g></svg>`);
function getDotDotDotSVGData(color) {
    return dotdotdotStart + encodeURIComponent(color.toString()) + dotdotdotEnd;
}
registerThemingParticipant((theme, collector) => {
    const errorBorderColor = theme.getColor(editorErrorBorder);
    if (errorBorderColor) {
        collector.addRule(`.monaco-editor .${"squiggly-error" /* EditorErrorDecoration */} { border-bottom: 4px double ${errorBorderColor}; }`);
    }
    const errorForeground = theme.getColor(editorErrorForeground);
    if (errorForeground) {
        collector.addRule(`.monaco-editor .${"squiggly-error" /* EditorErrorDecoration */} { background: url("data:image/svg+xml,${getSquigglySVGData(errorForeground)}") repeat-x bottom left; }`);
    }
    const errorBackground = theme.getColor(editorErrorBackground);
    if (errorBackground) {
        collector.addRule(`.monaco-editor .${"squiggly-error" /* EditorErrorDecoration */}::before { display: block; content: ''; width: 100%; height: 100%; background: ${errorBackground}; }`);
    }
    const warningBorderColor = theme.getColor(editorWarningBorder);
    if (warningBorderColor) {
        collector.addRule(`.monaco-editor .${"squiggly-warning" /* EditorWarningDecoration */} { border-bottom: 4px double ${warningBorderColor}; }`);
    }
    const warningForeground = theme.getColor(editorWarningForeground);
    if (warningForeground) {
        collector.addRule(`.monaco-editor .${"squiggly-warning" /* EditorWarningDecoration */} { background: url("data:image/svg+xml,${getSquigglySVGData(warningForeground)}") repeat-x bottom left; }`);
    }
    const warningBackground = theme.getColor(editorWarningBackground);
    if (warningBackground) {
        collector.addRule(`.monaco-editor .${"squiggly-warning" /* EditorWarningDecoration */}::before { display: block; content: ''; width: 100%; height: 100%; background: ${warningBackground}; }`);
    }
    const infoBorderColor = theme.getColor(editorInfoBorder);
    if (infoBorderColor) {
        collector.addRule(`.monaco-editor .${"squiggly-info" /* EditorInfoDecoration */} { border-bottom: 4px double ${infoBorderColor}; }`);
    }
    const infoForeground = theme.getColor(editorInfoForeground);
    if (infoForeground) {
        collector.addRule(`.monaco-editor .${"squiggly-info" /* EditorInfoDecoration */} { background: url("data:image/svg+xml,${getSquigglySVGData(infoForeground)}") repeat-x bottom left; }`);
    }
    const infoBackground = theme.getColor(editorInfoBackground);
    if (infoBackground) {
        collector.addRule(`.monaco-editor .${"squiggly-info" /* EditorInfoDecoration */}::before { display: block; content: ''; width: 100%; height: 100%; background: ${infoBackground}; }`);
    }
    const hintBorderColor = theme.getColor(editorHintBorder);
    if (hintBorderColor) {
        collector.addRule(`.monaco-editor .${"squiggly-hint" /* EditorHintDecoration */} { border-bottom: 2px dotted ${hintBorderColor}; }`);
    }
    const hintForeground = theme.getColor(editorHintForeground);
    if (hintForeground) {
        collector.addRule(`.monaco-editor .${"squiggly-hint" /* EditorHintDecoration */} { background: url("data:image/svg+xml,${getDotDotDotSVGData(hintForeground)}") no-repeat bottom left; }`);
    }
    const unnecessaryForeground = theme.getColor(editorUnnecessaryCodeOpacity);
    if (unnecessaryForeground) {
        collector.addRule(`.monaco-editor.showUnused .${"squiggly-inline-unnecessary" /* EditorUnnecessaryInlineDecoration */} { opacity: ${unnecessaryForeground.rgba.a}; }`);
    }
    const unnecessaryBorder = theme.getColor(editorUnnecessaryCodeBorder);
    if (unnecessaryBorder) {
        collector.addRule(`.monaco-editor.showUnused .${"squiggly-unnecessary" /* EditorUnnecessaryDecoration */} { border-bottom: 2px dashed ${unnecessaryBorder}; }`);
    }
    const deprecatedForeground = theme.getColor(editorForeground) || 'inherit';
    collector.addRule(`.monaco-editor.showDeprecated .${"squiggly-inline-deprecated" /* EditorDeprecatedInlineDecoration */} { text-decoration: line-through; text-decoration-color: ${deprecatedForeground}}`);
});
