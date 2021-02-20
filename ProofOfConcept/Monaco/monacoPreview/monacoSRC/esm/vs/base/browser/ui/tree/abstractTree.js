/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import './media/tree.css';
import { dispose, Disposable, toDisposable, DisposableStore } from '../../../common/lifecycle.js';
import { List, MouseController, DefaultKeyboardNavigationDelegate, isInputElement, isMonacoEditor } from '../list/listWidget.js';
import { append, $, getDomNodePagePosition, hasParentWithClass, createStyleSheet, clearNode } from '../../dom.js';
import { Event, Relay, Emitter, EventBufferer } from '../../../common/event.js';
import { StandardKeyboardEvent } from '../../keyboardEvent.js';
import { TreeMouseEventTarget } from './tree.js';
import { StaticDND, DragAndDropData } from '../../dnd.js';
import { range, equals, distinctES6 } from '../../../common/arrays.js';
import { ElementsDragAndDropData } from '../list/listView.js';
import { domEvent } from '../../event.js';
import { fuzzyScore, FuzzyScore } from '../../../common/filters.js';
import { getVisibleState, isFilterResult } from './indexTreeModel.js';
import { localize } from '../../../../nls.js';
import { disposableTimeout } from '../../../common/async.js';
import { isMacintosh } from '../../../common/platform.js';
import { clamp } from '../../../common/numbers.js';
import { SetMap } from '../../../common/collections.js';
import { treeItemExpandedIcon, treeFilterOnTypeOnIcon, treeFilterOnTypeOffIcon, treeFilterClearIcon } from './treeIcons.js';
class TreeElementsDragAndDropData extends ElementsDragAndDropData {
    constructor(data) {
        super(data.elements.map(node => node.element));
        this.data = data;
    }
}
function asTreeDragAndDropData(data) {
    if (data instanceof ElementsDragAndDropData) {
        return new TreeElementsDragAndDropData(data);
    }
    return data;
}
class TreeNodeListDragAndDrop {
    constructor(modelProvider, dnd) {
        this.modelProvider = modelProvider;
        this.dnd = dnd;
        this.autoExpandDisposable = Disposable.None;
    }
    getDragURI(node) {
        return this.dnd.getDragURI(node.element);
    }
    getDragLabel(nodes, originalEvent) {
        if (this.dnd.getDragLabel) {
            return this.dnd.getDragLabel(nodes.map(node => node.element), originalEvent);
        }
        return undefined;
    }
    onDragStart(data, originalEvent) {
        if (this.dnd.onDragStart) {
            this.dnd.onDragStart(asTreeDragAndDropData(data), originalEvent);
        }
    }
    onDragOver(data, targetNode, targetIndex, originalEvent, raw = true) {
        const result = this.dnd.onDragOver(asTreeDragAndDropData(data), targetNode && targetNode.element, targetIndex, originalEvent);
        const didChangeAutoExpandNode = this.autoExpandNode !== targetNode;
        if (didChangeAutoExpandNode) {
            this.autoExpandDisposable.dispose();
            this.autoExpandNode = targetNode;
        }
        if (typeof targetNode === 'undefined') {
            return result;
        }
        if (didChangeAutoExpandNode && typeof result !== 'boolean' && result.autoExpand) {
            this.autoExpandDisposable = disposableTimeout(() => {
                const model = this.modelProvider();
                const ref = model.getNodeLocation(targetNode);
                if (model.isCollapsed(ref)) {
                    model.setCollapsed(ref, false);
                }
                this.autoExpandNode = undefined;
            }, 500);
        }
        if (typeof result === 'boolean' || !result.accept || typeof result.bubble === 'undefined' || result.feedback) {
            if (!raw) {
                const accept = typeof result === 'boolean' ? result : result.accept;
                const effect = typeof result === 'boolean' ? undefined : result.effect;
                return { accept, effect, feedback: [targetIndex] };
            }
            return result;
        }
        if (result.bubble === 1 /* Up */) {
            const model = this.modelProvider();
            const ref = model.getNodeLocation(targetNode);
            const parentRef = model.getParentNodeLocation(ref);
            const parentNode = model.getNode(parentRef);
            const parentIndex = parentRef && model.getListIndex(parentRef);
            return this.onDragOver(data, parentNode, parentIndex, originalEvent, false);
        }
        const model = this.modelProvider();
        const ref = model.getNodeLocation(targetNode);
        const start = model.getListIndex(ref);
        const length = model.getListRenderCount(ref);
        return Object.assign(Object.assign({}, result), { feedback: range(start, start + length) });
    }
    drop(data, targetNode, targetIndex, originalEvent) {
        this.autoExpandDisposable.dispose();
        this.autoExpandNode = undefined;
        this.dnd.drop(asTreeDragAndDropData(data), targetNode && targetNode.element, targetIndex, originalEvent);
    }
    onDragEnd(originalEvent) {
        if (this.dnd.onDragEnd) {
            this.dnd.onDragEnd(originalEvent);
        }
    }
}
function asListOptions(modelProvider, options) {
    return options && Object.assign(Object.assign({}, options), { identityProvider: options.identityProvider && {
            getId(el) {
                return options.identityProvider.getId(el.element);
            }
        }, dnd: options.dnd && new TreeNodeListDragAndDrop(modelProvider, options.dnd), multipleSelectionController: options.multipleSelectionController && {
            isSelectionSingleChangeEvent(e) {
                return options.multipleSelectionController.isSelectionSingleChangeEvent(Object.assign(Object.assign({}, e), { element: e.element }));
            },
            isSelectionRangeChangeEvent(e) {
                return options.multipleSelectionController.isSelectionRangeChangeEvent(Object.assign(Object.assign({}, e), { element: e.element }));
            }
        }, accessibilityProvider: options.accessibilityProvider && Object.assign(Object.assign({}, options.accessibilityProvider), { getSetSize(node) {
                const model = modelProvider();
                const ref = model.getNodeLocation(node);
                const parentRef = model.getParentNodeLocation(ref);
                const parentNode = model.getNode(parentRef);
                return parentNode.visibleChildrenCount;
            },
            getPosInSet(node) {
                return node.visibleChildIndex + 1;
            }, isChecked: options.accessibilityProvider && options.accessibilityProvider.isChecked ? (node) => {
                return options.accessibilityProvider.isChecked(node.element);
            } : undefined, getRole: options.accessibilityProvider && options.accessibilityProvider.getRole ? (node) => {
                return options.accessibilityProvider.getRole(node.element);
            } : () => 'treeitem', getAriaLabel(e) {
                return options.accessibilityProvider.getAriaLabel(e.element);
            },
            getWidgetAriaLabel() {
                return options.accessibilityProvider.getWidgetAriaLabel();
            }, getWidgetRole: options.accessibilityProvider && options.accessibilityProvider.getWidgetRole ? () => options.accessibilityProvider.getWidgetRole() : () => 'tree', getAriaLevel: options.accessibilityProvider && options.accessibilityProvider.getAriaLevel ? (node) => options.accessibilityProvider.getAriaLevel(node.element) : (node) => {
                return node.depth;
            }, getActiveDescendantId: options.accessibilityProvider.getActiveDescendantId && (node => {
                return options.accessibilityProvider.getActiveDescendantId(node.element);
            }) }), keyboardNavigationLabelProvider: options.keyboardNavigationLabelProvider && Object.assign(Object.assign({}, options.keyboardNavigationLabelProvider), { getKeyboardNavigationLabel(node) {
                return options.keyboardNavigationLabelProvider.getKeyboardNavigationLabel(node.element);
            } }), enableKeyboardNavigation: options.simpleKeyboardNavigation });
}
export class ComposedTreeDelegate {
    constructor(delegate) {
        this.delegate = delegate;
    }
    getHeight(element) {
        return this.delegate.getHeight(element.element);
    }
    getTemplateId(element) {
        return this.delegate.getTemplateId(element.element);
    }
    hasDynamicHeight(element) {
        return !!this.delegate.hasDynamicHeight && this.delegate.hasDynamicHeight(element.element);
    }
    setDynamicHeight(element, height) {
        if (this.delegate.setDynamicHeight) {
            this.delegate.setDynamicHeight(element.element, height);
        }
    }
}
export var RenderIndentGuides;
(function (RenderIndentGuides) {
    RenderIndentGuides["None"] = "none";
    RenderIndentGuides["OnHover"] = "onHover";
    RenderIndentGuides["Always"] = "always";
})(RenderIndentGuides || (RenderIndentGuides = {}));
class EventCollection {
    constructor(onDidChange, _elements = []) {
        this._elements = _elements;
        this.onDidChange = Event.forEach(onDidChange, elements => this._elements = elements);
    }
    get elements() {
        return this._elements;
    }
}
class TreeRenderer {
    constructor(renderer, modelProvider, onDidChangeCollapseState, activeNodes, options = {}) {
        this.renderer = renderer;
        this.modelProvider = modelProvider;
        this.activeNodes = activeNodes;
        this.renderedElements = new Map();
        this.renderedNodes = new Map();
        this.indent = TreeRenderer.DefaultIndent;
        this.hideTwistiesOfChildlessElements = false;
        this.shouldRenderIndentGuides = false;
        this.renderedIndentGuides = new SetMap();
        this.activeIndentNodes = new Set();
        this.indentGuidesDisposable = Disposable.None;
        this.disposables = new DisposableStore();
        this.templateId = renderer.templateId;
        this.updateOptions(options);
        Event.map(onDidChangeCollapseState, e => e.node)(this.onDidChangeNodeTwistieState, this, this.disposables);
        if (renderer.onDidChangeTwistieState) {
            renderer.onDidChangeTwistieState(this.onDidChangeTwistieState, this, this.disposables);
        }
    }
    updateOptions(options = {}) {
        if (typeof options.indent !== 'undefined') {
            this.indent = clamp(options.indent, 0, 40);
        }
        if (typeof options.renderIndentGuides !== 'undefined') {
            const shouldRenderIndentGuides = options.renderIndentGuides !== RenderIndentGuides.None;
            if (shouldRenderIndentGuides !== this.shouldRenderIndentGuides) {
                this.shouldRenderIndentGuides = shouldRenderIndentGuides;
                this.indentGuidesDisposable.dispose();
                if (shouldRenderIndentGuides) {
                    const disposables = new DisposableStore();
                    this.activeNodes.onDidChange(this._onDidChangeActiveNodes, this, disposables);
                    this.indentGuidesDisposable = disposables;
                    this._onDidChangeActiveNodes(this.activeNodes.elements);
                }
            }
        }
        if (typeof options.hideTwistiesOfChildlessElements !== 'undefined') {
            this.hideTwistiesOfChildlessElements = options.hideTwistiesOfChildlessElements;
        }
    }
    renderTemplate(container) {
        const el = append(container, $('.monaco-tl-row'));
        const indent = append(el, $('.monaco-tl-indent'));
        const twistie = append(el, $('.monaco-tl-twistie'));
        const contents = append(el, $('.monaco-tl-contents'));
        const templateData = this.renderer.renderTemplate(contents);
        return { container, indent, twistie, indentGuidesDisposable: Disposable.None, templateData };
    }
    renderElement(node, index, templateData, height) {
        if (typeof height === 'number') {
            this.renderedNodes.set(node, { templateData, height });
            this.renderedElements.set(node.element, node);
        }
        const indent = TreeRenderer.DefaultIndent + (node.depth - 1) * this.indent;
        templateData.twistie.style.paddingLeft = `${indent}px`;
        templateData.indent.style.width = `${indent + this.indent - 16}px`;
        this.renderTwistie(node, templateData);
        if (typeof height === 'number') {
            this.renderIndentGuides(node, templateData);
        }
        this.renderer.renderElement(node, index, templateData.templateData, height);
    }
    disposeElement(node, index, templateData, height) {
        templateData.indentGuidesDisposable.dispose();
        if (this.renderer.disposeElement) {
            this.renderer.disposeElement(node, index, templateData.templateData, height);
        }
        if (typeof height === 'number') {
            this.renderedNodes.delete(node);
            this.renderedElements.delete(node.element);
        }
    }
    disposeTemplate(templateData) {
        this.renderer.disposeTemplate(templateData.templateData);
    }
    onDidChangeTwistieState(element) {
        const node = this.renderedElements.get(element);
        if (!node) {
            return;
        }
        this.onDidChangeNodeTwistieState(node);
    }
    onDidChangeNodeTwistieState(node) {
        const data = this.renderedNodes.get(node);
        if (!data) {
            return;
        }
        this.renderTwistie(node, data.templateData);
        this._onDidChangeActiveNodes(this.activeNodes.elements);
        this.renderIndentGuides(node, data.templateData);
    }
    renderTwistie(node, templateData) {
        templateData.twistie.classList.remove(...treeItemExpandedIcon.classNamesArray);
        let twistieRendered = false;
        if (this.renderer.renderTwistie) {
            twistieRendered = this.renderer.renderTwistie(node.element, templateData.twistie);
        }
        if (node.collapsible && (!this.hideTwistiesOfChildlessElements || node.visibleChildrenCount > 0)) {
            if (!twistieRendered) {
                templateData.twistie.classList.add(...treeItemExpandedIcon.classNamesArray);
            }
            templateData.twistie.classList.add('collapsible');
            templateData.twistie.classList.toggle('collapsed', node.collapsed);
        }
        else {
            templateData.twistie.classList.remove('collapsible', 'collapsed');
        }
        if (node.collapsible) {
            templateData.container.setAttribute('aria-expanded', String(!node.collapsed));
        }
        else {
            templateData.container.removeAttribute('aria-expanded');
        }
    }
    renderIndentGuides(target, templateData) {
        clearNode(templateData.indent);
        templateData.indentGuidesDisposable.dispose();
        if (!this.shouldRenderIndentGuides) {
            return;
        }
        const disposableStore = new DisposableStore();
        const model = this.modelProvider();
        let node = target;
        while (true) {
            const ref = model.getNodeLocation(node);
            const parentRef = model.getParentNodeLocation(ref);
            if (!parentRef) {
                break;
            }
            const parent = model.getNode(parentRef);
            const guide = $('.indent-guide', { style: `width: ${this.indent}px` });
            if (this.activeIndentNodes.has(parent)) {
                guide.classList.add('active');
            }
            if (templateData.indent.childElementCount === 0) {
                templateData.indent.appendChild(guide);
            }
            else {
                templateData.indent.insertBefore(guide, templateData.indent.firstElementChild);
            }
            this.renderedIndentGuides.add(parent, guide);
            disposableStore.add(toDisposable(() => this.renderedIndentGuides.delete(parent, guide)));
            node = parent;
        }
        templateData.indentGuidesDisposable = disposableStore;
    }
    _onDidChangeActiveNodes(nodes) {
        if (!this.shouldRenderIndentGuides) {
            return;
        }
        const set = new Set();
        const model = this.modelProvider();
        nodes.forEach(node => {
            const ref = model.getNodeLocation(node);
            try {
                const parentRef = model.getParentNodeLocation(ref);
                if (node.collapsible && node.children.length > 0 && !node.collapsed) {
                    set.add(node);
                }
                else if (parentRef) {
                    set.add(model.getNode(parentRef));
                }
            }
            catch (_a) {
                // noop
            }
        });
        this.activeIndentNodes.forEach(node => {
            if (!set.has(node)) {
                this.renderedIndentGuides.forEach(node, line => line.classList.remove('active'));
            }
        });
        set.forEach(node => {
            if (!this.activeIndentNodes.has(node)) {
                this.renderedIndentGuides.forEach(node, line => line.classList.add('active'));
            }
        });
        this.activeIndentNodes = set;
    }
    dispose() {
        this.renderedNodes.clear();
        this.renderedElements.clear();
        this.indentGuidesDisposable.dispose();
        dispose(this.disposables);
    }
}
TreeRenderer.DefaultIndent = 8;
class TypeFilter {
    constructor(tree, keyboardNavigationLabelProvider, _filter) {
        this.tree = tree;
        this.keyboardNavigationLabelProvider = keyboardNavigationLabelProvider;
        this._filter = _filter;
        this._totalCount = 0;
        this._matchCount = 0;
        this._pattern = '';
        this._lowercasePattern = '';
        this.disposables = new DisposableStore();
        tree.onWillRefilter(this.reset, this, this.disposables);
    }
    get totalCount() { return this._totalCount; }
    get matchCount() { return this._matchCount; }
    set pattern(pattern) {
        this._pattern = pattern;
        this._lowercasePattern = pattern.toLowerCase();
    }
    filter(element, parentVisibility) {
        if (this._filter) {
            const result = this._filter.filter(element, parentVisibility);
            if (this.tree.options.simpleKeyboardNavigation) {
                return result;
            }
            let visibility;
            if (typeof result === 'boolean') {
                visibility = result ? 1 /* Visible */ : 0 /* Hidden */;
            }
            else if (isFilterResult(result)) {
                visibility = getVisibleState(result.visibility);
            }
            else {
                visibility = result;
            }
            if (visibility === 0 /* Hidden */) {
                return false;
            }
        }
        this._totalCount++;
        if (this.tree.options.simpleKeyboardNavigation || !this._pattern) {
            this._matchCount++;
            return { data: FuzzyScore.Default, visibility: true };
        }
        const label = this.keyboardNavigationLabelProvider.getKeyboardNavigationLabel(element);
        const labels = Array.isArray(label) ? label : [label];
        for (const l of labels) {
            const labelStr = l && l.toString();
            if (typeof labelStr === 'undefined') {
                return { data: FuzzyScore.Default, visibility: true };
            }
            const score = fuzzyScore(this._pattern, this._lowercasePattern, 0, labelStr, labelStr.toLowerCase(), 0, true);
            if (score) {
                this._matchCount++;
                return labels.length === 1 ?
                    { data: score, visibility: true } :
                    { data: { label: labelStr, score: score }, visibility: true };
            }
        }
        if (this.tree.options.filterOnType) {
            return 2 /* Recurse */;
        }
        else {
            return { data: FuzzyScore.Default, visibility: true };
        }
    }
    reset() {
        this._totalCount = 0;
        this._matchCount = 0;
    }
    dispose() {
        dispose(this.disposables);
    }
}
class TypeFilterController {
    constructor(tree, model, view, filter, keyboardNavigationDelegate) {
        this.tree = tree;
        this.view = view;
        this.filter = filter;
        this.keyboardNavigationDelegate = keyboardNavigationDelegate;
        this._enabled = false;
        this._pattern = '';
        this._empty = false;
        this._onDidChangeEmptyState = new Emitter();
        this.positionClassName = 'ne';
        this.automaticKeyboardNavigation = true;
        this.triggered = false;
        this._onDidChangePattern = new Emitter();
        this.enabledDisposables = new DisposableStore();
        this.disposables = new DisposableStore();
        this.domNode = $(`.monaco-list-type-filter.${this.positionClassName}`);
        this.domNode.draggable = true;
        domEvent(this.domNode, 'dragstart')(this.onDragStart, this, this.disposables);
        this.messageDomNode = append(view.getHTMLElement(), $(`.monaco-list-type-filter-message`));
        this.labelDomNode = append(this.domNode, $('span.label'));
        const controls = append(this.domNode, $('.controls'));
        this._filterOnType = !!tree.options.filterOnType;
        this.filterOnTypeDomNode = append(controls, $('input.filter'));
        this.filterOnTypeDomNode.type = 'checkbox';
        this.filterOnTypeDomNode.checked = this._filterOnType;
        this.filterOnTypeDomNode.tabIndex = -1;
        this.updateFilterOnTypeTitleAndIcon();
        domEvent(this.filterOnTypeDomNode, 'input')(this.onDidChangeFilterOnType, this, this.disposables);
        this.clearDomNode = append(controls, $('button.clear' + treeFilterClearIcon.cssSelector));
        this.clearDomNode.tabIndex = -1;
        this.clearDomNode.title = localize('clear', "Clear");
        this.keyboardNavigationEventFilter = tree.options.keyboardNavigationEventFilter;
        model.onDidSplice(this.onDidSpliceModel, this, this.disposables);
        this.updateOptions(tree.options);
    }
    get enabled() { return this._enabled; }
    get pattern() { return this._pattern; }
    get filterOnType() { return this._filterOnType; }
    updateOptions(options) {
        if (options.simpleKeyboardNavigation) {
            this.disable();
        }
        else {
            this.enable();
        }
        if (typeof options.filterOnType !== 'undefined') {
            this._filterOnType = !!options.filterOnType;
            this.filterOnTypeDomNode.checked = this._filterOnType;
        }
        if (typeof options.automaticKeyboardNavigation !== 'undefined') {
            this.automaticKeyboardNavigation = options.automaticKeyboardNavigation;
        }
        this.tree.refilter();
        this.render();
        if (!this.automaticKeyboardNavigation) {
            this.onEventOrInput('');
        }
    }
    enable() {
        if (this._enabled) {
            return;
        }
        const onKeyDown = Event.chain(domEvent(this.view.getHTMLElement(), 'keydown'))
            .filter(e => !isInputElement(e.target) || e.target === this.filterOnTypeDomNode)
            .filter(e => e.key !== 'Dead' && !/^Media/.test(e.key))
            .map(e => new StandardKeyboardEvent(e))
            .filter(this.keyboardNavigationEventFilter || (() => true))
            .filter(() => this.automaticKeyboardNavigation || this.triggered)
            .filter(e => (this.keyboardNavigationDelegate.mightProducePrintableCharacter(e) && !(e.keyCode === 18 /* DownArrow */ || e.keyCode === 16 /* UpArrow */ || e.keyCode === 15 /* LeftArrow */ || e.keyCode === 17 /* RightArrow */)) || ((this.pattern.length > 0 || this.triggered) && ((e.keyCode === 9 /* Escape */ || e.keyCode === 1 /* Backspace */) && !e.altKey && !e.ctrlKey && !e.metaKey) || (e.keyCode === 1 /* Backspace */ && (isMacintosh ? (e.altKey && !e.metaKey) : e.ctrlKey) && !e.shiftKey)))
            .forEach(e => { e.stopPropagation(); e.preventDefault(); })
            .event;
        const onClear = domEvent(this.clearDomNode, 'click');
        Event.chain(Event.any(onKeyDown, onClear))
            .event(this.onEventOrInput, this, this.enabledDisposables);
        this.filter.pattern = '';
        this.tree.refilter();
        this.render();
        this._enabled = true;
        this.triggered = false;
    }
    disable() {
        if (!this._enabled) {
            return;
        }
        this.domNode.remove();
        this.enabledDisposables.clear();
        this.tree.refilter();
        this.render();
        this._enabled = false;
        this.triggered = false;
    }
    onEventOrInput(e) {
        if (typeof e === 'string') {
            this.onInput(e);
        }
        else if (e instanceof MouseEvent || e.keyCode === 9 /* Escape */ || (e.keyCode === 1 /* Backspace */ && (isMacintosh ? e.altKey : e.ctrlKey))) {
            this.onInput('');
        }
        else if (e.keyCode === 1 /* Backspace */) {
            this.onInput(this.pattern.length === 0 ? '' : this.pattern.substr(0, this.pattern.length - 1));
        }
        else {
            this.onInput(this.pattern + e.browserEvent.key);
        }
    }
    onInput(pattern) {
        const container = this.view.getHTMLElement();
        if (pattern && !this.domNode.parentElement) {
            container.append(this.domNode);
        }
        else if (!pattern && this.domNode.parentElement) {
            this.domNode.remove();
            this.tree.domFocus();
        }
        this._pattern = pattern;
        this._onDidChangePattern.fire(pattern);
        this.filter.pattern = pattern;
        this.tree.refilter();
        if (pattern) {
            this.tree.focusNext(0, true, undefined, node => !FuzzyScore.isDefault(node.filterData));
        }
        const focus = this.tree.getFocus();
        if (focus.length > 0) {
            const element = focus[0];
            if (this.tree.getRelativeTop(element) === null) {
                this.tree.reveal(element, 0.5);
            }
        }
        this.render();
        if (!pattern) {
            this.triggered = false;
        }
    }
    onDragStart() {
        const container = this.view.getHTMLElement();
        const { left } = getDomNodePagePosition(container);
        const containerWidth = container.clientWidth;
        const midContainerWidth = containerWidth / 2;
        const width = this.domNode.clientWidth;
        const disposables = new DisposableStore();
        let positionClassName = this.positionClassName;
        const updatePosition = () => {
            switch (positionClassName) {
                case 'nw':
                    this.domNode.style.top = `4px`;
                    this.domNode.style.left = `4px`;
                    break;
                case 'ne':
                    this.domNode.style.top = `4px`;
                    this.domNode.style.left = `${containerWidth - width - 6}px`;
                    break;
            }
        };
        const onDragOver = (event) => {
            event.preventDefault(); // needed so that the drop event fires (https://stackoverflow.com/questions/21339924/drop-event-not-firing-in-chrome)
            const x = event.clientX - left;
            if (event.dataTransfer) {
                event.dataTransfer.dropEffect = 'none';
            }
            if (x < midContainerWidth) {
                positionClassName = 'nw';
            }
            else {
                positionClassName = 'ne';
            }
            updatePosition();
        };
        const onDragEnd = () => {
            this.positionClassName = positionClassName;
            this.domNode.className = `monaco-list-type-filter ${this.positionClassName}`;
            this.domNode.style.top = '';
            this.domNode.style.left = '';
            dispose(disposables);
        };
        updatePosition();
        this.domNode.classList.remove(positionClassName);
        this.domNode.classList.add('dragging');
        disposables.add(toDisposable(() => this.domNode.classList.remove('dragging')));
        domEvent(document, 'dragover')(onDragOver, null, disposables);
        domEvent(this.domNode, 'dragend')(onDragEnd, null, disposables);
        StaticDND.CurrentDragAndDropData = new DragAndDropData('vscode-ui');
        disposables.add(toDisposable(() => StaticDND.CurrentDragAndDropData = undefined));
    }
    onDidSpliceModel() {
        if (!this._enabled || this.pattern.length === 0) {
            return;
        }
        this.tree.refilter();
        this.render();
    }
    onDidChangeFilterOnType() {
        this.tree.updateOptions({ filterOnType: this.filterOnTypeDomNode.checked });
        this.tree.refilter();
        this.tree.domFocus();
        this.render();
        this.updateFilterOnTypeTitleAndIcon();
    }
    updateFilterOnTypeTitleAndIcon() {
        if (this.filterOnType) {
            this.filterOnTypeDomNode.classList.remove(...treeFilterOnTypeOffIcon.classNamesArray);
            this.filterOnTypeDomNode.classList.add(...treeFilterOnTypeOnIcon.classNamesArray);
            this.filterOnTypeDomNode.title = localize('disable filter on type', "Disable Filter on Type");
        }
        else {
            this.filterOnTypeDomNode.classList.remove(...treeFilterOnTypeOnIcon.classNamesArray);
            this.filterOnTypeDomNode.classList.add(...treeFilterOnTypeOffIcon.classNamesArray);
            this.filterOnTypeDomNode.title = localize('enable filter on type', "Enable Filter on Type");
        }
    }
    render() {
        const noMatches = this.filter.totalCount > 0 && this.filter.matchCount === 0;
        if (this.pattern && this.tree.options.filterOnType && noMatches) {
            this.messageDomNode.textContent = localize('empty', "No elements found");
            this._empty = true;
        }
        else {
            this.messageDomNode.innerText = '';
            this._empty = false;
        }
        this.domNode.classList.toggle('no-matches', noMatches);
        this.domNode.title = localize('found', "Matched {0} out of {1} elements", this.filter.matchCount, this.filter.totalCount);
        this.labelDomNode.textContent = this.pattern.length > 16 ? '…' + this.pattern.substr(this.pattern.length - 16) : this.pattern;
        this._onDidChangeEmptyState.fire(this._empty);
    }
    shouldAllowFocus(node) {
        if (!this.enabled || !this.pattern || this.filterOnType) {
            return true;
        }
        if (this.filter.totalCount > 0 && this.filter.matchCount <= 1) {
            return true;
        }
        return !FuzzyScore.isDefault(node.filterData);
    }
    dispose() {
        if (this._enabled) {
            this.domNode.remove();
            this.enabledDisposables.dispose();
            this._enabled = false;
            this.triggered = false;
        }
        this._onDidChangePattern.dispose();
        dispose(this.disposables);
    }
}
function asTreeMouseEvent(event) {
    let target = TreeMouseEventTarget.Unknown;
    if (hasParentWithClass(event.browserEvent.target, 'monaco-tl-twistie', 'monaco-tl-row')) {
        target = TreeMouseEventTarget.Twistie;
    }
    else if (hasParentWithClass(event.browserEvent.target, 'monaco-tl-contents', 'monaco-tl-row')) {
        target = TreeMouseEventTarget.Element;
    }
    return {
        browserEvent: event.browserEvent,
        element: event.element ? event.element.element : null,
        target
    };
}
function dfs(node, fn) {
    fn(node);
    node.children.forEach(child => dfs(child, fn));
}
/**
 * The trait concept needs to exist at the tree level, because collapsed
 * tree nodes will not be known by the list.
 */
