/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import './findInput.css';
import * as nls from '../../../../nls.js';
import * as dom from '../../dom.js';
import { HistoryInputBox } from '../inputbox/inputBox.js';
import { Widget } from '../widget.js';
import { Emitter } from '../../../common/event.js';
import { Checkbox } from '../checkbox/checkbox.js';
import { Codicon } from '../../../common/codicons.js';
const NLS_DEFAULT_LABEL = nls.localize('defaultLabel', "input");
const NLS_PRESERVE_CASE_LABEL = nls.localize('label.preserveCaseCheckbox', "Preserve Case");
export class PreserveCaseCheckbox extends Checkbox {
    constructor(opts) {
        super({
            // TODO: does this need its own icon?
            icon: Codicon.preserveCase,
            title: NLS_PRESERVE_CASE_LABEL + opts.appendTitle,
            isChecked: opts.isChecked,
            inputActiveOptionBorder: opts.inputActiveOptionBorder,
            inputActiveOptionForeground: opts.inputActiveOptionForeground,
            inputActiveOptionBackground: opts.inputActiveOptionBackground
        });
    }
}
export class ReplaceInput extends Widget {
    constructor(parent, contextViewProvider, _showOptionButtons, options) {
        super();
        this._showOptionButtons = _showOptionButtons;
        this.fixFocusOnOptionClickEnabled = true;
        this.cachedOptionsWidth = 0;
        this._onDidOptionChange = this._register(new Emitter());
        this.onDidOptionChange = this._onDidOptionChange.event;
        this._onKeyDown = this._register(new Emitter());
        this.onKeyDown = this._onKeyDown.event;
        this._onMouseDown = this._register(new Emitter());
        this._onInput = this._register(new Emitter());
        this._onKeyUp = this._register(new Emitter());
        this._onPreserveCaseKeyDown = this._register(new Emitter());
        this.onPreserveCaseKeyDown = this._onPreserveCaseKeyDown.event;
        this.contextViewProvider = contextViewProvider;
        this.placeholder = options.placeholder || '';
        this.validation = options.validation;
        this.label = options.label || NLS_DEFAULT_LABEL;
        this.inputActiveOptionBorder = options.inputActiveOptionBorder;
        this.inputActiveOptionForeground = options.inputActiveOptionForeground;
        this.inputActiveOptionBackground = options.inputActiveOptionBackground;
        this.inputBackground = options.inputBackground;
        this.inputForeground = options.inputForeground;
        this.inputBorder = options.inputBorder;
        this.inputValidationInfoBorder = options.inputValidationInfoBorder;
        this.inputValidationInfoBackground = options.inputValidationInfoBackground;
        this.inputValidationInfoForeground = options.inputValidationInfoForeground;
        this.inputValidationWarningBorder = options.inputValidationWarningBorder;
        this.inputValidationWarningBackground = options.inputValidationWarningBackground;
        this.inputValidationWarningForeground = options.inputValidationWarningForeground;
        this.inputValidationErrorBorder = options.inputValidationErrorBorder;
        this.inputValidationErrorBackground = options.inputValidationErrorBackground;
        this.inputValidationErrorForeground = options.inputValidationErrorForeground;
        const appendPreserveCaseLabel = options.appendPreserveCaseLabel || '';
        const history = options.history || [];
        const flexibleHeight = !!options.flexibleHeight;
        const flexibleWidth = !!options.flexibleWidth;
        const flexibleMaxHeight = options.flexibleMaxHeight;
        this.domNode = document.createElement('div');
        this.domNode.classList.add('monaco-findInput');
        this.inputBox = this._register(new HistoryInputBox(this.domNode, this.contextViewProvider, {
            ariaLabel: this.label || '',
            placeholder: this.placeholder || '',
            validationOptions: {
                validation: this.validation
            },
            inputBackground: this.inputBackground,
            inputForeground: this.inputForeground,
            inputBorder: this.inputBorder,
            inputValidationInfoBackground: this.inputValidationInfoBackground,
            inputValidationInfoForeground: this.inputValidationInfoForeground,
            inputValidationInfoBorder: this.inputValidationInfoBorder,
            inputValidationWarningBackground: this.inputValidationWarningBackground,
            inputValidationWarningForeground: this.inputValidationWarningForeground,
            inputValidationWarningBorder: this.inputValidationWarningBorder,
            inputValidationErrorBackground: this.inputValidationErrorBackground,
            inputValidationErrorForeground: this.inputValidationErrorForeground,
            inputValidationErrorBorder: this.inputValidationErrorBorder,
            history,
            flexibleHeight,
            flexibleWidth,
            flexibleMaxHeight
        }));
        this.preserveCase = this._register(new PreserveCaseCheckbox({
            appendTitle: appendPreserveCaseLabel,
            isChecked: false,
            inputActiveOptionBorder: this.inputActiveOptionBorder,
            inputActiveOptionForeground: this.inputActiveOptionForeground,
            inputActiveOptionBackground: this.inputActiveOptionBackground,
        }));
        this._register(this.preserveCase.onChange(viaKeyboard => {
            this._onDidOptionChange.fire(viaKeyboard);
            if (!viaKeyboard && this.fixFocusOnOptionClickEnabled) {
                this.inputBox.focus();
            }
            this.validate();
        }));
        this._register(this.preserveCase.onKeyDown(e => {
            this._onPreserveCaseKeyDown.fire(e);
        }));
        if (this._showOptionButtons) {
            this.cachedOptionsWidth = this.preserveCase.width();
        }
        else {
            this.cachedOptionsWidth = 0;
        }
        // Arrow-Key support to navigate between options
        let indexes = [this.preserveCase.domNode];
        this.onkeydown(this.domNode, (event) => {
            if (event.equals(15 /* LeftArrow */) || event.equals(17 /* RightArrow */) || event.equals(9 /* Escape */)) {
                let index = indexes.indexOf(document.activeElement);
                if (index >= 0) {
                    let newIndex = -1;
                    if (event.equals(17 /* RightArrow */)) {
                        newIndex = (index + 1) % indexes.length;
                    }
                    else if (event.equals(15 /* LeftArrow */)) {
                        if (index === 0) {
                            newIndex = indexes.length - 1;
                        }
                        else {
                            newIndex = index - 1;
                        }
                    }
                    if (event.equals(9 /* Escape */)) {
                        indexes[index].blur();
                        this.inputBox.focus();
                    }
                    else if (newIndex >= 0) {
                        indexes[newIndex].focus();
                    }
                    dom.EventHelper.stop(event, true);
                }
            }
        });
        let controls = document.createElement('div');
        controls.className = 'controls';
        controls.style.display = this._showOptionButtons ? 'block' : 'none';
        controls.appendChild(this.preserveCase.domNode);
        this.domNode.appendChild(controls);
        if (parent) {
            parent.appendChild(this.domNode);
        }
        this.onkeydown(this.inputBox.inputElement, (e) => this._onKeyDown.fire(e));
        this.onkeyup(this.inputBox.inputElement, (e) => this._onKeyUp.fire(e));
        this.oninput(this.inputBox.inputElement, (e) => this._onInput.fire());
        this.onmousedown(this.inputBox.inputElement, (e) => this._onMouseDown.fire(e));
    }
    enable() {
        this.domNode.classList.remove('disabled');
        this.inputBox.enable();
        this.preserveCase.enable();
    }
    disable() {
        this.domNode.classList.add('disabled');
        this.inputBox.disable();
        this.preserveCase.disable();
    }
    setEnabled(enabled) {
        if (enabled) {
            this.enable();
        }
        else {
            this.disable();
        }
    }
    style(styles) {
        this.inputActiveOptionBorder = styles.inputActiveOptionBorder;
        this.inputActiveOptionForeground = styles.inputActiveOptionForeground;
        this.inputActiveOptionBackground = styles.inputActiveOptionBackground;
        this.inputBackground = styles.inputBackground;
        this.inputForeground = styles.inputForeground;
        this.inputBorder = styles.inputBorder;
        this.inputValidationInfoBackground = styles.inputValidationInfoBackground;
        this.inputValidationInfoForeground = styles.inputValidationInfoForeground;
        this.inputValidationInfoBorder = styles.inputValidationInfoBorder;
        this.inputValidationWarningBackground = styles.inputValidationWarningBackground;
        this.inputValidationWarningForeground = styles.inputValidationWarningForeground;
        this.inputValidationWarningBorder = styles.inputValidationWarningBorder;
        this.inputValidationErrorBackground = styles.inputValidationErrorBackground;
        this.inputValidationErrorForeground = styles.inputValidationErrorForeground;
        this.inputValidationErrorBorder = styles.inputValidationErrorBorder;
        this.applyStyles();
    }
    applyStyles() {
        if (this.domNode) {
            const checkBoxStyles = {
                inputActiveOptionBorder: this.inputActiveOptionBorder,
                inputActiveOptionForeground: this.inputActiveOptionForeground,
                inputActiveOptionBackground: this.inputActiveOptionBackground,
            };
            this.preserveCase.style(checkBoxStyles);
            const inputBoxStyles = {
                inputBackground: this.inputBackground,
                inputForeground: this.inputForeground,
                inputBorder: this.inputBorder,
                inputValidationInfoBackground: this.inputValidationInfoBackground,
                inputValidationInfoForeground: this.inputValidationInfoForeground,
                inputValidationInfoBorder: this.inputValidationInfoBorder,
                inputValidationWarningBackground: this.inputValidationWarningBackground,
                inputValidationWarningForeground: this.inputValidationWarningForeground,
                inputValidationWarningBorder: this.inputValidationWarningBorder,
                inputValidationErrorBackground: this.inputValidationErrorBackground,
                inputValidationErrorForeground: this.inputValidationErrorForeground,
                inputValidationErrorBorder: this.inputValidationErrorBorder
            };
            this.inputBox.style(inputBoxStyles);
        }
    }
    select() {
        this.inputBox.select();
    }
    focus() {
        this.inputBox.focus();
    }
    getPreserveCase() {
        return this.preserveCase.checked;
    }
    setPreserveCase(value) {
        this.preserveCase.checked = value;
    }
    focusOnPreserve() {
        this.preserveCase.focus();
    }
    validate() {
        if (this.inputBox) {
            this.inputBox.validate();
        }
    }
    set width(newWidth) {
        this.inputBox.paddingRight = this.cachedOptionsWidth;
        this.inputBox.width = newWidth;
        this.domNode.style.width = newWidth + 'px';
    }
    dispose() {
        super.dispose();
    }
}
