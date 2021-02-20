/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
var _a;
import * as DOM from './dom.js';
import { createElement } from './formattedTextRenderer.js';
import { onUnexpectedError } from '../common/errors.js';
import { parseHrefAndDimensions, removeMarkdownEscapes } from '../common/htmlContent.js';
import { defaultGenerator } from '../common/idGenerator.js';
import * as marked from '../common/marked/marked.js';
import { insane } from '../common/insane/insane.js';
import { parse } from '../common/marshalling.js';
import { cloneAndChange } from '../common/objects.js';
import { escape } from '../common/strings.js';
import { URI } from '../common/uri.js';
import { FileAccess, Schemas } from '../common/network.js';
import { markdownEscapeEscapedIcons } from '../common/iconLabels.js';
import { resolvePath } from '../common/resources.js';
import { StandardMouseEvent } from './mouseEvent.js';
import { renderLabelWithIcons } from './ui/iconLabel/iconLabels.js';
import { Event } from '../common/event.js';
import { domEvent } from './event.js';
const _ttpInsane = (_a = window.trustedTypes) === null || _a === void 0 ? void 0 : _a.createPolicy('insane', {
    createHTML(value, options) {
        return insane(value, options);
    }
});
/**
 * Low-level way create a html element from a markdown string.
 *
 * **Note** that for most cases you should be using [`MarkdownRenderer`](./src/vs/editor/browser/core/markdownRenderer.ts)
 * which comes with support for pretty code block rendering and which uses the default way of handling links.
 */
