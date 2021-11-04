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
import * as dom from '../../../browser/dom.js';
import { dispose } from '../../../common/lifecycle.js';
import { matchesFuzzyIconAware, parseLabelWithIcons } from '../../../common/iconLabels.js';
import { compareAnything } from '../../../common/comparers.js';
import { Emitter, Event } from '../../../common/event.js';
import { StandardKeyboardEvent } from '../../../browser/keyboardEvent.js';
import { IconLabel } from '../../../browser/ui/iconLabel/iconLabel.js';
import { HighlightedLabel } from '../../../browser/ui/highlightedlabel/highlightedLabel.js';
import { memoize } from '../../../common/decorators.js';
import { range } from '../../../common/arrays.js';
import * as platform from '../../../common/platform.js';
import { ActionBar } from '../../../browser/ui/actionbar/actionbar.js';
import { Action } from '../../../common/actions.js';
import { getIconClass } from './quickInputUtils.js';
import { withNullAsUndefined } from '../../../common/types.js';
import { KeybindingLabel } from '../../../browser/ui/keybindingLabel/keybindingLabel.js';
import { localize } from '../../../../nls.js';
const $ = dom.$;
class ListElement {
    constructor(init) {
        this.hidden = false;
        this._onChecked = new Emitter();
        this.onChecked = this._onChecked.event;
        Object.assign(this, init);
    }
    get checked() {
        return !!this._checked;
    }
    set checked(value) {
        if (value !== this._checked) {
            this._checked = value;
            this._onChecked.fire(value);
        }
    }
    dispose() {
        this._onChecked.dispose();
    }
}
class ListElementRenderer {
    get templateId() {
        return ListElementRenderer.ID;
    }
    renderTemplate(container) {
        const data = Object.create(null);
        data.toDisposeElement = [];
        data.toDisposeTemplate = [];
        data.entry = dom.append(container, $('.quick-input-list-entry'));
        // Checkbox
        const label = dom.append(data.entry, $('label.quick-input-list-label'));
        data.toDisposeTemplate.push(dom.addStandardDisposableListener(label, dom.EventType.CLICK, e => {
            if (!data.checkbox.offsetParent) { // If checkbox not visible:
                e.preventDefault(); // Prevent toggle of checkbox when it is immediately shown afterwards. #91740
            }
        }));
        data.checkbox = dom.append(label, $('input.quick-input-list-checkbox'));
        data.checkbox.type = 'checkbox';
        data.toDisposeTemplate.push(dom.addStandardDisposableListener(data.checkbox, dom.EventType.CHANGE, e => {
            data.element.checked = data.checkbox.checked;
        }));
        // Rows
        const rows = dom.append(label, $('.quick-input-list-rows'));
        const row1 = dom.append(rows, $('.quick-input-list-row'));
        const row2 = dom.append(rows, $('.quick-input-list-row'));
        // Label
        data.label = new IconLabel(row1, { supportHighlights: true, supportDescriptionHighlights: true, supportIcons: true });
        // Keybinding
        const keybindingContainer = dom.append(row1, $('.quick-input-list-entry-keybinding'));
        data.keybinding = new KeybindingLabel(keybindingContainer, platform.OS);
        // Detail
        const detailContainer = dom.append(row2, $('.quick-input-list-label-meta'));
        data.detail = new HighlightedLabel(detailContainer, true);
        // Separator
        data.separator = dom.append(data.entry, $('.quick-input-list-separator'));
        // Actions
        data.actionBar = new ActionBar(data.entry);
        data.actionBar.domNode.classList.add('quick-input-list-entry-action-bar');
        data.toDisposeTemplate.push(data.actionBar);
        return data;
    }
    renderElement(element, index, data) {
        data.toDisposeElement = dispose(data.toDisposeElement);
        data.element = element;
        data.checkbox.checked = element.checked;
        data.toDisposeElement.push(element.onChecked(checked => data.checkbox.checked = checked));
        const { labelHighlights, descriptionHighlights, detailHighlights } = element;
        // Label
        const options = Object.create(null);
        options.matches = labelHighlights || [];
        options.descriptionTitle = element.saneDescription;
        options.descriptionMatches = descriptionHighlights || [];
        options.extraClasses = element.item.iconClasses;
        options.italic = element.item.italic;
        options.strikethrough = element.item.strikethrough;
        data.label.setLabel(element.saneLabel, element.saneDescription, options);
        // Keybinding
        data.keybinding.set(element.item.keybinding);
        // Meta
        data.detail.set(element.saneDetail, detailHighlights);
        // Separator
        if (element.separator && element.separator.label) {
            data.separator.textContent = element.separator.label;
            data.separator.style.display = '';
        }
        else {
            data.separator.style.display = 'none';
        }
        data.entry.classList.toggle('quick-input-list-separator-border', !!element.separator);
        // Actions
        data.actionBar.clear();
        const buttons = element.item.buttons;
        if (buttons && buttons.length) {
            data.actionBar.push(buttons.map((button, index) => {
                let cssClasses = button.iconClass || (button.iconPath ? getIconClass(button.iconPath) : undefined);
                if (button.alwaysVisible) {
                    cssClasses = cssClasses ? `${cssClasses} always-visible` : 'always-visible';
                }
                const action = new Action(`id-${index}`, '', cssClasses, true, () => __awaiter(this, void 0, void 0, function* () {
                    element.fireButtonTriggered({
                        button,
                        item: element.item
                    });
                }));
                action.tooltip = button.tooltip || '';
                return action;
            }), { icon: true, label: false });
            data.entry.classList.add('has-actions');
        }
        else {
            data.entry.classList.remove('has-actions');
        }
    }
    disposeElement(element, index, data) {
        data.toDisposeElement = dispose(data.toDisposeElement);
    }
    disposeTemplate(data) {
        data.toDisposeElement = dispose(data.toDisposeElement);
        data.toDisposeTemplate = dispose(data.toDisposeTemplate);
    }
}
ListElementRenderer.ID = 'listelement';
class ListElementDelegate {
    getHeight(element) {
        return element.saneDetail ? 44 : 22;
    }
    getTemplateId(element) {
        return ListElementRenderer.ID;
    }
}
export var QuickInputListFocus;
(function (QuickInputListFocus) {
    QuickInputListFocus[QuickInputListFocus["First"] = 1] = "First";
    QuickInputListFocus[QuickInputListFocus["Second"] = 2] = "Second";
    QuickInputListFocus[QuickInputListFocus["Last"] = 3] = "Last";
    QuickInputListFocus[QuickInputListFocus["Next"] = 4] = "Next";
    QuickInputListFocus[QuickInputListFocus["Previous"] = 5] = "Previous";
    QuickInputListFocus[QuickInputListFocus["NextPage"] = 6] = "NextPage";
    QuickInputListFocus[QuickInputListFocus["PreviousPage"] = 7] = "PreviousPage";
})(QuickInputListFocus || (QuickInputListFocus = {}));
export class QuickInputList {
    constructor(parent, id, options) {
        this.parent = parent;
        this.inputElements = [];
        this.elements = [];
        this.elementsToIndexes = new Map();
        this.matchOnDescription = false;
        this.matchOnDetail = false;
        this.matchOnLabel = true;
        this.matchOnMeta = true;
        this.sortByLabel = true;
        this._onChangedAllVisibleChecked = new Emitter();
        this.onChangedAllVisibleChecked = this._onChangedAllVisibleChecked.event;
        this._onChangedCheckedCount = new Emitter();
        this.onChangedCheckedCount = this._onChangedCheckedCount.event;
        this._onChangedVisibleCount = new Emitter();
        this.onChangedVisibleCount = this._onChangedVisibleCount.event;
        this._onChangedCheckedElements = new Emitter();
        this.onChangedCheckedElements = this._onChangedCheckedElements.event;
        this._onButtonTriggered = new Emitter();
        this.onButtonTriggered = this._onButtonTriggered.event;
        this._onKeyDown = new Emitter();
        this.onKeyDown = this._onKeyDown.event;
        this._onLeave = new Emitter();
        this.onLeave = this._onLeave.event;
        this._fireCheckedEvents = true;
        this.elementDisposables = [];
        this.disposables = [];
        this.id = id;
        this.container = dom.append(this.parent, $('.quick-input-list'));
        const delegate = new ListElementDelegate();
        const accessibilityProvider = new QuickInputAccessibilityProvider();
        this.list = options.createList('QuickInput', this.container, delegate, [new ListElementRenderer()], {
            identityProvider: { getId: element => element.saneLabel },
            setRowLineHeight: false,
            multipleSelectionSupport: false,
            horizontalScrolling: false,
            accessibilityProvider
        });
        this.list.getHTMLElement().id = id;
        this.disposables.push(this.list);
        this.disposables.push(this.list.onKeyDown(e => {
            const event = new StandardKeyboardEvent(e);
            switch (event.keyCode) {
                case 10 /* Space */:
                    this.toggleCheckbox();
                    break;
                case 31 /* KEY_A */:
                    if (platform.isMacintosh ? e.metaKey : e.ctrlKey) {
                        this.list.setFocus(range(this.list.length));
                    }
                    break;
                case 16 /* UpArrow */:
                    const focus1 = this.list.getFocus();
                    if (focus1.length === 1 && focus1[0] === 0) {
                        this._onLeave.fire();
                    }
                    break;
                case 18 /* DownArrow */:
                    const focus2 = this.list.getFocus();
                    if (focus2.length === 1 && focus2[0] === this.list.length - 1) {
                        this._onLeave.fire();
                    }
                    break;
            }
            this._onKeyDown.fire(event);
        }));
        this.disposables.push(this.list.onMouseDown(e => {
            if (e.browserEvent.button !== 2) {
                // Works around / fixes #64350.
                e.browserEvent.preventDefault();
            }
        }));
        this.disposables.push(dom.addDisposableListener(this.container, dom.EventType.CLICK, e => {
            if (e.x || e.y) { // Avoid 'click' triggered by 'space' on checkbox.
                this._onLeave.fire();
            }
        }));
        this.disposables.push(this.list.onMouseMiddleClick(e => {
            this._onLeave.fire();
        }));
        this.disposables.push(this.list.onContextMenu(e => {
            if (typeof e.index === 'number') {
                e.browserEvent.preventDefault();
                // we want to treat a context menu event as
                // a gesture to open the item at the index
                // since we do not have any context menu
                // this enables for example macOS to Ctrl-
                // click on an item to open it.
                this.list.setSelection([e.index]);
            }
        }));
        this.disposables.push(this._onChangedAllVisibleChecked, this._onChangedCheckedCount, this._onChangedVisibleCount, this._onChangedCheckedElements, this._onButtonTriggered, this._onLeave, this._onKeyDown);
    }
    get onDidChangeFocus() {
        return Event.map(this.list.onDidChangeFocus, e => e.elements.map(e => e.item));
    }
    get onDidChangeSelection() {
        return Event.map(this.list.onDidChangeSelection, e => ({ items: e.elements.map(e => e.item), event: e.browserEvent }));
    }
    getAllVisibleChecked() {
        return this.allVisibleChecked(this.elements, false);
    }
    allVisibleChecked(elements, whenNoneVisible = true) {
        for (let i = 0, n = elements.length; i < n; i++) {
            const element = elements[i];
            if (!element.hidden) {
                if (!element.checked) {
                    return false;
                }
                else {
                    whenNoneVisible = true;
                }
            }
        }
        return whenNoneVisible;
    }
    getCheckedCount() {
        let count = 0;
        const elements = this.elements;
        for (let i = 0, n = elements.length; i < n; i++) {
            if (elements[i].checked) {
                count++;
            }
        }
        return count;
    }
    getVisibleCount() {
        let count = 0;
        const elements = this.elements;
        for (let i = 0, n = elements.length; i < n; i++) {
            if (!elements[i].hidden) {
                count++;
            }
        }
        return count;
    }
    setAllVisibleChecked(checked) {
        try {
            this._fireCheckedEvents = false;
            this.elements.forEach(element => {
                if (!element.hidden) {
                    element.checked = checked;
                }
            });
        }
        finally {
            this._fireCheckedEvents = true;
            this.fireCheckedEvents();
        }
    }
    setElements(inputElements) {
        this.elementDisposables = dispose(this.elementDisposables);
        const fireButtonTriggered = (event) => this.fireButtonTriggered(event);
        this.inputElements = inputElements;
        this.elements = inputElements.reduce((result, item, index) => {
            var _a, _b, _c;
            if (item.type !== 'separator') {
                const previous = index && inputElements[index - 1];
                const saneLabel = item.label && item.label.replace(/\r?\n/g, ' ');
                const saneMeta = item.meta && item.meta.replace(/\r?\n/g, ' ');
                const saneDescription = item.description && item.description.replace(/\r?\n/g, ' ');
                const saneDetail = item.detail && item.detail.replace(/\r?\n/g, ' ');
                const saneAriaLabel = item.ariaLabel || [saneLabel, saneDescription, saneDetail]
                    .map(s => s && parseLabelWithIcons(s).text)
                    .filter(s => !!s)
                    .join(', ');
                result.push(new ListElement({
                    index,
                    item,
                    saneLabel,
                    saneMeta,
                    saneAriaLabel,
                    saneDescription,
                    saneDetail,
                    labelHighlights: (_a = item.highlights) === null || _a === void 0 ? void 0 : _a.label,
                    descriptionHighlights: (_b = item.highlights) === null || _b === void 0 ? void 0 : _b.description,
                    detailHighlights: (_c = item.highlights) === null || _c === void 0 ? void 0 : _c.detail,
                    checked: false,
                    separator: previous && previous.type === 'separator' ? previous : undefined,
                    fireButtonTriggered
                }));
            }
            return result;
        }, []);
        this.elementDisposables.push(...this.elements);
        this.elementDisposables.push(...this.elements.map(element => element.onChecked(() => this.fireCheckedEvents())));
        this.elementsToIndexes = this.elements.reduce((map, element, index) => {
            map.set(element.item, index);
            return map;
        }, new Map());
        this.list.splice(0, this.list.length); // Clear focus and selection first, sending the events when the list is empty.
        this.list.splice(0, this.list.length, this.elements);
        this._onChangedVisibleCount.fire(this.elements.length);
    }
    getFocusedElements() {
        return this.list.getFocusedElements()
            .map(e => e.item);
    }
    setFocusedElements(items) {
        this.list.setFocus(items
            .filter(item => this.elementsToIndexes.has(item))
            .map(item => this.elementsToIndexes.get(item)));
        if (items.length > 0) {
            const focused = this.list.getFocus()[0];
            if (typeof focused === 'number') {
                this.list.reveal(focused);
            }
        }
    }
    getActiveDescendant() {
        return this.list.getHTMLElement().getAttribute('aria-activedescendant');
    }
    setSelectedElements(items) {
        this.list.setSelection(items
            .filter(item => this.elementsToIndexes.has(item))
            .map(item => this.elementsToIndexes.get(item)));
    }
    getCheckedElements() {
        return this.elements.filter(e => e.checked)
            .map(e => e.item);
    }
    setCheckedElements(items) {
        try {
            this._fireCheckedEvents = false;
            const checked = new Set();
            for (const item of items) {
                checked.add(item);
            }
            for (const element of this.elements) {
                element.checked = checked.has(element.item);
            }
        }
        finally {
            this._fireCheckedEvents = true;
            this.fireCheckedEvents();
        }
    }
    set enabled(value) {
        this.list.getHTMLElement().style.pointerEvents = value ? '' : 'none';
    }
    focus(what) {
        if (!this.list.length) {
            return;
        }
        if (what === QuickInputListFocus.Next && this.list.getFocus()[0] === this.list.length - 1) {
            what = QuickInputListFocus.First;
        }
        if (what === QuickInputListFocus.Previous && this.list.getFocus()[0] === 0) {
            what = QuickInputListFocus.Last;
        }
        if (what === QuickInputListFocus.Second && this.list.length < 2) {
            what = QuickInputListFocus.First;
        }
        switch (what) {
            case QuickInputListFocus.First:
                this.list.focusFirst();
                break;
            case QuickInputListFocus.Second:
                this.list.focusNth(1);
                break;
            case QuickInputListFocus.Last:
                this.list.focusLast();
                break;
            case QuickInputListFocus.Next:
                this.list.focusNext();
                break;
            case QuickInputListFocus.Previous:
                this.list.focusPrevious();
                break;
            case QuickInputListFocus.NextPage:
                this.list.focusNextPage();
                break;
            case QuickInputListFocus.PreviousPage:
                this.list.focusPreviousPage();
                break;
        }
        const focused = this.list.getFocus()[0];
        if (typeof focused === 'number') {
            this.list.reveal(focused);
        }
    }
    clearFocus() {
        this.list.setFocus([]);
    }
    domFocus() {
        this.list.domFocus();
    }
    layout(maxHeight) {
        this.list.getHTMLElement().style.maxHeight = maxHeight ? `calc(${Math.floor(maxHeight / 44) * 44}px)` : '';
        this.list.layout();
    }
    filter(query) {
        if (!(this.sortByLabel || this.matchOnLabel || this.matchOnDescription || this.matchOnDetail)) {
            this.list.layout();
            return false;
        }
        query = query.trim();
        // Reset filtering
        if (!query || !(this.matchOnLabel || this.matchOnDescription || this.matchOnDetail)) {
            this.elements.forEach(element => {
                element.labelHighlights = undefined;
                element.descriptionHighlights = undefined;
                element.detailHighlights = undefined;
                element.hidden = false;
                const previous = element.index && this.inputElements[element.index - 1];
                element.separator = previous && previous.type === 'separator' ? previous : undefined;
            });
        }
        // Filter by value (since we support icons in labels, use $(..) aware fuzzy matching)
        else {
            this.elements.forEach(element => {
                const labelHighlights = this.matchOnLabel ? withNullAsUndefined(matchesFuzzyIconAware(query, parseLabelWithIcons(element.saneLabel))) : undefined;
                const descriptionHighlights = this.matchOnDescription ? withNullAsUndefined(matchesFuzzyIconAware(query, parseLabelWithIcons(element.saneDescription || ''))) : undefined;
                const detailHighlights = this.matchOnDetail ? withNullAsUndefined(matchesFuzzyIconAware(query, parseLabelWithIcons(element.saneDetail || ''))) : undefined;
                const metaHighlights = this.matchOnMeta ? withNullAsUndefined(matchesFuzzyIconAware(query, parseLabelWithIcons(element.saneMeta || ''))) : undefined;
                if (labelHighlights || descriptionHighlights || detailHighlights || metaHighlights) {
                    element.labelHighlights = labelHighlights;
                    element.descriptionHighlights = descriptionHighlights;
                    element.detailHighlights = detailHighlights;
                    element.hidden = false;
                }
                else {
                    element.labelHighlights = undefined;
                    element.descriptionHighlights = undefined;
                    element.detailHighlights = undefined;
                    element.hidden = !element.item.alwaysShow;
                }
                element.separator = undefined;
            });
        }
        const shownElements = this.elements.filter(element => !element.hidden);
        // Sort by value
        if (this.sortByLabel && query) {
            const normalizedSearchValue = query.toLowerCase();
            shownElements.sort((a, b) => {
                return compareEntries(a, b, normalizedSearchValue);
            });
        }
        this.elementsToIndexes = shownElements.reduce((map, element, index) => {
            map.set(element.item, index);
            return map;
        }, new Map());
        this.list.splice(0, this.list.length, shownElements);
        this.list.setFocus([]);
        this.list.layout();
        this._onChangedAllVisibleChecked.fire(this.getAllVisibleChecked());
        this._onChangedVisibleCount.fire(shownElements.length);
        return true;
    }
    toggleCheckbox() {
        try {
            this._fireCheckedEvents = false;
            const elements = this.list.getFocusedElements();
            const allChecked = this.allVisibleChecked(elements);
            for (const element of elements) {
                element.checked = !allChecked;
            }
        }
        finally {
            this._fireCheckedEvents = true;
            this.fireCheckedEvents();
        }
    }
    display(display) {
        this.container.style.display = display ? '' : 'none';
    }
    isDisplayed() {
        return this.container.style.display !== 'none';
    }
    dispose() {
        this.elementDisposables = dispose(this.elementDisposables);
        this.disposables = dispose(this.disposables);
    }
    fireCheckedEvents() {
        if (this._fireCheckedEvents) {
            this._onChangedAllVisibleChecked.fire(this.getAllVisibleChecked());
            this._onChangedCheckedCount.fire(this.getCheckedCount());
            this._onChangedCheckedElements.fire(this.getCheckedElements());
        }
    }
    fireButtonTriggered(event) {
        this._onButtonTriggered.fire(event);
    }
    style(styles) {
        this.list.style(styles);
    }
}
__decorate([
    memoize
], QuickInputList.prototype, "onDidChangeFocus", null);
__decorate([
    memoize
], QuickInputList.prototype, "onDidChangeSelection", null);
function compareEntries(elementA, elementB, lookFor) {
    const labelHighlightsA = elementA.labelHighlights || [];
    const labelHighlightsB = elementB.labelHighlights || [];
    if (labelHighlightsA.length && !labelHighlightsB.length) {
        return -1;
    }
    if (!labelHighlightsA.length && labelHighlightsB.length) {
        return 1;
    }
    if (labelHighlightsA.length === 0 && labelHighlightsB.length === 0) {
        return 0;
    }
    return compareAnything(elementA.saneLabel, elementB.saneLabel, lookFor);
}
class QuickInputAccessibilityProvider {
    getWidgetAriaLabel() {
        return localize('quickInput', "Quick Input");
    }
    getAriaLabel(element) {
        return element.saneAriaLabel;
    }
    getWidgetRole() {
        return 'listbox';
    }
    getRole() {
        return 'option';
    }
}
