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
import './standaloneQuickInput.css';
import { registerEditorContribution } from '../../../browser/editorExtensions.js';
import { IThemeService } from '../../../../platform/theme/common/themeService.js';
import { CancellationToken } from '../../../../base/common/cancellation.js';
import { IInstantiationService } from '../../../../platform/instantiation/common/instantiation.js';
import { IContextKeyService } from '../../../../platform/contextkey/common/contextkey.js';
import { IAccessibilityService } from '../../../../platform/accessibility/common/accessibility.js';
import { ILayoutService } from '../../../../platform/layout/browser/layoutService.js';
import { ICodeEditorService } from '../../../browser/services/codeEditorService.js';
import { QuickInputService } from '../../../../platform/quickinput/browser/quickInput.js';
import { once } from '../../../../base/common/functional.js';
let EditorScopedQuickInputServiceImpl = class EditorScopedQuickInputServiceImpl extends QuickInputService {
    constructor(editor, instantiationService, contextKeyService, themeService, accessibilityService, layoutService) {
        super(instantiationService, contextKeyService, themeService, accessibilityService, layoutService);
        this.host = undefined;
        // Use the passed in code editor as host for the quick input widget
        const contribution = QuickInputEditorContribution.get(editor);
        this.host = {
            _serviceBrand: undefined,
            get container() { return contribution.widget.getDomNode(); },
            get dimension() { return editor.getLayoutInfo(); },
            get onLayout() { return editor.onDidLayoutChange; },
            focus: () => editor.focus()
        };
    }
    createController() {
        return super.createController(this.host);
    }
};
EditorScopedQuickInputServiceImpl = __decorate([
    __param(1, IInstantiationService),
    __param(2, IContextKeyService),
    __param(3, IThemeService),
    __param(4, IAccessibilityService),
    __param(5, ILayoutService)
], EditorScopedQuickInputServiceImpl);
export { EditorScopedQuickInputServiceImpl };
let StandaloneQuickInputServiceImpl = class StandaloneQuickInputServiceImpl {
    constructor(instantiationService, codeEditorService) {
        this.instantiationService = instantiationService;
        this.codeEditorService = codeEditorService;
        this.mapEditorToService = new Map();
    }
    get activeService() {
        const editor = this.codeEditorService.getFocusedCodeEditor();
        if (!editor) {
            throw new Error('Quick input service needs a focused editor to work.');
        }
        // Find the quick input implementation for the focused
        // editor or create it lazily if not yet created
        let quickInputService = this.mapEditorToService.get(editor);
        if (!quickInputService) {
            const newQuickInputService = quickInputService = this.instantiationService.createInstance(EditorScopedQuickInputServiceImpl, editor);
            this.mapEditorToService.set(editor, quickInputService);
            once(editor.onDidDispose)(() => {
                newQuickInputService.dispose();
                this.mapEditorToService.delete(editor);
            });
        }
        return quickInputService;
    }
    get quickAccess() { return this.activeService.quickAccess; }
    pick(picks, options = {}, token = CancellationToken.None) {
        return this.activeService /* TS fail */.pick(picks, options, token);
    }
    createQuickPick() {
        return this.activeService.createQuickPick();
    }
};
StandaloneQuickInputServiceImpl = __decorate([
    __param(0, IInstantiationService),
    __param(1, ICodeEditorService)
], StandaloneQuickInputServiceImpl);
export { StandaloneQuickInputServiceImpl };
export class QuickInputEditorContribution {
    constructor(editor) {
        this.editor = editor;
        this.widget = new QuickInputEditorWidget(this.editor);
    }
    static get(editor) {
        return editor.getContribution(QuickInputEditorContribution.ID);
    }
    dispose() {
        this.widget.dispose();
    }
}
QuickInputEditorContribution.ID = 'editor.controller.quickInput';
export class QuickInputEditorWidget {
    constructor(codeEditor) {
        this.codeEditor = codeEditor;
        this.domNode = document.createElement('div');
        this.codeEditor.addOverlayWidget(this);
    }
    getId() {
        return QuickInputEditorWidget.ID;
    }
    getDomNode() {
        return this.domNode;
    }
    getPosition() {
        return { preference: 2 /* TOP_CENTER */ };
    }
    dispose() {
        this.codeEditor.removeOverlayWidget(this);
    }
}
QuickInputEditorWidget.ID = 'editor.contrib.quickInputWidget';
registerEditorContribution(QuickInputEditorContribution.ID, QuickInputEditorContribution);
