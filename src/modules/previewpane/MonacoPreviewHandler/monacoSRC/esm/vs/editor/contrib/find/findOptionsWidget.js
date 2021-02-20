/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import * as dom from '../../../base/browser/dom.js';
import { CaseSensitiveCheckbox, RegexCheckbox, WholeWordsCheckbox } from '../../../base/browser/ui/findinput/findInputCheckboxes.js';
import { Widget } from '../../../base/browser/ui/widget.js';
import { RunOnceScheduler } from '../../../base/common/async.js';
import { FIND_IDS } from './findModel.js';
import { contrastBorder, editorWidgetBackground, inputActiveOptionBorder, inputActiveOptionBackground, widgetShadow, editorWidgetForeground, inputActiveOptionForeground } from '../../../platform/theme/common/colorRegistry.js';
import { registerThemingParticipant } from '../../../platform/theme/common/themeService.js';
export class FindOptionsWidget extends Widget {
    constructor(editor, state, keybindingService, themeService) {
        super();
        this._hideSoon = this._register(new RunOnceScheduler(() => this._hide(), 2000));
        this._isVisible = false;
        this._editor = editor;
        this._state = state;
        this._keybindingService = keybindingService;
        this._domNode = document.createElement('div');
        this._domNode.className = 'findOptionsWidget';
        this._domNode.style.display = 'none';
        this._domNode.style.top = '10px';
        this._domNode.setAttribute('role', 'presentation');
        this._domNode.setAttribute('aria-hidden', 'true');
        const inputActiveOptionBorderColor = themeService.getColorTheme().getColor(inputActiveOptionBorder);
        const inputActiveOptionForegroundColor = themeService.getColorTheme().getColor(inputActiveOptionForeground);
        const inputActiveOptionBackgroundColor = themeService.getColorTheme().getColor(inputActiveOptionBackground);
        this.caseSensitive = this._register(new CaseSensitiveCheckbox({
            appendTitle: this._keybindingLabelFor(FIND_IDS.ToggleCaseSensitiveCommand),
            isChecked: this._state.matchCase,
            inputActiveOptionBorder: inputActiveOptionBorderColor,
            inputActiveOptionForeground: inputActiveOptionForegroundColor,
            inputActiveOptionBackground: inputActiveOptionBackgroundColor
        }));
        this._domNode.appendChild(this.caseSensitive.domNode);
        this._register(this.caseSensitive.onChange(() => {
            this._state.change({
                matchCase: this.caseSensitive.checked
            }, false);
        }));
        this.wholeWords = this._register(new WholeWordsCheckbox({
            appendTitle: this._keybindingLabelFor(FIND_IDS.ToggleWholeWordCommand),
            isChecked: this._state.wholeWord,
            inputActiveOptionBorder: inputActiveOptionBorderColor,
            inputActiveOptionForeground: inputActiveOptionForegroundColor,
            inputActiveOptionBackground: inputActiveOptionBackgroundColor
        }));
        this._domNode.appendChild(this.wholeWords.domNode);
        this._register(this.wholeWords.onChange(() => {
            this._state.change({
                wholeWord: this.wholeWords.checked
            }, false);
        }));
        this.regex = this._register(new RegexCheckbox({
            appendTitle: this._keybindingLabelFor(FIND_IDS.ToggleRegexCommand),
            isChecked: this._state.isRegex,
            inputActiveOptionBorder: inputActiveOptionBorderColor,
            inputActiveOptionForeground: inputActiveOptionForegroundColor,
            inputActiveOptionBackground: inputActiveOptionBackgroundColor
        }));
        this._domNode.appendChild(this.regex.domNode);
        this._register(this.regex.onChange(() => {
            this._state.change({
                isRegex: this.regex.checked
            }, false);
        }));
        this._editor.addOverlayWidget(this);
        this._register(this._state.onFindReplaceStateChange((e) => {
            let somethingChanged = false;
            if (e.isRegex) {
                this.regex.checked = this._state.isRegex;
                somethingChanged = true;
            }
            if (e.wholeWord) {
                this.wholeWords.checked = this._state.wholeWord;
                somethingChanged = true;
            }
            if (e.matchCase) {
                this.caseSensitive.checked = this._state.matchCase;
                somethingChanged = true;
            }
            if (!this._state.isRevealed && somethingChanged) {
                this._revealTemporarily();
            }
        }));
        this._register(dom.addDisposableNonBubblingMouseOutListener(this._domNode, (e) => this._onMouseOut()));
        this._register(dom.addDisposableListener(this._domNode, 'mouseover', (e) => this._onMouseOver()));
        this._applyTheme(themeService.getColorTheme());
        this._register(themeService.onDidColorThemeChange(this._applyTheme.bind(this)));
    }
    _keybindingLabelFor(actionId) {
        let kb = this._keybindingService.lookupKeybinding(actionId);
        if (!kb) {
            return '';
        }
        return ` (${kb.getLabel()})`;
    }
    dispose() {
        this._editor.removeOverlayWidget(this);
        super.dispose();
    }
    // ----- IOverlayWidget API
    getId() {
        return FindOptionsWidget.ID;
    }
    getDomNode() {
        return this._domNode;
    }
    getPosition() {
        return {
            preference: 0 /* TOP_RIGHT_CORNER */
        };
    }
    highlightFindOptions() {
        this._revealTemporarily();
    }
    _revealTemporarily() {
        this._show();
        this._hideSoon.schedule();
    }
    _onMouseOut() {
        this._hideSoon.schedule();
    }
    _onMouseOver() {
        this._hideSoon.cancel();
    }
    _show() {
        if (this._isVisible) {
            return;
        }
        this._isVisible = true;
        this._domNode.style.display = 'block';
    }
    _hide() {
        if (!this._isVisible) {
            return;
        }
        this._isVisible = false;
        this._domNode.style.display = 'none';
    }
    _applyTheme(theme) {
        let inputStyles = {
            inputActiveOptionBorder: theme.getColor(inputActiveOptionBorder),
            inputActiveOptionForeground: theme.getColor(inputActiveOptionForeground),
            inputActiveOptionBackground: theme.getColor(inputActiveOptionBackground)
        };
        this.caseSensitive.style(inputStyles);
        this.wholeWords.style(inputStyles);
        this.regex.style(inputStyles);
    }
}
FindOptionsWidget.ID = 'editor.contrib.findOptionsWidget';
registerThemingParticipant((theme, collector) => {
    const widgetBackground = theme.getColor(editorWidgetBackground);
    if (widgetBackground) {
        collector.addRule(`.monaco-editor .findOptionsWidget { background-color: ${widgetBackground}; }`);
    }
    const widgetForeground = theme.getColor(editorWidgetForeground);
    if (widgetForeground) {
        collector.addRule(`.monaco-editor .findOptionsWidget { color: ${widgetForeground}; }`);
    }
    const widgetShadowColor = theme.getColor(widgetShadow);
    if (widgetShadowColor) {
        collector.addRule(`.monaco-editor .findOptionsWidget { box-shadow: 0 0 8px 2px ${widgetShadowColor}; }`);
    }
    const hcBorder = theme.getColor(contrastBorder);
    if (hcBorder) {
        collector.addRule(`.monaco-editor .findOptionsWidget { border: 2px solid ${hcBorder}; }`);
    }
});
