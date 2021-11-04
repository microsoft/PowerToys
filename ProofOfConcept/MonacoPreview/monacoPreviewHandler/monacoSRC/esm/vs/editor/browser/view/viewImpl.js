/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import * as dom from '../../../base/browser/dom.js';
import * as browser from '../../../base/browser/browser.js';
import { Selection } from '../../common/core/selection.js';
import { createFastDomNode } from '../../../base/browser/fastDomNode.js';
import { onUnexpectedError } from '../../../base/common/errors.js';
import { PointerHandler } from '../controller/pointerHandler.js';
import { TextAreaHandler } from '../controller/textAreaHandler.js';
import { ViewController } from './viewController.js';
import { ViewUserInputEvents } from './viewUserInputEvents.js';
import { ContentViewOverlays, MarginViewOverlays } from './viewOverlays.js';
import { PartFingerprints } from './viewPart.js';
import { ViewContentWidgets } from '../viewParts/contentWidgets/contentWidgets.js';
import { CurrentLineHighlightOverlay, CurrentLineMarginHighlightOverlay } from '../viewParts/currentLineHighlight/currentLineHighlight.js';
import { DecorationsOverlay } from '../viewParts/decorations/decorations.js';
import { EditorScrollbar } from '../viewParts/editorScrollbar/editorScrollbar.js';
import { GlyphMarginOverlay } from '../viewParts/glyphMargin/glyphMargin.js';
import { IndentGuidesOverlay } from '../viewParts/indentGuides/indentGuides.js';
import { LineNumbersOverlay } from '../viewParts/lineNumbers/lineNumbers.js';
import { ViewLines } from '../viewParts/lines/viewLines.js';
import { LinesDecorationsOverlay } from '../viewParts/linesDecorations/linesDecorations.js';
import { Margin } from '../viewParts/margin/margin.js';
import { MarginViewLineDecorationsOverlay } from '../viewParts/marginDecorations/marginDecorations.js';
import { Minimap } from '../viewParts/minimap/minimap.js';
import { ViewOverlayWidgets } from '../viewParts/overlayWidgets/overlayWidgets.js';
import { DecorationsOverviewRuler } from '../viewParts/overviewRuler/decorationsOverviewRuler.js';
import { OverviewRuler } from '../viewParts/overviewRuler/overviewRuler.js';
import { Rulers } from '../viewParts/rulers/rulers.js';
import { ScrollDecorationViewPart } from '../viewParts/scrollDecoration/scrollDecoration.js';
import { SelectionsOverlay } from '../viewParts/selections/selections.js';
import { ViewCursors } from '../viewParts/viewCursors/viewCursors.js';
import { ViewZones } from '../viewParts/viewZones/viewZones.js';
import { Position } from '../../common/core/position.js';
import { Range } from '../../common/core/range.js';
import { RenderingContext } from '../../common/view/renderingContext.js';
import { ViewContext } from '../../common/view/viewContext.js';
import { ViewportData } from '../../common/viewLayout/viewLinesViewportData.js';
import { ViewEventHandler } from '../../common/viewModel/viewEventHandler.js';
import { getThemeTypeSelector } from '../../../platform/theme/common/themeService.js';
import { PointerHandlerLastRenderData } from '../controller/mouseTarget.js';
export class View extends ViewEventHandler {
    constructor(commandDelegate, configuration, themeService, model, userInputEvents, overflowWidgetsDomNode) {
        super();
        this._selections = [new Selection(1, 1, 1, 1)];
        this._renderAnimationFrame = null;
        const viewController = new ViewController(configuration, model, userInputEvents, commandDelegate);
        // The view context is passed on to most classes (basically to reduce param. counts in ctors)
        this._context = new ViewContext(configuration, themeService.getColorTheme(), model);
        this._configPixelRatio = this._configPixelRatio = this._context.configuration.options.get(122 /* pixelRatio */);
        // Ensure the view is the first event handler in order to update the layout
        this._context.addEventHandler(this);
        this._register(themeService.onDidColorThemeChange(theme => {
            this._context.theme.update(theme);
            this._context.model.onDidColorThemeChange();
            this.render(true, false);
        }));
        this._viewParts = [];
        // Keyboard handler
        this._textAreaHandler = new TextAreaHandler(this._context, viewController, this._createTextAreaHandlerHelper());
        this._viewParts.push(this._textAreaHandler);
        // These two dom nodes must be constructed up front, since references are needed in the layout provider (scrolling & co.)
        this._linesContent = createFastDomNode(document.createElement('div'));
        this._linesContent.setClassName('lines-content' + ' monaco-editor-background');
        this._linesContent.setPosition('absolute');
        this.domNode = createFastDomNode(document.createElement('div'));
        this.domNode.setClassName(this._getEditorClassName());
        // Set role 'code' for better screen reader support https://github.com/microsoft/vscode/issues/93438
        this.domNode.setAttribute('role', 'code');
        this._overflowGuardContainer = createFastDomNode(document.createElement('div'));
        PartFingerprints.write(this._overflowGuardContainer, 3 /* OverflowGuard */);
        this._overflowGuardContainer.setClassName('overflow-guard');
        this._scrollbar = new EditorScrollbar(this._context, this._linesContent, this.domNode, this._overflowGuardContainer);
        this._viewParts.push(this._scrollbar);
        // View Lines
        this._viewLines = new ViewLines(this._context, this._linesContent);
        // View Zones
        this._viewZones = new ViewZones(this._context);
        this._viewParts.push(this._viewZones);
        // Decorations overview ruler
        const decorationsOverviewRuler = new DecorationsOverviewRuler(this._context);
        this._viewParts.push(decorationsOverviewRuler);
        const scrollDecoration = new ScrollDecorationViewPart(this._context);
        this._viewParts.push(scrollDecoration);
        const contentViewOverlays = new ContentViewOverlays(this._context);
        this._viewParts.push(contentViewOverlays);
        contentViewOverlays.addDynamicOverlay(new CurrentLineHighlightOverlay(this._context));
        contentViewOverlays.addDynamicOverlay(new SelectionsOverlay(this._context));
        contentViewOverlays.addDynamicOverlay(new IndentGuidesOverlay(this._context));
        contentViewOverlays.addDynamicOverlay(new DecorationsOverlay(this._context));
        const marginViewOverlays = new MarginViewOverlays(this._context);
        this._viewParts.push(marginViewOverlays);
        marginViewOverlays.addDynamicOverlay(new CurrentLineMarginHighlightOverlay(this._context));
        marginViewOverlays.addDynamicOverlay(new GlyphMarginOverlay(this._context));
        marginViewOverlays.addDynamicOverlay(new MarginViewLineDecorationsOverlay(this._context));
        marginViewOverlays.addDynamicOverlay(new LinesDecorationsOverlay(this._context));
        marginViewOverlays.addDynamicOverlay(new LineNumbersOverlay(this._context));
        const margin = new Margin(this._context);
        margin.getDomNode().appendChild(this._viewZones.marginDomNode);
        margin.getDomNode().appendChild(marginViewOverlays.getDomNode());
        this._viewParts.push(margin);
        // Content widgets
        this._contentWidgets = new ViewContentWidgets(this._context, this.domNode);
        this._viewParts.push(this._contentWidgets);
        this._viewCursors = new ViewCursors(this._context);
        this._viewParts.push(this._viewCursors);
        // Overlay widgets
        this._overlayWidgets = new ViewOverlayWidgets(this._context);
        this._viewParts.push(this._overlayWidgets);
        const rulers = new Rulers(this._context);
        this._viewParts.push(rulers);
        const minimap = new Minimap(this._context);
        this._viewParts.push(minimap);
        // -------------- Wire dom nodes up
        if (decorationsOverviewRuler) {
            const overviewRulerData = this._scrollbar.getOverviewRulerLayoutInfo();
            overviewRulerData.parent.insertBefore(decorationsOverviewRuler.getDomNode(), overviewRulerData.insertBefore);
        }
        this._linesContent.appendChild(contentViewOverlays.getDomNode());
        this._linesContent.appendChild(rulers.domNode);
        this._linesContent.appendChild(this._viewZones.domNode);
        this._linesContent.appendChild(this._viewLines.getDomNode());
        this._linesContent.appendChild(this._contentWidgets.domNode);
        this._linesContent.appendChild(this._viewCursors.getDomNode());
        this._overflowGuardContainer.appendChild(margin.getDomNode());
        this._overflowGuardContainer.appendChild(this._scrollbar.getDomNode());
        this._overflowGuardContainer.appendChild(scrollDecoration.getDomNode());
        this._overflowGuardContainer.appendChild(this._textAreaHandler.textArea);
        this._overflowGuardContainer.appendChild(this._textAreaHandler.textAreaCover);
        this._overflowGuardContainer.appendChild(this._overlayWidgets.getDomNode());
        this._overflowGuardContainer.appendChild(minimap.getDomNode());
        this.domNode.appendChild(this._overflowGuardContainer);
        if (overflowWidgetsDomNode) {
            overflowWidgetsDomNode.appendChild(this._contentWidgets.overflowingContentWidgetsDomNode.domNode);
        }
        else {
            this.domNode.appendChild(this._contentWidgets.overflowingContentWidgetsDomNode);
        }
        this._applyLayout();
        // Pointer handler
        this._pointerHandler = this._register(new PointerHandler(this._context, viewController, this._createPointerHandlerHelper()));
    }
    _flushAccumulatedAndRenderNow() {
        this._renderNow();
    }
    _createPointerHandlerHelper() {
        return {
            viewDomNode: this.domNode.domNode,
            linesContentDomNode: this._linesContent.domNode,
            focusTextArea: () => {
                this.focus();
            },
            getLastRenderData: () => {
                const lastViewCursorsRenderData = this._viewCursors.getLastRenderData() || [];
                const lastTextareaPosition = this._textAreaHandler.getLastRenderData();
                return new PointerHandlerLastRenderData(lastViewCursorsRenderData, lastTextareaPosition);
            },
            shouldSuppressMouseDownOnViewZone: (viewZoneId) => {
                return this._viewZones.shouldSuppressMouseDownOnViewZone(viewZoneId);
            },
            shouldSuppressMouseDownOnWidget: (widgetId) => {
                return this._contentWidgets.shouldSuppressMouseDownOnWidget(widgetId);
            },
            getPositionFromDOMInfo: (spanNode, offset) => {
                this._flushAccumulatedAndRenderNow();
                return this._viewLines.getPositionFromDOMInfo(spanNode, offset);
            },
            visibleRangeForPosition: (lineNumber, column) => {
                this._flushAccumulatedAndRenderNow();
                return this._viewLines.visibleRangeForPosition(new Position(lineNumber, column));
            },
            getLineWidth: (lineNumber) => {
                this._flushAccumulatedAndRenderNow();
                return this._viewLines.getLineWidth(lineNumber);
            }
        };
    }
    _createTextAreaHandlerHelper() {
        return {
            visibleRangeForPositionRelativeToEditor: (lineNumber, column) => {
                this._flushAccumulatedAndRenderNow();
                return this._viewLines.visibleRangeForPosition(new Position(lineNumber, column));
            }
        };
    }
    _applyLayout() {
        const options = this._context.configuration.options;
        const layoutInfo = options.get(124 /* layoutInfo */);
        this.domNode.setWidth(layoutInfo.width);
        this.domNode.setHeight(layoutInfo.height);
        this._overflowGuardContainer.setWidth(layoutInfo.width);
        this._overflowGuardContainer.setHeight(layoutInfo.height);
        this._linesContent.setWidth(1000000);
        this._linesContent.setHeight(1000000);
    }
    _getEditorClassName() {
        const focused = this._textAreaHandler.isFocused() ? ' focused' : '';
        return this._context.configuration.options.get(121 /* editorClassName */) + ' ' + getThemeTypeSelector(this._context.theme.type) + focused;
    }
    // --- begin event handlers
    handleEvents(events) {
        super.handleEvents(events);
        this._scheduleRender();
    }
    onConfigurationChanged(e) {
        this._configPixelRatio = this._context.configuration.options.get(122 /* pixelRatio */);
        this.domNode.setClassName(this._getEditorClassName());
        this._applyLayout();
        return false;
    }
    onCursorStateChanged(e) {
        this._selections = e.selections;
        return false;
    }
    onFocusChanged(e) {
        this.domNode.setClassName(this._getEditorClassName());
        return false;
    }
    onThemeChanged(e) {
        this.domNode.setClassName(this._getEditorClassName());
        return false;
    }
    // --- end event handlers
    dispose() {
        if (this._renderAnimationFrame !== null) {
            this._renderAnimationFrame.dispose();
            this._renderAnimationFrame = null;
        }
        this._contentWidgets.overflowingContentWidgetsDomNode.domNode.remove();
        this._context.removeEventHandler(this);
        this._viewLines.dispose();
        // Destroy view parts
        for (const viewPart of this._viewParts) {
            viewPart.dispose();
        }
        super.dispose();
    }
    _scheduleRender() {
        if (this._renderAnimationFrame === null) {
            this._renderAnimationFrame = dom.runAtThisOrScheduleAtNextAnimationFrame(this._onRenderScheduled.bind(this), 100);
        }
    }
    _onRenderScheduled() {
        this._renderAnimationFrame = null;
        this._flushAccumulatedAndRenderNow();
    }
    _renderNow() {
        safeInvokeNoArg(() => this._actualRender());
    }
    _getViewPartsToRender() {
        let result = [], resultLen = 0;
        for (const viewPart of this._viewParts) {
            if (viewPart.shouldRender()) {
                result[resultLen++] = viewPart;
            }
        }
        return result;
    }
    _actualRender() {
        if (!dom.isInDOM(this.domNode.domNode)) {
            return;
        }
        let viewPartsToRender = this._getViewPartsToRender();
        if (!this._viewLines.shouldRender() && viewPartsToRender.length === 0) {
            // Nothing to render
            return;
        }
        const partialViewportData = this._context.viewLayout.getLinesViewportData();
        this._context.model.setViewport(partialViewportData.startLineNumber, partialViewportData.endLineNumber, partialViewportData.centeredLineNumber);
        const viewportData = new ViewportData(this._selections, partialViewportData, this._context.viewLayout.getWhitespaceViewportData(), this._context.model);
        if (this._contentWidgets.shouldRender()) {
            // Give the content widgets a chance to set their max width before a possible synchronous layout
            this._contentWidgets.onBeforeRender(viewportData);
        }
        if (this._viewLines.shouldRender()) {
            this._viewLines.renderText(viewportData);
            this._viewLines.onDidRender();
            // Rendering of viewLines might cause scroll events to occur, so collect view parts to render again
            viewPartsToRender = this._getViewPartsToRender();
        }
        const renderingContext = new RenderingContext(this._context.viewLayout, viewportData, this._viewLines);
        // Render the rest of the parts
        for (const viewPart of viewPartsToRender) {
            viewPart.prepareRender(renderingContext);
        }
        for (const viewPart of viewPartsToRender) {
            viewPart.render(renderingContext);
            viewPart.onDidRender();
        }
        // Try to detect browser zooming and paint again if necessary
        if (Math.abs(browser.getPixelRatio() - this._configPixelRatio) > 0.001) {
            // looks like the pixel ratio has changed
            this._context.configuration.updatePixelRatio();
        }
    }
    // --- BEGIN CodeEditor helpers
    delegateVerticalScrollbarMouseDown(browserEvent) {
        this._scrollbar.delegateVerticalScrollbarMouseDown(browserEvent);
    }
    restoreState(scrollPosition) {
        this._context.model.setScrollPosition({ scrollTop: scrollPosition.scrollTop }, 1 /* Immediate */);
        this._context.model.tokenizeViewport();
        this._renderNow();
        this._viewLines.updateLineWidths();
        this._context.model.setScrollPosition({ scrollLeft: scrollPosition.scrollLeft }, 1 /* Immediate */);
    }
    getOffsetForColumn(modelLineNumber, modelColumn) {
        const modelPosition = this._context.model.validateModelPosition({
            lineNumber: modelLineNumber,
            column: modelColumn
        });
        const viewPosition = this._context.model.coordinatesConverter.convertModelPositionToViewPosition(modelPosition);
        this._flushAccumulatedAndRenderNow();
        const visibleRange = this._viewLines.visibleRangeForPosition(new Position(viewPosition.lineNumber, viewPosition.column));
        if (!visibleRange) {
            return -1;
        }
        return visibleRange.left;
    }
    getTargetAtClientPoint(clientX, clientY) {
        const mouseTarget = this._pointerHandler.getTargetAtClientPoint(clientX, clientY);
        if (!mouseTarget) {
            return null;
        }
        return ViewUserInputEvents.convertViewToModelMouseTarget(mouseTarget, this._context.model.coordinatesConverter);
    }
    createOverviewRuler(cssClassName) {
        return new OverviewRuler(this._context, cssClassName);
    }
    change(callback) {
        this._viewZones.changeViewZones(callback);
        this._scheduleRender();
    }
    render(now, everything) {
        if (everything) {
            // Force everything to render...
            this._viewLines.forceShouldRender();
            for (const viewPart of this._viewParts) {
                viewPart.forceShouldRender();
            }
        }
        if (now) {
            this._flushAccumulatedAndRenderNow();
        }
        else {
            this._scheduleRender();
        }
    }
    focus() {
        this._textAreaHandler.focusTextArea();
    }
    isFocused() {
        return this._textAreaHandler.isFocused();
    }
    setAriaOptions(options) {
        this._textAreaHandler.setAriaOptions(options);
    }
    addContentWidget(widgetData) {
        this._contentWidgets.addWidget(widgetData.widget);
        this.layoutContentWidget(widgetData);
        this._scheduleRender();
    }
    layoutContentWidget(widgetData) {
        let newRange = widgetData.position ? widgetData.position.range || null : null;
        if (newRange === null) {
            const newPosition = widgetData.position ? widgetData.position.position : null;
            if (newPosition !== null) {
                newRange = new Range(newPosition.lineNumber, newPosition.column, newPosition.lineNumber, newPosition.column);
            }
        }
        const newPreference = widgetData.position ? widgetData.position.preference : null;
        this._contentWidgets.setWidgetPosition(widgetData.widget, newRange, newPreference);
        this._scheduleRender();
    }
    removeContentWidget(widgetData) {
        this._contentWidgets.removeWidget(widgetData.widget);
        this._scheduleRender();
    }
    addOverlayWidget(widgetData) {
        this._overlayWidgets.addWidget(widgetData.widget);
        this.layoutOverlayWidget(widgetData);
        this._scheduleRender();
    }
    layoutOverlayWidget(widgetData) {
        const newPreference = widgetData.position ? widgetData.position.preference : null;
        const shouldRender = this._overlayWidgets.setWidgetPosition(widgetData.widget, newPreference);
        if (shouldRender) {
            this._scheduleRender();
        }
    }
    removeOverlayWidget(widgetData) {
        this._overlayWidgets.removeWidget(widgetData.widget);
        this._scheduleRender();
    }
}
function safeInvokeNoArg(func) {
    try {
        return func();
    }
    catch (e) {
        onUnexpectedError(e);
    }
}
