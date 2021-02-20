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
import { ILayoutService } from '../../layout/browser/layoutService.js';
import { IInstantiationService } from '../../instantiation/common/instantiation.js';
import { IThemeService, Themable } from '../../theme/common/themeService.js';
import { inputBackground, inputForeground, inputBorder, inputValidationInfoBackground, inputValidationInfoForeground, inputValidationInfoBorder, inputValidationWarningBackground, inputValidationWarningForeground, inputValidationWarningBorder, inputValidationErrorBackground, inputValidationErrorForeground, inputValidationErrorBorder, badgeBackground, badgeForeground, contrastBorder, buttonForeground, buttonBackground, buttonHoverBackground, progressBarBackground, widgetShadow, listFocusForeground, listFocusBackground, activeContrastBorder, pickerGroupBorder, pickerGroupForeground, quickInputForeground, quickInputBackground, quickInputTitleBackground } from '../../theme/common/colorRegistry.js';
import { CancellationToken } from '../../../base/common/cancellation.js';
import { computeStyles } from '../../theme/common/styler.js';
import { IContextKeyService, RawContextKey } from '../../contextkey/common/contextkey.js';
import { IAccessibilityService } from '../../accessibility/common/accessibility.js';
import { QuickInputController } from '../../../base/parts/quickinput/browser/quickInput.js';
import { WorkbenchList } from '../../list/browser/listService.js';
import { QuickAccessController } from './quickAccess.js';
let QuickInputService = class QuickInputService extends Themable {
    constructor(instantiationService, contextKeyService, themeService, accessibilityService, layoutService) {
        super(themeService);
        this.instantiationService = instantiationService;
        this.contextKeyService = contextKeyService;
        this.accessibilityService = accessibilityService;
        this.layoutService = layoutService;
        this.contexts = new Map();
    }
    get controller() {
        if (!this._controller) {
            this._controller = this._register(this.createController());
        }
        return this._controller;
    }
    get quickAccess() {
        if (!this._quickAccess) {
            this._quickAccess = this._register(this.instantiationService.createInstance(QuickAccessController));
        }
        return this._quickAccess;
    }
    createController(host = this.layoutService, options) {
        var _a, _b;
        const defaultOptions = {
            idPrefix: 'quickInput_',
            container: host.container,
            ignoreFocusOut: () => false,
            isScreenReaderOptimized: () => this.accessibilityService.isScreenReaderOptimized(),
            backKeybindingLabel: () => undefined,
            setContextKey: (id) => this.setContextKey(id),
            returnFocus: () => host.focus(),
            createList: (user, container, delegate, renderers, options) => this.instantiationService.createInstance(WorkbenchList, user, container, delegate, renderers, options),
            styles: this.computeStyles()
        };
        const controller = this._register(new QuickInputController(Object.assign(Object.assign({}, defaultOptions), options)));
        controller.layout(host.dimension, (_b = (_a = host.offset) === null || _a === void 0 ? void 0 : _a.top) !== null && _b !== void 0 ? _b : 0);
        // Layout changes
        this._register(host.onLayout(dimension => { var _a, _b; return controller.layout(dimension, (_b = (_a = host.offset) === null || _a === void 0 ? void 0 : _a.top) !== null && _b !== void 0 ? _b : 0); }));
        // Context keys
        this._register(controller.onShow(() => this.resetContextKeys()));
        this._register(controller.onHide(() => this.resetContextKeys()));
        return controller;
    }
    setContextKey(id) {
        let key;
        if (id) {
            key = this.contexts.get(id);
            if (!key) {
                key = new RawContextKey(id, false)
                    .bindTo(this.contextKeyService);
                this.contexts.set(id, key);
            }
        }
        if (key && key.get()) {
            return; // already active context
        }
        this.resetContextKeys();
        if (key) {
            key.set(true);
        }
    }
    resetContextKeys() {
        this.contexts.forEach(context => {
            if (context.get()) {
                context.reset();
            }
        });
    }
    pick(picks, options = {}, token = CancellationToken.None) {
        return this.controller.pick(picks, options, token);
    }
    createQuickPick() {
        return this.controller.createQuickPick();
    }
    updateStyles() {
        this.controller.applyStyles(this.computeStyles());
    }
    computeStyles() {
        return {
            widget: Object.assign({}, computeStyles(this.theme, {
                quickInputBackground,
                quickInputForeground,
                quickInputTitleBackground,
                contrastBorder,
                widgetShadow
            })),
            inputBox: computeStyles(this.theme, {
                inputForeground,
                inputBackground,
                inputBorder,
                inputValidationInfoBackground,
                inputValidationInfoForeground,
                inputValidationInfoBorder,
                inputValidationWarningBackground,
                inputValidationWarningForeground,
                inputValidationWarningBorder,
                inputValidationErrorBackground,
                inputValidationErrorForeground,
                inputValidationErrorBorder
            }),
            countBadge: computeStyles(this.theme, {
                badgeBackground,
                badgeForeground,
                badgeBorder: contrastBorder
            }),
            button: computeStyles(this.theme, {
                buttonForeground,
                buttonBackground,
                buttonHoverBackground,
                buttonBorder: contrastBorder
            }),
            progressBar: computeStyles(this.theme, {
                progressBarBackground
            }),
            list: computeStyles(this.theme, {
                listBackground: quickInputBackground,
                // Look like focused when inactive.
                listInactiveFocusForeground: listFocusForeground,
                listInactiveFocusBackground: listFocusBackground,
                listFocusOutline: activeContrastBorder,
                listInactiveFocusOutline: activeContrastBorder,
                pickerGroupBorder,
                pickerGroupForeground
            })
        };
    }
};
QuickInputService = __decorate([
    __param(0, IInstantiationService),
    __param(1, IContextKeyService),
    __param(2, IThemeService),
    __param(3, IAccessibilityService),
    __param(4, ILayoutService)
], QuickInputService);
export { QuickInputService };
