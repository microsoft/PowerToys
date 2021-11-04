/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import './keybindingLabel.css';
import { equals } from '../../../common/objects.js';
import { UILabelProvider } from '../../../common/keybindingLabels.js';
import * as dom from '../../dom.js';
import { localize } from '../../../../nls.js';
const $ = dom.$;
export class KeybindingLabel {
    constructor(container, os, options) {
        this.os = os;
        this.options = options;
        this.domNode = dom.append(container, $('.monaco-keybinding'));
        this.didEverRender = false;
        container.appendChild(this.domNode);
    }
    set(keybinding, matches) {
        if (this.didEverRender && this.keybinding === keybinding && KeybindingLabel.areSame(this.matches, matches)) {
            return;
        }
        this.keybinding = keybinding;
        this.matches = matches;
        this.render();
    }
    render() {
        dom.clearNode(this.domNode);
        if (this.keybinding) {
            let [firstPart, chordPart] = this.keybinding.getParts();
            if (firstPart) {
                this.renderPart(this.domNode, firstPart, this.matches ? this.matches.firstPart : null);
            }
            if (chordPart) {
                dom.append(this.domNode, $('span.monaco-keybinding-key-chord-separator', undefined, ' '));
                this.renderPart(this.domNode, chordPart, this.matches ? this.matches.chordPart : null);
            }
            this.domNode.title = this.keybinding.getAriaLabel() || '';
        }
        else if (this.options && this.options.renderUnboundKeybindings) {
            this.renderUnbound(this.domNode);
        }
        this.didEverRender = true;
    }
    renderPart(parent, part, match) {
        const modifierLabels = UILabelProvider.modifierLabels[this.os];
        if (part.ctrlKey) {
            this.renderKey(parent, modifierLabels.ctrlKey, Boolean(match === null || match === void 0 ? void 0 : match.ctrlKey), modifierLabels.separator);
        }
        if (part.shiftKey) {
            this.renderKey(parent, modifierLabels.shiftKey, Boolean(match === null || match === void 0 ? void 0 : match.shiftKey), modifierLabels.separator);
        }
        if (part.altKey) {
            this.renderKey(parent, modifierLabels.altKey, Boolean(match === null || match === void 0 ? void 0 : match.altKey), modifierLabels.separator);
        }
        if (part.metaKey) {
            this.renderKey(parent, modifierLabels.metaKey, Boolean(match === null || match === void 0 ? void 0 : match.metaKey), modifierLabels.separator);
        }
        const keyLabel = part.keyLabel;
        if (keyLabel) {
            this.renderKey(parent, keyLabel, Boolean(match === null || match === void 0 ? void 0 : match.keyCode), '');
        }
    }
    renderKey(parent, label, highlight, separator) {
        dom.append(parent, $('span.monaco-keybinding-key' + (highlight ? '.highlight' : ''), undefined, label));
        if (separator) {
            dom.append(parent, $('span.monaco-keybinding-key-separator', undefined, separator));
        }
    }
    renderUnbound(parent) {
        dom.append(parent, $('span.monaco-keybinding-key', undefined, localize('unbound', "Unbound")));
    }
    static areSame(a, b) {
        if (a === b || (!a && !b)) {
            return true;
        }
        return !!a && !!b && equals(a.firstPart, b.firstPart) && equals(a.chordPart, b.chordPart);
    }
}
