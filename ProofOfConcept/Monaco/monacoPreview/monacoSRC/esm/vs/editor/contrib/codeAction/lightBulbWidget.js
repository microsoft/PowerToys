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
import * as dom from '../../../base/browser/dom.js';
import { GlobalMouseMoveMonitor, standardMouseMoveMerger } from '../../../base/browser/globalMouseMoveMonitor.js';
import { Emitter } from '../../../base/common/event.js';
import { Disposable } from '../../../base/common/lifecycle.js';
import './lightBulbWidget.css';
import { TextModel } from '../../common/model/textModel.js';
import * as nls from '../../../nls.js';
import { IKeybindingService } from '../../../platform/keybinding/common/keybinding.js';
import { registerThemingParticipant } from '../../../platform/theme/common/themeService.js';
import { editorLightBulbForeground, editorLightBulbAutoFixForeground, editorBackground } from '../../../platform/theme/common/colorRegistry.js';
import { Gesture } from '../../../base/browser/touch.js';
import { Codicon } from '../../../base/common/codicons.js';
var LightBulbState;
(function (LightBulbState) {
    LightBulbState.Hidden = { type: 0 /* Hidden */ };
    class Showing {
        constructor(actions, trigger, editorPosition, widgetPosition) {
            this.actions = actions;
            this.trigger = trigger;
            this.editorPosition = editorPosition;
            this.widgetPosition = widgetPosition;
            this.type = 1 /* Showing */;
        }
    }
    LightBulbState.Showing = Showing;
})(LightBulbState || (LightBulbState = {}));
let LightBulbWidget = class LightBulbWidget extends Disposable {
    constructor(_editor, _quickFixActionId, _preferredFixActionId, _keybindingService) {
        super();
        this._editor = _editor;
        this._quickFixActionId = _quickFixActionId;
        this._preferredFixActionId = _preferredFixActionId;
        this._keybindingService = _keybindingService;
        this._onClick = this._register(new Emitter());
        this.onClick = this._onClick.event;
        this._state = LightBulbState.Hidden;
        this._domNode = document.createElement('div');
        this._domNode.className = Codicon.lightBulb.classNames;
        this._editor.addContentWidget(this);
        this._register(this._editor.onDidChangeModelContent(_ => {
            // cancel when the line in question has been removed
            const editorModel = this._editor.getModel();
            if (this.state.type !== 1 /* Showing */ || !editorModel || this.state.editorPosition.lineNumber >= editorModel.getLineCount()) {
                this.hide();
            }
        }));
        Gesture.ignoreTarget(this._domNode);
        this._register(dom.addStandardDisposableGenericMouseDownListner(this._domNode, e => {
            if (this.state.type !== 1 /* Showing */) {
                return;
            }
            // Make sure that focus / cursor location is not lost when clicking widget icon
            this._editor.focus();
            e.preventDefault();
            // a bit of extra work to make sure the menu
            // doesn't cover the line-text
            const { top, height } = dom.getDomNodePagePosition(this._domNode);
            const lineHeight = this._editor.getOption(53 /* lineHeight */);
            let pad = Math.floor(lineHeight / 3);
            if (this.state.widgetPosition.position !== null && this.state.widgetPosition.position.lineNumber < this.state.editorPosition.lineNumber) {
                pad += lineHeight;
            }
            this._onClick.fire({
                x: e.posx,
                y: top + height + pad,
                actions: this.state.actions,
                trigger: this.state.trigger,
            });
        }));
        this._register(dom.addDisposableListener(this._domNode, 'mouseenter', (e) => {
            if ((e.buttons & 1) !== 1) {
                return;
            }
            // mouse enters lightbulb while the primary/left button
            // is being pressed -> hide the lightbulb and block future
            // showings until mouse is released
            this.hide();
            const monitor = new GlobalMouseMoveMonitor();
            monitor.startMonitoring(e.target, e.buttons, standardMouseMoveMerger, () => { }, () => {
                monitor.dispose();
            });
        }));
        this._register(this._editor.onDidChangeConfiguration(e => {
            // hide when told to do so
            if (e.hasChanged(51 /* lightbulb */) && !this._editor.getOption(51 /* lightbulb */).enabled) {
                this.hide();
            }
        }));
        this._updateLightBulbTitleAndIcon();
        this._register(this._keybindingService.onDidUpdateKeybindings(this._updateLightBulbTitleAndIcon, this));
    }
    dispose() {
        super.dispose();
        this._editor.removeContentWidget(this);
    }
    getId() {
        return 'LightBulbWidget';
    }
    getDomNode() {
        return this._domNode;
    }
    getPosition() {
        return this._state.type === 1 /* Showing */ ? this._state.widgetPosition : null;
    }
    update(actions, trigger, atPosition) {
        if (actions.validActions.length <= 0) {
            return this.hide();
        }
        const options = this._editor.getOptions();
        if (!options.get(51 /* lightbulb */).enabled) {
            return this.hide();
        }
        const model = this._editor.getModel();
        if (!model) {
            return this.hide();
        }
        const { lineNumber, column } = model.validatePosition(atPosition);
        const tabSize = model.getOptions().tabSize;
        const fontInfo = options.get(38 /* fontInfo */);
        const lineContent = model.getLineContent(lineNumber);
        const indent = TextModel.computeIndentLevel(lineContent, tabSize);
        const lineHasSpace = fontInfo.spaceWidth * indent > 22;
        const isFolded = (lineNumber) => {
            return lineNumber > 2 && this._editor.getTopForLineNumber(lineNumber) === this._editor.getTopForLineNumber(lineNumber - 1);
        };
        let effectiveLineNumber = lineNumber;
        if (!lineHasSpace) {
            if (lineNumber > 1 && !isFolded(lineNumber - 1)) {
                effectiveLineNumber -= 1;
            }
            else if (!isFolded(lineNumber + 1)) {
                effectiveLineNumber += 1;
            }
            else if (column * fontInfo.spaceWidth < 22) {
                // cannot show lightbulb above/below and showing
                // it inline would overlay the cursor...
                return this.hide();
            }
        }
        this.state = new LightBulbState.Showing(actions, trigger, atPosition, {
            position: { lineNumber: effectiveLineNumber, column: 1 },
            preference: LightBulbWidget._posPref
        });
        this._editor.layoutContentWidget(this);
    }
    hide() {
        this.state = LightBulbState.Hidden;
        this._editor.layoutContentWidget(this);
    }
    get state() { return this._state; }
    set state(value) {
        this._state = value;
        this._updateLightBulbTitleAndIcon();
    }
    _updateLightBulbTitleAndIcon() {
        if (this.state.type === 1 /* Showing */ && this.state.actions.hasAutoFix) {
            // update icon
            this._domNode.classList.remove(...Codicon.lightBulb.classNamesArray);
            this._domNode.classList.add(...Codicon.lightbulbAutofix.classNamesArray);
            const preferredKb = this._keybindingService.lookupKeybinding(this._preferredFixActionId);
            if (preferredKb) {
                this.title = nls.localize('prefferedQuickFixWithKb', "Show Fixes. Preferred Fix Available ({0})", preferredKb.getLabel());
                return;
            }
        }
        // update icon
        this._domNode.classList.remove(...Codicon.lightbulbAutofix.classNamesArray);
        this._domNode.classList.add(...Codicon.lightBulb.classNamesArray);
        const kb = this._keybindingService.lookupKeybinding(this._quickFixActionId);
        if (kb) {
            this.title = nls.localize('quickFixWithKb', "Show Fixes ({0})", kb.getLabel());
        }
        else {
            this.title = nls.localize('quickFix', "Show Fixes");
        }
    }
    set title(value) {
        this._domNode.title = value;
    }
};
LightBulbWidget._posPref = [0 /* EXACT */];
LightBulbWidget = __decorate([
    __param(3, IKeybindingService)
], LightBulbWidget);
export { LightBulbWidget };
registerThemingParticipant((theme, collector) => {
    var _a;
    const editorBackgroundColor = (_a = theme.getColor(editorBackground)) === null || _a === void 0 ? void 0 : _a.transparent(0.7);
    // Lightbulb Icon
    const editorLightBulbForegroundColor = theme.getColor(editorLightBulbForeground);
    if (editorLightBulbForegroundColor) {
        collector.addRule(`
		.monaco-editor .contentWidgets ${Codicon.lightBulb.cssSelector} {
			color: ${editorLightBulbForegroundColor};
			background-color: ${editorBackgroundColor};
		}`);
    }
    // Lightbulb Auto Fix Icon
    const editorLightBulbAutoFixForegroundColor = theme.getColor(editorLightBulbAutoFixForeground);
    if (editorLightBulbAutoFixForegroundColor) {
        collector.addRule(`
		.monaco-editor .contentWidgets ${Codicon.lightbulbAutofix.cssSelector} {
			color: ${editorLightBulbAutoFixForegroundColor};
			background-color: ${editorBackgroundColor};
		}`);
    }
});
