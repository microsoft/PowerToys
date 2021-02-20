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
import { createStyleSheet } from '../../../base/browser/dom.js';
import { PagedList } from '../../../base/browser/ui/list/listPaging.js';
import { DefaultStyleController, isSelectionRangeChangeEvent, isSelectionSingleChangeEvent, List } from '../../../base/browser/ui/list/listWidget.js';
import { Emitter, Event } from '../../../base/common/event.js';
import { Disposable, dispose, toDisposable, DisposableStore, combinedDisposable } from '../../../base/common/lifecycle.js';
import { localize } from '../../../nls.js';
import { IConfigurationService } from '../../configuration/common/configuration.js';
import { Extensions as ConfigurationExtensions } from '../../configuration/common/configurationRegistry.js';
import { ContextKeyExpr, IContextKeyService, RawContextKey } from '../../contextkey/common/contextkey.js';
import { createDecorator } from '../../instantiation/common/instantiation.js';
import { IKeybindingService } from '../../keybinding/common/keybinding.js';
import { Registry } from '../../registry/common/platform.js';
import { attachListStyler, computeStyles, defaultListStyles } from '../../theme/common/styler.js';
import { IThemeService } from '../../theme/common/themeService.js';
import { InputFocusedContextKey } from '../../contextkey/common/contextkeys.js';
import { ObjectTree, CompressibleObjectTree } from '../../../base/browser/ui/tree/objectTree.js';
import { AsyncDataTree, CompressibleAsyncDataTree } from '../../../base/browser/ui/tree/asyncDataTree.js';
import { DataTree } from '../../../base/browser/ui/tree/dataTree.js';
import { IAccessibilityService } from '../../accessibility/common/accessibility.js';
export const IListService = createDecorator('listService');
let ListService = class ListService {
    constructor(_themeService) {
        this._themeService = _themeService;
        this.disposables = new DisposableStore();
        this.lists = [];
        this._lastFocusedWidget = undefined;
        this._hasCreatedStyleController = false;
    }
    get lastFocusedList() {
        return this._lastFocusedWidget;
    }
    register(widget, extraContextKeys) {
        if (!this._hasCreatedStyleController) {
            this._hasCreatedStyleController = true;
            // create a shared default tree style sheet for performance reasons
            const styleController = new DefaultStyleController(createStyleSheet(), '');
            this.disposables.add(attachListStyler(styleController, this._themeService));
        }
        if (this.lists.some(l => l.widget === widget)) {
            throw new Error('Cannot register the same widget multiple times');
        }
        // Keep in our lists list
        const registeredList = { widget, extraContextKeys };
        this.lists.push(registeredList);
        // Check for currently being focused
        if (widget.getHTMLElement() === document.activeElement) {
            this._lastFocusedWidget = widget;
        }
        return combinedDisposable(widget.onDidFocus(() => this._lastFocusedWidget = widget), toDisposable(() => this.lists.splice(this.lists.indexOf(registeredList), 1)), widget.onDidDispose(() => {
            this.lists = this.lists.filter(l => l !== registeredList);
            if (this._lastFocusedWidget === widget) {
                this._lastFocusedWidget = undefined;
            }
        }));
    }
    dispose() {
        this.disposables.dispose();
    }
};
ListService = __decorate([
    __param(0, IThemeService)
], ListService);
export { ListService };
const RawWorkbenchListFocusContextKey = new RawContextKey('listFocus', true);
export const WorkbenchListSupportsMultiSelectContextKey = new RawContextKey('listSupportsMultiselect', true);
export const WorkbenchListFocusContextKey = ContextKeyExpr.and(RawWorkbenchListFocusContextKey, ContextKeyExpr.not(InputFocusedContextKey));
export const WorkbenchListHasSelectionOrFocus = new RawContextKey('listHasSelectionOrFocus', false);
export const WorkbenchListDoubleSelection = new RawContextKey('listDoubleSelection', false);
export const WorkbenchListMultiSelection = new RawContextKey('listMultiSelection', false);
export const WorkbenchListSupportsKeyboardNavigation = new RawContextKey('listSupportsKeyboardNavigation', true);
export const WorkbenchListAutomaticKeyboardNavigationKey = 'listAutomaticKeyboardNavigation';
export const WorkbenchListAutomaticKeyboardNavigation = new RawContextKey(WorkbenchListAutomaticKeyboardNavigationKey, true);
export let didBindWorkbenchListAutomaticKeyboardNavigation = false;
function createScopedContextKeyService(contextKeyService, widget) {
    const result = contextKeyService.createScoped(widget.getHTMLElement());
    RawWorkbenchListFocusContextKey.bindTo(result);
    return result;
}
const multiSelectModifierSettingKey = 'workbench.list.multiSelectModifier';
const openModeSettingKey = 'workbench.list.openMode';
const horizontalScrollingKey = 'workbench.list.horizontalScrolling';
const keyboardNavigationSettingKey = 'workbench.list.keyboardNavigation';
const automaticKeyboardNavigationSettingKey = 'workbench.list.automaticKeyboardNavigation';
const treeIndentKey = 'workbench.tree.indent';
const treeRenderIndentGuidesKey = 'workbench.tree.renderIndentGuides';
const listSmoothScrolling = 'workbench.list.smoothScrolling';
const treeExpandMode = 'workbench.tree.expandMode';
function useAltAsMultipleSelectionModifier(configurationService) {
    return configurationService.getValue(multiSelectModifierSettingKey) === 'alt';
}
class MultipleSelectionController extends Disposable {
    constructor(configurationService) {
        super();
        this.configurationService = configurationService;
        this.useAltAsMultipleSelectionModifier = useAltAsMultipleSelectionModifier(configurationService);
        this.registerListeners();
    }
    registerListeners() {
        this._register(this.configurationService.onDidChangeConfiguration(e => {
            if (e.affectsConfiguration(multiSelectModifierSettingKey)) {
                this.useAltAsMultipleSelectionModifier = useAltAsMultipleSelectionModifier(this.configurationService);
            }
        }));
    }
    isSelectionSingleChangeEvent(event) {
        if (this.useAltAsMultipleSelectionModifier) {
            return event.browserEvent.altKey;
        }
        return isSelectionSingleChangeEvent(event);
    }
    isSelectionRangeChangeEvent(event) {
        return isSelectionRangeChangeEvent(event);
    }
}
function toWorkbenchListOptions(options, configurationService, keybindingService) {
    const disposables = new DisposableStore();
    const result = Object.assign({}, options);
    if (options.multipleSelectionSupport !== false && !options.multipleSelectionController) {
        const multipleSelectionController = new MultipleSelectionController(configurationService);
        result.multipleSelectionController = multipleSelectionController;
        disposables.add(multipleSelectionController);
    }
    result.keyboardNavigationDelegate = {
        mightProducePrintableCharacter(e) {
            return keybindingService.mightProducePrintableCharacter(e);
        }
    };
    result.smoothScrolling = configurationService.getValue(listSmoothScrolling);
    return [result, disposables];
}
let WorkbenchList = class WorkbenchList extends List {
    constructor(user, container, delegate, renderers, options, contextKeyService, listService, themeService, configurationService, keybindingService) {
        const horizontalScrolling = typeof options.horizontalScrolling !== 'undefined' ? options.horizontalScrolling : configurationService.getValue(horizontalScrollingKey);
        const [workbenchListOptions, workbenchListOptionsDisposable] = toWorkbenchListOptions(options, configurationService, keybindingService);
        super(user, container, delegate, renderers, Object.assign(Object.assign(Object.assign({ keyboardSupport: false }, computeStyles(themeService.getColorTheme(), defaultListStyles)), workbenchListOptions), { horizontalScrolling }));
        this.disposables.add(workbenchListOptionsDisposable);
        this.contextKeyService = createScopedContextKeyService(contextKeyService, this);
        this.themeService = themeService;
        const listSupportsMultiSelect = WorkbenchListSupportsMultiSelectContextKey.bindTo(this.contextKeyService);
        listSupportsMultiSelect.set(!(options.multipleSelectionSupport === false));
        this.listHasSelectionOrFocus = WorkbenchListHasSelectionOrFocus.bindTo(this.contextKeyService);
        this.listDoubleSelection = WorkbenchListDoubleSelection.bindTo(this.contextKeyService);
        this.listMultiSelection = WorkbenchListMultiSelection.bindTo(this.contextKeyService);
        this.horizontalScrolling = options.horizontalScrolling;
        this._useAltAsMultipleSelectionModifier = useAltAsMultipleSelectionModifier(configurationService);
        this.disposables.add(this.contextKeyService);
        this.disposables.add(listService.register(this));
        if (options.overrideStyles) {
            this.updateStyles(options.overrideStyles);
        }
        this.disposables.add(this.onDidChangeSelection(() => {
            const selection = this.getSelection();
            const focus = this.getFocus();
            this.contextKeyService.bufferChangeEvents(() => {
                this.listHasSelectionOrFocus.set(selection.length > 0 || focus.length > 0);
                this.listMultiSelection.set(selection.length > 1);
                this.listDoubleSelection.set(selection.length === 2);
            });
        }));
        this.disposables.add(this.onDidChangeFocus(() => {
            const selection = this.getSelection();
            const focus = this.getFocus();
            this.listHasSelectionOrFocus.set(selection.length > 0 || focus.length > 0);
        }));
        this.disposables.add(configurationService.onDidChangeConfiguration(e => {
            if (e.affectsConfiguration(multiSelectModifierSettingKey)) {
                this._useAltAsMultipleSelectionModifier = useAltAsMultipleSelectionModifier(configurationService);
            }
            let options = {};
            if (e.affectsConfiguration(horizontalScrollingKey) && this.horizontalScrolling === undefined) {
                const horizontalScrolling = configurationService.getValue(horizontalScrollingKey);
                options = Object.assign(Object.assign({}, options), { horizontalScrolling });
            }
            if (e.affectsConfiguration(listSmoothScrolling)) {
                const smoothScrolling = configurationService.getValue(listSmoothScrolling);
                options = Object.assign(Object.assign({}, options), { smoothScrolling });
            }
            if (Object.keys(options).length > 0) {
                this.updateOptions(options);
            }
        }));
    }
    updateOptions(options) {
        super.updateOptions(options);
        if (options.overrideStyles) {
            this.updateStyles(options.overrideStyles);
        }
    }
    dispose() {
        super.dispose();
        if (this._styler) {
            this._styler.dispose();
        }
    }
    updateStyles(styles) {
        if (this._styler) {
            this._styler.dispose();
        }
        this._styler = attachListStyler(this, this.themeService, styles);
    }
};
WorkbenchList = __decorate([
    __param(5, IContextKeyService),
    __param(6, IListService),
    __param(7, IThemeService),
    __param(8, IConfigurationService),
    __param(9, IKeybindingService)
], WorkbenchList);
export { WorkbenchList };
let WorkbenchPagedList = class WorkbenchPagedList extends PagedList {
    constructor(user, container, delegate, renderers, options, contextKeyService, listService, themeService, configurationService, keybindingService) {
        const horizontalScrolling = typeof options.horizontalScrolling !== 'undefined' ? options.horizontalScrolling : configurationService.getValue(horizontalScrollingKey);
        const [workbenchListOptions, workbenchListOptionsDisposable] = toWorkbenchListOptions(options, configurationService, keybindingService);
        super(user, container, delegate, renderers, Object.assign(Object.assign(Object.assign({ keyboardSupport: false }, computeStyles(themeService.getColorTheme(), defaultListStyles)), workbenchListOptions), { horizontalScrolling }));
        this.disposables = new DisposableStore();
        this.disposables.add(workbenchListOptionsDisposable);
        this.contextKeyService = createScopedContextKeyService(contextKeyService, this);
        this.horizontalScrolling = options.horizontalScrolling;
        const listSupportsMultiSelect = WorkbenchListSupportsMultiSelectContextKey.bindTo(this.contextKeyService);
        listSupportsMultiSelect.set(!(options.multipleSelectionSupport === false));
        this._useAltAsMultipleSelectionModifier = useAltAsMultipleSelectionModifier(configurationService);
        this.disposables.add(this.contextKeyService);
        this.disposables.add(listService.register(this));
        if (options.overrideStyles) {
            this.disposables.add(attachListStyler(this, themeService, options.overrideStyles));
        }
        this.disposables.add(configurationService.onDidChangeConfiguration(e => {
            if (e.affectsConfiguration(multiSelectModifierSettingKey)) {
                this._useAltAsMultipleSelectionModifier = useAltAsMultipleSelectionModifier(configurationService);
            }
            let options = {};
            if (e.affectsConfiguration(horizontalScrollingKey) && this.horizontalScrolling === undefined) {
                const horizontalScrolling = configurationService.getValue(horizontalScrollingKey);
                options = Object.assign(Object.assign({}, options), { horizontalScrolling });
            }
            if (e.affectsConfiguration(listSmoothScrolling)) {
                const smoothScrolling = configurationService.getValue(listSmoothScrolling);
                options = Object.assign(Object.assign({}, options), { smoothScrolling });
            }
            if (Object.keys(options).length > 0) {
                this.updateOptions(options);
            }
        }));
    }
    dispose() {
        super.dispose();
        this.disposables.dispose();
    }
};
WorkbenchPagedList = __decorate([
    __param(5, IContextKeyService),
    __param(6, IListService),
    __param(7, IThemeService),
    __param(8, IConfigurationService),
    __param(9, IKeybindingService)
], WorkbenchPagedList);
export { WorkbenchPagedList };
class ResourceNavigator extends Disposable {
    constructor(widget, options) {
        var _a, _b;
        super();
        this.widget = widget;
        this._onDidOpen = this._register(new Emitter());
        this.onDidOpen = this._onDidOpen.event;
        this.openOnFocus = (_a = options === null || options === void 0 ? void 0 : options.openOnFocus) !== null && _a !== void 0 ? _a : false;
        this._register(Event.filter(this.widget.onDidChangeSelection, e => e.browserEvent instanceof KeyboardEvent)(e => this.onSelectionFromKeyboard(e)));
        this._register(this.widget.onPointer((e) => this.onPointer(e.element, e.browserEvent)));
        this._register(this.widget.onMouseDblClick((e) => this.onMouseDblClick(e.element, e.browserEvent)));
        if (this.openOnFocus) {
            this._register(Event.filter(this.widget.onDidChangeFocus, e => e.browserEvent instanceof KeyboardEvent)(e => this.onFocusFromKeyboard(e)));
        }
        if (typeof (options === null || options === void 0 ? void 0 : options.openOnSingleClick) !== 'boolean' && (options === null || options === void 0 ? void 0 : options.configurationService)) {
            this.openOnSingleClick = (options === null || options === void 0 ? void 0 : options.configurationService.getValue(openModeSettingKey)) !== 'doubleClick';
            this._register(options === null || options === void 0 ? void 0 : options.configurationService.onDidChangeConfiguration(() => {
                this.openOnSingleClick = (options === null || options === void 0 ? void 0 : options.configurationService.getValue(openModeSettingKey)) !== 'doubleClick';
            }));
        }
        else {
            this.openOnSingleClick = (_b = options === null || options === void 0 ? void 0 : options.openOnSingleClick) !== null && _b !== void 0 ? _b : true;
        }
    }
    onFocusFromKeyboard(event) {
        const focus = this.widget.getFocus();
        this.widget.setSelection(focus, event.browserEvent);
        const selectionKeyboardEvent = event.browserEvent;
        const preserveFocus = typeof selectionKeyboardEvent.preserveFocus === 'boolean' ? selectionKeyboardEvent.preserveFocus : true;
        const pinned = typeof selectionKeyboardEvent.pinned === 'boolean' ? selectionKeyboardEvent.pinned : !preserveFocus;
        const sideBySide = false;
        this._open(this.getSelectedElement(), preserveFocus, pinned, sideBySide, event.browserEvent);
    }
    onSelectionFromKeyboard(event) {
        if (event.elements.length !== 1) {
            return;
        }
        const selectionKeyboardEvent = event.browserEvent;
        const preserveFocus = typeof selectionKeyboardEvent.preserveFocus === 'boolean' ? selectionKeyboardEvent.preserveFocus : true;
        const pinned = typeof selectionKeyboardEvent.pinned === 'boolean' ? selectionKeyboardEvent.pinned : !preserveFocus;
        const sideBySide = false;
        this._open(this.getSelectedElement(), preserveFocus, pinned, sideBySide, event.browserEvent);
    }
    onPointer(element, browserEvent) {
        if (!this.openOnSingleClick) {
            return;
        }
        const isDoubleClick = browserEvent.detail === 2;
        if (isDoubleClick) {
            return;
        }
        const isMiddleClick = browserEvent.button === 1;
        const preserveFocus = true;
        const pinned = isMiddleClick;
        const sideBySide = browserEvent.ctrlKey || browserEvent.metaKey || browserEvent.altKey;
        this._open(element, preserveFocus, pinned, sideBySide, browserEvent);
    }
    onMouseDblClick(element, browserEvent) {
        if (!browserEvent) {
            return;
        }
        const preserveFocus = false;
        const pinned = true;
        const sideBySide = (browserEvent.ctrlKey || browserEvent.metaKey || browserEvent.altKey);
        this._open(element, preserveFocus, pinned, sideBySide, browserEvent);
    }
    _open(element, preserveFocus, pinned, sideBySide, browserEvent) {
        if (!element) {
            return;
        }
        this._onDidOpen.fire({
            editorOptions: {
                preserveFocus,
                pinned,
                revealIfVisible: true
            },
            sideBySide,
            element,
            browserEvent
        });
    }
}
class TreeResourceNavigator extends ResourceNavigator {
    constructor(widget, options) {
        super(widget, options);
        this.widget = widget;
    }
    getSelectedElement() {
        var _a;
        return (_a = this.widget.getSelection()[0]) !== null && _a !== void 0 ? _a : undefined;
    }
}
function createKeyboardNavigationEventFilter(container, keybindingService) {
    let inChord = false;
    return event => {
        if (inChord) {
            inChord = false;
            return false;
        }
        const result = keybindingService.softDispatch(event, container);
        if (result && result.enterChord) {
            inChord = true;
            return false;
        }
        inChord = false;
        return true;
    };
}
let WorkbenchObjectTree = class WorkbenchObjectTree extends ObjectTree {
    constructor(user, container, delegate, renderers, options, contextKeyService, listService, themeService, configurationService, keybindingService, accessibilityService) {
        const { options: treeOptions, getAutomaticKeyboardNavigation, disposable } = workbenchTreeDataPreamble(container, options, contextKeyService, configurationService, keybindingService, accessibilityService);
        super(user, container, delegate, renderers, treeOptions);
        this.disposables.add(disposable);
        this.internals = new WorkbenchTreeInternals(this, options, getAutomaticKeyboardNavigation, options.overrideStyles, contextKeyService, listService, themeService, configurationService, accessibilityService);
        this.disposables.add(this.internals);
    }
};
WorkbenchObjectTree = __decorate([
    __param(5, IContextKeyService),
    __param(6, IListService),
    __param(7, IThemeService),
    __param(8, IConfigurationService),
    __param(9, IKeybindingService),
    __param(10, IAccessibilityService)
], WorkbenchObjectTree);
export { WorkbenchObjectTree };
let WorkbenchCompressibleObjectTree = class WorkbenchCompressibleObjectTree extends CompressibleObjectTree {
    constructor(user, container, delegate, renderers, options, contextKeyService, listService, themeService, configurationService, keybindingService, accessibilityService) {
        const { options: treeOptions, getAutomaticKeyboardNavigation, disposable } = workbenchTreeDataPreamble(container, options, contextKeyService, configurationService, keybindingService, accessibilityService);
        super(user, container, delegate, renderers, treeOptions);
        this.disposables.add(disposable);
        this.internals = new WorkbenchTreeInternals(this, options, getAutomaticKeyboardNavigation, options.overrideStyles, contextKeyService, listService, themeService, configurationService, accessibilityService);
        this.disposables.add(this.internals);
    }
    updateOptions(options = {}) {
        super.updateOptions(options);
        if (options.overrideStyles) {
            this.internals.updateStyleOverrides(options.overrideStyles);
        }
    }
};
WorkbenchCompressibleObjectTree = __decorate([
    __param(5, IContextKeyService),
    __param(6, IListService),
    __param(7, IThemeService),
    __param(8, IConfigurationService),
    __param(9, IKeybindingService),
    __param(10, IAccessibilityService)
], WorkbenchCompressibleObjectTree);
export { WorkbenchCompressibleObjectTree };
let WorkbenchDataTree = class WorkbenchDataTree extends DataTree {
    constructor(user, container, delegate, renderers, dataSource, options, contextKeyService, listService, themeService, configurationService, keybindingService, accessibilityService) {
        const { options: treeOptions, getAutomaticKeyboardNavigation, disposable } = workbenchTreeDataPreamble(container, options, contextKeyService, configurationService, keybindingService, accessibilityService);
        super(user, container, delegate, renderers, dataSource, treeOptions);
        this.disposables.add(disposable);
        this.internals = new WorkbenchTreeInternals(this, options, getAutomaticKeyboardNavigation, options.overrideStyles, contextKeyService, listService, themeService, configurationService, accessibilityService);
        this.disposables.add(this.internals);
    }
    updateOptions(options = {}) {
        super.updateOptions(options);
        if (options.overrideStyles) {
            this.internals.updateStyleOverrides(options.overrideStyles);
        }
    }
};
WorkbenchDataTree = __decorate([
    __param(6, IContextKeyService),
    __param(7, IListService),
    __param(8, IThemeService),
    __param(9, IConfigurationService),
    __param(10, IKeybindingService),
    __param(11, IAccessibilityService)
], WorkbenchDataTree);
export { WorkbenchDataTree };
let WorkbenchAsyncDataTree = class WorkbenchAsyncDataTree extends AsyncDataTree {
    constructor(user, container, delegate, renderers, dataSource, options, contextKeyService, listService, themeService, configurationService, keybindingService, accessibilityService) {
        const { options: treeOptions, getAutomaticKeyboardNavigation, disposable } = workbenchTreeDataPreamble(container, options, contextKeyService, configurationService, keybindingService, accessibilityService);
        super(user, container, delegate, renderers, dataSource, treeOptions);
        this.disposables.add(disposable);
        this.internals = new WorkbenchTreeInternals(this, options, getAutomaticKeyboardNavigation, options.overrideStyles, contextKeyService, listService, themeService, configurationService, accessibilityService);
        this.disposables.add(this.internals);
    }
    get onDidOpen() { return this.internals.onDidOpen; }
    updateOptions(options = {}) {
        super.updateOptions(options);
        if (options.overrideStyles) {
            this.internals.updateStyleOverrides(options.overrideStyles);
        }
    }
};
WorkbenchAsyncDataTree = __decorate([
    __param(6, IContextKeyService),
    __param(7, IListService),
    __param(8, IThemeService),
    __param(9, IConfigurationService),
    __param(10, IKeybindingService),
    __param(11, IAccessibilityService)
], WorkbenchAsyncDataTree);
export { WorkbenchAsyncDataTree };
let WorkbenchCompressibleAsyncDataTree = class WorkbenchCompressibleAsyncDataTree extends CompressibleAsyncDataTree {
    constructor(user, container, virtualDelegate, compressionDelegate, renderers, dataSource, options, contextKeyService, listService, themeService, configurationService, keybindingService, accessibilityService) {
        const { options: treeOptions, getAutomaticKeyboardNavigation, disposable } = workbenchTreeDataPreamble(container, options, contextKeyService, configurationService, keybindingService, accessibilityService);
        super(user, container, virtualDelegate, compressionDelegate, renderers, dataSource, treeOptions);
        this.disposables.add(disposable);
        this.internals = new WorkbenchTreeInternals(this, options, getAutomaticKeyboardNavigation, options.overrideStyles, contextKeyService, listService, themeService, configurationService, accessibilityService);
        this.disposables.add(this.internals);
    }
};
WorkbenchCompressibleAsyncDataTree = __decorate([
    __param(7, IContextKeyService),
    __param(8, IListService),
    __param(9, IThemeService),
    __param(10, IConfigurationService),
    __param(11, IKeybindingService),
    __param(12, IAccessibilityService)
], WorkbenchCompressibleAsyncDataTree);
export { WorkbenchCompressibleAsyncDataTree };
function workbenchTreeDataPreamble(container, options, contextKeyService, configurationService, keybindingService, accessibilityService) {
    var _a;
    WorkbenchListSupportsKeyboardNavigation.bindTo(contextKeyService);
    if (!didBindWorkbenchListAutomaticKeyboardNavigation) {
        WorkbenchListAutomaticKeyboardNavigation.bindTo(contextKeyService);
        didBindWorkbenchListAutomaticKeyboardNavigation = true;
    }
    const getAutomaticKeyboardNavigation = () => {
        // give priority to the context key value to disable this completely
        let automaticKeyboardNavigation = contextKeyService.getContextKeyValue(WorkbenchListAutomaticKeyboardNavigationKey);
        if (automaticKeyboardNavigation) {
            automaticKeyboardNavigation = configurationService.getValue(automaticKeyboardNavigationSettingKey);
        }
        return automaticKeyboardNavigation;
    };
    const accessibilityOn = accessibilityService.isScreenReaderOptimized();
    const keyboardNavigation = options.simpleKeyboardNavigation || accessibilityOn ? 'simple' : configurationService.getValue(keyboardNavigationSettingKey);
    const horizontalScrolling = options.horizontalScrolling !== undefined ? options.horizontalScrolling : configurationService.getValue(horizontalScrollingKey);
    const [workbenchListOptions, disposable] = toWorkbenchListOptions(options, configurationService, keybindingService);
    const additionalScrollHeight = options.additionalScrollHeight;
    return {
        getAutomaticKeyboardNavigation,
        disposable,
        options: Object.assign(Object.assign({ 
            // ...options, // TODO@Joao why is this not splatted here?
            keyboardSupport: false }, workbenchListOptions), { indent: configurationService.getValue(treeIndentKey), renderIndentGuides: configurationService.getValue(treeRenderIndentGuidesKey), smoothScrolling: configurationService.getValue(listSmoothScrolling), automaticKeyboardNavigation: getAutomaticKeyboardNavigation(), simpleKeyboardNavigation: keyboardNavigation === 'simple', filterOnType: keyboardNavigation === 'filter', horizontalScrolling, keyboardNavigationEventFilter: createKeyboardNavigationEventFilter(container, keybindingService), additionalScrollHeight, hideTwistiesOfChildlessElements: options.hideTwistiesOfChildlessElements, expandOnlyOnDoubleClick: configurationService.getValue(openModeSettingKey) === 'doubleClick', expandOnlyOnTwistieClick: (_a = options.expandOnlyOnTwistieClick) !== null && _a !== void 0 ? _a : (configurationService.getValue(treeExpandMode) === 'doubleClick') })
    };
}
let WorkbenchTreeInternals = class WorkbenchTreeInternals {
    constructor(tree, options, getAutomaticKeyboardNavigation, overrideStyles, contextKeyService, listService, themeService, configurationService, accessibilityService) {
        this.tree = tree;
        this.themeService = themeService;
        this.disposables = [];
        this.contextKeyService = createScopedContextKeyService(contextKeyService, tree);
        const listSupportsMultiSelect = WorkbenchListSupportsMultiSelectContextKey.bindTo(this.contextKeyService);
        listSupportsMultiSelect.set(!(options.multipleSelectionSupport === false));
        this.hasSelectionOrFocus = WorkbenchListHasSelectionOrFocus.bindTo(this.contextKeyService);
        this.hasDoubleSelection = WorkbenchListDoubleSelection.bindTo(this.contextKeyService);
        this.hasMultiSelection = WorkbenchListMultiSelection.bindTo(this.contextKeyService);
        this._useAltAsMultipleSelectionModifier = useAltAsMultipleSelectionModifier(configurationService);
        const interestingContextKeys = new Set();
        interestingContextKeys.add(WorkbenchListAutomaticKeyboardNavigationKey);
        const updateKeyboardNavigation = () => {
            const accessibilityOn = accessibilityService.isScreenReaderOptimized();
            const keyboardNavigation = accessibilityOn ? 'simple' : configurationService.getValue(keyboardNavigationSettingKey);
            tree.updateOptions({
                simpleKeyboardNavigation: keyboardNavigation === 'simple',
                filterOnType: keyboardNavigation === 'filter'
            });
        };
        this.updateStyleOverrides(overrideStyles);
        this.disposables.push(this.contextKeyService, listService.register(tree), tree.onDidChangeSelection(() => {
            const selection = tree.getSelection();
            const focus = tree.getFocus();
            this.contextKeyService.bufferChangeEvents(() => {
                this.hasSelectionOrFocus.set(selection.length > 0 || focus.length > 0);
                this.hasMultiSelection.set(selection.length > 1);
                this.hasDoubleSelection.set(selection.length === 2);
            });
        }), tree.onDidChangeFocus(() => {
            const selection = tree.getSelection();
            const focus = tree.getFocus();
            this.hasSelectionOrFocus.set(selection.length > 0 || focus.length > 0);
        }), configurationService.onDidChangeConfiguration(e => {
            let newOptions = {};
            if (e.affectsConfiguration(multiSelectModifierSettingKey)) {
                this._useAltAsMultipleSelectionModifier = useAltAsMultipleSelectionModifier(configurationService);
            }
            if (e.affectsConfiguration(treeIndentKey)) {
                const indent = configurationService.getValue(treeIndentKey);
                newOptions = Object.assign(Object.assign({}, newOptions), { indent });
            }
            if (e.affectsConfiguration(treeRenderIndentGuidesKey)) {
                const renderIndentGuides = configurationService.getValue(treeRenderIndentGuidesKey);
                newOptions = Object.assign(Object.assign({}, newOptions), { renderIndentGuides });
            }
            if (e.affectsConfiguration(listSmoothScrolling)) {
                const smoothScrolling = configurationService.getValue(listSmoothScrolling);
                newOptions = Object.assign(Object.assign({}, newOptions), { smoothScrolling });
            }
            if (e.affectsConfiguration(keyboardNavigationSettingKey)) {
                updateKeyboardNavigation();
            }
            if (e.affectsConfiguration(automaticKeyboardNavigationSettingKey)) {
                newOptions = Object.assign(Object.assign({}, newOptions), { automaticKeyboardNavigation: getAutomaticKeyboardNavigation() });
            }
            if (e.affectsConfiguration(horizontalScrollingKey) && options.horizontalScrolling === undefined) {
                const horizontalScrolling = configurationService.getValue(horizontalScrollingKey);
                newOptions = Object.assign(Object.assign({}, newOptions), { horizontalScrolling });
            }
            if (e.affectsConfiguration(openModeSettingKey)) {
                newOptions = Object.assign(Object.assign({}, newOptions), { expandOnlyOnDoubleClick: configurationService.getValue(openModeSettingKey) === 'doubleClick' });
            }
            if (e.affectsConfiguration(treeExpandMode) && options.expandOnlyOnTwistieClick === undefined) {
                newOptions = Object.assign(Object.assign({}, newOptions), { expandOnlyOnTwistieClick: configurationService.getValue(treeExpandMode) === 'doubleClick' });
            }
            if (Object.keys(newOptions).length > 0) {
                tree.updateOptions(newOptions);
            }
        }), this.contextKeyService.onDidChangeContext(e => {
            if (e.affectsSome(interestingContextKeys)) {
                tree.updateOptions({ automaticKeyboardNavigation: getAutomaticKeyboardNavigation() });
            }
        }), accessibilityService.onDidChangeScreenReaderOptimized(() => updateKeyboardNavigation()));
        this.navigator = new TreeResourceNavigator(tree, Object.assign({ configurationService }, options));
        this.disposables.push(this.navigator);
    }
    get onDidOpen() { return this.navigator.onDidOpen; }
    updateStyleOverrides(overrideStyles) {
        dispose(this.styler);
        this.styler = overrideStyles ? attachListStyler(this.tree, this.themeService, overrideStyles) : Disposable.None;
    }
    dispose() {
        this.disposables = dispose(this.disposables);
        dispose(this.styler);
        this.styler = undefined;
    }
};
WorkbenchTreeInternals = __decorate([
    __param(4, IContextKeyService),
    __param(5, IListService),
    __param(6, IThemeService),
    __param(7, IConfigurationService),
    __param(8, IAccessibilityService)
], WorkbenchTreeInternals);
const configurationRegistry = Registry.as(ConfigurationExtensions.Configuration);
configurationRegistry.registerConfiguration({
    'id': 'workbench',
    'order': 7,
    'title': localize('workbenchConfigurationTitle', "Workbench"),
    'type': 'object',
    'properties': {
        [multiSelectModifierSettingKey]: {
            'type': 'string',
            'enum': ['ctrlCmd', 'alt'],
            'enumDescriptions': [
                localize('multiSelectModifier.ctrlCmd', "Maps to `Control` on Windows and Linux and to `Command` on macOS."),
                localize('multiSelectModifier.alt', "Maps to `Alt` on Windows and Linux and to `Option` on macOS.")
            ],
            'default': 'ctrlCmd',
            'description': localize({
                key: 'multiSelectModifier',
                comment: [
                    '- `ctrlCmd` refers to a value the setting can take and should not be localized.',
                    '- `Control` and `Command` refer to the modifier keys Ctrl or Cmd on the keyboard and can be localized.'
                ]
            }, "The modifier to be used to add an item in trees and lists to a multi-selection with the mouse (for example in the explorer, open editors and scm view). The 'Open to Side' mouse gestures - if supported - will adapt such that they do not conflict with the multiselect modifier.")
        },
        [openModeSettingKey]: {
            'type': 'string',
            'enum': ['singleClick', 'doubleClick'],
            'default': 'singleClick',
            'description': localize({
                key: 'openModeModifier',
                comment: ['`singleClick` and `doubleClick` refers to a value the setting can take and should not be localized.']
            }, "Controls how to open items in trees and lists using the mouse (if supported). For parents with children in trees, this setting will control if a single click expands the parent or a double click. Note that some trees and lists might choose to ignore this setting if it is not applicable. ")
        },
        [horizontalScrollingKey]: {
            'type': 'boolean',
            'default': false,
            'description': localize('horizontalScrolling setting', "Controls whether lists and trees support horizontal scrolling in the workbench. Warning: turning on this setting has a performance implication.")
        },
        [treeIndentKey]: {
            'type': 'number',
            'default': 8,
            minimum: 0,
            maximum: 40,
            'description': localize('tree indent setting', "Controls tree indentation in pixels.")
        },
        [treeRenderIndentGuidesKey]: {
            type: 'string',
            enum: ['none', 'onHover', 'always'],
            default: 'onHover',
            description: localize('render tree indent guides', "Controls whether the tree should render indent guides.")
        },
        [listSmoothScrolling]: {
            type: 'boolean',
            default: false,
            description: localize('list smoothScrolling setting', "Controls whether lists and trees have smooth scrolling."),
        },
        [keyboardNavigationSettingKey]: {
            'type': 'string',
            'enum': ['simple', 'highlight', 'filter'],
            'enumDescriptions': [
                localize('keyboardNavigationSettingKey.simple', "Simple keyboard navigation focuses elements which match the keyboard input. Matching is done only on prefixes."),
                localize('keyboardNavigationSettingKey.highlight', "Highlight keyboard navigation highlights elements which match the keyboard input. Further up and down navigation will traverse only the highlighted elements."),
                localize('keyboardNavigationSettingKey.filter', "Filter keyboard navigation will filter out and hide all the elements which do not match the keyboard input.")
            ],
            'default': 'highlight',
            'description': localize('keyboardNavigationSettingKey', "Controls the keyboard navigation style for lists and trees in the workbench. Can be simple, highlight and filter.")
        },
        [automaticKeyboardNavigationSettingKey]: {
            'type': 'boolean',
            'default': true,
            markdownDescription: localize('automatic keyboard navigation setting', "Controls whether keyboard navigation in lists and trees is automatically triggered simply by typing. If set to `false`, keyboard navigation is only triggered when executing the `list.toggleKeyboardNavigation` command, for which you can assign a keyboard shortcut.")
        },
        [treeExpandMode]: {
            type: 'string',
            enum: ['singleClick', 'doubleClick'],
            default: 'singleClick',
            description: localize('expand mode', "Controls how tree folders are expanded when clicking the folder names."),
        }
    }
});
