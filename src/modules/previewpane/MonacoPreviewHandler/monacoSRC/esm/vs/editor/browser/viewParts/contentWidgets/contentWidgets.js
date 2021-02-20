/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import * as dom from '../../../../base/browser/dom.js';
import { createFastDomNode } from '../../../../base/browser/fastDomNode.js';
import { PartFingerprints, ViewPart } from '../../view/viewPart.js';
class Coordinate {
    constructor(top, left) {
        this.top = top;
        this.left = left;
    }
}
export class ViewContentWidgets extends ViewPart {
    constructor(context, viewDomNode) {
        super(context);
        this._viewDomNode = viewDomNode;
        this._widgets = {};
        this.domNode = createFastDomNode(document.createElement('div'));
        PartFingerprints.write(this.domNode, 1 /* ContentWidgets */);
        this.domNode.setClassName('contentWidgets');
        this.domNode.setPosition('absolute');
        this.domNode.setTop(0);
        this.overflowingContentWidgetsDomNode = createFastDomNode(document.createElement('div'));
        PartFingerprints.write(this.overflowingContentWidgetsDomNode, 2 /* OverflowingContentWidgets */);
        this.overflowingContentWidgetsDomNode.setClassName('overflowingContentWidgets');
    }
    dispose() {
        super.dispose();
        this._widgets = {};
    }
    // --- begin event handlers
    onConfigurationChanged(e) {
        const keys = Object.keys(this._widgets);
        for (const widgetId of keys) {
            this._widgets[widgetId].onConfigurationChanged(e);
        }
        return true;
    }
    onDecorationsChanged(e) {
        // true for inline decorations that can end up relayouting text
        return true;
    }
    onFlushed(e) {
        return true;
    }
    onLineMappingChanged(e) {
        const keys = Object.keys(this._widgets);
        for (const widgetId of keys) {
            this._widgets[widgetId].onLineMappingChanged(e);
        }
        return true;
    }
    onLinesChanged(e) {
        return true;
    }
    onLinesDeleted(e) {
        return true;
    }
    onLinesInserted(e) {
        return true;
    }
    onScrollChanged(e) {
        return true;
    }
    onZonesChanged(e) {
        return true;
    }
    // ---- end view event handlers
    addWidget(_widget) {
        const myWidget = new Widget(this._context, this._viewDomNode, _widget);
        this._widgets[myWidget.id] = myWidget;
        if (myWidget.allowEditorOverflow) {
            this.overflowingContentWidgetsDomNode.appendChild(myWidget.domNode);
        }
        else {
            this.domNode.appendChild(myWidget.domNode);
        }
        this.setShouldRender();
    }
    setWidgetPosition(widget, range, preference) {
        const myWidget = this._widgets[widget.getId()];
        myWidget.setPosition(range, preference);
        this.setShouldRender();
    }
    removeWidget(widget) {
        const widgetId = widget.getId();
        if (this._widgets.hasOwnProperty(widgetId)) {
            const myWidget = this._widgets[widgetId];
            delete this._widgets[widgetId];
            const domNode = myWidget.domNode.domNode;
            domNode.parentNode.removeChild(domNode);
            domNode.removeAttribute('monaco-visible-content-widget');
            this.setShouldRender();
        }
    }
    shouldSuppressMouseDownOnWidget(widgetId) {
        if (this._widgets.hasOwnProperty(widgetId)) {
            return this._widgets[widgetId].suppressMouseDown;
        }
        return false;
    }
    onBeforeRender(viewportData) {
        const keys = Object.keys(this._widgets);
        for (const widgetId of keys) {
            this._widgets[widgetId].onBeforeRender(viewportData);
        }
    }
    prepareRender(ctx) {
        const keys = Object.keys(this._widgets);
        for (const widgetId of keys) {
            this._widgets[widgetId].prepareRender(ctx);
        }
    }
    render(ctx) {
        const keys = Object.keys(this._widgets);
        for (const widgetId of keys) {
            this._widgets[widgetId].render(ctx);
        }
    }
}
class Widget {
    constructor(context, viewDomNode, actual) {
        this._context = context;
        this._viewDomNode = viewDomNode;
        this._actual = actual;
        this.domNode = createFastDomNode(this._actual.getDomNode());
        this.id = this._actual.getId();
        this.allowEditorOverflow = this._actual.allowEditorOverflow || false;
        this.suppressMouseDown = this._actual.suppressMouseDown || false;
        const options = this._context.configuration.options;
        const layoutInfo = options.get(124 /* layoutInfo */);
        this._fixedOverflowWidgets = options.get(32 /* fixedOverflowWidgets */);
        this._contentWidth = layoutInfo.contentWidth;
        this._contentLeft = layoutInfo.contentLeft;
        this._lineHeight = options.get(53 /* lineHeight */);
        this._range = null;
        this._viewRange = null;
        this._preference = [];
        this._cachedDomNodeClientWidth = -1;
        this._cachedDomNodeClientHeight = -1;
        this._maxWidth = this._getMaxWidth();
        this._isVisible = false;
        this._renderData = null;
        this.domNode.setPosition((this._fixedOverflowWidgets && this.allowEditorOverflow) ? 'fixed' : 'absolute');
        this.domNode.setVisibility('hidden');
        this.domNode.setAttribute('widgetId', this.id);
        this.domNode.setMaxWidth(this._maxWidth);
    }
    onConfigurationChanged(e) {
        const options = this._context.configuration.options;
        this._lineHeight = options.get(53 /* lineHeight */);
        if (e.hasChanged(124 /* layoutInfo */)) {
            const layoutInfo = options.get(124 /* layoutInfo */);
            this._contentLeft = layoutInfo.contentLeft;
            this._contentWidth = layoutInfo.contentWidth;
            this._maxWidth = this._getMaxWidth();
        }
    }
    onLineMappingChanged(e) {
        this._setPosition(this._range);
    }
    _setPosition(range) {
        this._range = range;
        this._viewRange = null;
        if (this._range) {
            // Do not trust that widgets give a valid position
            const validModelRange = this._context.model.validateModelRange(this._range);
            if (this._context.model.coordinatesConverter.modelPositionIsVisible(validModelRange.getStartPosition()) || this._context.model.coordinatesConverter.modelPositionIsVisible(validModelRange.getEndPosition())) {
                this._viewRange = this._context.model.coordinatesConverter.convertModelRangeToViewRange(validModelRange);
            }
        }
    }
    _getMaxWidth() {
        return (this.allowEditorOverflow
            ? window.innerWidth || document.documentElement.clientWidth || document.body.clientWidth
            : this._contentWidth);
    }
    setPosition(range, preference) {
        this._setPosition(range);
        this._preference = preference;
        this._cachedDomNodeClientWidth = -1;
        this._cachedDomNodeClientHeight = -1;
    }
    _layoutBoxInViewport(topLeft, bottomLeft, width, height, ctx) {
        // Our visible box is split horizontally by the current line => 2 boxes
        // a) the box above the line
        const aboveLineTop = topLeft.top;
        const heightAboveLine = aboveLineTop;
        // b) the box under the line
        const underLineTop = bottomLeft.top + this._lineHeight;
        const heightUnderLine = ctx.viewportHeight - underLineTop;
        const aboveTop = aboveLineTop - height;
        const fitsAbove = (heightAboveLine >= height);
        const belowTop = underLineTop;
        const fitsBelow = (heightUnderLine >= height);
        // And its left
        let actualAboveLeft = topLeft.left;
        let actualBelowLeft = bottomLeft.left;
        if (actualAboveLeft + width > ctx.scrollLeft + ctx.viewportWidth) {
            actualAboveLeft = ctx.scrollLeft + ctx.viewportWidth - width;
        }
        if (actualBelowLeft + width > ctx.scrollLeft + ctx.viewportWidth) {
            actualBelowLeft = ctx.scrollLeft + ctx.viewportWidth - width;
        }
        if (actualAboveLeft < ctx.scrollLeft) {
            actualAboveLeft = ctx.scrollLeft;
        }
        if (actualBelowLeft < ctx.scrollLeft) {
            actualBelowLeft = ctx.scrollLeft;
        }
        return {
            fitsAbove: fitsAbove,
            aboveTop: aboveTop,
            aboveLeft: actualAboveLeft,
            fitsBelow: fitsBelow,
            belowTop: belowTop,
            belowLeft: actualBelowLeft,
        };
    }
    _layoutHorizontalSegmentInPage(windowSize, domNodePosition, left, width) {
        // Initially, the limits are defined as the dom node limits
        const MIN_LIMIT = Math.max(0, domNodePosition.left - width);
        const MAX_LIMIT = Math.min(domNodePosition.left + domNodePosition.width + width, windowSize.width);
        let absoluteLeft = domNodePosition.left + left - dom.StandardWindow.scrollX;
        if (absoluteLeft + width > MAX_LIMIT) {
            const delta = absoluteLeft - (MAX_LIMIT - width);
            absoluteLeft -= delta;
            left -= delta;
        }
        if (absoluteLeft < MIN_LIMIT) {
            const delta = absoluteLeft - MIN_LIMIT;
            absoluteLeft -= delta;
            left -= delta;
        }
        return [left, absoluteLeft];
    }
    _layoutBoxInPage(topLeft, bottomLeft, width, height, ctx) {
        const aboveTop = topLeft.top - height;
        const belowTop = bottomLeft.top + this._lineHeight;
        const domNodePosition = dom.getDomNodePagePosition(this._viewDomNode.domNode);
        const absoluteAboveTop = domNodePosition.top + aboveTop - dom.StandardWindow.scrollY;
        const absoluteBelowTop = domNodePosition.top + belowTop - dom.StandardWindow.scrollY;
        const windowSize = dom.getClientArea(document.body);
        const [aboveLeft, absoluteAboveLeft] = this._layoutHorizontalSegmentInPage(windowSize, domNodePosition, topLeft.left - ctx.scrollLeft + this._contentLeft, width);
        const [belowLeft, absoluteBelowLeft] = this._layoutHorizontalSegmentInPage(windowSize, domNodePosition, bottomLeft.left - ctx.scrollLeft + this._contentLeft, width);
        // Leave some clearance to the top/bottom
        const TOP_PADDING = 22;
        const BOTTOM_PADDING = 22;
        const fitsAbove = (absoluteAboveTop >= TOP_PADDING);
        const fitsBelow = (absoluteBelowTop + height <= windowSize.height - BOTTOM_PADDING);
        if (this._fixedOverflowWidgets) {
            return {
                fitsAbove,
                aboveTop: Math.max(absoluteAboveTop, TOP_PADDING),
                aboveLeft: absoluteAboveLeft,
                fitsBelow,
                belowTop: absoluteBelowTop,
                belowLeft: absoluteBelowLeft
            };
        }
        return {
            fitsAbove,
            aboveTop: aboveTop,
            aboveLeft,
            fitsBelow,
            belowTop,
            belowLeft
        };
    }
    _prepareRenderWidgetAtExactPositionOverflowing(topLeft) {
        return new Coordinate(topLeft.top, topLeft.left + this._contentLeft);
    }
    /**
     * Compute `this._topLeft`
     */
    _getTopAndBottomLeft(ctx) {
        if (!this._viewRange) {
            return [null, null];
        }
        const visibleRangesForRange = ctx.linesVisibleRangesForRange(this._viewRange, false);
        if (!visibleRangesForRange || visibleRangesForRange.length === 0) {
            return [null, null];
        }
        let firstLine = visibleRangesForRange[0];
        let lastLine = visibleRangesForRange[0];
        for (const visibleRangesForLine of visibleRangesForRange) {
            if (visibleRangesForLine.lineNumber < firstLine.lineNumber) {
                firstLine = visibleRangesForLine;
            }
            if (visibleRangesForLine.lineNumber > lastLine.lineNumber) {
                lastLine = visibleRangesForLine;
            }
        }
        let firstLineMinLeft = 1073741824 /* MAX_SAFE_SMALL_INTEGER */; //firstLine.Constants.MAX_SAFE_SMALL_INTEGER;
        for (const visibleRange of firstLine.ranges) {
            if (visibleRange.left < firstLineMinLeft) {
                firstLineMinLeft = visibleRange.left;
            }
        }
        let lastLineMinLeft = 1073741824 /* MAX_SAFE_SMALL_INTEGER */; //lastLine.Constants.MAX_SAFE_SMALL_INTEGER;
        for (const visibleRange of lastLine.ranges) {
            if (visibleRange.left < lastLineMinLeft) {
                lastLineMinLeft = visibleRange.left;
            }
        }
        const topForPosition = ctx.getVerticalOffsetForLineNumber(firstLine.lineNumber) - ctx.scrollTop;
        const topLeft = new Coordinate(topForPosition, firstLineMinLeft);
        const topForBottomLine = ctx.getVerticalOffsetForLineNumber(lastLine.lineNumber) - ctx.scrollTop;
        const bottomLeft = new Coordinate(topForBottomLine, lastLineMinLeft);
        return [topLeft, bottomLeft];
    }
    _prepareRenderWidget(ctx) {
        const [topLeft, bottomLeft] = this._getTopAndBottomLeft(ctx);
        if (!topLeft || !bottomLeft) {
            return null;
        }
        if (this._cachedDomNodeClientWidth === -1 || this._cachedDomNodeClientHeight === -1) {
            let preferredDimensions = null;
            if (typeof this._actual.beforeRender === 'function') {
                preferredDimensions = safeInvoke(this._actual.beforeRender, this._actual);
            }
            if (preferredDimensions) {
                this._cachedDomNodeClientWidth = preferredDimensions.width;
                this._cachedDomNodeClientHeight = preferredDimensions.height;
            }
            else {
                const domNode = this.domNode.domNode;
                this._cachedDomNodeClientWidth = domNode.clientWidth;
                this._cachedDomNodeClientHeight = domNode.clientHeight;
            }
        }
        let placement;
        if (this.allowEditorOverflow) {
            placement = this._layoutBoxInPage(topLeft, bottomLeft, this._cachedDomNodeClientWidth, this._cachedDomNodeClientHeight, ctx);
        }
        else {
            placement = this._layoutBoxInViewport(topLeft, bottomLeft, this._cachedDomNodeClientWidth, this._cachedDomNodeClientHeight, ctx);
        }
        // Do two passes, first for perfect fit, second picks first option
        if (this._preference) {
            for (let pass = 1; pass <= 2; pass++) {
                for (const pref of this._preference) {
                    // placement
                    if (pref === 1 /* ABOVE */) {
                        if (!placement) {
                            // Widget outside of viewport
                            return null;
                        }
                        if (pass === 2 || placement.fitsAbove) {
                            return { coordinate: new Coordinate(placement.aboveTop, placement.aboveLeft), position: 1 /* ABOVE */ };
                        }
                    }
                    else if (pref === 2 /* BELOW */) {
                        if (!placement) {
                            // Widget outside of viewport
                            return null;
                        }
                        if (pass === 2 || placement.fitsBelow) {
                            return { coordinate: new Coordinate(placement.belowTop, placement.belowLeft), position: 2 /* BELOW */ };
                        }
                    }
                    else {
                        if (this.allowEditorOverflow) {
                            return { coordinate: this._prepareRenderWidgetAtExactPositionOverflowing(topLeft), position: 0 /* EXACT */ };
                        }
                        else {
                            return { coordinate: topLeft, position: 0 /* EXACT */ };
                        }
                    }
                }
            }
        }
        return null;
    }
    /**
     * On this first pass, we ensure that the content widget (if it is in the viewport) has the max width set correctly.
     */
    onBeforeRender(viewportData) {
        if (!this._viewRange || !this._preference) {
            return;
        }
        if (this._viewRange.endLineNumber < viewportData.startLineNumber || this._viewRange.startLineNumber > viewportData.endLineNumber) {
            // Outside of viewport
            return;
        }
        this.domNode.setMaxWidth(this._maxWidth);
    }
    prepareRender(ctx) {
        this._renderData = this._prepareRenderWidget(ctx);
    }
    render(ctx) {
        if (!this._renderData) {
            // This widget should be invisible
            if (this._isVisible) {
                this.domNode.removeAttribute('monaco-visible-content-widget');
                this._isVisible = false;
                this.domNode.setVisibility('hidden');
            }
            if (typeof this._actual.afterRender === 'function') {
                safeInvoke(this._actual.afterRender, this._actual, null);
            }
            return;
        }
        // This widget should be visible
        if (this.allowEditorOverflow) {
            this.domNode.setTop(this._renderData.coordinate.top);
            this.domNode.setLeft(this._renderData.coordinate.left);
        }
        else {
            this.domNode.setTop(this._renderData.coordinate.top + ctx.scrollTop - ctx.bigNumbersDelta);
            this.domNode.setLeft(this._renderData.coordinate.left);
        }
        if (!this._isVisible) {
            this.domNode.setVisibility('inherit');
            this.domNode.setAttribute('monaco-visible-content-widget', 'true');
            this._isVisible = true;
        }
        if (typeof this._actual.afterRender === 'function') {
            safeInvoke(this._actual.afterRender, this._actual, this._renderData.position);
        }
    }
}
function safeInvoke(fn, thisArg, ...args) {
    try {
        return fn.call(thisArg, ...args);
    }
    catch (_a) {
        // ignore
        return null;
    }
}
