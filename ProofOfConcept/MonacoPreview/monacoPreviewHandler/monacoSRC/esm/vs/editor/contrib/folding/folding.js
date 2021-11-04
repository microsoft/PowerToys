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
import './folding.css';
import * as nls from '../../../nls.js';
import * as types from '../../../base/common/types.js';
import { escapeRegExpCharacters } from '../../../base/common/strings.js';
import { RunOnceScheduler, Delayer, createCancelablePromise } from '../../../base/common/async.js';
import { KeyChord } from '../../../base/common/keyCodes.js';
import { Disposable, DisposableStore } from '../../../base/common/lifecycle.js';
import { registerEditorAction, registerEditorContribution, EditorAction, registerInstantiatedEditorAction } from '../../browser/editorExtensions.js';
import { FoldingModel, setCollapseStateAtLevel, setCollapseStateLevelsDown, setCollapseStateLevelsUp, setCollapseStateForMatchingLines, setCollapseStateForType, toggleCollapseState, setCollapseStateUp } from './foldingModel.js';
import { FoldingDecorationProvider, foldingCollapsedIcon, foldingExpandedIcon } from './foldingDecorations.js';
import { EditorContextKeys } from '../../common/editorContextKeys.js';
import { HiddenRangeModel } from './hiddenRangeModel.js';
import { LanguageConfigurationRegistry } from '../../common/modes/languageConfigurationRegistry.js';
import { IndentRangeProvider } from './indentRangeProvider.js';
import { FoldingRangeProviderRegistry, FoldingRangeKind } from '../../common/modes.js';
import { SyntaxRangeProvider, ID_SYNTAX_PROVIDER } from './syntaxRangeProvider.js';
import { InitializingRangeProvider, ID_INIT_PROVIDER } from './intializingRangeProvider.js';
import { onUnexpectedError } from '../../../base/common/errors.js';
import { RawContextKey, IContextKeyService } from '../../../platform/contextkey/common/contextkey.js';
import { registerThemingParticipant, ThemeIcon } from '../../../platform/theme/common/themeService.js';
import { registerColor, editorSelectionBackground, transparent, iconForeground } from '../../../platform/theme/common/colorRegistry.js';
const CONTEXT_FOLDING_ENABLED = new RawContextKey('foldingEnabled', false);
let FoldingController = class FoldingController extends Disposable {
    constructor(editor, contextKeyService) {
        super();
        this.contextKeyService = contextKeyService;
        this.localToDispose = this._register(new DisposableStore());
        this.editor = editor;
        const options = this.editor.getOptions();
        this._isEnabled = options.get(33 /* folding */);
        this._useFoldingProviders = options.get(34 /* foldingStrategy */) !== 'indentation';
        this._unfoldOnClickAfterEndOfLine = options.get(36 /* unfoldOnClickAfterEndOfLine */);
        this._restoringViewState = false;
        this.foldingModel = null;
        this.hiddenRangeModel = null;
        this.rangeProvider = null;
        this.foldingRegionPromise = null;
        this.foldingStateMemento = null;
        this.foldingModelPromise = null;
        this.updateScheduler = null;
        this.cursorChangedScheduler = null;
        this.mouseDownInfo = null;
        this.foldingDecorationProvider = new FoldingDecorationProvider(editor);
        this.foldingDecorationProvider.autoHideFoldingControls = options.get(94 /* showFoldingControls */) === 'mouseover';
        this.foldingDecorationProvider.showFoldingHighlights = options.get(35 /* foldingHighlight */);
        this.foldingEnabled = CONTEXT_FOLDING_ENABLED.bindTo(this.contextKeyService);
        this.foldingEnabled.set(this._isEnabled);
        this._register(this.editor.onDidChangeModel(() => this.onModelChanged()));
        this._register(this.editor.onDidChangeConfiguration((e) => {
            if (e.hasChanged(33 /* folding */)) {
                this._isEnabled = this.editor.getOptions().get(33 /* folding */);
                this.foldingEnabled.set(this._isEnabled);
                this.onModelChanged();
            }
            if (e.hasChanged(94 /* showFoldingControls */) || e.hasChanged(35 /* foldingHighlight */)) {
                const options = this.editor.getOptions();
                this.foldingDecorationProvider.autoHideFoldingControls = options.get(94 /* showFoldingControls */) === 'mouseover';
                this.foldingDecorationProvider.showFoldingHighlights = options.get(35 /* foldingHighlight */);
                this.onModelContentChanged();
            }
            if (e.hasChanged(34 /* foldingStrategy */)) {
                this._useFoldingProviders = this.editor.getOptions().get(34 /* foldingStrategy */) !== 'indentation';
                this.onFoldingStrategyChanged();
            }
            if (e.hasChanged(36 /* unfoldOnClickAfterEndOfLine */)) {
                this._unfoldOnClickAfterEndOfLine = this.editor.getOptions().get(36 /* unfoldOnClickAfterEndOfLine */);
            }
        }));
        this.onModelChanged();
    }
    static get(editor) {
        return editor.getContribution(FoldingController.ID);
    }
    /**
     * Store view state.
     */
    saveViewState() {
        let model = this.editor.getModel();
        if (!model || !this._isEnabled || model.isTooLargeForTokenization()) {
            return {};
        }
        if (this.foldingModel) { // disposed ?
            let collapsedRegions = this.foldingModel.isInitialized ? this.foldingModel.getMemento() : this.hiddenRangeModel.getMemento();
            let provider = this.rangeProvider ? this.rangeProvider.id : undefined;
            return { collapsedRegions, lineCount: model.getLineCount(), provider };
        }
        return undefined;
    }
    /**
     * Restore view state.
     */
    restoreViewState(state) {
        let model = this.editor.getModel();
        if (!model || !this._isEnabled || model.isTooLargeForTokenization() || !this.hiddenRangeModel) {
            return;
        }
        if (!state || !state.collapsedRegions || state.lineCount !== model.getLineCount()) {
            return;
        }
        if (state.provider === ID_SYNTAX_PROVIDER || state.provider === ID_INIT_PROVIDER) {
            this.foldingStateMemento = state;
        }
        const collapsedRegions = state.collapsedRegions;
        // set the hidden ranges right away, before waiting for the folding model.
        if (this.hiddenRangeModel.applyMemento(collapsedRegions)) {
            const foldingModel = this.getFoldingModel();
            if (foldingModel) {
                foldingModel.then(foldingModel => {
                    if (foldingModel) {
                        this._restoringViewState = true;
                        try {
                            foldingModel.applyMemento(collapsedRegions);
                        }
                        finally {
                            this._restoringViewState = false;
                        }
                    }
                }).then(undefined, onUnexpectedError);
            }
        }
    }
    onModelChanged() {
        this.localToDispose.clear();
        let model = this.editor.getModel();
        if (!this._isEnabled || !model || model.isTooLargeForTokenization()) {
            // huge files get no view model, so they cannot support hidden areas
            return;
        }
        this.foldingModel = new FoldingModel(model, this.foldingDecorationProvider);
        this.localToDispose.add(this.foldingModel);
        this.hiddenRangeModel = new HiddenRangeModel(this.foldingModel);
        this.localToDispose.add(this.hiddenRangeModel);
        this.localToDispose.add(this.hiddenRangeModel.onDidChange(hr => this.onHiddenRangesChanges(hr)));
        this.updateScheduler = new Delayer(200);
        this.cursorChangedScheduler = new RunOnceScheduler(() => this.revealCursor(), 200);
        this.localToDispose.add(this.cursorChangedScheduler);
        this.localToDispose.add(FoldingRangeProviderRegistry.onDidChange(() => this.onFoldingStrategyChanged()));
        this.localToDispose.add(this.editor.onDidChangeModelLanguageConfiguration(() => this.onFoldingStrategyChanged())); // covers model language changes as well
        this.localToDispose.add(this.editor.onDidChangeModelContent(() => this.onModelContentChanged()));
        this.localToDispose.add(this.editor.onDidChangeCursorPosition(() => this.onCursorPositionChanged()));
        this.localToDispose.add(this.editor.onMouseDown(e => this.onEditorMouseDown(e)));
        this.localToDispose.add(this.editor.onMouseUp(e => this.onEditorMouseUp(e)));
        this.localToDispose.add({
            dispose: () => {
                if (this.foldingRegionPromise) {
                    this.foldingRegionPromise.cancel();
                    this.foldingRegionPromise = null;
                }
                if (this.updateScheduler) {
                    this.updateScheduler.cancel();
                }
                this.updateScheduler = null;
                this.foldingModel = null;
                this.foldingModelPromise = null;
                this.hiddenRangeModel = null;
                this.cursorChangedScheduler = null;
                this.foldingStateMemento = null;
                if (this.rangeProvider) {
                    this.rangeProvider.dispose();
                }
                this.rangeProvider = null;
            }
        });
        this.onModelContentChanged();
    }
    onFoldingStrategyChanged() {
        if (this.rangeProvider) {
            this.rangeProvider.dispose();
        }
        this.rangeProvider = null;
        this.onModelContentChanged();
    }
    getRangeProvider(editorModel) {
        if (this.rangeProvider) {
            return this.rangeProvider;
        }
        this.rangeProvider = new IndentRangeProvider(editorModel); // fallback
        if (this._useFoldingProviders && this.foldingModel) {
            let foldingProviders = FoldingRangeProviderRegistry.ordered(this.foldingModel.textModel);
            if (foldingProviders.length === 0 && this.foldingStateMemento && this.foldingStateMemento.collapsedRegions) {
                const rangeProvider = this.rangeProvider = new InitializingRangeProvider(editorModel, this.foldingStateMemento.collapsedRegions, () => {
                    // if after 30 the InitializingRangeProvider is still not replaced, force a refresh
                    this.foldingStateMemento = null;
                    this.onFoldingStrategyChanged();
                }, 30000);
                return rangeProvider; // keep memento in case there are still no foldingProviders on the next request.
            }
            else if (foldingProviders.length > 0) {
                this.rangeProvider = new SyntaxRangeProvider(editorModel, foldingProviders, () => this.onModelContentChanged());
            }
        }
        this.foldingStateMemento = null;
        return this.rangeProvider;
    }
    getFoldingModel() {
        return this.foldingModelPromise;
    }
    onModelContentChanged() {
        if (this.updateScheduler) {
            if (this.foldingRegionPromise) {
                this.foldingRegionPromise.cancel();
                this.foldingRegionPromise = null;
            }
            this.foldingModelPromise = this.updateScheduler.trigger(() => {
                const foldingModel = this.foldingModel;
                if (!foldingModel) { // null if editor has been disposed, or folding turned off
                    return null;
                }
                let foldingRegionPromise = this.foldingRegionPromise = createCancelablePromise(token => this.getRangeProvider(foldingModel.textModel).compute(token));
                return foldingRegionPromise.then(foldingRanges => {
                    if (foldingRanges && foldingRegionPromise === this.foldingRegionPromise) { // new request or cancelled in the meantime?
                        // some cursors might have moved into hidden regions, make sure they are in expanded regions
                        let selections = this.editor.getSelections();
                        let selectionLineNumbers = selections ? selections.map(s => s.startLineNumber) : [];
                        foldingModel.update(foldingRanges, selectionLineNumbers);
                    }
                    return foldingModel;
                });
            }).then(undefined, (err) => {
                onUnexpectedError(err);
                return null;
            });
        }
    }
    onHiddenRangesChanges(hiddenRanges) {
        if (this.hiddenRangeModel && hiddenRanges.length && !this._restoringViewState) {
            let selections = this.editor.getSelections();
            if (selections) {
                if (this.hiddenRangeModel.adjustSelections(selections)) {
                    this.editor.setSelections(selections);
                }
            }
        }
        this.editor.setHiddenAreas(hiddenRanges);
    }
    onCursorPositionChanged() {
        if (this.hiddenRangeModel && this.hiddenRangeModel.hasRanges()) {
            this.cursorChangedScheduler.schedule();
        }
    }
    revealCursor() {
        const foldingModel = this.getFoldingModel();
        if (!foldingModel) {
            return;
        }
        foldingModel.then(foldingModel => {
            if (foldingModel) {
                let selections = this.editor.getSelections();
                if (selections && selections.length > 0) {
                    let toToggle = [];
                    for (let selection of selections) {
                        let lineNumber = selection.selectionStartLineNumber;
                        if (this.hiddenRangeModel && this.hiddenRangeModel.isHidden(lineNumber)) {
                            toToggle.push(...foldingModel.getAllRegionsAtLine(lineNumber, r => r.isCollapsed && lineNumber > r.startLineNumber));
                        }
                    }
                    if (toToggle.length) {
                        foldingModel.toggleCollapseState(toToggle);
                        this.reveal(selections[0].getPosition());
                    }
                }
            }
        }).then(undefined, onUnexpectedError);
    }
    onEditorMouseDown(e) {
        this.mouseDownInfo = null;
        if (!this.hiddenRangeModel || !e.target || !e.target.range) {
            return;
        }
        if (!e.event.leftButton && !e.event.middleButton) {
            return;
        }
        const range = e.target.range;
        let iconClicked = false;
        switch (e.target.type) {
            case 4 /* GUTTER_LINE_DECORATIONS */:
                const data = e.target.detail;
                const offsetLeftInGutter = e.target.element.offsetLeft;
                const gutterOffsetX = data.offsetX - offsetLeftInGutter;
                // const gutterOffsetX = data.offsetX - data.glyphMarginWidth - data.lineNumbersWidth - data.glyphMarginLeft;
                // TODO@joao TODO@alex TODO@martin this is such that we don't collide with dirty diff
                if (gutterOffsetX < 5) { // the whitespace between the border and the real folding icon border is 5px
                    return;
                }
                iconClicked = true;
                break;
            case 7 /* CONTENT_EMPTY */: {
                if (this._unfoldOnClickAfterEndOfLine && this.hiddenRangeModel.hasRanges()) {
                    const data = e.target.detail;
                    if (!data.isAfterLines) {
                        break;
                    }
                }
                return;
            }
            case 6 /* CONTENT_TEXT */: {
                if (this.hiddenRangeModel.hasRanges()) {
                    let model = this.editor.getModel();
                    if (model && range.startColumn === model.getLineMaxColumn(range.startLineNumber)) {
                        break;
                    }
                }
                return;
            }
            default:
                return;
        }
        this.mouseDownInfo = { lineNumber: range.startLineNumber, iconClicked };
    }
    onEditorMouseUp(e) {
        const foldingModel = this.getFoldingModel();
        if (!foldingModel || !this.mouseDownInfo || !e.target) {
            return;
        }
        let lineNumber = this.mouseDownInfo.lineNumber;
        let iconClicked = this.mouseDownInfo.iconClicked;
        let range = e.target.range;
        if (!range || range.startLineNumber !== lineNumber) {
            return;
        }
        if (iconClicked) {
            if (e.target.type !== 4 /* GUTTER_LINE_DECORATIONS */) {
                return;
            }
        }
        else {
            let model = this.editor.getModel();
            if (!model || range.startColumn !== model.getLineMaxColumn(lineNumber)) {
                return;
            }
        }
        foldingModel.then(foldingModel => {
            if (foldingModel) {
                let region = foldingModel.getRegionAtLine(lineNumber);
                if (region && region.startLineNumber === lineNumber) {
                    let isCollapsed = region.isCollapsed;
                    if (iconClicked || isCollapsed) {
                        let toToggle = [];
                        let recursive = e.event.middleButton || e.event.shiftKey;
                        if (recursive) {
                            for (const r of foldingModel.getRegionsInside(region)) {
                                if (r.isCollapsed === isCollapsed) {
                                    toToggle.push(r);
                                }
                            }
                        }
                        // when recursive, first only collapse all children. If all are already folded or there are no children, also fold parent.
                        if (isCollapsed || !recursive || toToggle.length === 0) {
                            toToggle.push(region);
                        }
                        foldingModel.toggleCollapseState(toToggle);
                        this.reveal({ lineNumber, column: 1 });
                    }
                }
            }
        }).then(undefined, onUnexpectedError);
    }
    reveal(position) {
        this.editor.revealPositionInCenterIfOutsideViewport(position, 0 /* Smooth */);
    }
};
FoldingController.ID = 'editor.contrib.folding';
FoldingController = __decorate([
    __param(1, IContextKeyService)
], FoldingController);
export { FoldingController };
class FoldingAction extends EditorAction {
    runEditorCommand(accessor, editor, args) {
        let foldingController = FoldingController.get(editor);
        if (!foldingController) {
            return;
        }
        let foldingModelPromise = foldingController.getFoldingModel();
        if (foldingModelPromise) {
            this.reportTelemetry(accessor, editor);
            return foldingModelPromise.then(foldingModel => {
                if (foldingModel) {
                    this.invoke(foldingController, foldingModel, editor, args);
                    const selection = editor.getSelection();
                    if (selection) {
                        foldingController.reveal(selection.getStartPosition());
                    }
                }
            });
        }
    }
    getSelectedLines(editor) {
        let selections = editor.getSelections();
        return selections ? selections.map(s => s.startLineNumber) : [];
    }
    getLineNumbers(args, editor) {
        if (args && args.selectionLines) {
            return args.selectionLines.map(l => l + 1); // to 0-bases line numbers
        }
        return this.getSelectedLines(editor);
    }
    run(_accessor, _editor) {
    }
}
function foldingArgumentsConstraint(args) {
    if (!types.isUndefined(args)) {
        if (!types.isObject(args)) {
            return false;
        }
        const foldingArgs = args;
        if (!types.isUndefined(foldingArgs.levels) && !types.isNumber(foldingArgs.levels)) {
            return false;
        }
        if (!types.isUndefined(foldingArgs.direction) && !types.isString(foldingArgs.direction)) {
            return false;
        }
        if (!types.isUndefined(foldingArgs.selectionLines) && (!types.isArray(foldingArgs.selectionLines) || !foldingArgs.selectionLines.every(types.isNumber))) {
            return false;
        }
    }
    return true;
}
class UnfoldAction extends FoldingAction {
    constructor() {
        super({
            id: 'editor.unfold',
            label: nls.localize('unfoldAction.label', "Unfold"),
            alias: 'Unfold',
            precondition: CONTEXT_FOLDING_ENABLED,
            kbOpts: {
                kbExpr: EditorContextKeys.editorTextFocus,
                primary: 2048 /* CtrlCmd */ | 1024 /* Shift */ | 89 /* US_CLOSE_SQUARE_BRACKET */,
                mac: {
                    primary: 2048 /* CtrlCmd */ | 512 /* Alt */ | 89 /* US_CLOSE_SQUARE_BRACKET */
                },
                weight: 100 /* EditorContrib */
            },
            description: {
                description: 'Unfold the content in the editor',
                args: [
                    {
                        name: 'Unfold editor argument',
                        description: `Property-value pairs that can be passed through this argument:
						* 'levels': Number of levels to unfold. If not set, defaults to 1.
						* 'direction': If 'up', unfold given number of levels up otherwise unfolds down.
						* 'selectionLines': The start lines (0-based) of the editor selections to apply the unfold action to. If not set, the active selection(s) will be used.
						`,
                        constraint: foldingArgumentsConstraint,
                        schema: {
                            'type': 'object',
                            'properties': {
                                'levels': {
                                    'type': 'number',
                                    'default': 1
                                },
                                'direction': {
                                    'type': 'string',
                                    'enum': ['up', 'down'],
                                    'default': 'down'
                                },
                                'selectionLines': {
                                    'type': 'array',
                                    'items': {
                                        'type': 'number'
                                    }
                                }
                            }
                        }
                    }
                ]
            }
        });
    }
    invoke(_foldingController, foldingModel, editor, args) {
        let levels = args && args.levels || 1;
        let lineNumbers = this.getLineNumbers(args, editor);
        if (args && args.direction === 'up') {
            setCollapseStateLevelsUp(foldingModel, false, levels, lineNumbers);
        }
        else {
            setCollapseStateLevelsDown(foldingModel, false, levels, lineNumbers);
        }
    }
}
class UnFoldRecursivelyAction extends FoldingAction {
    constructor() {
        super({
            id: 'editor.unfoldRecursively',
            label: nls.localize('unFoldRecursivelyAction.label', "Unfold Recursively"),
            alias: 'Unfold Recursively',
            precondition: CONTEXT_FOLDING_ENABLED,
            kbOpts: {
                kbExpr: EditorContextKeys.editorTextFocus,
                primary: KeyChord(2048 /* CtrlCmd */ | 41 /* KEY_K */, 2048 /* CtrlCmd */ | 89 /* US_CLOSE_SQUARE_BRACKET */),
                weight: 100 /* EditorContrib */
            }
        });
    }
    invoke(_foldingController, foldingModel, editor, _args) {
        setCollapseStateLevelsDown(foldingModel, false, Number.MAX_VALUE, this.getSelectedLines(editor));
    }
}
class FoldAction extends FoldingAction {
    constructor() {
        super({
            id: 'editor.fold',
            label: nls.localize('foldAction.label', "Fold"),
            alias: 'Fold',
            precondition: CONTEXT_FOLDING_ENABLED,
            kbOpts: {
                kbExpr: EditorContextKeys.editorTextFocus,
                primary: 2048 /* CtrlCmd */ | 1024 /* Shift */ | 87 /* US_OPEN_SQUARE_BRACKET */,
                mac: {
                    primary: 2048 /* CtrlCmd */ | 512 /* Alt */ | 87 /* US_OPEN_SQUARE_BRACKET */
                },
                weight: 100 /* EditorContrib */
            },
            description: {
                description: 'Fold the content in the editor',
                args: [
                    {
                        name: 'Fold editor argument',
                        description: `Property-value pairs that can be passed through this argument:
							* 'levels': Number of levels to fold.
							* 'direction': If 'up', folds given number of levels up otherwise folds down.
							* 'selectionLines': The start lines (0-based) of the editor selections to apply the fold action to. If not set, the active selection(s) will be used.
							If no levels or direction is set, folds the region at the locations or if already collapsed, the first uncollapsed parent instead.
						`,
                        constraint: foldingArgumentsConstraint,
                        schema: {
                            'type': 'object',
                            'properties': {
                                'levels': {
                                    'type': 'number',
                                },
                                'direction': {
                                    'type': 'string',
                                    'enum': ['up', 'down'],
                                },
                                'selectionLines': {
                                    'type': 'array',
                                    'items': {
                                        'type': 'number'
                                    }
                                }
                            }
                        }
                    }
                ]
            }
        });
    }
    invoke(_foldingController, foldingModel, editor, args) {
        let lineNumbers = this.getLineNumbers(args, editor);
        const levels = args && args.levels;
        const direction = args && args.direction;
        if (typeof levels !== 'number' && typeof direction !== 'string') {
            // fold the region at the location or if already collapsed, the first uncollapsed parent instead.
            setCollapseStateUp(foldingModel, true, lineNumbers);
        }
        else {
            if (direction === 'up') {
                setCollapseStateLevelsUp(foldingModel, true, levels || 1, lineNumbers);
            }
            else {
                setCollapseStateLevelsDown(foldingModel, true, levels || 1, lineNumbers);
            }
        }
    }
}
class ToggleFoldAction extends FoldingAction {
    constructor() {
        super({
            id: 'editor.toggleFold',
            label: nls.localize('toggleFoldAction.label', "Toggle Fold"),
            alias: 'Toggle Fold',
            precondition: CONTEXT_FOLDING_ENABLED,
            kbOpts: {
                kbExpr: EditorContextKeys.editorTextFocus,
                primary: KeyChord(2048 /* CtrlCmd */ | 41 /* KEY_K */, 2048 /* CtrlCmd */ | 42 /* KEY_L */),
                weight: 100 /* EditorContrib */
            }
        });
    }
    invoke(_foldingController, foldingModel, editor) {
        let selectedLines = this.getSelectedLines(editor);
        toggleCollapseState(foldingModel, 1, selectedLines);
    }
}
class FoldRecursivelyAction extends FoldingAction {
    constructor() {
        super({
            id: 'editor.foldRecursively',
            label: nls.localize('foldRecursivelyAction.label', "Fold Recursively"),
            alias: 'Fold Recursively',
            precondition: CONTEXT_FOLDING_ENABLED,
            kbOpts: {
                kbExpr: EditorContextKeys.editorTextFocus,
                primary: KeyChord(2048 /* CtrlCmd */ | 41 /* KEY_K */, 2048 /* CtrlCmd */ | 87 /* US_OPEN_SQUARE_BRACKET */),
                weight: 100 /* EditorContrib */
            }
        });
    }
    invoke(_foldingController, foldingModel, editor) {
        let selectedLines = this.getSelectedLines(editor);
        setCollapseStateLevelsDown(foldingModel, true, Number.MAX_VALUE, selectedLines);
    }
}
class FoldAllBlockCommentsAction extends FoldingAction {
    constructor() {
        super({
            id: 'editor.foldAllBlockComments',
            label: nls.localize('foldAllBlockComments.label', "Fold All Block Comments"),
            alias: 'Fold All Block Comments',
            precondition: CONTEXT_FOLDING_ENABLED,
            kbOpts: {
                kbExpr: EditorContextKeys.editorTextFocus,
                primary: KeyChord(2048 /* CtrlCmd */ | 41 /* KEY_K */, 2048 /* CtrlCmd */ | 85 /* US_SLASH */),
                weight: 100 /* EditorContrib */
            }
        });
    }
    invoke(_foldingController, foldingModel, editor) {
        if (foldingModel.regions.hasTypes()) {
            setCollapseStateForType(foldingModel, FoldingRangeKind.Comment.value, true);
        }
        else {
            const editorModel = editor.getModel();
            if (!editorModel) {
                return;
            }
            let comments = LanguageConfigurationRegistry.getComments(editorModel.getLanguageIdentifier().id);
            if (comments && comments.blockCommentStartToken) {
                let regExp = new RegExp('^\\s*' + escapeRegExpCharacters(comments.blockCommentStartToken));
                setCollapseStateForMatchingLines(foldingModel, regExp, true);
            }
        }
    }
}
class FoldAllRegionsAction extends FoldingAction {
    constructor() {
        super({
            id: 'editor.foldAllMarkerRegions',
            label: nls.localize('foldAllMarkerRegions.label', "Fold All Regions"),
            alias: 'Fold All Regions',
            precondition: CONTEXT_FOLDING_ENABLED,
            kbOpts: {
                kbExpr: EditorContextKeys.editorTextFocus,
                primary: KeyChord(2048 /* CtrlCmd */ | 41 /* KEY_K */, 2048 /* CtrlCmd */ | 29 /* KEY_8 */),
                weight: 100 /* EditorContrib */
            }
        });
    }
    invoke(_foldingController, foldingModel, editor) {
        if (foldingModel.regions.hasTypes()) {
            setCollapseStateForType(foldingModel, FoldingRangeKind.Region.value, true);
        }
        else {
            const editorModel = editor.getModel();
            if (!editorModel) {
                return;
            }
            let foldingRules = LanguageConfigurationRegistry.getFoldingRules(editorModel.getLanguageIdentifier().id);
            if (foldingRules && foldingRules.markers && foldingRules.markers.start) {
                let regExp = new RegExp(foldingRules.markers.start);
                setCollapseStateForMatchingLines(foldingModel, regExp, true);
            }
        }
    }
}
class UnfoldAllRegionsAction extends FoldingAction {
    constructor() {
        super({
            id: 'editor.unfoldAllMarkerRegions',
            label: nls.localize('unfoldAllMarkerRegions.label', "Unfold All Regions"),
            alias: 'Unfold All Regions',
            precondition: CONTEXT_FOLDING_ENABLED,
            kbOpts: {
                kbExpr: EditorContextKeys.editorTextFocus,
                primary: KeyChord(2048 /* CtrlCmd */ | 41 /* KEY_K */, 2048 /* CtrlCmd */ | 30 /* KEY_9 */),
                weight: 100 /* EditorContrib */
            }
        });
    }
    invoke(_foldingController, foldingModel, editor) {
        if (foldingModel.regions.hasTypes()) {
            setCollapseStateForType(foldingModel, FoldingRangeKind.Region.value, false);
        }
        else {
            const editorModel = editor.getModel();
            if (!editorModel) {
                return;
            }
            let foldingRules = LanguageConfigurationRegistry.getFoldingRules(editorModel.getLanguageIdentifier().id);
            if (foldingRules && foldingRules.markers && foldingRules.markers.start) {
                let regExp = new RegExp(foldingRules.markers.start);
                setCollapseStateForMatchingLines(foldingModel, regExp, false);
            }
        }
    }
}
class FoldAllAction extends FoldingAction {
    constructor() {
        super({
            id: 'editor.foldAll',
            label: nls.localize('foldAllAction.label', "Fold All"),
            alias: 'Fold All',
            precondition: CONTEXT_FOLDING_ENABLED,
            kbOpts: {
                kbExpr: EditorContextKeys.editorTextFocus,
                primary: KeyChord(2048 /* CtrlCmd */ | 41 /* KEY_K */, 2048 /* CtrlCmd */ | 21 /* KEY_0 */),
                weight: 100 /* EditorContrib */
            }
        });
    }
    invoke(_foldingController, foldingModel, _editor) {
        setCollapseStateLevelsDown(foldingModel, true);
    }
}
class UnfoldAllAction extends FoldingAction {
    constructor() {
        super({
            id: 'editor.unfoldAll',
            label: nls.localize('unfoldAllAction.label', "Unfold All"),
            alias: 'Unfold All',
            precondition: CONTEXT_FOLDING_ENABLED,
            kbOpts: {
                kbExpr: EditorContextKeys.editorTextFocus,
                primary: KeyChord(2048 /* CtrlCmd */ | 41 /* KEY_K */, 2048 /* CtrlCmd */ | 40 /* KEY_J */),
                weight: 100 /* EditorContrib */
            }
        });
    }
    invoke(_foldingController, foldingModel, _editor) {
        setCollapseStateLevelsDown(foldingModel, false);
    }
}
class FoldLevelAction extends FoldingAction {
    getFoldingLevel() {
        return parseInt(this.id.substr(FoldLevelAction.ID_PREFIX.length));
    }
    invoke(_foldingController, foldingModel, editor) {
        setCollapseStateAtLevel(foldingModel, this.getFoldingLevel(), true, this.getSelectedLines(editor));
    }
}
FoldLevelAction.ID_PREFIX = 'editor.foldLevel';
FoldLevelAction.ID = (level) => FoldLevelAction.ID_PREFIX + level;
registerEditorContribution(FoldingController.ID, FoldingController);
registerEditorAction(UnfoldAction);
registerEditorAction(UnFoldRecursivelyAction);
registerEditorAction(FoldAction);
registerEditorAction(FoldRecursivelyAction);
registerEditorAction(FoldAllAction);
registerEditorAction(UnfoldAllAction);
registerEditorAction(FoldAllBlockCommentsAction);
registerEditorAction(FoldAllRegionsAction);
registerEditorAction(UnfoldAllRegionsAction);
registerEditorAction(ToggleFoldAction);
for (let i = 1; i <= 7; i++) {
    registerInstantiatedEditorAction(new FoldLevelAction({
        id: FoldLevelAction.ID(i),
        label: nls.localize('foldLevelAction.label', "Fold Level {0}", i),
        alias: `Fold Level ${i}`,
        precondition: CONTEXT_FOLDING_ENABLED,
        kbOpts: {
            kbExpr: EditorContextKeys.editorTextFocus,
            primary: KeyChord(2048 /* CtrlCmd */ | 41 /* KEY_K */, 2048 /* CtrlCmd */ | (21 /* KEY_0 */ + i)),
            weight: 100 /* EditorContrib */
        }
    }));
}
export const foldBackgroundBackground = registerColor('editor.foldBackground', { light: transparent(editorSelectionBackground, 0.3), dark: transparent(editorSelectionBackground, 0.3), hc: null }, nls.localize('foldBackgroundBackground', "Background color behind folded ranges. The color must not be opaque so as not to hide underlying decorations."), true);
export const editorFoldForeground = registerColor('editorGutter.foldingControlForeground', { dark: iconForeground, light: iconForeground, hc: iconForeground }, nls.localize('editorGutter.foldingControlForeground', 'Color of the folding control in the editor gutter.'));
registerThemingParticipant((theme, collector) => {
    const foldBackground = theme.getColor(foldBackgroundBackground);
    if (foldBackground) {
        collector.addRule(`.monaco-editor .folded-background { background-color: ${foldBackground}; }`);
    }
    const editorFoldColor = theme.getColor(editorFoldForeground);
    if (editorFoldColor) {
        collector.addRule(`
		.monaco-editor .cldr${ThemeIcon.asCSSSelector(foldingExpandedIcon)},
		.monaco-editor .cldr${ThemeIcon.asCSSSelector(foldingCollapsedIcon)} {
			color: ${editorFoldColor} !important;
		}
		`);
    }
});