export function renderMarkdown(markdown, options = {}, markedOptions = {}) {
    var _a;
    const element = createElement(options);
    const _uriMassage = function (part) {
        let data;
        try {
            data = parse(decodeURIComponent(part));
        }
        catch (e) {
            // ignore
        }
        if (!data) {
            return part;
        }
        data = cloneAndChange(data, value => {
            if (markdown.uris && markdown.uris[value]) {
                return URI.revive(markdown.uris[value]);
            }
            else {
                return undefined;
            }
        });
        return encodeURIComponent(JSON.stringify(data));
    };
    const _href = function (href, isDomUri) {
        const data = markdown.uris && markdown.uris[href];
        if (!data) {
            return href; // no uri exists
        }
        let uri = URI.revive(data);
        if (URI.parse(href).toString() === uri.toString()) {
            return href; // no tranformation performed
        }
        if (isDomUri) {
            // this URI will end up as "src"-attribute of a dom node
            // and because of that special rewriting needs to be done
            // so that the URI uses a protocol that's understood by
            // browsers (like http or https)
            return FileAccess.asBrowserUri(uri).toString(true);
        }
        if (uri.query) {
            uri = uri.with({ query: _uriMassage(uri.query) });
        }
        return uri.toString();
    };
    // signal to code-block render that the
    // element has been created
    let signalInnerHTML;
    const withInnerHTML = new Promise(c => signalInnerHTML = c);
    const renderer = new marked.Renderer();
    renderer.image = (href, title, text) => {
        let dimensions = [];
        let attributes = [];
        if (href) {
            ({ href, dimensions } = parseHrefAndDimensions(href));
            href = _href(href, true);
            try {
                const hrefAsUri = URI.parse(href);
                if (options.baseUrl && hrefAsUri.scheme === Schemas.file) { // absolute or relative local path, or file: uri
                    href = resolvePath(options.baseUrl, href).toString();
                }
            }
            catch (err) { }
            attributes.push(`src="${href}"`);
        }
        if (text) {
            attributes.push(`alt="${text}"`);
        }
        if (title) {
            attributes.push(`title="${title}"`);
        }
        if (dimensions.length) {
            attributes = attributes.concat(dimensions);
        }
        return '<img ' + attributes.join(' ') + '>';
    };
    renderer.link = (href, title, text) => {
        // Remove markdown escapes. Workaround for https://github.com/chjj/marked/issues/829
        if (href === text) { // raw link case
            text = removeMarkdownEscapes(text);
        }
        href = _href(href, false);
        if (options.baseUrl) {
            const hasScheme = /^\w[\w\d+.-]*:/.test(href);
            if (!hasScheme) {
                href = resolvePath(options.baseUrl, href).toString();
            }
        }
        title = removeMarkdownEscapes(title);
        href = removeMarkdownEscapes(href);
        if (!href
            || href.match(/^data:|javascript:/i)
            || (href.match(/^command:/i) && !markdown.isTrusted)
            || href.match(/^command:(\/\/\/)?_workbench\.downloadResource/i)) {
            // drop the link
            return text;
        }
        else {
            // HTML Encode href
            href = href.replace(/&/g, '&amp;')
                .replace(/</g, '&lt;')
                .replace(/>/g, '&gt;')
                .replace(/"/g, '&quot;')
                .replace(/'/g, '&#39;');
            return `<a href="#" data-href="${href}" title="${title || href}">${text}</a>`;
        }
    };
    renderer.paragraph = (text) => {
        if (markdown.supportThemeIcons) {
            const elements = renderLabelWithIcons(text);
            text = elements.map(e => typeof e === 'string' ? e : e.outerHTML).join('');
        }
        return `<p>${text}</p>`;
    };
    if (options.codeBlockRenderer) {
        renderer.code = (code, lang) => {
            const value = options.codeBlockRenderer(lang, code);
            // when code-block rendering is async we return sync
            // but update the node with the real result later.
            const id = defaultGenerator.nextId();
            const promise = Promise.all([value, withInnerHTML]).then(values => {
                const span = element.querySelector(`div[data-code="${id}"]`);
                if (span) {
                    DOM.reset(span, values[0]);
                }
            }).catch(_err => {
                // ignore
            });
            if (options.asyncRenderCallback) {
                promise.then(options.asyncRenderCallback);
            }
            return `<div class="code" data-code="${id}">${escape(code)}</div>`;
        };
    }
    if (options.actionHandler) {
        options.actionHandler.disposeables.add(Event.any(domEvent(element, 'click'), domEvent(element, 'auxclick'))(e => {
            const mouseEvent = new StandardMouseEvent(e);
            if (!mouseEvent.leftButton && !mouseEvent.middleButton) {
                return;
            }
            let target = mouseEvent.target;
            if (target.tagName !== 'A') {
                target = target.parentElement;
                if (!target || target.tagName !== 'A') {
                    return;
                }
            }
            try {
                const href = target.dataset['href'];
                if (href) {
                    options.actionHandler.callback(href, mouseEvent);
                }
            }
            catch (err) {
                onUnexpectedError(err);
            }
            finally {
                mouseEvent.preventDefault();
            }
        }));
    }
    // Use our own sanitizer so that we can let through only spans.
    // Otherwise, we'd be letting all html be rendered.
    // If we want to allow markdown permitted tags, then we can delete sanitizer and sanitize.
    // We always pass the output through insane after this so that we don't rely on
    // marked for sanitization.
    markedOptions.sanitizer = (html) => {
        const match = markdown.isTrusted ? html.match(/^(<span[^>]+>)|(<\/\s*span>)$/) : undefined;
        return match ? html : '';
    };
    markedOptions.sanitize = true;
    markedOptions.silent = true;
    markedOptions.renderer = renderer;
    // values that are too long will freeze the UI
    let value = (_a = markdown.value) !== null && _a !== void 0 ? _a : '';
    if (value.length > 100000) {
        value = `${value.substr(0, 100000)}…`;
    }
    // escape theme icons
    if (markdown.supportThemeIcons) {
        value = markdownEscapeEscapedIcons(value);
    }
    const renderedMarkdown = marked.parse(value, markedOptions);
    // sanitize with insane
    element.innerHTML = sanitizeRenderedMarkdown(markdown, renderedMarkdown);
    // signal that async code blocks can be now be inserted
    signalInnerHTML();
    // signal size changes for image tags
    if (options.asyncRenderCallback) {
        for (const img of element.getElementsByTagName('img')) {
            const listener = DOM.addDisposableListener(img, 'load', () => {
                listener.dispose();
                options.asyncRenderCallback();
            });
        }
    }
    return element;
}
function sanitizeRenderedMarkdown(options, renderedMarkdown) {
    var _a;
    const insaneOptions = getInsaneOptions(options);
    return (_a = _ttpInsane === null || _ttpInsane === void 0 ? void 0 : _ttpInsane.createHTML(renderedMarkdown, insaneOptions)) !== null && _a !== void 0 ? _a : insane(renderedMarkdown, insaneOptions);
}
function getInsaneOptions(options) {
    const allowedSchemes = [
        Schemas.http,
        Schemas.https,
        Schemas.mailto,
        Schemas.data,
        Schemas.file,
        Schemas.vscodeRemote,
        Schemas.vscodeRemoteResource,
    ];
    if (options.isTrusted) {
        allowedSchemes.push(Schemas.command);
    }
    return {
        allowedSchemes,
        // allowedTags should included everything that markdown renders to.
        // Since we have our own sanitize function for marked, it's possible we missed some tag so let insane make sure.
        // HTML tags that can result from markdown are from reading https://spec.commonmark.org/0.29/
        // HTML table tags that can result from markdown are from https://github.github.com/gfm/#tables-extension-
        allowedTags: ['ul', 'li', 'p', 'code', 'blockquote', 'ol', 'h1', 'h2', 'h3', 'h4', 'h5', 'h6', 'hr', 'em', 'pre', 'table', 'thead', 'tbody', 'tr', 'th', 'td', 'div', 'del', 'a', 'strong', 'br', 'img', 'span'],
        allowedAttributes: {
            'a': ['href', 'name', 'target', 'data-href'],
            'img': ['src', 'title', 'alt', 'width', 'height'],
            'div': ['class', 'data-code'],
            'span': ['class', 'style'],
            // https://github.com/microsoft/vscode/issues/95937
            'th': ['align'],
            'td': ['align']
        },
        filter(token) {
            if (token.tag === 'span' && options.isTrusted) {
                if (token.attrs['style'] && (Object.keys(token.attrs).length === 1)) {
                    return !!token.attrs['style'].match(/^(color\:#[0-9a-fA-F]+;)?(background-color\:#[0-9a-fA-F]+;)?$/);
                }
                else if (token.attrs['class']) {
                    // The class should match codicon rendering in src\vs\base\common\codicons.ts
                    return !!token.attrs['class'].match(/^codicon codicon-[a-z\-]+( codicon-modifier-[a-z\-]+)?$/);
                }
                return false;
            }
            return true;
        }
    };
}