class Trait {
    constructor(identityProvider) {
        this.identityProvider = identityProvider;
        this.nodes = [];
        this._onDidChange = new Emitter();
        this.onDidChange = this._onDidChange.event;
    }
    get nodeSet() {
        if (!this._nodeSet) {
            this._nodeSet = this.createNodeSet();
        }
        return this._nodeSet;
    }
    set(nodes, browserEvent) {
        var _a;
        if (!((_a = browserEvent) === null || _a === void 0 ? void 0 : _a.__forceEvent) && equals(this.nodes, nodes)) {
            return;
        }
        this._set(nodes, false, browserEvent);
    }
    _set(nodes, silent, browserEvent) {
        this.nodes = [...nodes];
        this.elements = undefined;
        this._nodeSet = undefined;
        if (!silent) {
            const that = this;
            this._onDidChange.fire({ get elements() { return that.get(); }, browserEvent });
        }
    }
    get() {
        if (!this.elements) {
            this.elements = this.nodes.map(node => node.element);
        }
        return [...this.elements];
    }
    getNodes() {
        return this.nodes;
    }
    has(node) {
        return this.nodeSet.has(node);
    }
    onDidModelSplice({ insertedNodes, deletedNodes }) {
        if (!this.identityProvider) {
            const set = this.createNodeSet();
            const visit = (node) => set.delete(node);
            deletedNodes.forEach(node => dfs(node, visit));
            this.set([...set.values()]);
            return;
        }
        const deletedNodesIdSet = new Set();
        const deletedNodesVisitor = (node) => deletedNodesIdSet.add(this.identityProvider.getId(node.element).toString());
        deletedNodes.forEach(node => dfs(node, deletedNodesVisitor));
        const insertedNodesMap = new Map();
        const insertedNodesVisitor = (node) => insertedNodesMap.set(this.identityProvider.getId(node.element).toString(), node);
        insertedNodes.forEach(node => dfs(node, insertedNodesVisitor));
        const nodes = [];
        for (const node of this.nodes) {
            const id = this.identityProvider.getId(node.element).toString();
            const wasDeleted = deletedNodesIdSet.has(id);
            if (!wasDeleted) {
                nodes.push(node);
            }
            else {
                const insertedNode = insertedNodesMap.get(id);
                if (insertedNode) {
                    nodes.push(insertedNode);
                }
            }
        }
        this._set(nodes, true);
    }
    createNodeSet() {
        const set = new Set();
        for (const node of this.nodes) {
            set.add(node);
        }
        return set;
    }
}
class TreeNodeListMouseController extends MouseController {
    constructor(list, tree) {
        super(list);
        this.tree = tree;
    }
    onViewPointer(e) {
        if (isInputElement(e.browserEvent.target) || isMonacoEditor(e.browserEvent.target)) {
            return;
        }
        const node = e.element;
        if (!node) {
            return super.onViewPointer(e);
        }
        if (this.isSelectionRangeChangeEvent(e) || this.isSelectionSingleChangeEvent(e)) {
            return super.onViewPointer(e);
        }
        const target = e.browserEvent.target;
        const onTwistie = target.classList.contains('monaco-tl-twistie')
            || (target.classList.contains('monaco-icon-label') && target.classList.contains('folder-icon') && e.browserEvent.offsetX < 16);
        let expandOnlyOnTwistieClick = false;
        if (typeof this.tree.expandOnlyOnTwistieClick === 'function') {
            expandOnlyOnTwistieClick = this.tree.expandOnlyOnTwistieClick(node.element);
        }
        else {
            expandOnlyOnTwistieClick = !!this.tree.expandOnlyOnTwistieClick;
        }
        if (expandOnlyOnTwistieClick && !onTwistie && e.browserEvent.detail !== 2) {
            return super.onViewPointer(e);
        }
        if (this.tree.expandOnlyOnDoubleClick && e.browserEvent.detail !== 2 && !onTwistie) {
            return super.onViewPointer(e);
        }
        if (node.collapsible) {
            const model = this.tree.model; // internal
            const location = model.getNodeLocation(node);
            const recursive = e.browserEvent.altKey;
            model.setCollapsed(location, undefined, recursive);
            if (expandOnlyOnTwistieClick && onTwistie) {
                return;
            }
        }
        super.onViewPointer(e);
    }
    onDoubleClick(e) {
        const onTwistie = e.browserEvent.target.classList.contains('monaco-tl-twistie');
        if (onTwistie) {
            return;
        }
        super.onDoubleClick(e);
    }
}
/**
 * We use this List subclass to restore selection and focus as nodes
 * get rendered in the list, possibly due to a node expand() call.
 */
