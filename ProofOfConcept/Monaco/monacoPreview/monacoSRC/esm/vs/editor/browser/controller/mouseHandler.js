/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import * as dom from '../../../base/browser/dom.js';
import { StandardWheelEvent } from '../../../base/browser/mouseEvent.js';
import { TimeoutTimer } from '../../../base/common/async.js';
import { Disposable } from '../../../base/common/lifecycle.js';
import * as platform from '../../../base/common/platform.js';
import { HitTestContext, MouseTarget, MouseTargetFactory } from './mouseTarget.js';
import { ClientCoordinates, EditorMouseEventFactory, GlobalEditorMouseMoveMonitor, createEditorPagePosition } from '../editorDom.js';
import { EditorZoom } from '../../common/config/editorZoom.js';
import { Position } from '../../common/core/position.js';
import { Selection } from '../../common/core/selection.js';
import { ViewEventHandler } from '../../common/viewModel/viewEventHandler.js';
/**
 * Merges mouse events when mouse move events are throttled
 */
export function createMouseMoveEventMerger(mouseTargetFactory) {
    return function (lastEvent, currentEvent) {
        let targetIsWidget = false;
        if (mouseTargetFactory) {
            targetIsWidget = mouseTargetFactory.mouseTargetIsWidget(currentEvent);
        }
        if (!targetIsWidget) {
            currentEvent.preventDefault();
        }
        return currentEvent;
    };
}
export class MouseHandler extends ViewEventHandler {
    constructor(context, viewController, viewHelper) {
        super();
        this._context = context;
        this.viewController = viewController;
        this.viewHelper = viewHelper;
        this.mouseTargetFactory = new MouseTargetFactory(this._context, viewHelper);
        this._mouseDownOperation = this._register(new MouseDownOperation(this._context, this.viewController, this.viewHelper, (e, testEventTarget) => this._createMouseTarget(e, testEventTarget), (e) => this._getMouseColumn(e)));
        this.lastMouseLeaveTime = -1;
        const mouseEvents = new EditorMouseEventFactory(this.viewHelper.viewDomNode);
        this._register(mouseEvents.onContextMenu(this.viewHelper.viewDomNode, (e) => this._onContextMenu(e, true)));
        this._register(mouseEvents.onMouseMoveThrottled(this.viewHelper.viewDomNode, (e) => this._onMouseMove(e), createMouseMoveEventMerger(this.mouseTargetFactory), MouseHandler.MOUSE_MOVE_MINIMUM_TIME));
        this._register(mouseEvents.onMouseUp(this.viewHelper.viewDomNode, (e) => this._onMouseUp(e)));
        this._register(mouseEvents.onMouseLeave(this.viewHelper.viewDomNode, (e) => this._onMouseLeave(e)));
        this._register(mouseEvents.onMouseDown(this.viewHelper.viewDomNode, (e) => this._onMouseDown(e)));
        const onMouseWheel = (browserEvent) => {
            this.viewController.emitMouseWheel(browserEvent);
            if (!this._context.configuration.options.get(62 /* mouseWheelZoom */)) {
                return;
            }
            const e = new StandardWheelEvent(browserEvent);
            const doMouseWheelZoom = (platform.isMacintosh
                ? (browserEvent.metaKey && !browserEvent.ctrlKey && !browserEvent.shiftKey && !browserEvent.altKey)
                : (browserEvent.ctrlKey && !browserEvent.metaKey && !browserEvent.shiftKey && !browserEvent.altKey));
            if (doMouseWheelZoom) {
                const zoomLevel = EditorZoom.getZoomLevel();
                const delta = e.deltaY > 0 ? 1 : -1;
                EditorZoom.setZoomLevel(zoomLevel + delta);
                e.preventDefault();
                e.stopPropagation();
            }
        };
        this._register(dom.addDisposableListener(this.viewHelper.viewDomNode, dom.EventType.MOUSE_WHEEL, onMouseWheel, { capture: true, passive: false }));
        this._context.addEventHandler(this);
    }
    dispose() {
        this._context.removeEventHandler(this);
        super.dispose();
    }
    // --- begin event handlers
    onCursorStateChanged(e) {
        this._mouseDownOperation.onCursorStateChanged(e);
        return false;
    }
    onFocusChanged(e) {
        return false;
    }
    onScrollChanged(e) {
        this._mouseDownOperation.onScrollChanged();
        return false;
    }
    // --- end event handlers
    getTargetAtClientPoint(clientX, clientY) {
        const clientPos = new ClientCoordinates(clientX, clientY);
        const pos = clientPos.toPageCoordinates();
        const editorPos = createEditorPagePosition(this.viewHelper.viewDomNode);
        if (pos.y < editorPos.y || pos.y > editorPos.y + editorPos.height || pos.x < editorPos.x || pos.x > editorPos.x + editorPos.width) {
            return null;
        }
        return this.mouseTargetFactory.createMouseTarget(this.viewHelper.getLastRenderData(), editorPos, pos, null);
    }
    _createMouseTarget(e, testEventTarget) {
        return this.mouseTargetFactory.createMouseTarget(this.viewHelper.getLastRenderData(), e.editorPos, e.pos, testEventTarget ? e.target : null);
    }
    _getMouseColumn(e) {
        return this.mouseTargetFactory.getMouseColumn(e.editorPos, e.pos);
    }
    _onContextMenu(e, testEventTarget) {
        this.viewController.emitContextMenu({
            event: e,
            target: this._createMouseTarget(e, testEventTarget)
        });
    }
    _onMouseMove(e) {
        if (this._mouseDownOperation.isActive()) {
            // In selection/drag operation
            return;
        }
        const actualMouseMoveTime = e.timestamp;
        if (actualMouseMoveTime < this.lastMouseLeaveTime) {
            // Due to throttling, this event occurred before the mouse left the editor, therefore ignore it.
            return;
        }
        this.viewController.emitMouseMove({
            event: e,
            target: this._createMouseTarget(e, true)
        });
    }
    _onMouseLeave(e) {
        this.lastMouseLeaveTime = (new Date()).getTime();
        this.viewController.emitMouseLeave({
            event: e,
            target: null
        });
    }
    _onMouseUp(e) {
        this.viewController.emitMouseUp({
            event: e,
            target: this._createMouseTarget(e, true)
        });
    }
    _onMouseDown(e) {
        const t = this._createMouseTarget(e, true);
        const targetIsContent = (t.type === 6 /* CONTENT_TEXT */ || t.type === 7 /* CONTENT_EMPTY */);
        const targetIsGutter = (t.type === 2 /* GUTTER_GLYPH_MARGIN */ || t.type === 3 /* GUTTER_LINE_NUMBERS */ || t.type === 4 /* GUTTER_LINE_DECORATIONS */);
        const targetIsLineNumbers = (t.type === 3 /* GUTTER_LINE_NUMBERS */);
        const selectOnLineNumbers = this._context.configuration.options.get(93 /* selectOnLineNumbers */);
        const targetIsViewZone = (t.type === 8 /* CONTENT_VIEW_ZONE */ || t.type === 5 /* GUTTER_VIEW_ZONE */);
        const targetIsWidget = (t.type === 9 /* CONTENT_WIDGET */);
        let shouldHandle = e.leftButton || e.middleButton;
        if (platform.isMacintosh && e.leftButton && e.ctrlKey) {
            shouldHandle = false;
        }
        const focus = () => {
            e.preventDefault();
            this.viewHelper.focusTextArea();
        };
        if (shouldHandle && (targetIsContent || (targetIsLineNumbers && selectOnLineNumbers))) {
            focus();
            this._mouseDownOperation.start(t.type, e);
        }
        else if (targetIsGutter) {
            // Do not steal focus
            e.preventDefault();
        }
        else if (targetIsViewZone) {
            const viewZoneData = t.detail;
            if (this.viewHelper.shouldSuppressMouseDownOnViewZone(viewZoneData.viewZoneId)) {
                focus();
                this._mouseDownOperation.start(t.type, e);
                e.preventDefault();
            }
        }
        else if (targetIsWidget && this.viewHelper.shouldSuppressMouseDownOnWidget(t.detail)) {
            focus();
            e.preventDefault();
        }
        this.viewController.emitMouseDown({
            event: e,
            target: t
        });
    }
}
MouseHandler.MOUSE_MOVE_MINIMUM_TIME = 100; // ms
class MouseDownOperation extends Disposable {
    constructor(context, viewController, viewHelper, createMouseTarget, getMouseColumn) {
        super();
        this._context = context;
        this._viewController = viewController;
        this._viewHelper = viewHelper;
        this._createMouseTarget = createMouseTarget;
        this._getMouseColumn = getMouseColumn;
        this._mouseMoveMonitor = this._register(new GlobalEditorMouseMoveMonitor(this._viewHelper.viewDomNode));
        this._onScrollTimeout = this._register(new TimeoutTimer());
        this._mouseState = new MouseDownState();
        this._currentSelection = new Selection(1, 1, 1, 1);
        this._isActive = false;
        this._lastMouseEvent = null;
    }
    dispose() {
        super.dispose();
    }
    isActive() {
        return this._isActive;
    }
    _onMouseDownThenMove(e) {
        this._lastMouseEvent = e;
        this._mouseState.setModifiers(e);
        const position = this._findMousePosition(e, true);
        if (!position) {
            // Ignoring because position is unknown
            return;
        }
        if (this._mouseState.isDragAndDrop) {
            this._viewController.emitMouseDrag({
                event: e,
                target: position
            });
        }
        else {
            this._dispatchMouse(position, true);
        }
    }
    start(targetType, e) {
        this._lastMouseEvent = e;
        this._mouseState.setStartedOnLineNumbers(targetType === 3 /* GUTTER_LINE_NUMBERS */);
        this._mouseState.setStartButtons(e);
        this._mouseState.setModifiers(e);
        const position = this._findMousePosition(e, true);
        if (!position || !position.position) {
            // Ignoring because position is unknown
            return;
        }
        this._mouseState.trySetCount(e.detail, position.position);
        // Overwrite the detail of the MouseEvent, as it will be sent out in an event and contributions might rely on it.
        e.detail = this._mouseState.count;
        const options = this._context.configuration.options;
        if (!options.get(75 /* readOnly */)
            && options.get(27 /* dragAndDrop */)
            && !options.get(15 /* columnSelection */)
            && !this._mouseState.altKey // we don't support multiple mouse
            && e.detail < 2 // only single click on a selection can work
            && !this._isActive // the mouse is not down yet
            && !this._currentSelection.isEmpty() // we don't drag single cursor
            && (position.type === 6 /* CONTENT_TEXT */) // single click on text
            && position.position && this._currentSelection.containsPosition(position.position) // single click on a selection
        ) {
            this._mouseState.isDragAndDrop = true;
            this._isActive = true;
            this._mouseMoveMonitor.startMonitoring(e.target, e.buttons, createMouseMoveEventMerger(null), (e) => this._onMouseDownThenMove(e), (browserEvent) => {
                const position = this._findMousePosition(this._lastMouseEvent, true);
                if (browserEvent && browserEvent instanceof KeyboardEvent) {
                    // cancel
                    this._viewController.emitMouseDropCanceled();
                }
                else {
                    this._viewController.emitMouseDrop({
                        event: this._lastMouseEvent,
                        target: (position ? this._createMouseTarget(this._lastMouseEvent, true) : null) // Ignoring because position is unknown, e.g., Content View Zone
                    });
                }
                this._stop();
            });
            return;
        }
        this._mouseState.isDragAndDrop = false;
        this._dispatchMouse(position, e.shiftKey);
        if (!this._isActive) {
            this._isActive = true;
            this._mouseMoveMonitor.startMonitoring(e.target, e.buttons, createMouseMoveEventMerger(null), (e) => this._onMouseDownThenMove(e), () => this._stop());
        }
    }
    _stop() {
        this._isActive = false;
        this._onScrollTimeout.cancel();
    }
    onScrollChanged() {
        if (!this._isActive) {
            return;
        }
        this._onScrollTimeout.setIfNotSet(() => {
            if (!this._lastMouseEvent) {
                return;
            }
            const position = this._findMousePosition(this._lastMouseEvent, false);
            if (!position) {
                // Ignoring because position is unknown
                return;
            }
            if (this._mouseState.isDragAndDrop) {
                // Ignoring because users are dragging the text
                return;
            }
            this._dispatchMouse(position, true);
        }, 10);
    }
    onCursorStateChanged(e) {
        this._currentSelection = e.selections[0];
    }
    _getPositionOutsideEditor(e) {
        const editorContent = e.editorPos;
        const model = this._context.model;
        const viewLayout = this._context.viewLayout;
        const mouseColumn = this._getMouseColumn(e);
        if (e.posy < editorContent.y) {
            const verticalOffset = Math.max(viewLayout.getCurrentScrollTop() - (editorContent.y - e.posy), 0);
            const viewZoneData = HitTestContext.getZoneAtCoord(this._context, verticalOffset);
            if (viewZoneData) {
                const newPosition = this._helpPositionJumpOverViewZone(viewZoneData);
                if (newPosition) {
                    return new MouseTarget(null, 13 /* OUTSIDE_EDITOR */, mouseColumn, newPosition);
                }
            }
            const aboveLineNumber = viewLayout.getLineNumberAtVerticalOffset(verticalOffset);
            return new MouseTarget(null, 13 /* OUTSIDE_EDITOR */, mouseColumn, new Position(aboveLineNumber, 1));
        }
        if (e.posy > editorContent.y + editorContent.height) {
            const verticalOffset = viewLayout.getCurrentScrollTop() + (e.posy - editorContent.y);
            const viewZoneData = HitTestContext.getZoneAtCoord(this._context, verticalOffset);
            if (viewZoneData) {
                const newPosition = this._helpPositionJumpOverViewZone(viewZoneData);
                if (newPosition) {
                    return new MouseTarget(null, 13 /* OUTSIDE_EDITOR */, mouseColumn, newPosition);
                }
            }
            const belowLineNumber = viewLayout.getLineNumberAtVerticalOffset(verticalOffset);
            return new MouseTarget(null, 13 /* OUTSIDE_EDITOR */, mouseColumn, new Position(belowLineNumber, model.getLineMaxColumn(belowLineNumber)));
        }
        const possibleLineNumber = viewLayout.getLineNumberAtVerticalOffset(viewLayout.getCurrentScrollTop() + (e.posy - editorContent.y));
        if (e.posx < editorContent.x) {
            return new MouseTarget(null, 13 /* OUTSIDE_EDITOR */, mouseColumn, new Position(possibleLineNumber, 1));
        }
        if (e.posx > editorContent.x + editorContent.width) {
            return new MouseTarget(null, 13 /* OUTSIDE_EDITOR */, mouseColumn, new Position(possibleLineNumber, model.getLineMaxColumn(possibleLineNumber)));
        }
        return null;
    }
    _findMousePosition(e, testEventTarget) {
        const positionOutsideEditor = this._getPositionOutsideEditor(e);
        if (positionOutsideEditor) {
            return positionOutsideEditor;
        }
        const t = this._createMouseTarget(e, testEventTarget);
        const hintedPosition = t.position;
        if (!hintedPosition) {
            return null;
        }
        if (t.type === 8 /* CONTENT_VIEW_ZONE */ || t.type === 5 /* GUTTER_VIEW_ZONE */) {
            const newPosition = this._helpPositionJumpOverViewZone(t.detail);
            if (newPosition) {
                return new MouseTarget(t.element, t.type, t.mouseColumn, newPosition, null, t.detail);
            }
        }
        return t;
    }
    _helpPositionJumpOverViewZone(viewZoneData) {
        // Force position on view zones to go above or below depending on where selection started from
        const selectionStart = new Position(this._currentSelection.selectionStartLineNumber, this._currentSelection.selectionStartColumn);
        const positionBefore = viewZoneData.positionBefore;
        const positionAfter = viewZoneData.positionAfter;
        if (positionBefore && positionAfter) {
            if (positionBefore.isBefore(selectionStart)) {
                return positionBefore;
            }
            else {
                return positionAfter;
            }
        }
        return null;
    }
    _dispatchMouse(position, inSelectionMode) {
        if (!position.position) {
            return;
        }
        this._viewController.dispatchMouse({
            position: position.position,
            mouseColumn: position.mouseColumn,
            startedOnLineNumbers: this._mouseState.startedOnLineNumbers,
            inSelectionMode: inSelectionMode,
            mouseDownCount: this._mouseState.count,
            altKey: this._mouseState.altKey,
            ctrlKey: this._mouseState.ctrlKey,
            metaKey: this._mouseState.metaKey,
            shiftKey: this._mouseState.shiftKey,
            leftButton: this._mouseState.leftButton,
            middleButton: this._mouseState.middleButton,
        });
    }
}
class MouseDownState {
    constructor() {
        this._altKey = false;
        this._ctrlKey = false;
        this._metaKey = false;
        this._shiftKey = false;
        this._leftButton = false;
        this._middleButton = false;
        this._startedOnLineNumbers = false;
        this._lastMouseDownPosition = null;
        this._lastMouseDownPositionEqualCount = 0;
        this._lastMouseDownCount = 0;
        this._lastSetMouseDownCountTime = 0;
        this.isDragAndDrop = false;
    }
    get altKey() { return this._altKey; }
    get ctrlKey() { return this._ctrlKey; }
    get metaKey() { return this._metaKey; }
    get shiftKey() { return this._shiftKey; }
    get leftButton() { return this._leftButton; }
    get middleButton() { return this._middleButton; }
    get startedOnLineNumbers() { return this._startedOnLineNumbers; }
    get count() {
        return this._lastMouseDownCount;
    }
    setModifiers(source) {
        this._altKey = source.altKey;
        this._ctrlKey = source.ctrlKey;
        this._metaKey = source.metaKey;
        this._shiftKey = source.shiftKey;
    }
    setStartButtons(source) {
        this._leftButton = source.leftButton;
        this._middleButton = source.middleButton;
    }
    setStartedOnLineNumbers(startedOnLineNumbers) {
        this._startedOnLineNumbers = startedOnLineNumbers;
    }
    trySetCount(setMouseDownCount, newMouseDownPosition) {
        // a. Invalidate multiple clicking if too much time has passed (will be hit by IE because the detail field of mouse events contains garbage in IE10)
        const currentTime = (new Date()).getTime();
        if (currentTime - this._lastSetMouseDownCountTime > MouseDownState.CLEAR_MOUSE_DOWN_COUNT_TIME) {
            setMouseDownCount = 1;
        }
        this._lastSetMouseDownCountTime = currentTime;
        // b. Ensure that we don't jump from single click to triple click in one go (will be hit by IE because the detail field of mouse events contains garbage in IE10)
        if (setMouseDownCount > this._lastMouseDownCount + 1) {
            setMouseDownCount = this._lastMouseDownCount + 1;
        }
        // c. Invalidate multiple clicking if the logical position is different
        if (this._lastMouseDownPosition && this._lastMouseDownPosition.equals(newMouseDownPosition)) {
            this._lastMouseDownPositionEqualCount++;
        }
        else {
            this._lastMouseDownPositionEqualCount = 1;
        }
        this._lastMouseDownPosition = newMouseDownPosition;
        // Finally set the lastMouseDownCount
        this._lastMouseDownCount = Math.min(setMouseDownCount, this._lastMouseDownPositionEqualCount);
    }
}
MouseDownState.CLEAR_MOUSE_DOWN_COUNT_TIME = 400; // ms
