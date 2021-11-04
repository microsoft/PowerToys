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
import * as dom from '../../../base/browser/dom.js';
import { domEvent, stop } from '../../../base/browser/event.js';
import * as aria from '../../../base/browser/ui/aria/aria.js';
import { DomScrollableElement } from '../../../base/browser/ui/scrollbar/scrollableElement.js';
import { Event } from '../../../base/common/event.js';
import { Disposable, DisposableStore } from '../../../base/common/lifecycle.js';
import './parameterHints.css';
import { IModeService } from '../../common/services/modeService.js';
import { MarkdownRenderer } from '../../browser/core/markdownRenderer.js';
import { Context } from './provideSignatureHelp.js';
import * as nls from '../../../nls.js';
import { IContextKeyService } from '../../../platform/contextkey/common/contextkey.js';
import { IOpenerService } from '../../../platform/opener/common/opener.js';
import { editorHoverBackground, editorHoverBorder, textCodeBlockBackground, textLinkForeground, editorHoverForeground } from '../../../platform/theme/common/colorRegistry.js';
import { registerThemingParticipant, ThemeIcon } from '../../../platform/theme/common/themeService.js';
import { ParameterHintsModel } from './parameterHintsModel.js';
import { escapeRegExpCharacters } from '../../../base/common/strings.js';
import { Codicon } from '../../../base/common/codicons.js';
import { assertIsDefined } from '../../../base/common/types.js';
import { ColorScheme } from '../../../platform/theme/common/theme.js';
import { registerIcon } from '../../../platform/theme/common/iconRegistry.js';
const $ = dom.$;
const parameterHintsNextIcon = registerIcon('parameter-hints-next', Codicon.chevronDown, nls.localize('parameterHintsNextIcon', 'Icon for show next parameter hint.'));
const parameterHintsPreviousIcon = registerIcon('parameter-hints-previous', Codicon.chevronUp, nls.localize('parameterHintsPreviousIcon', 'Icon for show previous parameter hint.'));
let ParameterHintsWidget = class ParameterHintsWidget extends Disposable {
    constructor(editor, contextKeyService, openerService, modeService) {
        super();
        this.editor = editor;
        this.renderDisposeables = this._register(new DisposableStore());
        this.visible = false;
        this.announcedLabel = null;
        // Editor.IContentWidget.allowEditorOverflow
        this.allowEditorOverflow = true;
        this.markdownRenderer = this._register(new MarkdownRenderer({ editor }, modeService, openerService));
        this.model = this._register(new ParameterHintsModel(editor));
        this.keyVisible = Context.Visible.bindTo(contextKeyService);
        this.keyMultipleSignatures = Context.MultipleSignatures.bindTo(contextKeyService);
        this._register(this.model.onChangedHints(newParameterHints => {
            if (newParameterHints) {
                this.show();
                this.render(newParameterHints);
            }
            else {
                this.hide();
            }
        }));
    }
    createParamaterHintDOMNodes() {
        const element = $('.editor-widget.parameter-hints-widget');
        const wrapper = dom.append(element, $('.phwrapper'));
        wrapper.tabIndex = -1;
        const controls = dom.append(wrapper, $('.controls'));
        const previous = dom.append(controls, $('.button' + ThemeIcon.asCSSSelector(parameterHintsPreviousIcon)));
        const overloads = dom.append(controls, $('.overloads'));
        const next = dom.append(controls, $('.button' + ThemeIcon.asCSSSelector(parameterHintsNextIcon)));
        const onPreviousClick = stop(domEvent(previous, 'click'));
        this._register(onPreviousClick(this.previous, this));
        const onNextClick = stop(domEvent(next, 'click'));
        this._register(onNextClick(this.next, this));
        const body = $('.body');
        const scrollbar = new DomScrollableElement(body, {});
        this._register(scrollbar);
        wrapper.appendChild(scrollbar.getDomNode());
        const signature = dom.append(body, $('.signature'));
        const docs = dom.append(body, $('.docs'));
        element.style.userSelect = 'text';
        this.domNodes = {
            element,
            signature,
            overloads,
            docs,
            scrollbar,
        };
        this.editor.addContentWidget(this);
        this.hide();
        this._register(this.editor.onDidChangeCursorSelection(e => {
            if (this.visible) {
                this.editor.layoutContentWidget(this);
            }
        }));
        const updateFont = () => {
            if (!this.domNodes) {
                return;
            }
            const fontInfo = this.editor.getOption(38 /* fontInfo */);
            this.domNodes.element.style.fontSize = `${fontInfo.fontSize}px`;
        };
        updateFont();
        this._register(Event.chain(this.editor.onDidChangeConfiguration.bind(this.editor))
            .filter(e => e.hasChanged(38 /* fontInfo */))
            .on(updateFont, null));
        this._register(this.editor.onDidLayoutChange(e => this.updateMaxHeight()));
        this.updateMaxHeight();
    }
    show() {
        if (this.visible) {
            return;
        }
        if (!this.domNodes) {
            this.createParamaterHintDOMNodes();
        }
        this.keyVisible.set(true);
        this.visible = true;
        setTimeout(() => {
            if (this.domNodes) {
                this.domNodes.element.classList.add('visible');
            }
        }, 100);
        this.editor.layoutContentWidget(this);
    }
    hide() {
        this.renderDisposeables.clear();
        if (!this.visible) {
            return;
        }
        this.keyVisible.reset();
        this.visible = false;
        this.announcedLabel = null;
        if (this.domNodes) {
            this.domNodes.element.classList.remove('visible');
        }
        this.editor.layoutContentWidget(this);
    }
    getPosition() {
        if (this.visible) {
            return {
                position: this.editor.getPosition(),
                preference: [1 /* ABOVE */, 2 /* BELOW */]
            };
        }
        return null;
    }
    render(hints) {
        var _a;
        this.renderDisposeables.clear();
        if (!this.domNodes) {
            return;
        }
        const multiple = hints.signatures.length > 1;
        this.domNodes.element.classList.toggle('multiple', multiple);
        this.keyMultipleSignatures.set(multiple);
        this.domNodes.signature.innerText = '';
        this.domNodes.docs.innerText = '';
        const signature = hints.signatures[hints.activeSignature];
        if (!signature) {
            return;
        }
        const code = dom.append(this.domNodes.signature, $('.code'));
        const fontInfo = this.editor.getOption(38 /* fontInfo */);
        code.style.fontSize = `${fontInfo.fontSize}px`;
        code.style.fontFamily = fontInfo.fontFamily;
        const hasParameters = signature.parameters.length > 0;
        const activeParameterIndex = (_a = signature.activeParameter) !== null && _a !== void 0 ? _a : hints.activeParameter;
        if (!hasParameters) {
            const label = dom.append(code, $('span'));
            label.textContent = signature.label;
        }
        else {
            this.renderParameters(code, signature, activeParameterIndex);
        }
        const activeParameter = signature.parameters[activeParameterIndex];
        if (activeParameter === null || activeParameter === void 0 ? void 0 : activeParameter.documentation) {
            const documentation = $('span.documentation');
            if (typeof activeParameter.documentation === 'string') {
                documentation.textContent = activeParameter.documentation;
            }
            else {
                const renderedContents = this.renderMarkdownDocs(activeParameter.documentation);
                documentation.appendChild(renderedContents.element);
            }
            dom.append(this.domNodes.docs, $('p', {}, documentation));
        }
        if (signature.documentation === undefined) {
            /** no op */
        }
        else if (typeof signature.documentation === 'string') {
            dom.append(this.domNodes.docs, $('p', {}, signature.documentation));
        }
        else {
            const renderedContents = this.renderMarkdownDocs(signature.documentation);
            dom.append(this.domNodes.docs, renderedContents.element);
        }
        const hasDocs = this.hasDocs(signature, activeParameter);
        this.domNodes.signature.classList.toggle('has-docs', hasDocs);
        this.domNodes.docs.classList.toggle('empty', !hasDocs);
        this.domNodes.overloads.textContent =
            String(hints.activeSignature + 1).padStart(hints.signatures.length.toString().length, '0') + '/' + hints.signatures.length;
        if (activeParameter) {
            const labelToAnnounce = this.getParameterLabel(signature, activeParameterIndex);
            // Select method gets called on every user type while parameter hints are visible.
            // We do not want to spam the user with same announcements, so we only announce if the current parameter changed.
            if (this.announcedLabel !== labelToAnnounce) {
                aria.alert(nls.localize('hint', "{0}, hint", labelToAnnounce));
                this.announcedLabel = labelToAnnounce;
            }
        }
        this.editor.layoutContentWidget(this);
        this.domNodes.scrollbar.scanDomNode();
    }
    renderMarkdownDocs(markdown) {
        const renderedContents = this.renderDisposeables.add(this.markdownRenderer.render(markdown, {
            asyncRenderCallback: () => {
                var _a;
                (_a = this.domNodes) === null || _a === void 0 ? void 0 : _a.scrollbar.scanDomNode();
            }
        }));
        renderedContents.element.classList.add('markdown-docs');
        return renderedContents;
    }
    hasDocs(signature, activeParameter) {
        if (activeParameter && typeof activeParameter.documentation === 'string' && assertIsDefined(activeParameter.documentation).length > 0) {
            return true;
        }
        if (activeParameter && typeof activeParameter.documentation === 'object' && assertIsDefined(activeParameter.documentation).value.length > 0) {
            return true;
        }
        if (signature.documentation && typeof signature.documentation === 'string' && assertIsDefined(signature.documentation).length > 0) {
            return true;
        }
        if (signature.documentation && typeof signature.documentation === 'object' && assertIsDefined(signature.documentation.value).length > 0) {
            return true;
        }
        return false;
    }
    renderParameters(parent, signature, activeParameterIndex) {
        const [start, end] = this.getParameterLabelOffsets(signature, activeParameterIndex);
        const beforeSpan = document.createElement('span');
        beforeSpan.textContent = signature.label.substring(0, start);
        const paramSpan = document.createElement('span');
        paramSpan.textContent = signature.label.substring(start, end);
        paramSpan.className = 'parameter active';
        const afterSpan = document.createElement('span');
        afterSpan.textContent = signature.label.substring(end);
        dom.append(parent, beforeSpan, paramSpan, afterSpan);
    }
    getParameterLabel(signature, paramIdx) {
        const param = signature.parameters[paramIdx];
        if (Array.isArray(param.label)) {
            return signature.label.substring(param.label[0], param.label[1]);
        }
        else {
            return param.label;
        }
    }
    getParameterLabelOffsets(signature, paramIdx) {
        const param = signature.parameters[paramIdx];
        if (!param) {
            return [0, 0];
        }
        else if (Array.isArray(param.label)) {
            return param.label;
        }
        else if (!param.label.length) {
            return [0, 0];
        }
        else {
            const regex = new RegExp(`(\\W|^)${escapeRegExpCharacters(param.label)}(?=\\W|$)`, 'g');
            regex.test(signature.label);
            const idx = regex.lastIndex - param.label.length;
            return idx >= 0
                ? [idx, regex.lastIndex]
                : [0, 0];
        }
    }
    next() {
        this.editor.focus();
        this.model.next();
    }
    previous() {
        this.editor.focus();
        this.model.previous();
    }
    cancel() {
        this.model.cancel();
    }
    getDomNode() {
        if (!this.domNodes) {
            this.createParamaterHintDOMNodes();
        }
        return this.domNodes.element;
    }
    getId() {
        return ParameterHintsWidget.ID;
    }
    trigger(context) {
        this.model.trigger(context, 0);
    }
    updateMaxHeight() {
        if (!this.domNodes) {
            return;
        }
        const height = Math.max(this.editor.getLayoutInfo().height / 4, 250);
        const maxHeight = `${height}px`;
        this.domNodes.element.style.maxHeight = maxHeight;
        const wrapper = this.domNodes.element.getElementsByClassName('phwrapper');
        if (wrapper.length) {
            wrapper[0].style.maxHeight = maxHeight;
        }
    }
};
ParameterHintsWidget.ID = 'editor.widget.parameterHintsWidget';
ParameterHintsWidget = __decorate([
    __param(1, IContextKeyService),
    __param(2, IOpenerService),
    __param(3, IModeService)
], ParameterHintsWidget);
export { ParameterHintsWidget };
registerThemingParticipant((theme, collector) => {
    const border = theme.getColor(editorHoverBorder);
    if (border) {
        const borderWidth = theme.type === ColorScheme.HIGH_CONTRAST ? 2 : 1;
        collector.addRule(`.monaco-editor .parameter-hints-widget { border: ${borderWidth}px solid ${border}; }`);
        collector.addRule(`.monaco-editor .parameter-hints-widget.multiple .body { border-left: 1px solid ${border.transparent(0.5)}; }`);
        collector.addRule(`.monaco-editor .parameter-hints-widget .signature.has-docs { border-bottom: 1px solid ${border.transparent(0.5)}; }`);
    }
    const background = theme.getColor(editorHoverBackground);
    if (background) {
        collector.addRule(`.monaco-editor .parameter-hints-widget { background-color: ${background}; }`);
    }
    const link = theme.getColor(textLinkForeground);
    if (link) {
        collector.addRule(`.monaco-editor .parameter-hints-widget a { color: ${link}; }`);
    }
    const foreground = theme.getColor(editorHoverForeground);
    if (foreground) {
        collector.addRule(`.monaco-editor .parameter-hints-widget { color: ${foreground}; }`);
    }
    const codeBackground = theme.getColor(textCodeBlockBackground);
    if (codeBackground) {
        collector.addRule(`.monaco-editor .parameter-hints-widget code { background-color: ${codeBackground}; }`);
    }
});
