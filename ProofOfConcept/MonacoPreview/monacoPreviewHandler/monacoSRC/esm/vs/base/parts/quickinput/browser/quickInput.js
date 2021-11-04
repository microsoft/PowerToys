/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
var __awaiter = (this && this.__awaiter) || function (thisArg, _arguments, P, generator) {
    function adopt(value) { return value instanceof P ? value : new P(function (resolve) { resolve(value); }); }
    return new (P || (P = Promise))(function (resolve, reject) {
        function fulfilled(value) { try { step(generator.next(value)); } catch (e) { reject(e); } }
        function rejected(value) { try { step(generator["throw"](value)); } catch (e) { reject(e); } }
        function step(result) { result.done ? resolve(result.value) : adopt(result.value).then(fulfilled, rejected); }
        step((generator = generator.apply(thisArg, _arguments || [])).next());
    });
};
import './media/quickInput.css';
import { NO_KEY_MODS, ItemActivation } from '../common/quickInput.js';
import * as dom from '../../../browser/dom.js';
import { CancellationToken } from '../../../common/cancellation.js';
import { QuickInputList, QuickInputListFocus } from './quickInputList.js';
import { QuickInputBox } from './quickInputBox.js';
import { StandardKeyboardEvent } from '../../../browser/keyboardEvent.js';
import { localize } from '../../../../nls.js';
import { CountBadge } from '../../../browser/ui/countBadge/countBadge.js';
import { ProgressBar } from '../../../browser/ui/progressbar/progressbar.js';
import { Emitter } from '../../../common/event.js';
import { Button } from '../../../browser/ui/button/button.js';
import { dispose, Disposable, DisposableStore } from '../../../common/lifecycle.js';
import Severity from '../../../common/severity.js';
import { ActionBar } from '../../../browser/ui/actionbar/actionbar.js';
import { Action } from '../../../common/actions.js';
import { equals } from '../../../common/arrays.js';
import { TimeoutTimer } from '../../../common/async.js';
import { getIconClass } from './quickInputUtils.js';
import { registerCodicon, Codicon } from '../../../common/codicons.js';
import { escape } from '../../../common/strings.js';
import { renderLabelWithIcons } from '../../../browser/ui/iconLabel/iconLabels.js';
const $ = dom.$;
const backButtonIcon = registerCodicon('quick-input-back', Codicon.arrowLeft);
const backButton = {
    iconClass: backButtonIcon.classNames,
    tooltip: localize('quickInput.back', "Back"),
    handle: -1 // TODO
};
class QuickInput extends Disposable {
    constructor(ui) {
        super();
        this.ui = ui;
        this.visible = false;
        this._enabled = true;
        this._busy = false;
        this._ignoreFocusOut = false;
        this._buttons = [];
        this.buttonsUpdated = false;
        this.onDidTriggerButtonEmitter = this._register(new Emitter());
        this.onDidHideEmitter = this._register(new Emitter());
        this.onDisposeEmitter = this._register(new Emitter());
        this.visibleDisposables = this._register(new DisposableStore());
        this.onDidHide = this.onDidHideEmitter.event;
    }
    get title() {
        return this._title;
    }
    set title(title) {
        this._title = title;
        this.update();
    }
    get description() {
        return this._description;
    }
    set description(description) {
        this._description = description;
        this.update();
    }
    get step() {
        return this._steps;
    }
    set step(step) {
        this._steps = step;
        this.update();
    }
    get totalSteps() {
        return this._totalSteps;
    }
    set totalSteps(totalSteps) {
        this._totalSteps = totalSteps;
        this.update();
    }
    get enabled() {
        return this._enabled;
    }
    set enabled(enabled) {
        this._enabled = enabled;
        this.update();
    }
    get contextKey() {
        return this._contextKey;
    }
    set contextKey(contextKey) {
        this._contextKey = contextKey;
        this.update();
    }
    get busy() {
        return this._busy;
    }
    set busy(busy) {
        this._busy = busy;
        this.update();
    }
    get ignoreFocusOut() {
        return this._ignoreFocusOut;
    }
    set ignoreFocusOut(ignoreFocusOut) {
        this._ignoreFocusOut = ignoreFocusOut;
        this.update();
    }
    get buttons() {
        return this._buttons;
    }
    set buttons(buttons) {
        this._buttons = buttons;
        this.buttonsUpdated = true;
        this.update();
    }
    show() {
        if (this.visible) {
            return;
        }
        this.visibleDisposables.add(this.ui.onDidTriggerButton(button => {
            if (this.buttons.indexOf(button) !== -1) {
                this.onDidTriggerButtonEmitter.fire(button);
            }
        }));
        this.ui.show(this);
        this.visible = true;
        this.update();
    }
    hide() {
        if (!this.visible) {
            return;
        }
        this.ui.hide();
    }
    didHide() {
        this.visible = false;
        this.visibleDisposables.clear();
        this.onDidHideEmitter.fire();
    }
    update() {
        if (!this.visible) {
            return;
        }
        const title = this.getTitle();
        if (title && this.ui.title.textContent !== title) {
            this.ui.title.textContent = title;
        }
        else if (!title && this.ui.title.innerHTML !== '&nbsp;') {
            this.ui.title.innerText = '\u00a0;';
        }
        const description = this.getDescription();
        if (this.ui.description1.textContent !== description) {
            this.ui.description1.textContent = description;
        }
        if (this.ui.description2.textContent !== description) {
            this.ui.description2.textContent = description;
        }
        if (this.busy && !this.busyDelay) {
            this.busyDelay = new TimeoutTimer();
            this.busyDelay.setIfNotSet(() => {
                if (this.visible) {
                    this.ui.progressBar.infinite();
                }
            }, 800);
        }
        if (!this.busy && this.busyDelay) {
            this.ui.progressBar.stop();
            this.busyDelay.cancel();
            this.busyDelay = undefined;
        }
        if (this.buttonsUpdated) {
            this.buttonsUpdated = false;
            this.ui.leftActionBar.clear();
            const leftButtons = this.buttons.filter(button => button === backButton);
            this.ui.leftActionBar.push(leftButtons.map((button, index) => {
                const action = new Action(`id-${index}`, '', button.iconClass || getIconClass(button.iconPath), true, () => __awaiter(this, void 0, void 0, function* () {
                    this.onDidTriggerButtonEmitter.fire(button);
                }));
                action.tooltip = button.tooltip || '';
                return action;
            }), { icon: true, label: false });
            this.ui.rightActionBar.clear();
            const rightButtons = this.buttons.filter(button => button !== backButton);
            this.ui.rightActionBar.push(rightButtons.map((button, index) => {
                const action = new Action(`id-${index}`, '', button.iconClass || getIconClass(button.iconPath), true, () => __awaiter(this, void 0, void 0, function* () {
                    this.onDidTriggerButtonEmitter.fire(button);
                }));
                action.tooltip = button.tooltip || '';
                return action;
            }), { icon: true, label: false });
        }
        this.ui.ignoreFocusOut = this.ignoreFocusOut;
        this.ui.setEnabled(this.enabled);
        this.ui.setContextKey(this.contextKey);
    }
    getTitle() {
        if (this.title && this.step) {
            return `${this.title} (${this.getSteps()})`;
        }
        if (this.title) {
            return this.title;
        }
        if (this.step) {
            return this.getSteps();
        }
        return '';
    }
    getDescription() {
        return this.description || '';
    }
    getSteps() {
        if (this.step && this.totalSteps) {
            return localize('quickInput.steps', "{0}/{1}", this.step, this.totalSteps);
        }
        if (this.step) {
            return String(this.step);
        }
        return '';
    }
    showMessageDecoration(severity) {
        this.ui.inputBox.showDecoration(severity);
        if (severity === Severity.Error) {
            const styles = this.ui.inputBox.stylesForType(severity);
            this.ui.message.style.color = styles.foreground ? `${styles.foreground}` : '';
            this.ui.message.style.backgroundColor = styles.background ? `${styles.background}` : '';
            this.ui.message.style.border = styles.border ? `1px solid ${styles.border}` : '';
            this.ui.message.style.paddingBottom = '4px';
        }
        else {
            this.ui.message.style.color = '';
            this.ui.message.style.backgroundColor = '';
            this.ui.message.style.border = '';
            this.ui.message.style.paddingBottom = '';
        }
    }
    dispose() {
        this.hide();
        this.onDisposeEmitter.fire();
        super.dispose();
    }
}
class QuickPick extends QuickInput {
    constructor() {
        super(...arguments);
        this._value = '';
        this.onDidChangeValueEmitter = this._register(new Emitter());
        this.onDidAcceptEmitter = this._register(new Emitter());
        this.onDidCustomEmitter = this._register(new Emitter());
        this._items = [];
        this.itemsUpdated = false;
        this._canSelectMany = false;
        this._canAcceptInBackground = false;
        this._matchOnDescription = false;
        this._matchOnDetail = false;
        this._matchOnLabel = true;
        this._sortByLabel = true;
        this._autoFocusOnList = true;
        this._itemActivation = this.ui.isScreenReaderOptimized() ? ItemActivation.NONE /* https://github.com/microsoft/vscode/issues/57501 */ : ItemActivation.FIRST;
        this._activeItems = [];
        this.activeItemsUpdated = false;
        this.activeItemsToConfirm = [];
        this.onDidChangeActiveEmitter = this._register(new Emitter());
        this._selectedItems = [];
        this.selectedItemsUpdated = false;
        this.selectedItemsToConfirm = [];
        this.onDidChangeSelectionEmitter = this._register(new Emitter());
        this.onDidTriggerItemButtonEmitter = this._register(new Emitter());
        this.valueSelectionUpdated = true;
        this._ok = 'default';
        this._customButton = false;
        this.filterValue = (value) => value;
        this.onDidChangeValue = this.onDidChangeValueEmitter.event;
        this.onDidAccept = this.onDidAcceptEmitter.event;
        this.onDidChangeActive = this.onDidChangeActiveEmitter.event;
        this.onDidChangeSelection = this.onDidChangeSelectionEmitter.event;
        this.onDidTriggerItemButton = this.onDidTriggerItemButtonEmitter.event;
    }
    get quickNavigate() {
        return this._quickNavigate;
    }
    set quickNavigate(quickNavigate) {
        this._quickNavigate = quickNavigate;
        this.update();
    }
    get value() {
        return this._value;
    }
    set value(value) {
        this._value = value || '';
        this.update();
    }
    set ariaLabel(ariaLabel) {
        this._ariaLabel = ariaLabel;
        this.update();
    }
    get ariaLabel() {
        return this._ariaLabel;
    }
    get placeholder() {
        return this._placeholder;
    }
    set placeholder(placeholder) {
        this._placeholder = placeholder;
        this.update();
    }
    get items() {
        return this._items;
    }
    set items(items) {
        this._items = items;
        this.itemsUpdated = true;
        this.update();
    }
    get canSelectMany() {
        return this._canSelectMany;
    }
    set canSelectMany(canSelectMany) {
        this._canSelectMany = canSelectMany;
        this.update();
    }
    get canAcceptInBackground() {
        return this._canAcceptInBackground;
    }
    set canAcceptInBackground(canAcceptInBackground) {
        this._canAcceptInBackground = canAcceptInBackground;
    }
    get matchOnDescription() {
        return this._matchOnDescription;
    }
    set matchOnDescription(matchOnDescription) {
        this._matchOnDescription = matchOnDescription;
        this.update();
    }
    get matchOnDetail() {
        return this._matchOnDetail;
    }
    set matchOnDetail(matchOnDetail) {
        this._matchOnDetail = matchOnDetail;
        this.update();
    }
    get matchOnLabel() {
        return this._matchOnLabel;
    }
    set matchOnLabel(matchOnLabel) {
        this._matchOnLabel = matchOnLabel;
        this.update();
    }
    get sortByLabel() {
        return this._sortByLabel;
    }
    set sortByLabel(sortByLabel) {
        this._sortByLabel = sortByLabel;
        this.update();
    }
    get autoFocusOnList() {
        return this._autoFocusOnList;
    }
    set autoFocusOnList(autoFocusOnList) {
        this._autoFocusOnList = autoFocusOnList;
        this.update();
    }
    get itemActivation() {
        return this._itemActivation;
    }
    set itemActivation(itemActivation) {
        this._itemActivation = itemActivation;
    }
    get activeItems() {
        return this._activeItems;
    }
    set activeItems(activeItems) {
        this._activeItems = activeItems;
        this.activeItemsUpdated = true;
        this.update();
    }
    get selectedItems() {
        return this._selectedItems;
    }
    set selectedItems(selectedItems) {
        this._selectedItems = selectedItems;
        this.selectedItemsUpdated = true;
        this.update();
    }
    get keyMods() {
        if (this._quickNavigate) {
            // Disable keyMods when quick navigate is enabled
            // because in this model the interaction is purely
            // keyboard driven and Ctrl/Alt are typically
            // pressed and hold during this interaction.
            return NO_KEY_MODS;
        }
        return this.ui.keyMods;
    }
    set valueSelection(valueSelection) {
        this._valueSelection = valueSelection;
        this.valueSelectionUpdated = true;
        this.update();
    }
    get validationMessage() {
        return this._validationMessage;
    }
    set validationMessage(validationMessage) {
        this._validationMessage = validationMessage;
        this.update();
    }
    get customButton() {
        return this._customButton;
    }
    set customButton(showCustomButton) {
        this._customButton = showCustomButton;
        this.update();
    }
    get customLabel() {
        return this._customButtonLabel;
    }
    set customLabel(label) {
        this._customButtonLabel = label;
        this.update();
    }
    get customHover() {
        return this._customButtonHover;
    }
    set customHover(hover) {
        this._customButtonHover = hover;
        this.update();
    }
    get ok() {
        return this._ok;
    }
    set ok(showOkButton) {
        this._ok = showOkButton;
        this.update();
    }
    get hideInput() {
        return !!this._hideInput;
    }
    set hideInput(hideInput) {
        this._hideInput = hideInput;
        this.update();
    }
    trySelectFirst() {
        if (this.autoFocusOnList) {
            if (!this.canSelectMany) {
                this.ui.list.focus(QuickInputListFocus.First);
            }
        }
    }
    show() {
        if (!this.visible) {
            this.visibleDisposables.add(this.ui.inputBox.onDidChange(value => {
                if (value === this.value) {
                    return;
                }
                this._value = value;
                const didFilter = this.ui.list.filter(this.filterValue(this.ui.inputBox.value));
                if (didFilter) {
                    this.trySelectFirst();
                }
                this.onDidChangeValueEmitter.fire(value);
            }));
            this.visibleDisposables.add(this.ui.inputBox.onMouseDown(event => {
                if (!this.autoFocusOnList) {
                    this.ui.list.clearFocus();
                }
            }));
            this.visibleDisposables.add((this._hideInput ? this.ui.list : this.ui.inputBox).onKeyDown((event) => {
                switch (event.keyCode) {
                    case 18 /* DownArrow */:
                        this.ui.list.focus(QuickInputListFocus.Next);
                        if (this.canSelectMany) {
                            this.ui.list.domFocus();
                        }
                        dom.EventHelper.stop(event, true);
                        break;
                    case 16 /* UpArrow */:
                        if (this.ui.list.getFocusedElements().length) {
                            this.ui.list.focus(QuickInputListFocus.Previous);
                        }
                        else {
                            this.ui.list.focus(QuickInputListFocus.Last);
                        }
                        if (this.canSelectMany) {
                            this.ui.list.domFocus();
                        }
                        dom.EventHelper.stop(event, true);
                        break;
                    case 12 /* PageDown */:
                        this.ui.list.focus(QuickInputListFocus.NextPage);
                        if (this.canSelectMany) {
                            this.ui.list.domFocus();
                        }
                        dom.EventHelper.stop(event, true);
                        break;
                    case 11 /* PageUp */:
                        this.ui.list.focus(QuickInputListFocus.PreviousPage);
                        if (this.canSelectMany) {
                            this.ui.list.domFocus();
                        }
                        dom.EventHelper.stop(event, true);
                        break;
                    case 17 /* RightArrow */:
                        if (!this._canAcceptInBackground) {
                            return; // needs to be enabled
                        }
                        if (!this.ui.inputBox.isSelectionAtEnd()) {
                            return; // ensure input box selection at end
                        }
                        if (this.activeItems[0]) {
                            this._selectedItems = [this.activeItems[0]];
                            this.onDidChangeSelectionEmitter.fire(this.selectedItems);
                            this.onDidAcceptEmitter.fire({ inBackground: true });
                        }
                        break;
                    case 14 /* Home */:
                        if ((event.ctrlKey || event.metaKey) && !event.shiftKey && !event.altKey) {
                            this.ui.list.focus(QuickInputListFocus.First);
                            dom.EventHelper.stop(event, true);
                        }
                        break;
                    case 13 /* End */:
                        if ((event.ctrlKey || event.metaKey) && !event.shiftKey && !event.altKey) {
                            this.ui.list.focus(QuickInputListFocus.Last);
                            dom.EventHelper.stop(event, true);
                        }
                        break;
                }
            }));
            this.visibleDisposables.add(this.ui.onDidAccept(() => {
                if (!this.canSelectMany && this.activeItems[0]) {
                    this._selectedItems = [this.activeItems[0]];
                    this.onDidChangeSelectionEmitter.fire(this.selectedItems);
                }
                this.onDidAcceptEmitter.fire({ inBackground: false });
            }));
            this.visibleDisposables.add(this.ui.onDidCustom(() => {
                this.onDidCustomEmitter.fire();
            }));
            this.visibleDisposables.add(this.ui.list.onDidChangeFocus(focusedItems => {
                if (this.activeItemsUpdated) {
                    return; // Expect another event.
                }
                if (this.activeItemsToConfirm !== this._activeItems && equals(focusedItems, this._activeItems, (a, b) => a === b)) {
                    return;
                }
                this._activeItems = focusedItems;
                this.onDidChangeActiveEmitter.fire(focusedItems);
            }));
            this.visibleDisposables.add(this.ui.list.onDidChangeSelection(({ items: selectedItems, event }) => {
                if (this.canSelectMany) {
                    if (selectedItems.length) {
                        this.ui.list.setSelectedElements([]);
                    }
                    return;
                }
                if (this.selectedItemsToConfirm !== this._selectedItems && equals(selectedItems, this._selectedItems, (a, b) => a === b)) {
                    return;
                }
                this._selectedItems = selectedItems;
                this.onDidChangeSelectionEmitter.fire(selectedItems);
                if (selectedItems.length) {
                    this.onDidAcceptEmitter.fire({ inBackground: event instanceof MouseEvent && event.button === 1 /* mouse middle click */ });
                }
            }));
            this.visibleDisposables.add(this.ui.list.onChangedCheckedElements(checkedItems => {
                if (!this.canSelectMany) {
                    return;
                }
                if (this.selectedItemsToConfirm !== this._selectedItems && equals(checkedItems, this._selectedItems, (a, b) => a === b)) {
                    return;
                }
                this._selectedItems = checkedItems;
                this.onDidChangeSelectionEmitter.fire(checkedItems);
            }));
            this.visibleDisposables.add(this.ui.list.onButtonTriggered(event => this.onDidTriggerItemButtonEmitter.fire(event)));
            this.visibleDisposables.add(this.registerQuickNavigation());
            this.valueSelectionUpdated = true;
        }
        super.show(); // TODO: Why have show() bubble up while update() trickles down? (Could move setComboboxAccessibility() here.)
    }
    registerQuickNavigation() {
        return dom.addDisposableListener(this.ui.container, dom.EventType.KEY_UP, e => {
            if (this.canSelectMany || !this._quickNavigate) {
                return;
            }
            const keyboardEvent = new StandardKeyboardEvent(e);
            const keyCode = keyboardEvent.keyCode;
            // Select element when keys are pressed that signal it
            const quickNavKeys = this._quickNavigate.keybindings;
            const wasTriggerKeyPressed = quickNavKeys.some(k => {
                const [firstPart, chordPart] = k.getParts();
                if (chordPart) {
                    return false;
                }
                if (firstPart.shiftKey && keyCode === 4 /* Shift */) {
                    if (keyboardEvent.ctrlKey || keyboardEvent.altKey || keyboardEvent.metaKey) {
                        return false; // this is an optimistic check for the shift key being used to navigate back in quick input
                    }
                    return true;
                }
                if (firstPart.altKey && keyCode === 6 /* Alt */) {
                    return true;
                }
                if (firstPart.ctrlKey && keyCode === 5 /* Ctrl */) {
                    return true;
                }
                if (firstPart.metaKey && keyCode === 57 /* Meta */) {
                    return true;
                }
                return false;
            });
            if (wasTriggerKeyPressed) {
                if (this.activeItems[0]) {
                    this._selectedItems = [this.activeItems[0]];
                    this.onDidChangeSelectionEmitter.fire(this.selectedItems);
                    this.onDidAcceptEmitter.fire({ inBackground: false });
                }
                // Unset quick navigate after press. It is only valid once
                // and should not result in any behaviour change afterwards
                // if the picker remains open because there was no active item
                this._quickNavigate = undefined;
            }
        });
    }
    update() {
        if (!this.visible) {
            return;
        }
        let hideInput = false;
        let inputShownJustForScreenReader = false;
        if (!!this._hideInput && this._items.length > 0) {
            if (this.ui.isScreenReaderOptimized()) {
                // Always show input if screen reader attached https://github.com/microsoft/vscode/issues/94360
                inputShownJustForScreenReader = true;
            }
            else {
                hideInput = true;
            }
        }
        this.ui.container.classList.toggle('hidden-input', hideInput && !this.description);
        const visibilities = {
            title: !!this.title || !!this.step || !!this.buttons.length,
            description: !!this.description,
            checkAll: this.canSelectMany && !this._hideCheckAll,
            checkBox: this.canSelectMany,
            inputBox: !hideInput,
            progressBar: !hideInput,
            visibleCount: true,
            count: this.canSelectMany,
            ok: this.ok === 'default' ? this.canSelectMany : this.ok,
            list: true,
            message: !!this.validationMessage,
            customButton: this.customButton
        };
        this.ui.setVisibilities(visibilities);
        super.update();
        if (this.ui.inputBox.value !== this.value) {
            this.ui.inputBox.value = this.value;
        }
        if (this.valueSelectionUpdated) {
            this.valueSelectionUpdated = false;
            this.ui.inputBox.select(this._valueSelection && { start: this._valueSelection[0], end: this._valueSelection[1] });
        }
        if (this.ui.inputBox.placeholder !== (this.placeholder || '')) {
            this.ui.inputBox.placeholder = (this.placeholder || '');
        }
        if (inputShownJustForScreenReader) {
            this.ui.inputBox.ariaLabel = '';
        }
        else {
            const ariaLabel = this.ariaLabel || this.placeholder || QuickPick.DEFAULT_ARIA_LABEL;
            if (this.ui.inputBox.ariaLabel !== ariaLabel) {
                this.ui.inputBox.ariaLabel = ariaLabel;
            }
        }
        this.ui.list.matchOnDescription = this.matchOnDescription;
        this.ui.list.matchOnDetail = this.matchOnDetail;
        this.ui.list.matchOnLabel = this.matchOnLabel;
        this.ui.list.sortByLabel = this.sortByLabel;
        if (this.itemsUpdated) {
            this.itemsUpdated = false;
            this.ui.list.setElements(this.items);
            this.ui.list.filter(this.filterValue(this.ui.inputBox.value));
            this.ui.checkAll.checked = this.ui.list.getAllVisibleChecked();
            this.ui.visibleCount.setCount(this.ui.list.getVisibleCount());
            this.ui.count.setCount(this.ui.list.getCheckedCount());
            switch (this._itemActivation) {
                case ItemActivation.NONE:
                    this._itemActivation = ItemActivation.FIRST; // only valid once, then unset
                    break;
                case ItemActivation.SECOND:
                    this.ui.list.focus(QuickInputListFocus.Second);
                    this._itemActivation = ItemActivation.FIRST; // only valid once, then unset
                    break;
                case ItemActivation.LAST:
                    this.ui.list.focus(QuickInputListFocus.Last);
                    this._itemActivation = ItemActivation.FIRST; // only valid once, then unset
                    break;
                default:
                    this.trySelectFirst();
                    break;
            }
        }
        if (this.ui.container.classList.contains('show-checkboxes') !== !!this.canSelectMany) {
            if (this.canSelectMany) {
                this.ui.list.clearFocus();
            }
            else {
                this.trySelectFirst();
            }
        }
        if (this.activeItemsUpdated) {
            this.activeItemsUpdated = false;
            this.activeItemsToConfirm = this._activeItems;
            this.ui.list.setFocusedElements(this.activeItems);
            if (this.activeItemsToConfirm === this._activeItems) {
                this.activeItemsToConfirm = null;
            }
        }
        if (this.selectedItemsUpdated) {
            this.selectedItemsUpdated = false;
            this.selectedItemsToConfirm = this._selectedItems;
            if (this.canSelectMany) {
                this.ui.list.setCheckedElements(this.selectedItems);
            }
            else {
                this.ui.list.setSelectedElements(this.selectedItems);
            }
            if (this.selectedItemsToConfirm === this._selectedItems) {
                this.selectedItemsToConfirm = null;
            }
        }
        const validationMessage = this.validationMessage || '';
        if (this._lastValidationMessage !== validationMessage) {
            this._lastValidationMessage = validationMessage;
            dom.reset(this.ui.message, ...renderLabelWithIcons(escape(validationMessage)));
            this.showMessageDecoration(this.validationMessage ? Severity.Error : Severity.Ignore);
        }
        this.ui.customButton.label = this.customLabel || '';
        this.ui.customButton.element.title = this.customHover || '';
        this.ui.setComboboxAccessibility(true);
        if (!visibilities.inputBox) {
            // we need to move focus into the tree to detect keybindings
            // properly when the input box is not visible (quick nav)
            this.ui.list.domFocus();
            // Focus the first element in the list if multiselect is enabled
            if (this.canSelectMany) {
                this.ui.list.focus(QuickInputListFocus.First);
            }
        }
    }
}
QuickPick.DEFAULT_ARIA_LABEL = localize('quickInputBox.ariaLabel', "Type to narrow down results.");
export class QuickInputController extends Disposable {
    constructor(options) {
        super();
        this.options = options;
        this.comboboxAccessibility = false;
        this.enabled = true;
        this.onDidAcceptEmitter = this._register(new Emitter());
        this.onDidCustomEmitter = this._register(new Emitter());
        this.onDidTriggerButtonEmitter = this._register(new Emitter());
        this.keyMods = { ctrlCmd: false, alt: false };
        this.controller = null;
        this.onShowEmitter = this._register(new Emitter());
        this.onShow = this.onShowEmitter.event;
        this.onHideEmitter = this._register(new Emitter());
        this.onHide = this.onHideEmitter.event;
        this.idPrefix = options.idPrefix;
        this.parentElement = options.container;
        this.styles = options.styles;
        this.registerKeyModsListeners();
    }
    registerKeyModsListeners() {
        const listener = (e) => {
            this.keyMods.ctrlCmd = e.ctrlKey || e.metaKey;
            this.keyMods.alt = e.altKey;
        };
        this._register(dom.addDisposableListener(window, dom.EventType.KEY_DOWN, listener, true));
        this._register(dom.addDisposableListener(window, dom.EventType.KEY_UP, listener, true));
        this._register(dom.addDisposableListener(window, dom.EventType.MOUSE_DOWN, listener, true));
    }
    getUI() {
        if (this.ui) {
            return this.ui;
        }
        const container = dom.append(this.parentElement, $('.quick-input-widget.show-file-icons'));
        container.tabIndex = -1;
        container.style.display = 'none';
        const styleSheet = dom.createStyleSheet(container);
        const titleBar = dom.append(container, $('.quick-input-titlebar'));
        const leftActionBar = this._register(new ActionBar(titleBar));
        leftActionBar.domNode.classList.add('quick-input-left-action-bar');
        const title = dom.append(titleBar, $('.quick-input-title'));
        const rightActionBar = this._register(new ActionBar(titleBar));
        rightActionBar.domNode.classList.add('quick-input-right-action-bar');
        const description1 = dom.append(container, $('.quick-input-description'));
        const headerContainer = dom.append(container, $('.quick-input-header'));
        const checkAll = dom.append(headerContainer, $('input.quick-input-check-all'));
        checkAll.type = 'checkbox';
        this._register(dom.addStandardDisposableListener(checkAll, dom.EventType.CHANGE, e => {
            const checked = checkAll.checked;
            list.setAllVisibleChecked(checked);
        }));
        this._register(dom.addDisposableListener(checkAll, dom.EventType.CLICK, e => {
            if (e.x || e.y) { // Avoid 'click' triggered by 'space'...
                inputBox.setFocus();
            }
        }));
        const description2 = dom.append(headerContainer, $('.quick-input-description'));
        const extraContainer = dom.append(headerContainer, $('.quick-input-and-message'));
        const filterContainer = dom.append(extraContainer, $('.quick-input-filter'));
        const inputBox = this._register(new QuickInputBox(filterContainer));
        inputBox.setAttribute('aria-describedby', `${this.idPrefix}message`);
        const visibleCountContainer = dom.append(filterContainer, $('.quick-input-visible-count'));
        visibleCountContainer.setAttribute('aria-live', 'polite');
        visibleCountContainer.setAttribute('aria-atomic', 'true');
        const visibleCount = new CountBadge(visibleCountContainer, { countFormat: localize({ key: 'quickInput.visibleCount', comment: ['This tells the user how many items are shown in a list of items to select from. The items can be anything. Currently not visible, but read by screen readers.'] }, "{0} Results") });
        const countContainer = dom.append(filterContainer, $('.quick-input-count'));
        countContainer.setAttribute('aria-live', 'polite');
        const count = new CountBadge(countContainer, { countFormat: localize({ key: 'quickInput.countSelected', comment: ['This tells the user how many items are selected in a list of items to select from. The items can be anything.'] }, "{0} Selected") });
        const okContainer = dom.append(headerContainer, $('.quick-input-action'));
        const ok = new Button(okContainer);
        ok.label = localize('ok', "OK");
        this._register(ok.onDidClick(e => {
            this.onDidAcceptEmitter.fire();
        }));
        const customButtonContainer = dom.append(headerContainer, $('.quick-input-action'));
        const customButton = new Button(customButtonContainer);
        customButton.label = localize('custom', "Custom");
        this._register(customButton.onDidClick(e => {
            this.onDidCustomEmitter.fire();
        }));
        const message = dom.append(extraContainer, $(`#${this.idPrefix}message.quick-input-message`));
        const progressBar = new ProgressBar(container);
        progressBar.getContainer().classList.add('quick-input-progress');
        const list = this._register(new QuickInputList(container, this.idPrefix + 'list', this.options));
        this._register(list.onChangedAllVisibleChecked(checked => {
            checkAll.checked = checked;
        }));
        this._register(list.onChangedVisibleCount(c => {
            visibleCount.setCount(c);
        }));
        this._register(list.onChangedCheckedCount(c => {
            count.setCount(c);
        }));
        this._register(list.onLeave(() => {
            // Defer to avoid the input field reacting to the triggering key.
            setTimeout(() => {
                inputBox.setFocus();
                if (this.controller instanceof QuickPick && this.controller.canSelectMany) {
                    list.clearFocus();
                }
            }, 0);
        }));
        this._register(list.onDidChangeFocus(() => {
            if (this.comboboxAccessibility) {
                this.getUI().inputBox.setAttribute('aria-activedescendant', this.getUI().list.getActiveDescendant() || '');
            }
        }));
        const focusTracker = dom.trackFocus(container);
        this._register(focusTracker);
        this._register(dom.addDisposableListener(container, dom.EventType.FOCUS, e => {
            this.previousFocusElement = e.relatedTarget instanceof HTMLElement ? e.relatedTarget : undefined;
        }, true));
        this._register(focusTracker.onDidBlur(() => {
            if (!this.getUI().ignoreFocusOut && !this.options.ignoreFocusOut()) {
                this.hide();
            }
            this.previousFocusElement = undefined;
        }));
        this._register(dom.addDisposableListener(container, dom.EventType.FOCUS, (e) => {
            inputBox.setFocus();
        }));
        this._register(dom.addDisposableListener(container, dom.EventType.KEY_DOWN, (e) => {
            const event = new StandardKeyboardEvent(e);
            switch (event.keyCode) {
                case 3 /* Enter */:
                    dom.EventHelper.stop(e, true);
                    this.onDidAcceptEmitter.fire();
                    break;
                case 9 /* Escape */:
                    dom.EventHelper.stop(e, true);
                    this.hide();
                    break;
                case 2 /* Tab */:
                    if (!event.altKey && !event.ctrlKey && !event.metaKey) {
                        const selectors = ['.action-label.codicon'];
                        if (container.classList.contains('show-checkboxes')) {
                            selectors.push('input');
                        }
                        else {
                            selectors.push('input[type=text]');
                        }
                        if (this.getUI().list.isDisplayed()) {
                            selectors.push('.monaco-list');
                        }
                        const stops = container.querySelectorAll(selectors.join(', '));
                        if (event.shiftKey && event.target === stops[0]) {
                            dom.EventHelper.stop(e, true);
                            stops[stops.length - 1].focus();
                        }
                        else if (!event.shiftKey && event.target === stops[stops.length - 1]) {
                            dom.EventHelper.stop(e, true);
                            stops[0].focus();
                        }
                    }
                    break;
            }
        }));
        this.ui = {
            container,
            styleSheet,
            leftActionBar,
            titleBar,
            title,
            description1,
            description2,
            rightActionBar,
            checkAll,
            filterContainer,
            inputBox,
            visibleCountContainer,
            visibleCount,
            countContainer,
            count,
            okContainer,
            ok,
            message,
            customButtonContainer,
            customButton,
            progressBar,
            list,
            onDidAccept: this.onDidAcceptEmitter.event,
            onDidCustom: this.onDidCustomEmitter.event,
            onDidTriggerButton: this.onDidTriggerButtonEmitter.event,
            ignoreFocusOut: false,
            keyMods: this.keyMods,
            isScreenReaderOptimized: () => this.options.isScreenReaderOptimized(),
            show: controller => this.show(controller),
            hide: () => this.hide(),
            setVisibilities: visibilities => this.setVisibilities(visibilities),
            setComboboxAccessibility: enabled => this.setComboboxAccessibility(enabled),
            setEnabled: enabled => this.setEnabled(enabled),
            setContextKey: contextKey => this.options.setContextKey(contextKey),
        };
        this.updateStyles();
        return this.ui;
    }
    pick(picks, options = {}, token = CancellationToken.None) {
        return new Promise((doResolve, reject) => {
            let resolve = (result) => {
                resolve = doResolve;
                if (options.onKeyMods) {
                    options.onKeyMods(input.keyMods);
                }
                doResolve(result);
            };
            if (token.isCancellationRequested) {
                resolve(undefined);
                return;
            }
            const input = this.createQuickPick();
            let activeItem;
            const disposables = [
                input,
                input.onDidAccept(() => {
                    if (input.canSelectMany) {
                        resolve(input.selectedItems.slice());
                        input.hide();
                    }
                    else {
                        const result = input.activeItems[0];
                        if (result) {
                            resolve(result);
                            input.hide();
                        }
                    }
                }),
                input.onDidChangeActive(items => {
                    const focused = items[0];
                    if (focused && options.onDidFocus) {
                        options.onDidFocus(focused);
                    }
                }),
                input.onDidChangeSelection(items => {
                    if (!input.canSelectMany) {
                        const result = items[0];
                        if (result) {
                            resolve(result);
                            input.hide();
                        }
                    }
                }),
                input.onDidTriggerItemButton(event => options.onDidTriggerItemButton && options.onDidTriggerItemButton(Object.assign(Object.assign({}, event), { removeItem: () => {
                        const index = input.items.indexOf(event.item);
                        if (index !== -1) {
                            const items = input.items.slice();
                            items.splice(index, 1);
                            input.items = items;
                        }
                    } }))),
                input.onDidChangeValue(value => {
                    if (activeItem && !value && (input.activeItems.length !== 1 || input.activeItems[0] !== activeItem)) {
                        input.activeItems = [activeItem];
                    }
                }),
                token.onCancellationRequested(() => {
                    input.hide();
                }),
                input.onDidHide(() => {
                    dispose(disposables);
                    resolve(undefined);
                }),
            ];
            input.canSelectMany = !!options.canPickMany;
            input.placeholder = options.placeHolder;
            input.ignoreFocusOut = !!options.ignoreFocusLost;
            input.matchOnDescription = !!options.matchOnDescription;
            input.matchOnDetail = !!options.matchOnDetail;
            input.matchOnLabel = (options.matchOnLabel === undefined) || options.matchOnLabel; // default to true
            input.autoFocusOnList = (options.autoFocusOnList === undefined) || options.autoFocusOnList; // default to true
            input.quickNavigate = options.quickNavigate;
            input.contextKey = options.contextKey;
            input.busy = true;
            Promise.all([picks, options.activeItem])
                .then(([items, _activeItem]) => {
                activeItem = _activeItem;
                input.busy = false;
                input.items = items;
                if (input.canSelectMany) {
                    input.selectedItems = items.filter(item => item.type !== 'separator' && item.picked);
                }
                if (activeItem) {
                    input.activeItems = [activeItem];
                }
            });
            input.show();
            Promise.resolve(picks).then(undefined, err => {
                reject(err);
                input.hide();
            });
        });
    }
    createQuickPick() {
        const ui = this.getUI();
        return new QuickPick(ui);
    }
    show(controller) {
        const ui = this.getUI();
        this.onShowEmitter.fire();
        const oldController = this.controller;
        this.controller = controller;
        if (oldController) {
            oldController.didHide();
        }
        this.setEnabled(true);
        ui.leftActionBar.clear();
        ui.title.textContent = '';
        ui.description1.textContent = '';
        ui.description2.textContent = '';
        ui.rightActionBar.clear();
        ui.checkAll.checked = false;
        // ui.inputBox.value = ''; Avoid triggering an event.
        ui.inputBox.placeholder = '';
        ui.inputBox.password = false;
        ui.inputBox.showDecoration(Severity.Ignore);
        ui.visibleCount.setCount(0);
        ui.count.setCount(0);
        dom.reset(ui.message);
        ui.progressBar.stop();
        ui.list.setElements([]);
        ui.list.matchOnDescription = false;
        ui.list.matchOnDetail = false;
        ui.list.matchOnLabel = true;
        ui.list.sortByLabel = true;
        ui.ignoreFocusOut = false;
        this.setComboboxAccessibility(false);
        ui.inputBox.ariaLabel = '';
        const backKeybindingLabel = this.options.backKeybindingLabel();
        backButton.tooltip = backKeybindingLabel ? localize('quickInput.backWithKeybinding', "Back ({0})", backKeybindingLabel) : localize('quickInput.back', "Back");
        ui.container.style.display = '';
        this.updateLayout();
        ui.inputBox.setFocus();
    }
    setVisibilities(visibilities) {
        const ui = this.getUI();
        ui.title.style.display = visibilities.title ? '' : 'none';
        ui.description1.style.display = visibilities.description && (visibilities.inputBox || visibilities.checkAll) ? '' : 'none';
        ui.description2.style.display = visibilities.description && !(visibilities.inputBox || visibilities.checkAll) ? '' : 'none';
        ui.checkAll.style.display = visibilities.checkAll ? '' : 'none';
        ui.filterContainer.style.display = visibilities.inputBox ? '' : 'none';
        ui.visibleCountContainer.style.display = visibilities.visibleCount ? '' : 'none';
        ui.countContainer.style.display = visibilities.count ? '' : 'none';
        ui.okContainer.style.display = visibilities.ok ? '' : 'none';
        ui.customButtonContainer.style.display = visibilities.customButton ? '' : 'none';
        ui.message.style.display = visibilities.message ? '' : 'none';
        ui.progressBar.getContainer().style.display = visibilities.progressBar ? '' : 'none';
        ui.list.display(!!visibilities.list);
        ui.container.classList[visibilities.checkBox ? 'add' : 'remove']('show-checkboxes');
        this.updateLayout(); // TODO
    }
    setComboboxAccessibility(enabled) {
        if (enabled !== this.comboboxAccessibility) {
            const ui = this.getUI();
            this.comboboxAccessibility = enabled;
            if (this.comboboxAccessibility) {
                ui.inputBox.setAttribute('role', 'combobox');
                ui.inputBox.setAttribute('aria-haspopup', 'true');
                ui.inputBox.setAttribute('aria-autocomplete', 'list');
                ui.inputBox.setAttribute('aria-activedescendant', ui.list.getActiveDescendant() || '');
            }
            else {
                ui.inputBox.removeAttribute('role');
                ui.inputBox.removeAttribute('aria-haspopup');
                ui.inputBox.removeAttribute('aria-autocomplete');
                ui.inputBox.removeAttribute('aria-activedescendant');
            }
        }
    }
    setEnabled(enabled) {
        if (enabled !== this.enabled) {
            this.enabled = enabled;
            for (const item of this.getUI().leftActionBar.viewItems) {
                item.getAction().enabled = enabled;
            }
            for (const item of this.getUI().rightActionBar.viewItems) {
                item.getAction().enabled = enabled;
            }
            this.getUI().checkAll.disabled = !enabled;
            // this.getUI().inputBox.enabled = enabled; Avoid loosing focus.
            this.getUI().ok.enabled = enabled;
            this.getUI().list.enabled = enabled;
        }
    }
    hide() {
        var _a;
        const controller = this.controller;
        if (controller) {
            const focusChanged = !((_a = this.ui) === null || _a === void 0 ? void 0 : _a.container.contains(document.activeElement));
            this.controller = null;
            this.onHideEmitter.fire();
            this.getUI().container.style.display = 'none';
            if (!focusChanged) {
                if (this.previousFocusElement && this.previousFocusElement.offsetParent) {
                    this.previousFocusElement.focus();
                    this.previousFocusElement = undefined;
                }
                else {
                    this.options.returnFocus();
                }
            }
            controller.didHide();
        }
    }
    layout(dimension, titleBarOffset) {
        this.dimension = dimension;
        this.titleBarOffset = titleBarOffset;
        this.updateLayout();
    }
    updateLayout() {
        if (this.ui) {
            this.ui.container.style.top = `${this.titleBarOffset}px`;
            const style = this.ui.container.style;
            const width = Math.min(this.dimension.width * 0.62 /* golden cut */, QuickInputController.MAX_WIDTH);
            style.width = width + 'px';
            style.marginLeft = '-' + (width / 2) + 'px';
            this.ui.inputBox.layout();
            this.ui.list.layout(this.dimension && this.dimension.height * 0.4);
        }
    }
    applyStyles(styles) {
        this.styles = styles;
        this.updateStyles();
    }
    updateStyles() {
        if (this.ui) {
            const { quickInputTitleBackground, quickInputBackground, quickInputForeground, contrastBorder, widgetShadow, } = this.styles.widget;
            this.ui.titleBar.style.backgroundColor = quickInputTitleBackground ? quickInputTitleBackground.toString() : '';
            this.ui.container.style.backgroundColor = quickInputBackground ? quickInputBackground.toString() : '';
            this.ui.container.style.color = quickInputForeground ? quickInputForeground.toString() : '';
            this.ui.container.style.border = contrastBorder ? `1px solid ${contrastBorder}` : '';
            this.ui.container.style.boxShadow = widgetShadow ? `0 0 8px 2px ${widgetShadow}` : '';
            this.ui.inputBox.style(this.styles.inputBox);
            this.ui.count.style(this.styles.countBadge);
            this.ui.ok.style(this.styles.button);
            this.ui.customButton.style(this.styles.button);
            this.ui.progressBar.style(this.styles.progressBar);
            this.ui.list.style(this.styles.list);
            const content = [];
            if (this.styles.list.listInactiveFocusForeground) {
                content.push(`.monaco-list .monaco-list-row.focused { color:  ${this.styles.list.listInactiveFocusForeground}; }`);
                content.push(`.monaco-list .monaco-list-row.focused:hover { color:  ${this.styles.list.listInactiveFocusForeground}; }`); // overwrite :hover style in this case!
            }
            if (this.styles.list.pickerGroupBorder) {
                content.push(`.quick-input-list .quick-input-list-entry { border-top-color:  ${this.styles.list.pickerGroupBorder}; }`);
            }
            if (this.styles.list.pickerGroupForeground) {
                content.push(`.quick-input-list .quick-input-list-separator { color:  ${this.styles.list.pickerGroupForeground}; }`);
            }
            const newStyles = content.join('\n');
            if (newStyles !== this.ui.styleSheet.textContent) {
                this.ui.styleSheet.textContent = newStyles;
            }
        }
    }
}
QuickInputController.MAX_WIDTH = 600; // Max total width of quick input widget
