/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import * as objects from '../../../common/objects.js';
import * as dom from '../../dom.js';
import { renderLabelWithIcons } from '../iconLabel/iconLabels.js';
export class HighlightedLabel {
    constructor(container, supportIcons) {
        this.supportIcons = supportIcons;
        this.text = '';
        this.title = '';
        this.highlights = [];
        this.didEverRender = false;
        this.domNode = document.createElement('span');
        this.domNode.className = 'monaco-highlighted-label';
        container.appendChild(this.domNode);
    }
    get element() {
        return this.domNode;
    }
    set(text, highlights = [], title = '', escapeNewLines) {
        if (!text) {
            text = '';
        }
        if (escapeNewLines) {
            // adjusts highlights inplace
            text = HighlightedLabel.escapeNewLines(text, highlights);
        }
        if (this.didEverRender && this.text === text && this.title === title && objects.equals(this.highlights, highlights)) {
            return;
        }
        this.text = text;
        this.title = title;
        this.highlights = highlights;
        this.render();
    }
    render() {
        const children = [];
        let pos = 0;
        for (const highlight of this.highlights) {
            if (highlight.end === highlight.start) {
                continue;
            }
            if (pos < highlight.start) {
                const substring = this.text.substring(pos, highlight.start);
                children.push(dom.$('span', undefined, ...this.supportIcons ? renderLabelWithIcons(substring) : [substring]));
                pos = highlight.end;
            }
            const substring = this.text.substring(highlight.start, highlight.end);
            const element = dom.$('span.highlight', undefined, ...this.supportIcons ? renderLabelWithIcons(substring) : [substring]);
            if (highlight.extraClasses) {
                element.classList.add(highlight.extraClasses);
            }
            children.push(element);
            pos = highlight.end;
        }
        if (pos < this.text.length) {
            const substring = this.text.substring(pos);
            children.push(dom.$('span', undefined, ...this.supportIcons ? renderLabelWithIcons(substring) : [substring]));
        }
        dom.reset(this.domNode, ...children);
        if (this.title) {
            this.domNode.title = this.title;
        }
        else {
            this.domNode.removeAttribute('title');
        }
        this.didEverRender = true;
    }
    static escapeNewLines(text, highlights) {
        let total = 0;
        let extra = 0;
        return text.replace(/\r\n|\r|\n/g, (match, offset) => {
            extra = match === '\r\n' ? -1 : 0;
            offset += total;
            for (const highlight of highlights) {
                if (highlight.end <= offset) {
                    continue;
                }
                if (highlight.start >= offset) {
                    highlight.start += extra;
                }
                if (highlight.end >= offset) {
                    highlight.end += extra;
                }
            }
            total += extra;
            return '\u23CE';
        });
    }
}
