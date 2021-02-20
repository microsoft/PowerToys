/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import './media/quickInput.css';
import * as dom from '../../../browser/dom.js';
import { InputBox } from '../../../browser/ui/inputbox/inputBox.js';
import { Disposable } from '../../../common/lifecycle.js';
import { StandardKeyboardEvent } from '../../../browser/keyboardEvent.js';
import Severity from '../../../common/severity.js';
import { StandardMouseEvent } from '../../../browser/mouseEvent.js';
const $ = dom.$;
export class QuickInputBox extends Disposable {
    constructor(parent) {
        super();
        this.parent = parent;
        this.onKeyDown = (handler) => {
            return dom.addDisposableListener(this.inputBox.inputElement, dom.EventType.KEY_DOWN, (e) => {
                handler(new StandardKeyboardEvent(e));
            });
        };
        this.onMouseDown = (handler) => {
            return dom.addDisposableListener(this.inputBox.inputElement, dom.EventType.MOUSE_DOWN, (e) => {
                handler(new StandardMouseEvent(e));
            });
        };
        this.onDidChange = (handler) => {
            return this.inputBox.onDidChange(handler);
        };
        this.container = dom.append(this.parent, $('.quick-input-box'));
        this.inputBox = this._register(new InputBox(this.container, undefined));
    }
    get value() {
        return this.inputBox.value;
    }
    set value(value) {
        this.inputBox.value = value;
    }
    select(range = null) {
        this.inputBox.select(range);
    }
    isSelectionAtEnd() {
        return this.inputBox.isSelectionAtEnd();
    }
    get placeholder() {
        return this.inputBox.inputElement.getAttribute('placeholder') || '';
    }
    set placeholder(placeholder) {
        this.inputBox.setPlaceHolder(placeholder);
    }
    get ariaLabel() {
        return this.inputBox.getAriaLabel();
    }
    set ariaLabel(ariaLabel) {
        this.inputBox.setAriaLabel(ariaLabel);
    }
    get password() {
        return this.inputBox.inputElement.type === 'password';
    }
    set password(password) {
        this.inputBox.inputElement.type = password ? 'password' : 'text';
    }
    setAttribute(name, value) {
        this.inputBox.inputElement.setAttribute(name, value);
    }
    removeAttribute(name) {
        this.inputBox.inputElement.removeAttribute(name);
    }
    showDecoration(decoration) {
        if (decoration === Severity.Ignore) {
            this.inputBox.hideMessage();
        }
        else {
            this.inputBox.showMessage({ type: decoration === Severity.Info ? 1 /* INFO */ : decoration === Severity.Warning ? 2 /* WARNING */ : 3 /* ERROR */, content: '' });
        }
    }
    stylesForType(decoration) {
        return this.inputBox.stylesForType(decoration === Severity.Info ? 1 /* INFO */ : decoration === Severity.Warning ? 2 /* WARNING */ : 3 /* ERROR */);
    }
    setFocus() {
        this.inputBox.focus();
    }
    layout() {
        this.inputBox.layout();
    }
    style(styles) {
        this.inputBox.style(styles);
    }
}
