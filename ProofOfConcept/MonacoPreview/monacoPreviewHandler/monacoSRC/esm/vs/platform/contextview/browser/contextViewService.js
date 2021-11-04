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
import { ContextView } from '../../../base/browser/ui/contextview/contextview.js';
import { Disposable, toDisposable } from '../../../base/common/lifecycle.js';
import { ILayoutService } from '../../layout/browser/layoutService.js';
let ContextViewService = class ContextViewService extends Disposable {
    constructor(layoutService) {
        super();
        this.layoutService = layoutService;
        this.currentViewDisposable = Disposable.None;
        this.container = layoutService.container;
        this.contextView = this._register(new ContextView(this.container, 1 /* ABSOLUTE */));
        this.layout();
        this._register(layoutService.onLayout(() => this.layout()));
    }
    // ContextView
    setContainer(container, domPosition) {
        this.contextView.setContainer(container, domPosition || 1 /* ABSOLUTE */);
    }
    showContextView(delegate, container, shadowRoot) {
        if (container) {
            if (container !== this.container) {
                this.container = container;
                this.setContainer(container, shadowRoot ? 3 /* FIXED_SHADOW */ : 2 /* FIXED */);
            }
        }
        else {
            if (this.container !== this.layoutService.container) {
                this.container = this.layoutService.container;
                this.setContainer(this.container, 1 /* ABSOLUTE */);
            }
        }
        this.contextView.show(delegate);
        const disposable = toDisposable(() => {
            if (this.currentViewDisposable === disposable) {
                this.hideContextView();
            }
        });
        this.currentViewDisposable = disposable;
        return disposable;
    }
    getContextViewElement() {
        return this.contextView.getViewElement();
    }
    layout() {
        this.contextView.layout();
    }
    hideContextView(data) {
        this.contextView.hide(data);
    }
};
ContextViewService = __decorate([
    __param(0, ILayoutService)
], ContextViewService);
export { ContextViewService };
