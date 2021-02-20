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
var _a;
import './media/diffEditor.css';
import * as nls from '../../../nls.js';
import * as dom from '../../../base/browser/dom.js';
import { createFastDomNode } from '../../../base/browser/fastDomNode.js';
import { Sash } from '../../../base/browser/ui/sash/sash.js';
import { RunOnceScheduler } from '../../../base/common/async.js';
import { Emitter } from '../../../base/common/event.js';
import { Disposable } from '../../../base/common/lifecycle.js';
import { Configuration } from '../config/configuration.js';
import { StableEditorScrollState } from '../core/editorState.js';
import { ICodeEditorService } from '../services/codeEditorService.js';
import { CodeEditorWidget } from './codeEditorWidget.js';
import { DiffReview } from './diffReview.js';
import { EditorOptions, EditorFontLigatures, stringSet as validateStringSetOption, boolean as validateBooleanOption } from '../../common/config/editorOptions.js';
import { Range } from '../../common/core/range.js';
import { createStringBuilder } from '../../common/core/stringBuilder.js';
import * as editorCommon from '../../common/editorCommon.js';
import { ModelDecorationOptions } from '../../common/model/textModel.js';
import { IEditorWorkerService } from '../../common/services/editorWorkerService.js';
import { OverviewRulerZone } from '../../common/view/overviewZoneManager.js';
import { LineDecoration } from '../../common/viewLayout/lineDecorations.js';
import { RenderLineInput, renderViewLine } from '../../common/viewLayout/viewLineRenderer.js';
import { InlineDecoration, ViewLineRenderingData } from '../../common/viewModel/viewModel.js';
import { IContextKeyService } from '../../../platform/contextkey/common/contextkey.js';
import { IInstantiationService } from '../../../platform/instantiation/common/instantiation.js';
import { ServiceCollection } from '../../../platform/instantiation/common/serviceCollection.js';
import { INotificationService } from '../../../platform/notification/common/notification.js';
import { defaultInsertColor, defaultRemoveColor, diffBorder, diffInserted, diffInsertedOutline, diffRemoved, diffRemovedOutline, scrollbarShadow, scrollbarSliderBackground, scrollbarSliderHoverBackground, scrollbarSliderActiveBackground, diffDiagonalFill } from '../../../platform/theme/common/colorRegistry.js';
import { IThemeService, getThemeTypeSelector, registerThemingParticipant, ThemeIcon } from '../../../platform/theme/common/themeService.js';
import { IContextMenuService } from '../../../platform/contextview/browser/contextView.js';
import { InlineDiffMargin } from './inlineDiffMargin.js';
import { IClipboardService } from '../../../platform/clipboard/common/clipboardService.js';
import { EditorExtensionsRegistry } from '../editorExtensions.js';
import { onUnexpectedError } from '../../../base/common/errors.js';
import { IEditorProgressService } from '../../../platform/progress/common/progress.js';
import { ElementSizeObserver } from '../config/elementSizeObserver.js';
import { Codicon } from '../../../base/common/codicons.js';
import { MOUSE_CURSOR_TEXT_CSS_CLASS_NAME } from '../../../base/browser/ui/mouseCursor/mouseCursor.js';
import { registerIcon } from '../../../platform/theme/common/iconRegistry.js';
class VisualEditorState {
    constructor(_contextMenuService, _clipboardService) {
        this._contextMenuService = _contextMenuService;
        this._clipboardService = _clipboardService;
        this._zones = [];
        this._inlineDiffMargins = [];
        this._zonesMap = {};
        this._decorations = [];
    }
    getForeignViewZones(allViewZones) {
        return allViewZones.filter((z) => !this._zonesMap[String(z.id)]);
    }
    clean(editor) {
        // (1) View zones
        if (this._zones.length > 0) {
            editor.changeViewZones((viewChangeAccessor) => {
                for (const zoneId of this._zones) {
                    viewChangeAccessor.removeZone(zoneId);
                }
            });
        }
        this._zones = [];
        this._zonesMap = {};
        // (2) Model decorations
        this._decorations = editor.deltaDecorations(this._decorations, []);
    }
    apply(editor, overviewRuler, newDecorations, restoreScrollState) {
        const scrollState = restoreScrollState ? StableEditorScrollState.capture(editor) : null;
        // view zones
        editor.changeViewZones((viewChangeAccessor) => {
            for (const zoneId of this._zones) {
                viewChangeAccessor.removeZone(zoneId);
            }
            for (const inlineDiffMargin of this._inlineDiffMargins) {
                inlineDiffMargin.dispose();
            }
            this._zones = [];
            this._zonesMap = {};
            this._inlineDiffMargins = [];
            for (let i = 0, length = newDecorations.zones.length; i < length; i++) {
                const viewZone = newDecorations.zones[i];
                viewZone.suppressMouseDown = true;
                const zoneId = viewChangeAccessor.addZone(viewZone);
                this._zones.push(zoneId);
                this._zonesMap[String(zoneId)] = true;
                if (newDecorations.zones[i].diff && viewZone.marginDomNode) {
                    viewZone.suppressMouseDown = false;
                    this._inlineDiffMargins.push(new InlineDiffMargin(zoneId, viewZone.marginDomNode, editor, newDecorations.zones[i].diff, this._contextMenuService, this._clipboardService));
                }
            }
        });
        if (scrollState) {
            scrollState.restore(editor);
        }
        // decorations
        this._decorations = editor.deltaDecorations(this._decorations, newDecorations.decorations);
        // overview ruler
        if (overviewRuler) {
            overviewRuler.setZones(newDecorations.overviewZones);
        }
    }
}
let DIFF_EDITOR_ID = 0;
const diffInsertIcon = registerIcon('diff-insert', Codicon.add, nls.localize('diffInsertIcon', 'Line decoration for inserts in the diff editor.'));
const diffRemoveIcon = registerIcon('diff-remove', Codicon.remove, nls.localize('diffRemoveIcon', 'Line decoration for removals in the diff editor.'));
const ttPolicy = (_a = window.trustedTypes) === null || _a === void 0 ? void 0 : _a.createPolicy('diffEditorWidget', { createHTML: value => value });
let DiffEditorWidget = class DiffEditorWidget extends Disposable {
    constructor(domElement, options, codeEditorWidgetOptions, clipboardService, editorWorkerService, contextKeyService, instantiationService, codeEditorService, themeService, notificationService, contextMenuService, _editorProgressService) {
        super();
        this._editorProgressService = _editorProgressService;
        this._onDidDispose = this._register(new Emitter());
        this.onDidDispose = this._onDidDispose.event;
        this._onDidUpdateDiff = this._register(new Emitter());
        this.onDidUpdateDiff = this._onDidUpdateDiff.event;
        this._onDidContentSizeChange = this._register(new Emitter());
        this._lastOriginalWarning = null;
        this._lastModifiedWarning = null;
        this._editorWorkerService = editorWorkerService;
        this._codeEditorService = codeEditorService;
        this._contextKeyService = this._register(contextKeyService.createScoped(domElement));
        this._contextKeyService.createKey('isInDiffEditor', true);
        this._themeService = themeService;
        this._notificationService = notificationService;
        this._id = (++DIFF_EDITOR_ID);
        this._state = 0 /* Idle */;
        this._updatingDiffProgress = null;
        this._domElement = domElement;
        options = options || {};
        // renderSideBySide
        this._renderSideBySide = true;
        if (typeof options.renderSideBySide !== 'undefined') {
            this._renderSideBySide = options.renderSideBySide;
        }
        // maxComputationTime
        this._maxComputationTime = 5000;
        if (typeof options.maxComputationTime !== 'undefined') {
            this._maxComputationTime = options.maxComputationTime;
        }
        // ignoreTrimWhitespace
        this._ignoreTrimWhitespace = true;
        if (typeof options.ignoreTrimWhitespace !== 'undefined') {
            this._ignoreTrimWhitespace = options.ignoreTrimWhitespace;
        }
        // renderIndicators
        this._renderIndicators = true;
        if (typeof options.renderIndicators !== 'undefined') {
            this._renderIndicators = options.renderIndicators;
        }
        this._originalIsEditable = validateBooleanOption(options.originalEditable, false);
        this._diffCodeLens = validateBooleanOption(options.diffCodeLens, false);
        this._diffWordWrap = validateDiffWordWrap(options.diffWordWrap, 'inherit');
        if (typeof options.isInEmbeddedEditor !== 'undefined') {
            this._contextKeyService.createKey('isInEmbeddedDiffEditor', options.isInEmbeddedEditor);
        }
        else {
            this._contextKeyService.createKey('isInEmbeddedDiffEditor', false);
        }
        this._renderOverviewRuler = true;
        if (typeof options.renderOverviewRuler !== 'undefined') {
            this._renderOverviewRuler = Boolean(options.renderOverviewRuler);
        }
        this._updateDecorationsRunner = this._register(new RunOnceScheduler(() => this._updateDecorations(), 0));
        this._containerDomElement = document.createElement('div');
        this._containerDomElement.className = DiffEditorWidget._getClassName(this._themeService.getColorTheme(), this._renderSideBySide);
        this._containerDomElement.style.position = 'relative';
        this._containerDomElement.style.height = '100%';
        this._domElement.appendChild(this._containerDomElement);
        this._overviewViewportDomElement = createFastDomNode(document.createElement('div'));
        this._overviewViewportDomElement.setClassName('diffViewport');
        this._overviewViewportDomElement.setPosition('absolute');
        this._overviewDomElement = document.createElement('div');
        this._overviewDomElement.className = 'diffOverview';
        this._overviewDomElement.style.position = 'absolute';
        this._overviewDomElement.appendChild(this._overviewViewportDomElement.domNode);
        this._register(dom.addStandardDisposableListener(this._overviewDomElement, 'mousedown', (e) => {
            this._modifiedEditor.delegateVerticalScrollbarMouseDown(e);
        }));
        if (this._renderOverviewRuler) {
            this._containerDomElement.appendChild(this._overviewDomElement);
        }
        // Create left side
        this._originalDomNode = document.createElement('div');
        this._originalDomNode.className = 'editor original';
        this._originalDomNode.style.position = 'absolute';
        this._originalDomNode.style.height = '100%';
        this._containerDomElement.appendChild(this._originalDomNode);
        // Create right side
        this._modifiedDomNode = document.createElement('div');
        this._modifiedDomNode.className = 'editor modified';
        this._modifiedDomNode.style.position = 'absolute';
        this._modifiedDomNode.style.height = '100%';
        this._containerDomElement.appendChild(this._modifiedDomNode);
        this._beginUpdateDecorationsTimeout = -1;
        this._currentlyChangingViewZones = false;
        this._diffComputationToken = 0;
        this._originalEditorState = new VisualEditorState(contextMenuService, clipboardService);
        this._modifiedEditorState = new VisualEditorState(contextMenuService, clipboardService);
        this._isVisible = true;
        this._isHandlingScrollEvent = false;
        this._elementSizeObserver = this._register(new ElementSizeObserver(this._containerDomElement, options.dimension, () => this._onDidContainerSizeChanged()));
        if (options.automaticLayout) {
            this._elementSizeObserver.startObserving();
        }
        this._diffComputationResult = null;
        const leftContextKeyService = this._contextKeyService.createScoped();
        const leftServices = new ServiceCollection();
        leftServices.set(IContextKeyService, leftContextKeyService);
        const leftScopedInstantiationService = instantiationService.createChild(leftServices);
        const rightContextKeyService = this._contextKeyService.createScoped();
        const rightServices = new ServiceCollection();
        rightServices.set(IContextKeyService, rightContextKeyService);
        const rightScopedInstantiationService = instantiationService.createChild(rightServices);
        this._originalEditor = this._createLeftHandSideEditor(options, codeEditorWidgetOptions.originalEditor || {}, leftScopedInstantiationService, leftContextKeyService);
        this._modifiedEditor = this._createRightHandSideEditor(options, codeEditorWidgetOptions.modifiedEditor || {}, rightScopedInstantiationService, rightContextKeyService);
        this._originalOverviewRuler = null;
        this._modifiedOverviewRuler = null;
        this._reviewPane = new DiffReview(this);
        this._containerDomElement.appendChild(this._reviewPane.domNode.domNode);
        this._containerDomElement.appendChild(this._reviewPane.shadow.domNode);
        this._containerDomElement.appendChild(this._reviewPane.actionBarContainer.domNode);
        // enableSplitViewResizing
        this._enableSplitViewResizing = true;
        if (typeof options.enableSplitViewResizing !== 'undefined') {
            this._enableSplitViewResizing = options.enableSplitViewResizing;
        }
        if (this._renderSideBySide) {
            this._setStrategy(new DiffEditorWidgetSideBySide(this._createDataSource(), this._enableSplitViewResizing));
        }
        else {
            this._setStrategy(new DiffEditorWidgetInline(this._createDataSource(), this._enableSplitViewResizing));
        }
        this._register(themeService.onDidColorThemeChange(t => {
            if (this._strategy && this._strategy.applyColors(t)) {
                this._updateDecorationsRunner.schedule();
            }
            this._containerDomElement.className = DiffEditorWidget._getClassName(this._themeService.getColorTheme(), this._renderSideBySide);
        }));
        const contributions = EditorExtensionsRegistry.getDiffEditorContributions();
        for (const desc of contributions) {
            try {
                this._register(instantiationService.createInstance(desc.ctor, this));
            }
            catch (err) {
                onUnexpectedError(err);
            }
        }
        this._codeEditorService.addDiffEditor(this);
    }
    _setState(newState) {
        if (this._state === newState) {
            return;
        }
        this._state = newState;
        if (this._updatingDiffProgress) {
            this._updatingDiffProgress.done();
            this._updatingDiffProgress = null;
        }
        if (this._state === 1 /* ComputingDiff */) {
            this._updatingDiffProgress = this._editorProgressService.show(true, 1000);
        }
    }
    diffReviewNext() {
        this._reviewPane.next();
    }
    diffReviewPrev() {
        this._reviewPane.prev();
    }
    static _getClassName(theme, renderSideBySide) {
        let result = 'monaco-diff-editor monaco-editor-background ';
        if (renderSideBySide) {
            result += 'side-by-side ';
        }
        result += getThemeTypeSelector(theme.type);
        return result;
    }
    _recreateOverviewRulers() {
        if (!this._renderOverviewRuler) {
            return;
        }
        if (this._originalOverviewRuler) {
            this._overviewDomElement.removeChild(this._originalOverviewRuler.getDomNode());
            this._originalOverviewRuler.dispose();
        }
        if (this._originalEditor.hasModel()) {
            this._originalOverviewRuler = this._originalEditor.createOverviewRuler('original diffOverviewRuler');
            this._overviewDomElement.appendChild(this._originalOverviewRuler.getDomNode());
        }
        if (this._modifiedOverviewRuler) {
            this._overviewDomElement.removeChild(this._modifiedOverviewRuler.getDomNode());
            this._modifiedOverviewRuler.dispose();
        }
        if (this._modifiedEditor.hasModel()) {
            this._modifiedOverviewRuler = this._modifiedEditor.createOverviewRuler('modified diffOverviewRuler');
            this._overviewDomElement.appendChild(this._modifiedOverviewRuler.getDomNode());
        }
        this._layoutOverviewRulers();
    }
    _createLeftHandSideEditor(options, codeEditorWidgetOptions, instantiationService, contextKeyService) {
        const editor = this._createInnerEditor(instantiationService, this._originalDomNode, this._adjustOptionsForLeftHandSide(options), codeEditorWidgetOptions);
        this._register(editor.onDidScrollChange((e) => {
            if (this._isHandlingScrollEvent) {
                return;
            }
            if (!e.scrollTopChanged && !e.scrollLeftChanged && !e.scrollHeightChanged) {
                return;
            }
            this._isHandlingScrollEvent = true;
            this._modifiedEditor.setScrollPosition({
                scrollLeft: e.scrollLeft,
                scrollTop: e.scrollTop
            });
            this._isHandlingScrollEvent = false;
            this._layoutOverviewViewport();
        }));
        this._register(editor.onDidChangeViewZones(() => {
            this._onViewZonesChanged();
        }));
        this._register(editor.onDidChangeConfiguration((e) => {
            if (!editor.getModel()) {
                return;
            }
            if (e.hasChanged(38 /* fontInfo */)) {
                this._updateDecorationsRunner.schedule();
            }
            if (e.hasChanged(125 /* wrappingInfo */)) {
                this._updateDecorationsRunner.cancel();
                this._updateDecorations();
            }
        }));
        this._register(editor.onDidChangeModelContent(() => {
            if (this._isVisible) {
                this._beginUpdateDecorationsSoon();
            }
        }));
        const isInDiffLeftEditorKey = contextKeyService.createKey('isInDiffLeftEditor', undefined);
        this._register(editor.onDidFocusEditorWidget(() => isInDiffLeftEditorKey.set(true)));
        this._register(editor.onDidBlurEditorWidget(() => isInDiffLeftEditorKey.set(false)));
        this._register(editor.onDidContentSizeChange(e => {
            const width = this._originalEditor.getContentWidth() + this._modifiedEditor.getContentWidth() + DiffEditorWidget.ONE_OVERVIEW_WIDTH;
            const height = Math.max(this._modifiedEditor.getContentHeight(), this._originalEditor.getContentHeight());
            this._onDidContentSizeChange.fire({
                contentHeight: height,
                contentWidth: width,
                contentHeightChanged: e.contentHeightChanged,
                contentWidthChanged: e.contentWidthChanged
            });
        }));
        return editor;
    }
    _createRightHandSideEditor(options, codeEditorWidgetOptions, instantiationService, contextKeyService) {
        const editor = this._createInnerEditor(instantiationService, this._modifiedDomNode, this._adjustOptionsForRightHandSide(options), codeEditorWidgetOptions);
        this._register(editor.onDidScrollChange((e) => {
            if (this._isHandlingScrollEvent) {
                return;
            }
            if (!e.scrollTopChanged && !e.scrollLeftChanged && !e.scrollHeightChanged) {
                return;
            }
            this._isHandlingScrollEvent = true;
            this._originalEditor.setScrollPosition({
                scrollLeft: e.scrollLeft,
                scrollTop: e.scrollTop
            });
            this._isHandlingScrollEvent = false;
            this._layoutOverviewViewport();
        }));
        this._register(editor.onDidChangeViewZones(() => {
            this._onViewZonesChanged();
        }));
        this._register(editor.onDidChangeConfiguration((e) => {
            if (!editor.getModel()) {
                return;
            }
            if (e.hasChanged(38 /* fontInfo */)) {
                this._updateDecorationsRunner.schedule();
            }
            if (e.hasChanged(125 /* wrappingInfo */)) {
                this._updateDecorationsRunner.cancel();
                this._updateDecorations();
            }
        }));
        this._register(editor.onDidChangeModelContent(() => {
            if (this._isVisible) {
                this._beginUpdateDecorationsSoon();
            }
        }));
        this._register(editor.onDidChangeModelOptions((e) => {
            if (e.tabSize) {
                this._updateDecorationsRunner.schedule();
            }
        }));
        const isInDiffRightEditorKey = contextKeyService.createKey('isInDiffRightEditor', undefined);
        this._register(editor.onDidFocusEditorWidget(() => isInDiffRightEditorKey.set(true)));
        this._register(editor.onDidBlurEditorWidget(() => isInDiffRightEditorKey.set(false)));
        this._register(editor.onDidContentSizeChange(e => {
            const width = this._originalEditor.getContentWidth() + this._modifiedEditor.getContentWidth() + DiffEditorWidget.ONE_OVERVIEW_WIDTH;
            const height = Math.max(this._modifiedEditor.getContentHeight(), this._originalEditor.getContentHeight());
            this._onDidContentSizeChange.fire({
                contentHeight: height,
                contentWidth: width,
                contentHeightChanged: e.contentHeightChanged,
                contentWidthChanged: e.contentWidthChanged
            });
        }));
        return editor;
    }
    _createInnerEditor(instantiationService, container, options, editorWidgetOptions) {
        return instantiationService.createInstance(CodeEditorWidget, container, options, editorWidgetOptions);
    }
    dispose() {
        this._codeEditorService.removeDiffEditor(this);
        if (this._beginUpdateDecorationsTimeout !== -1) {
            window.clearTimeout(this._beginUpdateDecorationsTimeout);
            this._beginUpdateDecorationsTimeout = -1;
        }
        this._cleanViewZonesAndDecorations();
        if (this._originalOverviewRuler) {
            this._overviewDomElement.removeChild(this._originalOverviewRuler.getDomNode());
            this._originalOverviewRuler.dispose();
        }
        if (this._modifiedOverviewRuler) {
            this._overviewDomElement.removeChild(this._modifiedOverviewRuler.getDomNode());
            this._modifiedOverviewRuler.dispose();
        }
        this._overviewDomElement.removeChild(this._overviewViewportDomElement.domNode);
        if (this._renderOverviewRuler) {
            this._containerDomElement.removeChild(this._overviewDomElement);
        }
        this._containerDomElement.removeChild(this._originalDomNode);
        this._originalEditor.dispose();
        this._containerDomElement.removeChild(this._modifiedDomNode);
        this._modifiedEditor.dispose();
        this._strategy.dispose();
        this._containerDomElement.removeChild(this._reviewPane.domNode.domNode);
        this._containerDomElement.removeChild(this._reviewPane.shadow.domNode);
        this._containerDomElement.removeChild(this._reviewPane.actionBarContainer.domNode);
        this._reviewPane.dispose();
        this._domElement.removeChild(this._containerDomElement);
        this._onDidDispose.fire();
        super.dispose();
    }
    //------------ begin IDiffEditor methods
    getId() {
        return this.getEditorType() + ':' + this._id;
    }
    getEditorType() {
        return editorCommon.EditorType.IDiffEditor;
    }
    getLineChanges() {
        if (!this._diffComputationResult) {
            return null;
        }
        return this._diffComputationResult.changes;
    }
    getOriginalEditor() {
        return this._originalEditor;
    }
    getModifiedEditor() {
        return this._modifiedEditor;
    }
    updateOptions(newOptions) {
        // Handle side by side
        let renderSideBySideChanged = false;
        if (typeof newOptions.renderSideBySide !== 'undefined') {
            if (this._renderSideBySide !== newOptions.renderSideBySide) {
                this._renderSideBySide = newOptions.renderSideBySide;
                renderSideBySideChanged = true;
            }
        }
        if (typeof newOptions.maxComputationTime !== 'undefined') {
            this._maxComputationTime = newOptions.maxComputationTime;
            if (this._isVisible) {
                this._beginUpdateDecorationsSoon();
            }
        }
        let beginUpdateDecorations = false;
        if (typeof newOptions.ignoreTrimWhitespace !== 'undefined') {
            if (this._ignoreTrimWhitespace !== newOptions.ignoreTrimWhitespace) {
                this._ignoreTrimWhitespace = newOptions.ignoreTrimWhitespace;
                // Begin comparing
                beginUpdateDecorations = true;
            }
        }
        if (typeof newOptions.renderIndicators !== 'undefined') {
            if (this._renderIndicators !== newOptions.renderIndicators) {
                this._renderIndicators = newOptions.renderIndicators;
                beginUpdateDecorations = true;
            }
        }
        if (beginUpdateDecorations) {
            this._beginUpdateDecorations();
        }
        this._originalIsEditable = validateBooleanOption(newOptions.originalEditable, this._originalIsEditable);
        this._diffCodeLens = validateBooleanOption(newOptions.diffCodeLens, this._diffCodeLens);
        this._diffWordWrap = validateDiffWordWrap(newOptions.diffWordWrap, this._diffWordWrap);
        this._modifiedEditor.updateOptions(this._adjustOptionsForRightHandSide(newOptions));
        this._originalEditor.updateOptions(this._adjustOptionsForLeftHandSide(newOptions));
        // enableSplitViewResizing
        if (typeof newOptions.enableSplitViewResizing !== 'undefined') {
            this._enableSplitViewResizing = newOptions.enableSplitViewResizing;
        }
        this._strategy.setEnableSplitViewResizing(this._enableSplitViewResizing);
        // renderSideBySide
        if (renderSideBySideChanged) {
            if (this._renderSideBySide) {
                this._setStrategy(new DiffEditorWidgetSideBySide(this._createDataSource(), this._enableSplitViewResizing));
            }
            else {
                this._setStrategy(new DiffEditorWidgetInline(this._createDataSource(), this._enableSplitViewResizing));
            }
            // Update class name
            this._containerDomElement.className = DiffEditorWidget._getClassName(this._themeService.getColorTheme(), this._renderSideBySide);
        }
        // renderOverviewRuler
        if (typeof newOptions.renderOverviewRuler !== 'undefined' && this._renderOverviewRuler !== newOptions.renderOverviewRuler) {
            this._renderOverviewRuler = newOptions.renderOverviewRuler;
            if (this._renderOverviewRuler) {
                this._containerDomElement.appendChild(this._overviewDomElement);
            }
            else {
                this._containerDomElement.removeChild(this._overviewDomElement);
            }
        }
    }
    getModel() {
        return {
            original: this._originalEditor.getModel(),
            modified: this._modifiedEditor.getModel()
        };
    }
    setModel(model) {
        // Guard us against partial null model
        if (model && (!model.original || !model.modified)) {
            throw new Error(!model.original ? 'DiffEditorWidget.setModel: Original model is null' : 'DiffEditorWidget.setModel: Modified model is null');
        }
        // Remove all view zones & decorations
        this._cleanViewZonesAndDecorations();
        // Update code editor models
        this._originalEditor.setModel(model ? model.original : null);
        this._modifiedEditor.setModel(model ? model.modified : null);
        this._updateDecorationsRunner.cancel();
        // this.originalEditor.onDidChangeModelOptions
        if (model) {
            this._originalEditor.setScrollTop(0);
            this._modifiedEditor.setScrollTop(0);
        }
        // Disable any diff computations that will come in
        this._diffComputationResult = null;
        this._diffComputationToken++;
        this._setState(0 /* Idle */);
        if (model) {
            this._recreateOverviewRulers();
            // Begin comparing
            this._beginUpdateDecorations();
        }
        this._layoutOverviewViewport();
    }
    getDomNode() {
        return this._domElement;
    }
    getVisibleColumnFromPosition(position) {
        return this._modifiedEditor.getVisibleColumnFromPosition(position);
    }
    getPosition() {
        return this._modifiedEditor.getPosition();
    }
    setPosition(position) {
        this._modifiedEditor.setPosition(position);
    }
    revealLine(lineNumber, scrollType = 0 /* Smooth */) {
        this._modifiedEditor.revealLine(lineNumber, scrollType);
    }
    revealLineInCenter(lineNumber, scrollType = 0 /* Smooth */) {
        this._modifiedEditor.revealLineInCenter(lineNumber, scrollType);
    }
    revealLineInCenterIfOutsideViewport(lineNumber, scrollType = 0 /* Smooth */) {
        this._modifiedEditor.revealLineInCenterIfOutsideViewport(lineNumber, scrollType);
    }
    revealLineNearTop(lineNumber, scrollType = 0 /* Smooth */) {
        this._modifiedEditor.revealLineNearTop(lineNumber, scrollType);
    }
    revealPosition(position, scrollType = 0 /* Smooth */) {
        this._modifiedEditor.revealPosition(position, scrollType);
    }
    revealPositionInCenter(position, scrollType = 0 /* Smooth */) {
        this._modifiedEditor.revealPositionInCenter(position, scrollType);
    }
    revealPositionInCenterIfOutsideViewport(position, scrollType = 0 /* Smooth */) {
        this._modifiedEditor.revealPositionInCenterIfOutsideViewport(position, scrollType);
    }
    revealPositionNearTop(position, scrollType = 0 /* Smooth */) {
        this._modifiedEditor.revealPositionNearTop(position, scrollType);
    }
    getSelection() {
        return this._modifiedEditor.getSelection();
    }
    getSelections() {
        return this._modifiedEditor.getSelections();
    }
    setSelection(something) {
        this._modifiedEditor.setSelection(something);
    }
    setSelections(ranges) {
        this._modifiedEditor.setSelections(ranges);
    }
    revealLines(startLineNumber, endLineNumber, scrollType = 0 /* Smooth */) {
        this._modifiedEditor.revealLines(startLineNumber, endLineNumber, scrollType);
    }
    revealLinesInCenter(startLineNumber, endLineNumber, scrollType = 0 /* Smooth */) {
        this._modifiedEditor.revealLinesInCenter(startLineNumber, endLineNumber, scrollType);
    }
    revealLinesInCenterIfOutsideViewport(startLineNumber, endLineNumber, scrollType = 0 /* Smooth */) {
        this._modifiedEditor.revealLinesInCenterIfOutsideViewport(startLineNumber, endLineNumber, scrollType);
    }
    revealLinesNearTop(startLineNumber, endLineNumber, scrollType = 0 /* Smooth */) {
        this._modifiedEditor.revealLinesNearTop(startLineNumber, endLineNumber, scrollType);
    }
    revealRange(range, scrollType = 0 /* Smooth */, revealVerticalInCenter = false, revealHorizontal = true) {
        this._modifiedEditor.revealRange(range, scrollType, revealVerticalInCenter, revealHorizontal);
    }
    revealRangeInCenter(range, scrollType = 0 /* Smooth */) {
        this._modifiedEditor.revealRangeInCenter(range, scrollType);
    }
    revealRangeInCenterIfOutsideViewport(range, scrollType = 0 /* Smooth */) {
        this._modifiedEditor.revealRangeInCenterIfOutsideViewport(range, scrollType);
    }
    revealRangeNearTop(range, scrollType = 0 /* Smooth */) {
        this._modifiedEditor.revealRangeNearTop(range, scrollType);
    }
    revealRangeNearTopIfOutsideViewport(range, scrollType = 0 /* Smooth */) {
        this._modifiedEditor.revealRangeNearTopIfOutsideViewport(range, scrollType);
    }
    revealRangeAtTop(range, scrollType = 0 /* Smooth */) {
        this._modifiedEditor.revealRangeAtTop(range, scrollType);
    }
    getSupportedActions() {
        return this._modifiedEditor.getSupportedActions();
    }
    saveViewState() {
        const originalViewState = this._originalEditor.saveViewState();
        const modifiedViewState = this._modifiedEditor.saveViewState();
        return {
            original: originalViewState,
            modified: modifiedViewState
        };
    }
    restoreViewState(s) {
        if (s && s.original && s.modified) {
            const diffEditorState = s;
            this._originalEditor.restoreViewState(diffEditorState.original);
            this._modifiedEditor.restoreViewState(diffEditorState.modified);
        }
    }
    layout(dimension) {
        this._elementSizeObserver.observe(dimension);
    }
    focus() {
        this._modifiedEditor.focus();
    }
    hasTextFocus() {
        return this._originalEditor.hasTextFocus() || this._modifiedEditor.hasTextFocus();
    }
    trigger(source, handlerId, payload) {
        this._modifiedEditor.trigger(source, handlerId, payload);
    }
    changeDecorations(callback) {
        return this._modifiedEditor.changeDecorations(callback);
    }
    //------------ end IDiffEditor methods
    //------------ begin layouting methods
    _onDidContainerSizeChanged() {
        this._doLayout();
    }
    _getReviewHeight() {
        return this._reviewPane.isVisible() ? this._elementSizeObserver.getHeight() : 0;
    }
    _layoutOverviewRulers() {
        if (!this._renderOverviewRuler) {
            return;
        }
        if (!this._originalOverviewRuler || !this._modifiedOverviewRuler) {
            return;
        }
        const height = this._elementSizeObserver.getHeight();
        const reviewHeight = this._getReviewHeight();
        const freeSpace = DiffEditorWidget.ENTIRE_DIFF_OVERVIEW_WIDTH - 2 * DiffEditorWidget.ONE_OVERVIEW_WIDTH;
        const layoutInfo = this._modifiedEditor.getLayoutInfo();
        if (layoutInfo) {
            this._originalOverviewRuler.setLayout({
                top: 0,
                width: DiffEditorWidget.ONE_OVERVIEW_WIDTH,
                right: freeSpace + DiffEditorWidget.ONE_OVERVIEW_WIDTH,
                height: (height - reviewHeight)
            });
            this._modifiedOverviewRuler.setLayout({
                top: 0,
                right: 0,
                width: DiffEditorWidget.ONE_OVERVIEW_WIDTH,
                height: (height - reviewHeight)
            });
        }
    }
    //------------ end layouting methods
    _onViewZonesChanged() {
        if (this._currentlyChangingViewZones) {
            return;
        }
        this._updateDecorationsRunner.schedule();
    }
    _beginUpdateDecorationsSoon() {
        // Clear previous timeout if necessary
        if (this._beginUpdateDecorationsTimeout !== -1) {
            window.clearTimeout(this._beginUpdateDecorationsTimeout);
            this._beginUpdateDecorationsTimeout = -1;
        }
        this._beginUpdateDecorationsTimeout = window.setTimeout(() => this._beginUpdateDecorations(), DiffEditorWidget.UPDATE_DIFF_DECORATIONS_DELAY);
    }
    static _equals(a, b) {
        if (!a && !b) {
            return true;
        }
        if (!a || !b) {
            return false;
        }
        return (a.toString() === b.toString());
    }
    _beginUpdateDecorations() {
        this._beginUpdateDecorationsTimeout = -1;
        const currentOriginalModel = this._originalEditor.getModel();
        const currentModifiedModel = this._modifiedEditor.getModel();
        if (!currentOriginalModel || !currentModifiedModel) {
            return;
        }
        // Prevent old diff requests to come if a new request has been initiated
        // The best method would be to call cancel on the Promise, but this is not
        // yet supported, so using tokens for now.
        this._diffComputationToken++;
        const currentToken = this._diffComputationToken;
        this._setState(1 /* ComputingDiff */);
        if (!this._editorWorkerService.canComputeDiff(currentOriginalModel.uri, currentModifiedModel.uri)) {
            if (!DiffEditorWidget._equals(currentOriginalModel.uri, this._lastOriginalWarning)
                || !DiffEditorWidget._equals(currentModifiedModel.uri, this._lastModifiedWarning)) {
                this._lastOriginalWarning = currentOriginalModel.uri;
                this._lastModifiedWarning = currentModifiedModel.uri;
                this._notificationService.warn(nls.localize("diff.tooLarge", "Cannot compare files because one file is too large."));
            }
            return;
        }
        this._editorWorkerService.computeDiff(currentOriginalModel.uri, currentModifiedModel.uri, this._ignoreTrimWhitespace, this._maxComputationTime).then((result) => {
            if (currentToken === this._diffComputationToken
                && currentOriginalModel === this._originalEditor.getModel()
                && currentModifiedModel === this._modifiedEditor.getModel()) {
                this._setState(2 /* DiffComputed */);
                this._diffComputationResult = result;
                this._updateDecorationsRunner.schedule();
                this._onDidUpdateDiff.fire();
            }
        }, (error) => {
            if (currentToken === this._diffComputationToken
                && currentOriginalModel === this._originalEditor.getModel()
                && currentModifiedModel === this._modifiedEditor.getModel()) {
                this._setState(2 /* DiffComputed */);
                this._diffComputationResult = null;
                this._updateDecorationsRunner.schedule();
            }
        });
    }
    _cleanViewZonesAndDecorations() {
        this._originalEditorState.clean(this._originalEditor);
        this._modifiedEditorState.clean(this._modifiedEditor);
    }
    _updateDecorations() {
        if (!this._originalEditor.getModel() || !this._modifiedEditor.getModel()) {
            return;
        }
        const lineChanges = (this._diffComputationResult ? this._diffComputationResult.changes : []);
        const foreignOriginal = this._originalEditorState.getForeignViewZones(this._originalEditor.getWhitespaces());
        const foreignModified = this._modifiedEditorState.getForeignViewZones(this._modifiedEditor.getWhitespaces());
        const diffDecorations = this._strategy.getEditorsDiffDecorations(lineChanges, this._ignoreTrimWhitespace, this._renderIndicators, foreignOriginal, foreignModified);
        try {
            this._currentlyChangingViewZones = true;
            this._originalEditorState.apply(this._originalEditor, this._originalOverviewRuler, diffDecorations.original, false);
            this._modifiedEditorState.apply(this._modifiedEditor, this._modifiedOverviewRuler, diffDecorations.modified, true);
        }
        finally {
            this._currentlyChangingViewZones = false;
        }
    }
    _adjustOptionsForSubEditor(options) {
        const clonedOptions = Object.assign({}, options);
        clonedOptions.inDiffEditor = true;
        clonedOptions.automaticLayout = false;
        clonedOptions.scrollbar = clonedOptions.scrollbar || {};
        clonedOptions.scrollbar.vertical = 'visible';
        clonedOptions.folding = false;
        clonedOptions.codeLens = this._diffCodeLens;
        clonedOptions.fixedOverflowWidgets = true;
        // clonedOptions.lineDecorationsWidth = '2ch';
        if (!clonedOptions.minimap) {
            clonedOptions.minimap = {};
        }
        clonedOptions.minimap.enabled = false;
        return clonedOptions;
    }
    _adjustOptionsForLeftHandSide(options) {
        const result = this._adjustOptionsForSubEditor(options);
        if (!this._renderSideBySide) {
            // never wrap hidden editor
            result.wordWrapOverride1 = 'off';
        }
        else {
            result.wordWrapOverride1 = this._diffWordWrap;
        }
        result.readOnly = !this._originalIsEditable;
        result.extraEditorClassName = 'original-in-monaco-diff-editor';
        return Object.assign(Object.assign({}, result), { dimension: {
                height: 0,
                width: 0
            } });
    }
    _adjustOptionsForRightHandSide(options) {
        const result = this._adjustOptionsForSubEditor(options);
        result.wordWrapOverride1 = this._diffWordWrap;
        result.revealHorizontalRightPadding = EditorOptions.revealHorizontalRightPadding.defaultValue + DiffEditorWidget.ENTIRE_DIFF_OVERVIEW_WIDTH;
        result.scrollbar.verticalHasArrows = false;
        result.extraEditorClassName = 'modified-in-monaco-diff-editor';
        return Object.assign(Object.assign({}, result), { dimension: {
                height: 0,
                width: 0
            } });
    }
    doLayout() {
        this._elementSizeObserver.observe();
        this._doLayout();
    }
    _doLayout() {
        const width = this._elementSizeObserver.getWidth();
        const height = this._elementSizeObserver.getHeight();
        const reviewHeight = this._getReviewHeight();
        const splitPoint = this._strategy.layout();
        this._originalDomNode.style.width = splitPoint + 'px';
        this._originalDomNode.style.left = '0px';
        this._modifiedDomNode.style.width = (width - splitPoint) + 'px';
        this._modifiedDomNode.style.left = splitPoint + 'px';
        this._overviewDomElement.style.top = '0px';
        this._overviewDomElement.style.height = (height - reviewHeight) + 'px';
        this._overviewDomElement.style.width = DiffEditorWidget.ENTIRE_DIFF_OVERVIEW_WIDTH + 'px';
        this._overviewDomElement.style.left = (width - DiffEditorWidget.ENTIRE_DIFF_OVERVIEW_WIDTH) + 'px';
        this._overviewViewportDomElement.setWidth(DiffEditorWidget.ENTIRE_DIFF_OVERVIEW_WIDTH);
        this._overviewViewportDomElement.setHeight(30);
        this._originalEditor.layout({ width: splitPoint, height: (height - reviewHeight) });
        this._modifiedEditor.layout({ width: width - splitPoint - (this._renderOverviewRuler ? DiffEditorWidget.ENTIRE_DIFF_OVERVIEW_WIDTH : 0), height: (height - reviewHeight) });
        if (this._originalOverviewRuler || this._modifiedOverviewRuler) {
            this._layoutOverviewRulers();
        }
        this._reviewPane.layout(height - reviewHeight, width, reviewHeight);
        this._layoutOverviewViewport();
    }
    _layoutOverviewViewport() {
        const layout = this._computeOverviewViewport();
        if (!layout) {
            this._overviewViewportDomElement.setTop(0);
            this._overviewViewportDomElement.setHeight(0);
        }
        else {
            this._overviewViewportDomElement.setTop(layout.top);
            this._overviewViewportDomElement.setHeight(layout.height);
        }
    }
    _computeOverviewViewport() {
        const layoutInfo = this._modifiedEditor.getLayoutInfo();
        if (!layoutInfo) {
            return null;
        }
        const scrollTop = this._modifiedEditor.getScrollTop();
        const scrollHeight = this._modifiedEditor.getScrollHeight();
        const computedAvailableSize = Math.max(0, layoutInfo.height);
        const computedRepresentableSize = Math.max(0, computedAvailableSize - 2 * 0);
        const computedRatio = scrollHeight > 0 ? (computedRepresentableSize / scrollHeight) : 0;
        const computedSliderSize = Math.max(0, Math.floor(layoutInfo.height * computedRatio));
        const computedSliderPosition = Math.floor(scrollTop * computedRatio);
        return {
            height: computedSliderSize,
            top: computedSliderPosition
        };
    }
    _createDataSource() {
        return {
            getWidth: () => {
                return this._elementSizeObserver.getWidth();
            },
            getHeight: () => {
                return (this._elementSizeObserver.getHeight() - this._getReviewHeight());
            },
            getOptions: () => {
                return {
                    renderOverviewRuler: this._renderOverviewRuler
                };
            },
            getContainerDomNode: () => {
                return this._containerDomElement;
            },
            relayoutEditors: () => {
                this._doLayout();
            },
            getOriginalEditor: () => {
                return this._originalEditor;
            },
            getModifiedEditor: () => {
                return this._modifiedEditor;
            }
        };
    }
    _setStrategy(newStrategy) {
        if (this._strategy) {
            this._strategy.dispose();
        }
        this._strategy = newStrategy;
        newStrategy.applyColors(this._themeService.getColorTheme());
        if (this._diffComputationResult) {
            this._updateDecorations();
        }
        // Just do a layout, the strategy might need it
        this._doLayout();
    }
    _getLineChangeAtOrBeforeLineNumber(lineNumber, startLineNumberExtractor) {
        const lineChanges = (this._diffComputationResult ? this._diffComputationResult.changes : []);
        if (lineChanges.length === 0 || lineNumber < startLineNumberExtractor(lineChanges[0])) {
            // There are no changes or `lineNumber` is before the first change
            return null;
        }
        let min = 0;
        let max = lineChanges.length - 1;
        while (min < max) {
            const mid = Math.floor((min + max) / 2);
            const midStart = startLineNumberExtractor(lineChanges[mid]);
            const midEnd = (mid + 1 <= max ? startLineNumberExtractor(lineChanges[mid + 1]) : 1073741824 /* MAX_SAFE_SMALL_INTEGER */);
            if (lineNumber < midStart) {
                max = mid - 1;
            }
            else if (lineNumber >= midEnd) {
                min = mid + 1;
            }
            else {
                // HIT!
                min = mid;
                max = mid;
            }
        }
        return lineChanges[min];
    }
    _getEquivalentLineForOriginalLineNumber(lineNumber) {
        const lineChange = this._getLineChangeAtOrBeforeLineNumber(lineNumber, (lineChange) => lineChange.originalStartLineNumber);
        if (!lineChange) {
            return lineNumber;
        }
        const originalEquivalentLineNumber = lineChange.originalStartLineNumber + (lineChange.originalEndLineNumber > 0 ? -1 : 0);
        const modifiedEquivalentLineNumber = lineChange.modifiedStartLineNumber + (lineChange.modifiedEndLineNumber > 0 ? -1 : 0);
        const lineChangeOriginalLength = (lineChange.originalEndLineNumber > 0 ? (lineChange.originalEndLineNumber - lineChange.originalStartLineNumber + 1) : 0);
        const lineChangeModifiedLength = (lineChange.modifiedEndLineNumber > 0 ? (lineChange.modifiedEndLineNumber - lineChange.modifiedStartLineNumber + 1) : 0);
        const delta = lineNumber - originalEquivalentLineNumber;
        if (delta <= lineChangeOriginalLength) {
            return modifiedEquivalentLineNumber + Math.min(delta, lineChangeModifiedLength);
        }
        return modifiedEquivalentLineNumber + lineChangeModifiedLength - lineChangeOriginalLength + delta;
    }
    _getEquivalentLineForModifiedLineNumber(lineNumber) {
        const lineChange = this._getLineChangeAtOrBeforeLineNumber(lineNumber, (lineChange) => lineChange.modifiedStartLineNumber);
        if (!lineChange) {
            return lineNumber;
        }
        const originalEquivalentLineNumber = lineChange.originalStartLineNumber + (lineChange.originalEndLineNumber > 0 ? -1 : 0);
        const modifiedEquivalentLineNumber = lineChange.modifiedStartLineNumber + (lineChange.modifiedEndLineNumber > 0 ? -1 : 0);
        const lineChangeOriginalLength = (lineChange.originalEndLineNumber > 0 ? (lineChange.originalEndLineNumber - lineChange.originalStartLineNumber + 1) : 0);
        const lineChangeModifiedLength = (lineChange.modifiedEndLineNumber > 0 ? (lineChange.modifiedEndLineNumber - lineChange.modifiedStartLineNumber + 1) : 0);
        const delta = lineNumber - modifiedEquivalentLineNumber;
        if (delta <= lineChangeModifiedLength) {
            return originalEquivalentLineNumber + Math.min(delta, lineChangeOriginalLength);
        }
        return originalEquivalentLineNumber + lineChangeOriginalLength - lineChangeModifiedLength + delta;
    }
    getDiffLineInformationForOriginal(lineNumber) {
        if (!this._diffComputationResult) {
            // Cannot answer that which I don't know
            return null;
        }
        return {
            equivalentLineNumber: this._getEquivalentLineForOriginalLineNumber(lineNumber)
        };
    }
    getDiffLineInformationForModified(lineNumber) {
        if (!this._diffComputationResult) {
            // Cannot answer that which I don't know
            return null;
        }
        return {
            equivalentLineNumber: this._getEquivalentLineForModifiedLineNumber(lineNumber)
        };
    }
};
DiffEditorWidget.ONE_OVERVIEW_WIDTH = 15;
DiffEditorWidget.ENTIRE_DIFF_OVERVIEW_WIDTH = 30;
DiffEditorWidget.UPDATE_DIFF_DECORATIONS_DELAY = 200; // ms
DiffEditorWidget = __decorate([
    __param(3, IClipboardService),
    __param(4, IEditorWorkerService),
    __param(5, IContextKeyService),
    __param(6, IInstantiationService),
    __param(7, ICodeEditorService),
    __param(8, IThemeService),
    __param(9, INotificationService),
    __param(10, IContextMenuService),
    __param(11, IEditorProgressService)
], DiffEditorWidget);
export { DiffEditorWidget };
class DiffEditorWidgetStyle extends Disposable {
    constructor(dataSource) {
        super();
        this._dataSource = dataSource;
        this._insertColor = null;
        this._removeColor = null;
    }
    applyColors(theme) {
        const newInsertColor = (theme.getColor(diffInserted) || defaultInsertColor).transparent(2);
        const newRemoveColor = (theme.getColor(diffRemoved) || defaultRemoveColor).transparent(2);
        const hasChanges = !newInsertColor.equals(this._insertColor) || !newRemoveColor.equals(this._removeColor);
        this._insertColor = newInsertColor;
        this._removeColor = newRemoveColor;
        return hasChanges;
    }
    getEditorsDiffDecorations(lineChanges, ignoreTrimWhitespace, renderIndicators, originalWhitespaces, modifiedWhitespaces) {
        // Get view zones
        modifiedWhitespaces = modifiedWhitespaces.sort((a, b) => {
            return a.afterLineNumber - b.afterLineNumber;
        });
        originalWhitespaces = originalWhitespaces.sort((a, b) => {
            return a.afterLineNumber - b.afterLineNumber;
        });
        const zones = this._getViewZones(lineChanges, originalWhitespaces, modifiedWhitespaces, renderIndicators);
        // Get decorations & overview ruler zones
        const originalDecorations = this._getOriginalEditorDecorations(lineChanges, ignoreTrimWhitespace, renderIndicators);
        const modifiedDecorations = this._getModifiedEditorDecorations(lineChanges, ignoreTrimWhitespace, renderIndicators);
        return {
            original: {
                decorations: originalDecorations.decorations,
                overviewZones: originalDecorations.overviewZones,
                zones: zones.original
            },
            modified: {
                decorations: modifiedDecorations.decorations,
                overviewZones: modifiedDecorations.overviewZones,
                zones: zones.modified
            }
        };
    }
}
class ForeignViewZonesIterator {
    constructor(source) {
        this._source = source;
        this._index = -1;
        this.current = null;
        this.advance();
    }
    advance() {
        this._index++;
        if (this._index < this._source.length) {
            this.current = this._source[this._index];
        }
        else {
            this.current = null;
        }
    }
}
class ViewZonesComputer {
    constructor(_lineChanges, _originalForeignVZ, _modifiedForeignVZ, _originalEditor, _modifiedEditor) {
        this._lineChanges = _lineChanges;
        this._originalForeignVZ = _originalForeignVZ;
        this._modifiedForeignVZ = _modifiedForeignVZ;
        this._originalEditor = _originalEditor;
        this._modifiedEditor = _modifiedEditor;
    }
    static _getViewLineCount(editor, startLineNumber, endLineNumber) {
        const model = editor.getModel();
        const viewModel = editor._getViewModel();
        if (model && viewModel) {
            const viewRange = getViewRange(model, viewModel, startLineNumber, endLineNumber);
            return (viewRange.endLineNumber - viewRange.startLineNumber + 1);
        }
        return (endLineNumber - startLineNumber + 1);
    }
    getViewZones() {
        const originalLineHeight = this._originalEditor.getOption(53 /* lineHeight */);
        const modifiedLineHeight = this._modifiedEditor.getOption(53 /* lineHeight */);
        const originalHasWrapping = (this._originalEditor.getOption(125 /* wrappingInfo */).wrappingColumn !== -1);
        const modifiedHasWrapping = (this._modifiedEditor.getOption(125 /* wrappingInfo */).wrappingColumn !== -1);
        const hasWrapping = (originalHasWrapping || modifiedHasWrapping);
        const originalModel = this._originalEditor.getModel();
        const originalCoordinatesConverter = this._originalEditor._getViewModel().coordinatesConverter;
        const modifiedCoordinatesConverter = this._modifiedEditor._getViewModel().coordinatesConverter;
        const result = {
            original: [],
            modified: []
        };
        let lineChangeModifiedLength = 0;
        let lineChangeOriginalLength = 0;
        let originalEquivalentLineNumber = 0;
        let modifiedEquivalentLineNumber = 0;
        let originalEndEquivalentLineNumber = 0;
        let modifiedEndEquivalentLineNumber = 0;
        const sortMyViewZones = (a, b) => {
            return a.afterLineNumber - b.afterLineNumber;
        };
        const addAndCombineIfPossible = (destination, item) => {
            if (item.domNode === null && destination.length > 0) {
                const lastItem = destination[destination.length - 1];
                if (lastItem.afterLineNumber === item.afterLineNumber && lastItem.domNode === null) {
                    lastItem.heightInLines += item.heightInLines;
                    return;
                }
            }
            destination.push(item);
        };
        const modifiedForeignVZ = new ForeignViewZonesIterator(this._modifiedForeignVZ);
        const originalForeignVZ = new ForeignViewZonesIterator(this._originalForeignVZ);
        let lastOriginalLineNumber = 1;
        let lastModifiedLineNumber = 1;
        // In order to include foreign view zones after the last line change, the for loop will iterate once more after the end of the `lineChanges` array
        for (let i = 0, length = this._lineChanges.length; i <= length; i++) {
            const lineChange = (i < length ? this._lineChanges[i] : null);
            if (lineChange !== null) {
                originalEquivalentLineNumber = lineChange.originalStartLineNumber + (lineChange.originalEndLineNumber > 0 ? -1 : 0);
                modifiedEquivalentLineNumber = lineChange.modifiedStartLineNumber + (lineChange.modifiedEndLineNumber > 0 ? -1 : 0);
                lineChangeOriginalLength = (lineChange.originalEndLineNumber > 0 ? ViewZonesComputer._getViewLineCount(this._originalEditor, lineChange.originalStartLineNumber, lineChange.originalEndLineNumber) : 0);
                lineChangeModifiedLength = (lineChange.modifiedEndLineNumber > 0 ? ViewZonesComputer._getViewLineCount(this._modifiedEditor, lineChange.modifiedStartLineNumber, lineChange.modifiedEndLineNumber) : 0);
                originalEndEquivalentLineNumber = Math.max(lineChange.originalStartLineNumber, lineChange.originalEndLineNumber);
                modifiedEndEquivalentLineNumber = Math.max(lineChange.modifiedStartLineNumber, lineChange.modifiedEndLineNumber);
            }
            else {
                // Increase to very large value to get the producing tests of foreign view zones running
                originalEquivalentLineNumber += 10000000 + lineChangeOriginalLength;
                modifiedEquivalentLineNumber += 10000000 + lineChangeModifiedLength;
                originalEndEquivalentLineNumber = originalEquivalentLineNumber;
                modifiedEndEquivalentLineNumber = modifiedEquivalentLineNumber;
            }
            // Each step produces view zones, and after producing them, we try to cancel them out, to avoid empty-empty view zone cases
            let stepOriginal = [];
            let stepModified = [];
            // ---------------------------- PRODUCE VIEW ZONES
            // [PRODUCE] View zones due to line mapping differences (equal lines but wrapped differently)
            if (hasWrapping) {
                let count;
                if (lineChange) {
                    if (lineChange.originalEndLineNumber > 0) {
                        count = lineChange.originalStartLineNumber - lastOriginalLineNumber;
                    }
                    else {
                        count = lineChange.modifiedStartLineNumber - lastModifiedLineNumber;
                    }
                }
                else {
                    count = originalModel.getLineCount() - lastOriginalLineNumber;
                }
                for (let i = 0; i < count; i++) {
                    const originalLineNumber = lastOriginalLineNumber + i;
                    const modifiedLineNumber = lastModifiedLineNumber + i;
                    const originalViewLineCount = originalCoordinatesConverter.getModelLineViewLineCount(originalLineNumber);
                    const modifiedViewLineCount = modifiedCoordinatesConverter.getModelLineViewLineCount(modifiedLineNumber);
                    if (originalViewLineCount < modifiedViewLineCount) {
                        stepOriginal.push({
                            afterLineNumber: originalLineNumber,
                            heightInLines: modifiedViewLineCount - originalViewLineCount,
                            domNode: null,
                            marginDomNode: null
                        });
                    }
                    else if (originalViewLineCount > modifiedViewLineCount) {
                        stepModified.push({
                            afterLineNumber: modifiedLineNumber,
                            heightInLines: originalViewLineCount - modifiedViewLineCount,
                            domNode: null,
                            marginDomNode: null
                        });
                    }
                }
                if (lineChange) {
                    lastOriginalLineNumber = (lineChange.originalEndLineNumber > 0 ? lineChange.originalEndLineNumber : lineChange.originalStartLineNumber) + 1;
                    lastModifiedLineNumber = (lineChange.modifiedEndLineNumber > 0 ? lineChange.modifiedEndLineNumber : lineChange.modifiedStartLineNumber) + 1;
                }
            }
            // [PRODUCE] View zone(s) in original-side due to foreign view zone(s) in modified-side
            while (modifiedForeignVZ.current && modifiedForeignVZ.current.afterLineNumber <= modifiedEndEquivalentLineNumber) {
                let viewZoneLineNumber;
                if (modifiedForeignVZ.current.afterLineNumber <= modifiedEquivalentLineNumber) {
                    viewZoneLineNumber = originalEquivalentLineNumber - modifiedEquivalentLineNumber + modifiedForeignVZ.current.afterLineNumber;
                }
                else {
                    viewZoneLineNumber = originalEndEquivalentLineNumber;
                }
                let marginDomNode = null;
                if (lineChange && lineChange.modifiedStartLineNumber <= modifiedForeignVZ.current.afterLineNumber && modifiedForeignVZ.current.afterLineNumber <= lineChange.modifiedEndLineNumber) {
                    marginDomNode = this._createOriginalMarginDomNodeForModifiedForeignViewZoneInAddedRegion();
                }
                stepOriginal.push({
                    afterLineNumber: viewZoneLineNumber,
                    heightInLines: modifiedForeignVZ.current.height / modifiedLineHeight,
                    domNode: null,
                    marginDomNode: marginDomNode
                });
                modifiedForeignVZ.advance();
            }
            // [PRODUCE] View zone(s) in modified-side due to foreign view zone(s) in original-side
            while (originalForeignVZ.current && originalForeignVZ.current.afterLineNumber <= originalEndEquivalentLineNumber) {
                let viewZoneLineNumber;
                if (originalForeignVZ.current.afterLineNumber <= originalEquivalentLineNumber) {
                    viewZoneLineNumber = modifiedEquivalentLineNumber - originalEquivalentLineNumber + originalForeignVZ.current.afterLineNumber;
                }
                else {
                    viewZoneLineNumber = modifiedEndEquivalentLineNumber;
                }
                stepModified.push({
                    afterLineNumber: viewZoneLineNumber,
                    heightInLines: originalForeignVZ.current.height / originalLineHeight,
                    domNode: null
                });
                originalForeignVZ.advance();
            }
            if (lineChange !== null && isChangeOrInsert(lineChange)) {
                const r = this._produceOriginalFromDiff(lineChange, lineChangeOriginalLength, lineChangeModifiedLength);
                if (r) {
                    stepOriginal.push(r);
                }
            }
            if (lineChange !== null && isChangeOrDelete(lineChange)) {
                const r = this._produceModifiedFromDiff(lineChange, lineChangeOriginalLength, lineChangeModifiedLength);
                if (r) {
                    stepModified.push(r);
                }
            }
            // ---------------------------- END PRODUCE VIEW ZONES
            // ---------------------------- EMIT MINIMAL VIEW ZONES
            // [CANCEL & EMIT] Try to cancel view zones out
            let stepOriginalIndex = 0;
            let stepModifiedIndex = 0;
            stepOriginal = stepOriginal.sort(sortMyViewZones);
            stepModified = stepModified.sort(sortMyViewZones);
            while (stepOriginalIndex < stepOriginal.length && stepModifiedIndex < stepModified.length) {
                const original = stepOriginal[stepOriginalIndex];
                const modified = stepModified[stepModifiedIndex];
                const originalDelta = original.afterLineNumber - originalEquivalentLineNumber;
                const modifiedDelta = modified.afterLineNumber - modifiedEquivalentLineNumber;
                if (originalDelta < modifiedDelta) {
                    addAndCombineIfPossible(result.original, original);
                    stepOriginalIndex++;
                }
                else if (modifiedDelta < originalDelta) {
                    addAndCombineIfPossible(result.modified, modified);
                    stepModifiedIndex++;
                }
                else if (original.shouldNotShrink) {
                    addAndCombineIfPossible(result.original, original);
                    stepOriginalIndex++;
                }
                else if (modified.shouldNotShrink) {
                    addAndCombineIfPossible(result.modified, modified);
                    stepModifiedIndex++;
                }
                else {
                    if (original.heightInLines >= modified.heightInLines) {
                        // modified view zone gets removed
                        original.heightInLines -= modified.heightInLines;
                        stepModifiedIndex++;
                    }
                    else {
                        // original view zone gets removed
                        modified.heightInLines -= original.heightInLines;
                        stepOriginalIndex++;
                    }
                }
            }
            // [EMIT] Remaining original view zones
            while (stepOriginalIndex < stepOriginal.length) {
                addAndCombineIfPossible(result.original, stepOriginal[stepOriginalIndex]);
                stepOriginalIndex++;
            }
            // [EMIT] Remaining modified view zones
            while (stepModifiedIndex < stepModified.length) {
                addAndCombineIfPossible(result.modified, stepModified[stepModifiedIndex]);
                stepModifiedIndex++;
            }
            // ---------------------------- END EMIT MINIMAL VIEW ZONES
        }
        return {
            original: ViewZonesComputer._ensureDomNodes(result.original),
            modified: ViewZonesComputer._ensureDomNodes(result.modified),
        };
    }
    static _ensureDomNodes(zones) {
        return zones.map((z) => {
            if (!z.domNode) {
                z.domNode = createFakeLinesDiv();
            }
            return z;
        });
    }
}
function createDecoration(startLineNumber, startColumn, endLineNumber, endColumn, options) {
    return {
        range: new Range(startLineNumber, startColumn, endLineNumber, endColumn),
        options: options
    };
}
const DECORATIONS = {
    charDelete: ModelDecorationOptions.register({
        className: 'char-delete'
    }),
    charDeleteWholeLine: ModelDecorationOptions.register({
        className: 'char-delete',
        isWholeLine: true
    }),
    charInsert: ModelDecorationOptions.register({
        className: 'char-insert'
    }),
    charInsertWholeLine: ModelDecorationOptions.register({
        className: 'char-insert',
        isWholeLine: true
    }),
    lineInsert: ModelDecorationOptions.register({
        className: 'line-insert',
        marginClassName: 'line-insert',
        isWholeLine: true
    }),
    lineInsertWithSign: ModelDecorationOptions.register({
        className: 'line-insert',
        linesDecorationsClassName: 'insert-sign ' + ThemeIcon.asClassName(diffInsertIcon),
        marginClassName: 'line-insert',
        isWholeLine: true
    }),
    lineDelete: ModelDecorationOptions.register({
        className: 'line-delete',
        marginClassName: 'line-delete',
        isWholeLine: true
    }),
    lineDeleteWithSign: ModelDecorationOptions.register({
        className: 'line-delete',
        linesDecorationsClassName: 'delete-sign ' + ThemeIcon.asClassName(diffRemoveIcon),
        marginClassName: 'line-delete',
        isWholeLine: true
    }),
    lineDeleteMargin: ModelDecorationOptions.register({
        marginClassName: 'line-delete',
    })
};
class DiffEditorWidgetSideBySide extends DiffEditorWidgetStyle {
    constructor(dataSource, enableSplitViewResizing) {
        super(dataSource);
        this._disableSash = (enableSplitViewResizing === false);
        this._sashRatio = null;
        this._sashPosition = null;
        this._startSashPosition = null;
        this._sash = this._register(new Sash(this._dataSource.getContainerDomNode(), this, { orientation: 0 /* VERTICAL */ }));
        if (this._disableSash) {
            this._sash.state = 0 /* Disabled */;
        }
        this._sash.onDidStart(() => this._onSashDragStart());
        this._sash.onDidChange((e) => this._onSashDrag(e));
        this._sash.onDidEnd(() => this._onSashDragEnd());
        this._sash.onDidReset(() => this._onSashReset());
    }
    setEnableSplitViewResizing(enableSplitViewResizing) {
        const newDisableSash = (enableSplitViewResizing === false);
        if (this._disableSash !== newDisableSash) {
            this._disableSash = newDisableSash;
            this._sash.state = this._disableSash ? 0 /* Disabled */ : 3 /* Enabled */;
        }
    }
    layout(sashRatio = this._sashRatio) {
        const w = this._dataSource.getWidth();
        const contentWidth = w - (this._dataSource.getOptions().renderOverviewRuler ? DiffEditorWidget.ENTIRE_DIFF_OVERVIEW_WIDTH : 0);
        let sashPosition = Math.floor((sashRatio || 0.5) * contentWidth);
        const midPoint = Math.floor(0.5 * contentWidth);
        sashPosition = this._disableSash ? midPoint : sashPosition || midPoint;
        if (contentWidth > DiffEditorWidgetSideBySide.MINIMUM_EDITOR_WIDTH * 2) {
            if (sashPosition < DiffEditorWidgetSideBySide.MINIMUM_EDITOR_WIDTH) {
                sashPosition = DiffEditorWidgetSideBySide.MINIMUM_EDITOR_WIDTH;
            }
            if (sashPosition > contentWidth - DiffEditorWidgetSideBySide.MINIMUM_EDITOR_WIDTH) {
                sashPosition = contentWidth - DiffEditorWidgetSideBySide.MINIMUM_EDITOR_WIDTH;
            }
        }
        else {
            sashPosition = midPoint;
        }
        if (this._sashPosition !== sashPosition) {
            this._sashPosition = sashPosition;
            this._sash.layout();
        }
        return this._sashPosition;
    }
    _onSashDragStart() {
        this._startSashPosition = this._sashPosition;
    }
    _onSashDrag(e) {
        const w = this._dataSource.getWidth();
        const contentWidth = w - (this._dataSource.getOptions().renderOverviewRuler ? DiffEditorWidget.ENTIRE_DIFF_OVERVIEW_WIDTH : 0);
        const sashPosition = this.layout((this._startSashPosition + (e.currentX - e.startX)) / contentWidth);
        this._sashRatio = sashPosition / contentWidth;
        this._dataSource.relayoutEditors();
    }
    _onSashDragEnd() {
        this._sash.layout();
    }
    _onSashReset() {
        this._sashRatio = 0.5;
        this._dataSource.relayoutEditors();
        this._sash.layout();
    }
    getVerticalSashTop(sash) {
        return 0;
    }
    getVerticalSashLeft(sash) {
        return this._sashPosition;
    }
    getVerticalSashHeight(sash) {
        return this._dataSource.getHeight();
    }
    _getViewZones(lineChanges, originalForeignVZ, modifiedForeignVZ) {
        const originalEditor = this._dataSource.getOriginalEditor();
        const modifiedEditor = this._dataSource.getModifiedEditor();
        const c = new SideBySideViewZonesComputer(lineChanges, originalForeignVZ, modifiedForeignVZ, originalEditor, modifiedEditor);
        return c.getViewZones();
    }
    _getOriginalEditorDecorations(lineChanges, ignoreTrimWhitespace, renderIndicators) {
        const originalEditor = this._dataSource.getOriginalEditor();
        const overviewZoneColor = String(this._removeColor);
        const result = {
            decorations: [],
            overviewZones: []
        };
        const originalModel = originalEditor.getModel();
        const originalViewModel = originalEditor._getViewModel();
        for (const lineChange of lineChanges) {
            if (isChangeOrDelete(lineChange)) {
                result.decorations.push({
                    range: new Range(lineChange.originalStartLineNumber, 1, lineChange.originalEndLineNumber, 1073741824 /* MAX_SAFE_SMALL_INTEGER */),
                    options: (renderIndicators ? DECORATIONS.lineDeleteWithSign : DECORATIONS.lineDelete)
                });
                if (!isChangeOrInsert(lineChange) || !lineChange.charChanges) {
                    result.decorations.push(createDecoration(lineChange.originalStartLineNumber, 1, lineChange.originalEndLineNumber, 1073741824 /* MAX_SAFE_SMALL_INTEGER */, DECORATIONS.charDeleteWholeLine));
                }
                const viewRange = getViewRange(originalModel, originalViewModel, lineChange.originalStartLineNumber, lineChange.originalEndLineNumber);
                result.overviewZones.push(new OverviewRulerZone(viewRange.startLineNumber, viewRange.endLineNumber, overviewZoneColor));
                if (lineChange.charChanges) {
                    for (const charChange of lineChange.charChanges) {
                        if (isChangeOrDelete(charChange)) {
                            if (ignoreTrimWhitespace) {
                                for (let lineNumber = charChange.originalStartLineNumber; lineNumber <= charChange.originalEndLineNumber; lineNumber++) {
                                    let startColumn;
                                    let endColumn;
                                    if (lineNumber === charChange.originalStartLineNumber) {
                                        startColumn = charChange.originalStartColumn;
                                    }
                                    else {
                                        startColumn = originalModel.getLineFirstNonWhitespaceColumn(lineNumber);
                                    }
                                    if (lineNumber === charChange.originalEndLineNumber) {
                                        endColumn = charChange.originalEndColumn;
                                    }
                                    else {
                                        endColumn = originalModel.getLineLastNonWhitespaceColumn(lineNumber);
                                    }
                                    result.decorations.push(createDecoration(lineNumber, startColumn, lineNumber, endColumn, DECORATIONS.charDelete));
                                }
                            }
                            else {
                                result.decorations.push(createDecoration(charChange.originalStartLineNumber, charChange.originalStartColumn, charChange.originalEndLineNumber, charChange.originalEndColumn, DECORATIONS.charDelete));
                            }
                        }
                    }
                }
            }
        }
        return result;
    }
    _getModifiedEditorDecorations(lineChanges, ignoreTrimWhitespace, renderIndicators) {
        const modifiedEditor = this._dataSource.getModifiedEditor();
        const overviewZoneColor = String(this._insertColor);
        const result = {
            decorations: [],
            overviewZones: []
        };
        const modifiedModel = modifiedEditor.getModel();
        const modifiedViewModel = modifiedEditor._getViewModel();
        for (const lineChange of lineChanges) {
            if (isChangeOrInsert(lineChange)) {
                result.decorations.push({
                    range: new Range(lineChange.modifiedStartLineNumber, 1, lineChange.modifiedEndLineNumber, 1073741824 /* MAX_SAFE_SMALL_INTEGER */),
                    options: (renderIndicators ? DECORATIONS.lineInsertWithSign : DECORATIONS.lineInsert)
                });
                if (!isChangeOrDelete(lineChange) || !lineChange.charChanges) {
                    result.decorations.push(createDecoration(lineChange.modifiedStartLineNumber, 1, lineChange.modifiedEndLineNumber, 1073741824 /* MAX_SAFE_SMALL_INTEGER */, DECORATIONS.charInsertWholeLine));
                }
                const viewRange = getViewRange(modifiedModel, modifiedViewModel, lineChange.modifiedStartLineNumber, lineChange.modifiedEndLineNumber);
                result.overviewZones.push(new OverviewRulerZone(viewRange.startLineNumber, viewRange.endLineNumber, overviewZoneColor));
                if (lineChange.charChanges) {
                    for (const charChange of lineChange.charChanges) {
                        if (isChangeOrInsert(charChange)) {
                            if (ignoreTrimWhitespace) {
                                for (let lineNumber = charChange.modifiedStartLineNumber; lineNumber <= charChange.modifiedEndLineNumber; lineNumber++) {
                                    let startColumn;
                                    let endColumn;
                                    if (lineNumber === charChange.modifiedStartLineNumber) {
                                        startColumn = charChange.modifiedStartColumn;
                                    }
                                    else {
                                        startColumn = modifiedModel.getLineFirstNonWhitespaceColumn(lineNumber);
                                    }
                                    if (lineNumber === charChange.modifiedEndLineNumber) {
                                        endColumn = charChange.modifiedEndColumn;
                                    }
                                    else {
                                        endColumn = modifiedModel.getLineLastNonWhitespaceColumn(lineNumber);
                                    }
                                    result.decorations.push(createDecoration(lineNumber, startColumn, lineNumber, endColumn, DECORATIONS.charInsert));
                                }
                            }
                            else {
                                result.decorations.push(createDecoration(charChange.modifiedStartLineNumber, charChange.modifiedStartColumn, charChange.modifiedEndLineNumber, charChange.modifiedEndColumn, DECORATIONS.charInsert));
                            }
                        }
                    }
                }
            }
        }
        return result;
    }
}
DiffEditorWidgetSideBySide.MINIMUM_EDITOR_WIDTH = 100;
class SideBySideViewZonesComputer extends ViewZonesComputer {
    constructor(lineChanges, originalForeignVZ, modifiedForeignVZ, originalEditor, modifiedEditor) {
        super(lineChanges, originalForeignVZ, modifiedForeignVZ, originalEditor, modifiedEditor);
    }
    _createOriginalMarginDomNodeForModifiedForeignViewZoneInAddedRegion() {
        return null;
    }
    _produceOriginalFromDiff(lineChange, lineChangeOriginalLength, lineChangeModifiedLength) {
        if (lineChangeModifiedLength > lineChangeOriginalLength) {
            return {
                afterLineNumber: Math.max(lineChange.originalStartLineNumber, lineChange.originalEndLineNumber),
                heightInLines: (lineChangeModifiedLength - lineChangeOriginalLength),
                domNode: null
            };
        }
        return null;
    }
    _produceModifiedFromDiff(lineChange, lineChangeOriginalLength, lineChangeModifiedLength) {
        if (lineChangeOriginalLength > lineChangeModifiedLength) {
            return {
                afterLineNumber: Math.max(lineChange.modifiedStartLineNumber, lineChange.modifiedEndLineNumber),
                heightInLines: (lineChangeOriginalLength - lineChangeModifiedLength),
                domNode: null
            };
        }
        return null;
    }
}
class DiffEditorWidgetInline extends DiffEditorWidgetStyle {
    constructor(dataSource, enableSplitViewResizing) {
        super(dataSource);
        this._decorationsLeft = dataSource.getOriginalEditor().getLayoutInfo().decorationsLeft;
        this._register(dataSource.getOriginalEditor().onDidLayoutChange((layoutInfo) => {
            if (this._decorationsLeft !== layoutInfo.decorationsLeft) {
                this._decorationsLeft = layoutInfo.decorationsLeft;
                dataSource.relayoutEditors();
            }
        }));
    }
    setEnableSplitViewResizing(enableSplitViewResizing) {
        // Nothing to do..
    }
    _getViewZones(lineChanges, originalForeignVZ, modifiedForeignVZ, renderIndicators) {
        const originalEditor = this._dataSource.getOriginalEditor();
        const modifiedEditor = this._dataSource.getModifiedEditor();
        const computer = new InlineViewZonesComputer(lineChanges, originalForeignVZ, modifiedForeignVZ, originalEditor, modifiedEditor, renderIndicators);
        return computer.getViewZones();
    }
    _getOriginalEditorDecorations(lineChanges, ignoreTrimWhitespace, renderIndicators) {
        const overviewZoneColor = String(this._removeColor);
        const result = {
            decorations: [],
            overviewZones: []
        };
        const originalEditor = this._dataSource.getOriginalEditor();
        const originalModel = originalEditor.getModel();
        const originalViewModel = originalEditor._getViewModel();
        for (const lineChange of lineChanges) {
            // Add overview zones in the overview ruler
            if (isChangeOrDelete(lineChange)) {
                result.decorations.push({
                    range: new Range(lineChange.originalStartLineNumber, 1, lineChange.originalEndLineNumber, 1073741824 /* MAX_SAFE_SMALL_INTEGER */),
                    options: DECORATIONS.lineDeleteMargin
                });
                const viewRange = getViewRange(originalModel, originalViewModel, lineChange.originalStartLineNumber, lineChange.originalEndLineNumber);
                result.overviewZones.push(new OverviewRulerZone(viewRange.startLineNumber, viewRange.endLineNumber, overviewZoneColor));
            }
        }
        return result;
    }
    _getModifiedEditorDecorations(lineChanges, ignoreTrimWhitespace, renderIndicators) {
        const modifiedEditor = this._dataSource.getModifiedEditor();
        const overviewZoneColor = String(this._insertColor);
        const result = {
            decorations: [],
            overviewZones: []
        };
        const modifiedModel = modifiedEditor.getModel();
        const modifiedViewModel = modifiedEditor._getViewModel();
        for (const lineChange of lineChanges) {
            // Add decorations & overview zones
            if (isChangeOrInsert(lineChange)) {
                result.decorations.push({
                    range: new Range(lineChange.modifiedStartLineNumber, 1, lineChange.modifiedEndLineNumber, 1073741824 /* MAX_SAFE_SMALL_INTEGER */),
                    options: (renderIndicators ? DECORATIONS.lineInsertWithSign : DECORATIONS.lineInsert)
                });
                const viewRange = getViewRange(modifiedModel, modifiedViewModel, lineChange.modifiedStartLineNumber, lineChange.modifiedEndLineNumber);
                result.overviewZones.push(new OverviewRulerZone(viewRange.startLineNumber, viewRange.endLineNumber, overviewZoneColor));
                if (lineChange.charChanges) {
                    for (const charChange of lineChange.charChanges) {
                        if (isChangeOrInsert(charChange)) {
                            if (ignoreTrimWhitespace) {
                                for (let lineNumber = charChange.modifiedStartLineNumber; lineNumber <= charChange.modifiedEndLineNumber; lineNumber++) {
                                    let startColumn;
                                    let endColumn;
                                    if (lineNumber === charChange.modifiedStartLineNumber) {
                                        startColumn = charChange.modifiedStartColumn;
                                    }
                                    else {
                                        startColumn = modifiedModel.getLineFirstNonWhitespaceColumn(lineNumber);
                                    }
                                    if (lineNumber === charChange.modifiedEndLineNumber) {
                                        endColumn = charChange.modifiedEndColumn;
                                    }
                                    else {
                                        endColumn = modifiedModel.getLineLastNonWhitespaceColumn(lineNumber);
                                    }
                                    result.decorations.push(createDecoration(lineNumber, startColumn, lineNumber, endColumn, DECORATIONS.charInsert));
                                }
                            }
                            else {
                                result.decorations.push(createDecoration(charChange.modifiedStartLineNumber, charChange.modifiedStartColumn, charChange.modifiedEndLineNumber, charChange.modifiedEndColumn, DECORATIONS.charInsert));
                            }
                        }
                    }
                }
                else {
                    result.decorations.push(createDecoration(lineChange.modifiedStartLineNumber, 1, lineChange.modifiedEndLineNumber, 1073741824 /* MAX_SAFE_SMALL_INTEGER */, DECORATIONS.charInsertWholeLine));
                }
            }
        }
        return result;
    }
    layout() {
        // An editor should not be smaller than 5px
        return Math.max(5, this._decorationsLeft);
    }
}
class InlineViewZonesComputer extends ViewZonesComputer {
    constructor(lineChanges, originalForeignVZ, modifiedForeignVZ, originalEditor, modifiedEditor, renderIndicators) {
        super(lineChanges, originalForeignVZ, modifiedForeignVZ, originalEditor, modifiedEditor);
        this._originalModel = originalEditor.getModel();
        this._renderIndicators = renderIndicators;
        this._pendingLineChange = [];
        this._pendingViewZones = [];
        this._lineBreaksComputer = this._modifiedEditor._getViewModel().createLineBreaksComputer();
    }
    getViewZones() {
        const result = super.getViewZones();
        this._finalize(result);
        return result;
    }
    _createOriginalMarginDomNodeForModifiedForeignViewZoneInAddedRegion() {
        const result = document.createElement('div');
        result.className = 'inline-added-margin-view-zone';
        return result;
    }
    _produceOriginalFromDiff(lineChange, lineChangeOriginalLength, lineChangeModifiedLength) {
        const marginDomNode = document.createElement('div');
        marginDomNode.className = 'inline-added-margin-view-zone';
        return {
            afterLineNumber: Math.max(lineChange.originalStartLineNumber, lineChange.originalEndLineNumber),
            heightInLines: lineChangeModifiedLength,
            domNode: document.createElement('div'),
            marginDomNode: marginDomNode
        };
    }
    _produceModifiedFromDiff(lineChange, lineChangeOriginalLength, lineChangeModifiedLength) {
        const domNode = document.createElement('div');
        domNode.className = `view-lines line-delete ${MOUSE_CURSOR_TEXT_CSS_CLASS_NAME}`;
        const marginDomNode = document.createElement('div');
        marginDomNode.className = 'inline-deleted-margin-view-zone';
        const viewZone = {
            shouldNotShrink: true,
            afterLineNumber: (lineChange.modifiedEndLineNumber === 0 ? lineChange.modifiedStartLineNumber : lineChange.modifiedStartLineNumber - 1),
            heightInLines: lineChangeOriginalLength,
            minWidthInPx: 0,
            domNode: domNode,
            marginDomNode: marginDomNode,
            diff: {
                originalStartLineNumber: lineChange.originalStartLineNumber,
                originalEndLineNumber: lineChange.originalEndLineNumber,
                modifiedStartLineNumber: lineChange.modifiedStartLineNumber,
                modifiedEndLineNumber: lineChange.modifiedEndLineNumber,
                originalModel: this._originalModel,
                viewLineCounts: null,
            }
        };
        for (let lineNumber = lineChange.originalStartLineNumber; lineNumber <= lineChange.originalEndLineNumber; lineNumber++) {
            this._lineBreaksComputer.addRequest(this._originalModel.getLineContent(lineNumber), null);
        }
        this._pendingLineChange.push(lineChange);
        this._pendingViewZones.push(viewZone);
        return viewZone;
    }
    _finalize(result) {
        const modifiedEditorOptions = this._modifiedEditor.getOptions();
        const tabSize = this._modifiedEditor.getModel().getOptions().tabSize;
        const fontInfo = modifiedEditorOptions.get(38 /* fontInfo */);
        const disableMonospaceOptimizations = modifiedEditorOptions.get(26 /* disableMonospaceOptimizations */);
        const typicalHalfwidthCharacterWidth = fontInfo.typicalHalfwidthCharacterWidth;
        const scrollBeyondLastColumn = modifiedEditorOptions.get(88 /* scrollBeyondLastColumn */);
        const mightContainNonBasicASCII = this._originalModel.mightContainNonBasicASCII();
        const mightContainRTL = this._originalModel.mightContainRTL();
        const lineHeight = modifiedEditorOptions.get(53 /* lineHeight */);
        const layoutInfo = modifiedEditorOptions.get(124 /* layoutInfo */);
        const lineDecorationsWidth = layoutInfo.decorationsWidth;
        const stopRenderingLineAfter = modifiedEditorOptions.get(100 /* stopRenderingLineAfter */);
        const renderWhitespace = modifiedEditorOptions.get(83 /* renderWhitespace */);
        const renderControlCharacters = modifiedEditorOptions.get(77 /* renderControlCharacters */);
        const fontLigatures = modifiedEditorOptions.get(39 /* fontLigatures */);
        const lineBreaks = this._lineBreaksComputer.finalize();
        let lineBreakIndex = 0;
        for (let i = 0; i < this._pendingLineChange.length; i++) {
            const lineChange = this._pendingLineChange[i];
            const viewZone = this._pendingViewZones[i];
            const domNode = viewZone.domNode;
            Configuration.applyFontInfoSlow(domNode, fontInfo);
            const marginDomNode = viewZone.marginDomNode;
            Configuration.applyFontInfoSlow(marginDomNode, fontInfo);
            const decorations = [];
            if (lineChange.charChanges) {
                for (const charChange of lineChange.charChanges) {
                    if (isChangeOrDelete(charChange)) {
                        decorations.push(new InlineDecoration(new Range(charChange.originalStartLineNumber, charChange.originalStartColumn, charChange.originalEndLineNumber, charChange.originalEndColumn), 'char-delete', 0 /* Regular */));
                    }
                }
            }
            const hasCharChanges = (decorations.length > 0);
            const sb = createStringBuilder(10000);
            let maxCharsPerLine = 0;
            let renderedLineCount = 0;
            let viewLineCounts = null;
            for (let lineNumber = lineChange.originalStartLineNumber; lineNumber <= lineChange.originalEndLineNumber; lineNumber++) {
                const lineIndex = lineNumber - lineChange.originalStartLineNumber;
                const lineTokens = this._originalModel.getLineTokens(lineNumber);
                const lineContent = lineTokens.getLineContent();
                const lineBreakData = lineBreaks[lineBreakIndex++];
                const actualDecorations = LineDecoration.filter(decorations, lineNumber, 1, lineContent.length + 1);
                if (lineBreakData) {
                    let lastBreakOffset = 0;
                    for (const breakOffset of lineBreakData.breakOffsets) {
                        const viewLineTokens = lineTokens.sliceAndInflate(lastBreakOffset, breakOffset, 0);
                        const viewLineContent = lineContent.substring(lastBreakOffset, breakOffset);
                        maxCharsPerLine = Math.max(maxCharsPerLine, this._renderOriginalLine(renderedLineCount++, viewLineContent, viewLineTokens, LineDecoration.extractWrapped(actualDecorations, lastBreakOffset, breakOffset), hasCharChanges, mightContainNonBasicASCII, mightContainRTL, fontInfo, disableMonospaceOptimizations, lineHeight, lineDecorationsWidth, stopRenderingLineAfter, renderWhitespace, renderControlCharacters, fontLigatures, tabSize, sb, marginDomNode));
                        lastBreakOffset = breakOffset;
                    }
                    if (!viewLineCounts) {
                        viewLineCounts = [];
                    }
                    // make sure all lines before this one have an entry in `viewLineCounts`
                    while (viewLineCounts.length < lineIndex) {
                        viewLineCounts[viewLineCounts.length] = 1;
                    }
                    viewLineCounts[lineIndex] = lineBreakData.breakOffsets.length;
                    viewZone.heightInLines += (lineBreakData.breakOffsets.length - 1);
                    const marginDomNode2 = document.createElement('div');
                    marginDomNode2.className = 'line-delete';
                    result.original.push({
                        afterLineNumber: lineNumber,
                        afterColumn: 0,
                        heightInLines: lineBreakData.breakOffsets.length - 1,
                        domNode: createFakeLinesDiv(),
                        marginDomNode: marginDomNode2
                    });
                }
                else {
                    maxCharsPerLine = Math.max(maxCharsPerLine, this._renderOriginalLine(renderedLineCount++, lineContent, lineTokens, actualDecorations, hasCharChanges, mightContainNonBasicASCII, mightContainRTL, fontInfo, disableMonospaceOptimizations, lineHeight, lineDecorationsWidth, stopRenderingLineAfter, renderWhitespace, renderControlCharacters, fontLigatures, tabSize, sb, marginDomNode));
                }
            }
            maxCharsPerLine += scrollBeyondLastColumn;
            const html = sb.build();
            const trustedhtml = ttPolicy ? ttPolicy.createHTML(html) : html;
            domNode.innerHTML = trustedhtml;
            viewZone.minWidthInPx = (maxCharsPerLine * typicalHalfwidthCharacterWidth);
            if (viewLineCounts) {
                // make sure all lines have an entry in `viewLineCounts`
                const cnt = lineChange.originalEndLineNumber - lineChange.originalStartLineNumber;
                while (viewLineCounts.length <= cnt) {
                    viewLineCounts[viewLineCounts.length] = 1;
                }
            }
            viewZone.diff.viewLineCounts = viewLineCounts;
        }
        result.original.sort((a, b) => {
            return a.afterLineNumber - b.afterLineNumber;
        });
    }
    _renderOriginalLine(renderedLineCount, lineContent, lineTokens, decorations, hasCharChanges, mightContainNonBasicASCII, mightContainRTL, fontInfo, disableMonospaceOptimizations, lineHeight, lineDecorationsWidth, stopRenderingLineAfter, renderWhitespace, renderControlCharacters, fontLigatures, tabSize, sb, marginDomNode) {
        sb.appendASCIIString('<div class="view-line');
        if (!hasCharChanges) {
            // No char changes
            sb.appendASCIIString(' char-delete');
        }
        sb.appendASCIIString('" style="top:');
        sb.appendASCIIString(String(renderedLineCount * lineHeight));
        sb.appendASCIIString('px;width:1000000px;">');
        const isBasicASCII = ViewLineRenderingData.isBasicASCII(lineContent, mightContainNonBasicASCII);
        const containsRTL = ViewLineRenderingData.containsRTL(lineContent, isBasicASCII, mightContainRTL);
        const output = renderViewLine(new RenderLineInput((fontInfo.isMonospace && !disableMonospaceOptimizations), fontInfo.canUseHalfwidthRightwardsArrow, lineContent, false, isBasicASCII, containsRTL, 0, lineTokens, decorations, tabSize, 0, fontInfo.spaceWidth, fontInfo.middotWidth, fontInfo.wsmiddotWidth, stopRenderingLineAfter, renderWhitespace, renderControlCharacters, fontLigatures !== EditorFontLigatures.OFF, null // Send no selections, original line cannot be selected
        ), sb);
        sb.appendASCIIString('</div>');
        if (this._renderIndicators) {
            const marginElement = document.createElement('div');
            marginElement.className = `delete-sign ${ThemeIcon.asClassName(diffRemoveIcon)}`;
            marginElement.setAttribute('style', `position:absolute;top:${renderedLineCount * lineHeight}px;width:${lineDecorationsWidth}px;height:${lineHeight}px;right:0;`);
            marginDomNode.appendChild(marginElement);
        }
        const absoluteOffsets = output.characterMapping.getAbsoluteOffsets();
        return absoluteOffsets.length > 0 ? absoluteOffsets[absoluteOffsets.length - 1] : 0;
    }
}
function validateDiffWordWrap(value, defaultValue) {
    return validateStringSetOption(value, defaultValue, ['off', 'on', 'inherit']);
}
function isChangeOrInsert(lineChange) {
    return lineChange.modifiedEndLineNumber > 0;
}
function isChangeOrDelete(lineChange) {
    return lineChange.originalEndLineNumber > 0;
}
function createFakeLinesDiv() {
    const r = document.createElement('div');
    r.className = 'diagonal-fill';
    return r;
}
function getViewRange(model, viewModel, startLineNumber, endLineNumber) {
    const lineCount = model.getLineCount();
    startLineNumber = Math.min(lineCount, Math.max(1, startLineNumber));
    endLineNumber = Math.min(lineCount, Math.max(1, endLineNumber));
    return viewModel.coordinatesConverter.convertModelRangeToViewRange(new Range(startLineNumber, model.getLineMinColumn(startLineNumber), endLineNumber, model.getLineMaxColumn(endLineNumber)));
}
registerThemingParticipant((theme, collector) => {
    const added = theme.getColor(diffInserted);
    if (added) {
        collector.addRule(`.monaco-editor .line-insert, .monaco-editor .char-insert { background-color: ${added}; }`);
        collector.addRule(`.monaco-diff-editor .line-insert, .monaco-diff-editor .char-insert { background-color: ${added}; }`);
        collector.addRule(`.monaco-editor .inline-added-margin-view-zone { background-color: ${added}; }`);
    }
    const removed = theme.getColor(diffRemoved);
    if (removed) {
        collector.addRule(`.monaco-editor .line-delete, .monaco-editor .char-delete { background-color: ${removed}; }`);
        collector.addRule(`.monaco-diff-editor .line-delete, .monaco-diff-editor .char-delete { background-color: ${removed}; }`);
        collector.addRule(`.monaco-editor .inline-deleted-margin-view-zone { background-color: ${removed}; }`);
    }
    const addedOutline = theme.getColor(diffInsertedOutline);
    if (addedOutline) {
        collector.addRule(`.monaco-editor .line-insert, .monaco-editor .char-insert { border: 1px ${theme.type === 'hc' ? 'dashed' : 'solid'} ${addedOutline}; }`);
    }
    const removedOutline = theme.getColor(diffRemovedOutline);
    if (removedOutline) {
        collector.addRule(`.monaco-editor .line-delete, .monaco-editor .char-delete { border: 1px ${theme.type === 'hc' ? 'dashed' : 'solid'} ${removedOutline}; }`);
    }
    const shadow = theme.getColor(scrollbarShadow);
    if (shadow) {
        collector.addRule(`.monaco-diff-editor.side-by-side .editor.modified { box-shadow: -6px 0 5px -5px ${shadow}; }`);
    }
    const border = theme.getColor(diffBorder);
    if (border) {
        collector.addRule(`.monaco-diff-editor.side-by-side .editor.modified { border-left: 1px solid ${border}; }`);
    }
    const scrollbarSliderBackgroundColor = theme.getColor(scrollbarSliderBackground);
    if (scrollbarSliderBackgroundColor) {
        collector.addRule(`
			.monaco-diff-editor .diffViewport {
				background: ${scrollbarSliderBackgroundColor};
			}
		`);
    }
    const scrollbarSliderHoverBackgroundColor = theme.getColor(scrollbarSliderHoverBackground);
    if (scrollbarSliderHoverBackgroundColor) {
        collector.addRule(`
			.monaco-diff-editor .diffViewport:hover {
				background: ${scrollbarSliderHoverBackgroundColor};
			}
		`);
    }
    const scrollbarSliderActiveBackgroundColor = theme.getColor(scrollbarSliderActiveBackground);
    if (scrollbarSliderActiveBackgroundColor) {
        collector.addRule(`
			.monaco-diff-editor .diffViewport:active {
				background: ${scrollbarSliderActiveBackgroundColor};
			}
		`);
    }
    const diffDiagonalFillColor = theme.getColor(diffDiagonalFill);
    collector.addRule(`
	.monaco-editor .diagonal-fill {
		background-image: linear-gradient(
			-45deg,
			${diffDiagonalFillColor} 12.5%,
			#0000 12.5%, #0000 50%,
			${diffDiagonalFillColor} 50%, ${diffDiagonalFillColor} 62.5%,
			#0000 62.5%, #0000 100%
		);
		background-size: 8px 8px;
	}
	`);
});
