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
import { Iterable } from '../../../common/iterator.js';
import { AbstractTree } from './abstractTree.js';
import { ObjectTreeModel } from './objectTreeModel.js';
import { CompressibleObjectTreeModel } from './compressedObjectTreeModel.js';
import { memoize } from '../../../common/decorators.js';
export class ObjectTree extends AbstractTree {
    constructor(user, container, delegate, renderers, options = {}) {
        super(user, container, delegate, renderers, options);
    }
    get onDidChangeCollapseState() { return this.model.onDidChangeCollapseState; }
    setChildren(element, children = Iterable.empty(), options) {
        this.model.setChildren(element, children, options);
    }
    rerender(element) {
        if (element === undefined) {
            this.view.rerender();
            return;
        }
        this.model.rerender(element);
    }
    hasElement(element) {
        return this.model.has(element);
    }
    createModel(user, view, options) {
        return new ObjectTreeModel(user, view, options);
    }
}
class CompressibleRenderer {
    constructor(_compressedTreeNodeProvider, renderer) {
        this._compressedTreeNodeProvider = _compressedTreeNodeProvider;
        this.renderer = renderer;
        this.templateId = renderer.templateId;
        if (renderer.onDidChangeTwistieState) {
            this.onDidChangeTwistieState = renderer.onDidChangeTwistieState;
        }
    }
    get compressedTreeNodeProvider() {
        return this._compressedTreeNodeProvider();
    }
    renderTemplate(container) {
        const data = this.renderer.renderTemplate(container);
        return { compressedTreeNode: undefined, data };
    }
    renderElement(node, index, templateData, height) {
        const compressedTreeNode = this.compressedTreeNodeProvider.getCompressedTreeNode(node.element);
        if (compressedTreeNode.element.elements.length === 1) {
            templateData.compressedTreeNode = undefined;
            this.renderer.renderElement(node, index, templateData.data, height);
        }
        else {
            templateData.compressedTreeNode = compressedTreeNode;
            this.renderer.renderCompressedElements(compressedTreeNode, index, templateData.data, height);
        }
    }
    disposeElement(node, index, templateData, height) {
        if (templateData.compressedTreeNode) {
            if (this.renderer.disposeCompressedElements) {
                this.renderer.disposeCompressedElements(templateData.compressedTreeNode, index, templateData.data, height);
            }
        }
        else {
            if (this.renderer.disposeElement) {
                this.renderer.disposeElement(node, index, templateData.data, height);
            }
        }
    }
    disposeTemplate(templateData) {
        this.renderer.disposeTemplate(templateData.data);
    }
    renderTwistie(element, twistieElement) {
        if (this.renderer.renderTwistie) {
            return this.renderer.renderTwistie(element, twistieElement);
        }
        return false;
    }
}
__decorate([
    memoize
], CompressibleRenderer.prototype, "compressedTreeNodeProvider", null);
function asObjectTreeOptions(compressedTreeNodeProvider, options) {
    return options && Object.assign(Object.assign({}, options), { keyboardNavigationLabelProvider: options.keyboardNavigationLabelProvider && {
            getKeyboardNavigationLabel(e) {
                let compressedTreeNode;
                try {
                    compressedTreeNode = compressedTreeNodeProvider().getCompressedTreeNode(e);
                }
                catch (_a) {
                    return options.keyboardNavigationLabelProvider.getKeyboardNavigationLabel(e);
                }
                if (compressedTreeNode.element.elements.length === 1) {
                    return options.keyboardNavigationLabelProvider.getKeyboardNavigationLabel(e);
                }
                else {
                    return options.keyboardNavigationLabelProvider.getCompressedNodeKeyboardNavigationLabel(compressedTreeNode.element.elements);
                }
            }
        } });
}
export class CompressibleObjectTree extends ObjectTree {
    constructor(user, container, delegate, renderers, options = {}) {
        const compressedTreeNodeProvider = () => this;
        const compressibleRenderers = renderers.map(r => new CompressibleRenderer(compressedTreeNodeProvider, r));
        super(user, container, delegate, compressibleRenderers, asObjectTreeOptions(compressedTreeNodeProvider, options));
    }
    setChildren(element, children = Iterable.empty(), options) {
        this.model.setChildren(element, children, options);
    }
    createModel(user, view, options) {
        return new CompressibleObjectTreeModel(user, view, options);
    }
    updateOptions(optionsUpdate = {}) {
        super.updateOptions(optionsUpdate);
        if (typeof optionsUpdate.compressionEnabled !== 'undefined') {
            this.model.setCompressionEnabled(optionsUpdate.compressionEnabled);
        }
    }
    getCompressedTreeNode(element = null) {
        return this.model.getCompressedTreeNode(element);
    }
}