class TreeNodeList extends List {
    constructor(user, container, virtualDelegate, renderers, focusTrait, selectionTrait, options) {
        super(user, container, virtualDelegate, renderers, options);
        this.focusTrait = focusTrait;
        this.selectionTrait = selectionTrait;
    }
    createMouseController(options) {
        return new TreeNodeListMouseController(this, options.tree);
    }
    splice(start, deleteCount, elements = []) {
        super.splice(start, deleteCount, elements);
        if (elements.length === 0) {
            return;
        }
        const additionalFocus = [];
        const additionalSelection = [];
        elements.forEach((node, index) => {
            if (this.focusTrait.has(node)) {
                additionalFocus.push(start + index);
            }
            if (this.selectionTrait.has(node)) {
                additionalSelection.push(start + index);
            }
        });
        if (additionalFocus.length > 0) {
            super.setFocus(distinctES6([...super.getFocus(), ...additionalFocus]));
        }
        if (additionalSelection.length > 0) {
            super.setSelection(distinctES6([...super.getSelection(), ...additionalSelection]));
        }
    }
    setFocus(indexes, browserEvent, fromAPI = false) {
        super.setFocus(indexes, browserEvent);
        if (!fromAPI) {
            this.focusTrait.set(indexes.map(i => this.element(i)), browserEvent);
        }
    }
    setSelection(indexes, browserEvent, fromAPI = false) {
        super.setSelection(indexes, browserEvent);
        if (!fromAPI) {
            this.selectionTrait.set(indexes.map(i => this.element(i)), browserEvent);
        }
    }
}
export class AbstractTree {
    constructor(user, container, delegate, renderers, _options = {}) {
        this._options = _options;
        this.eventBufferer = new EventBufferer();
        this.disposables = new DisposableStore();
        this._onWillRefilter = new Emitter();
        this.onWillRefilter = this._onWillRefilter.event;
        this._onDidUpdateOptions = new Emitter();
        const treeDelegate = new ComposedTreeDelegate(delegate);
        const onDidChangeCollapseStateRelay = new Relay();
        const onDidChangeActiveNodes = new Relay();
        const activeNodes = new EventCollection(onDidChangeActiveNodes.event);
        this.renderers = renderers.map(r => new TreeRenderer(r, () => this.model, onDidChangeCollapseStateRelay.event, activeNodes, _options));
        for (let r of this.renderers) {
            this.disposables.add(r);
        }
        let filter;
        if (_options.keyboardNavigationLabelProvider) {
            filter = new TypeFilter(this, _options.keyboardNavigationLabelProvider, _options.filter);
            _options = Object.assign(Object.assign({}, _options), { filter: filter }); // TODO need typescript help here
            this.disposables.add(filter);
        }
        this.focus = new Trait(_options.identityProvider);
        this.selection = new Trait(_options.identityProvider);
        this.view = new TreeNodeList(user, container, treeDelegate, this.renderers, this.focus, this.selection, Object.assign(Object.assign({}, asListOptions(() => this.model, _options)), { tree: this }));
        this.model = this.createModel(user, this.view, _options);
        onDidChangeCollapseStateRelay.input = this.model.onDidChangeCollapseState;
        const onDidModelSplice = Event.forEach(this.model.onDidSplice, e => {
            this.eventBufferer.bufferEvents(() => {
                this.focus.onDidModelSplice(e);
                this.selection.onDidModelSplice(e);
            });
        });
        // Make sure the `forEach` always runs
        onDidModelSplice(() => null, null, this.disposables);
        // Active nodes can change when the model changes or when focus or selection change.
        // We debounce it with 0 delay since these events may fire in the same stack and we only
        // want to run this once. It also doesn't matter if it runs on the next tick since it's only
        // a nice to have UI feature.
        onDidChangeActiveNodes.input = Event.chain(Event.any(onDidModelSplice, this.focus.onDidChange, this.selection.onDidChange))
            .debounce(() => null, 0)
            .map(() => {
            const set = new Set();
            for (const node of this.focus.getNodes()) {
                set.add(node);
            }
            for (const node of this.selection.getNodes()) {
                set.add(node);
            }
            return [...set.values()];
        }).event;
        if (_options.keyboardSupport !== false) {
            const onKeyDown = Event.chain(this.view.onKeyDown)
                .filter(e => !isInputElement(e.target))
                .map(e => new StandardKeyboardEvent(e));
            onKeyDown.filter(e => e.keyCode === 15 /* LeftArrow */).on(this.onLeftArrow, this, this.disposables);
            onKeyDown.filter(e => e.keyCode === 17 /* RightArrow */).on(this.onRightArrow, this, this.disposables);
            onKeyDown.filter(e => e.keyCode === 10 /* Space */).on(this.onSpace, this, this.disposables);
        }
        if (_options.keyboardNavigationLabelProvider) {
            const delegate = _options.keyboardNavigationDelegate || DefaultKeyboardNavigationDelegate;
            this.typeFilterController = new TypeFilterController(this, this.model, this.view, filter, delegate);
            this.focusNavigationFilter = node => this.typeFilterController.shouldAllowFocus(node);
            this.disposables.add(this.typeFilterController);
        }
        this.styleElement = createStyleSheet(this.view.getHTMLElement());
        this.getHTMLElement().classList.toggle('always', this._options.renderIndentGuides === RenderIndentGuides.Always);
    }
    get onDidChangeFocus() { return this.eventBufferer.wrapEvent(this.focus.onDidChange); }
    get onDidChangeSelection() { return this.eventBufferer.wrapEvent(this.selection.onDidChange); }
    get onMouseDblClick() { return Event.map(this.view.onMouseDblClick, asTreeMouseEvent); }
    get onPointer() { return Event.map(this.view.onPointer, asTreeMouseEvent); }
    get onDidFocus() { return this.view.onDidFocus; }
    get onDidChangeCollapseState() { return this.model.onDidChangeCollapseState; }
    get expandOnlyOnDoubleClick() { var _a; return (_a = this._options.expandOnlyOnDoubleClick) !== null && _a !== void 0 ? _a : false; }
    get expandOnlyOnTwistieClick() { return typeof this._options.expandOnlyOnTwistieClick === 'undefined' ? false : this._options.expandOnlyOnTwistieClick; }
    get onDidDispose() { return this.view.onDidDispose; }
    updateOptions(optionsUpdate = {}) {
        this._options = Object.assign(Object.assign({}, this._options), optionsUpdate);
        for (const renderer of this.renderers) {
            renderer.updateOptions(optionsUpdate);
        }
        this.view.updateOptions({
            enableKeyboardNavigation: this._options.simpleKeyboardNavigation,
            automaticKeyboardNavigation: this._options.automaticKeyboardNavigation,
            smoothScrolling: this._options.smoothScrolling,
            horizontalScrolling: this._options.horizontalScrolling
        });
        if (this.typeFilterController) {
            this.typeFilterController.updateOptions(this._options);
        }
        this._onDidUpdateOptions.fire(this._options);
        this.getHTMLElement().classList.toggle('always', this._options.renderIndentGuides === RenderIndentGuides.Always);
    }
    get options() {
        return this._options;
    }
    // Widget
    getHTMLElement() {
        return this.view.getHTMLElement();
    }
    get scrollTop() {
        return this.view.scrollTop;
    }
    set scrollTop(scrollTop) {
        this.view.scrollTop = scrollTop;
    }
    domFocus() {
        this.view.domFocus();
    }
    layout(height, width) {
        this.view.layout(height, width);
    }
    style(styles) {
        const suffix = `.${this.view.domId}`;
        const content = [];
        if (styles.treeIndentGuidesStroke) {
            content.push(`.monaco-list${suffix}:hover .monaco-tl-indent > .indent-guide, .monaco-list${suffix}.always .monaco-tl-indent > .indent-guide  { border-color: ${styles.treeIndentGuidesStroke.transparent(0.4)}; }`);
            content.push(`.monaco-list${suffix} .monaco-tl-indent > .indent-guide.active { border-color: ${styles.treeIndentGuidesStroke}; }`);
        }
        this.styleElement.textContent = content.join('\n');
        this.view.style(styles);
    }
    collapse(location, recursive = false) {
        return this.model.setCollapsed(location, true, recursive);
    }
    expand(location, recursive = false) {
        return this.model.setCollapsed(location, false, recursive);
    }
    isCollapsible(location) {
        return this.model.isCollapsible(location);
    }
    setCollapsible(location, collapsible) {
        return this.model.setCollapsible(location, collapsible);
    }
    isCollapsed(location) {
        return this.model.isCollapsed(location);
    }
    refilter() {
        this._onWillRefilter.fire(undefined);
        this.model.refilter();
    }
    setSelection(elements, browserEvent) {
        const nodes = elements.map(e => this.model.getNode(e));
        this.selection.set(nodes, browserEvent);
        const indexes = elements.map(e => this.model.getListIndex(e)).filter(i => i > -1);
        this.view.setSelection(indexes, browserEvent, true);
    }
    getSelection() {
        return this.selection.get();
    }
    setFocus(elements, browserEvent) {
        const nodes = elements.map(e => this.model.getNode(e));
        this.focus.set(nodes, browserEvent);
        const indexes = elements.map(e => this.model.getListIndex(e)).filter(i => i > -1);
        this.view.setFocus(indexes, browserEvent, true);
    }
    focusNext(n = 1, loop = false, browserEvent, filter = this.focusNavigationFilter) {
        this.view.focusNext(n, loop, browserEvent, filter);
    }
    getFocus() {
        return this.focus.get();
    }
    reveal(location, relativeTop) {
        this.model.expandTo(location);
        const index = this.model.getListIndex(location);
        if (index === -1) {
            return;
        }
        this.view.reveal(index, relativeTop);
    }
    /**
     * Returns the relative position of an element rendered in the list.
     * Returns `null` if the element isn't *entirely* in the visible viewport.
     */
    getRelativeTop(location) {
        const index = this.model.getListIndex(location);
        if (index === -1) {
            return null;
        }
        return this.view.getRelativeTop(index);
    }
    // List
    onLeftArrow(e) {
        e.preventDefault();
        e.stopPropagation();
        const nodes = this.view.getFocusedElements();
        if (nodes.length === 0) {
            return;
        }
        const node = nodes[0];
        const location = this.model.getNodeLocation(node);
        const didChange = this.model.setCollapsed(location, true);
        if (!didChange) {
            const parentLocation = this.model.getParentNodeLocation(location);
            if (!parentLocation) {
                return;
            }
            const parentListIndex = this.model.getListIndex(parentLocation);
            this.view.reveal(parentListIndex);
            this.view.setFocus([parentListIndex]);
        }
    }
    onRightArrow(e) {
        e.preventDefault();
        e.stopPropagation();
        const nodes = this.view.getFocusedElements();
        if (nodes.length === 0) {
            return;
        }
        const node = nodes[0];
        const location = this.model.getNodeLocation(node);
        const didChange = this.model.setCollapsed(location, false);
        if (!didChange) {
            if (!node.children.some(child => child.visible)) {
                return;
            }
            const [focusedIndex] = this.view.getFocus();
            const firstChildIndex = focusedIndex + 1;
            this.view.reveal(firstChildIndex);
            this.view.setFocus([firstChildIndex]);
        }
    }
    onSpace(e) {
        e.preventDefault();
        e.stopPropagation();
        const nodes = this.view.getFocusedElements();
        if (nodes.length === 0) {
            return;
        }
        const node = nodes[0];
        const location = this.model.getNodeLocation(node);
        const recursive = e.browserEvent.altKey;
        this.model.setCollapsed(location, undefined, recursive);
    }
    dispose() {
        dispose(this.disposables);
        this.view.dispose();
    }
}
