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
import './media/gotoErrorWidget.css';
import * as nls from '../../../nls.js';
import * as dom from '../../../base/browser/dom.js';
import { dispose, DisposableStore } from '../../../base/common/lifecycle.js';
import { MarkerSeverity } from '../../../platform/markers/common/markers.js';
import { Range } from '../../common/core/range.js';
import { registerColor, oneOf, textLinkForeground, editorErrorForeground, editorErrorBorder, editorWarningForeground, editorWarningBorder, editorInfoForeground, editorInfoBorder } from '../../../platform/theme/common/colorRegistry.js';
import { IThemeService, registerThemingParticipant } from '../../../platform/theme/common/themeService.js';
import { Color } from '../../../base/common/color.js';
import { ScrollableElement } from '../../../base/browser/ui/scrollbar/scrollableElement.js';
import { getBaseLabel } from '../../../base/common/labels.js';
import { isNonEmptyArray } from '../../../base/common/arrays.js';
import { Emitter } from '../../../base/common/event.js';
import { PeekViewWidget, peekViewTitleForeground, peekViewTitleInfoForeground } from '../peekView/peekView.js';
import { basename } from '../../../base/common/resources.js';
import { SeverityIcon } from '../../../platform/severityIcon/common/severityIcon.js';
import { IOpenerService } from '../../../platform/opener/common/opener.js';
import { MenuId, IMenuService } from '../../../platform/actions/common/actions.js';
import { IContextKeyService } from '../../../platform/contextkey/common/contextkey.js';
import { createAndFillInActionBarActions } from '../../../platform/actions/browser/menuEntryActionViewItem.js';
import { IInstantiationService } from '../../../platform/instantiation/common/instantiation.js';
import { splitLines } from '../../../base/common/strings.js';
import { ILabelService } from '../../../platform/label/common/label.js';
class MessageWidget {
    constructor(parent, editor, onRelatedInformation, _openerService, _labelService) {
        this._openerService = _openerService;
        this._labelService = _labelService;
        this._lines = 0;
        this._longestLineLength = 0;
        this._relatedDiagnostics = new WeakMap();
        this._disposables = new DisposableStore();
        this._editor = editor;
        const domNode = document.createElement('div');
        domNode.className = 'descriptioncontainer';
        this._messageBlock = document.createElement('div');
        this._messageBlock.classList.add('message');
        this._messageBlock.setAttribute('aria-live', 'assertive');
        this._messageBlock.setAttribute('role', 'alert');
        domNode.appendChild(this._messageBlock);
        this._relatedBlock = document.createElement('div');
        domNode.appendChild(this._relatedBlock);
        this._disposables.add(dom.addStandardDisposableListener(this._relatedBlock, 'click', event => {
            event.preventDefault();
            const related = this._relatedDiagnostics.get(event.target);
            if (related) {
                onRelatedInformation(related);
            }
        }));
        this._scrollable = new ScrollableElement(domNode, {
            horizontal: 1 /* Auto */,
            vertical: 1 /* Auto */,
            useShadows: false,
            horizontalScrollbarSize: 3,
            verticalScrollbarSize: 3
        });
        parent.appendChild(this._scrollable.getDomNode());
        this._disposables.add(this._scrollable.onScroll(e => {
            domNode.style.left = `-${e.scrollLeft}px`;
            domNode.style.top = `-${e.scrollTop}px`;
        }));
        this._disposables.add(this._scrollable);
    }
    dispose() {
        dispose(this._disposables);
    }
    update(marker) {
        const { source, message, relatedInformation, code } = marker;
        let sourceAndCodeLength = ((source === null || source === void 0 ? void 0 : source.length) || 0) + '()'.length;
        if (code) {
            if (typeof code === 'string') {
                sourceAndCodeLength += code.length;
            }
            else {
                sourceAndCodeLength += code.value.length;
            }
        }
        const lines = splitLines(message);
        this._lines = lines.length;
        this._longestLineLength = 0;
        for (const line of lines) {
            this._longestLineLength = Math.max(line.length + sourceAndCodeLength, this._longestLineLength);
        }
        dom.clearNode(this._messageBlock);
        this._messageBlock.setAttribute('aria-label', this.getAriaLabel(marker));
        this._editor.applyFontInfo(this._messageBlock);
        let lastLineElement = this._messageBlock;
        for (const line of lines) {
            lastLineElement = document.createElement('div');
            lastLineElement.innerText = line;
            if (line === '') {
                lastLineElement.style.height = this._messageBlock.style.lineHeight;
            }
            this._messageBlock.appendChild(lastLineElement);
        }
        if (source || code) {
            const detailsElement = document.createElement('span');
            detailsElement.classList.add('details');
            lastLineElement.appendChild(detailsElement);
            if (source) {
                const sourceElement = document.createElement('span');
                sourceElement.innerText = source;
                sourceElement.classList.add('source');
                detailsElement.appendChild(sourceElement);
            }
            if (code) {
                if (typeof code === 'string') {
                    const codeElement = document.createElement('span');
                    codeElement.innerText = `(${code})`;
                    codeElement.classList.add('code');
                    detailsElement.appendChild(codeElement);
                }
                else {
                    this._codeLink = dom.$('a.code-link');
                    this._codeLink.setAttribute('href', `${code.target.toString()}`);
                    this._codeLink.onclick = (e) => {
                        this._openerService.open(code.target);
                        e.preventDefault();
                        e.stopPropagation();
                    };
                    const codeElement = dom.append(this._codeLink, dom.$('span'));
                    codeElement.innerText = code.value;
                    detailsElement.appendChild(this._codeLink);
                }
            }
        }
        dom.clearNode(this._relatedBlock);
        this._editor.applyFontInfo(this._relatedBlock);
        if (isNonEmptyArray(relatedInformation)) {
            const relatedInformationNode = this._relatedBlock.appendChild(document.createElement('div'));
            relatedInformationNode.style.paddingTop = `${Math.floor(this._editor.getOption(53 /* lineHeight */) * 0.66)}px`;
            this._lines += 1;
            for (const related of relatedInformation) {
                let container = document.createElement('div');
                let relatedResource = document.createElement('a');
                relatedResource.classList.add('filename');
                relatedResource.innerText = `${getBaseLabel(related.resource)}(${related.startLineNumber}, ${related.startColumn}): `;
                relatedResource.title = this._labelService.getUriLabel(related.resource);
                this._relatedDiagnostics.set(relatedResource, related);
                let relatedMessage = document.createElement('span');
                relatedMessage.innerText = related.message;
                container.appendChild(relatedResource);
                container.appendChild(relatedMessage);
                this._lines += 1;
                relatedInformationNode.appendChild(container);
            }
        }
        const fontInfo = this._editor.getOption(38 /* fontInfo */);
        const scrollWidth = Math.ceil(fontInfo.typicalFullwidthCharacterWidth * this._longestLineLength * 0.75);
        const scrollHeight = fontInfo.lineHeight * this._lines;
        this._scrollable.setScrollDimensions({ scrollWidth, scrollHeight });
    }
    layout(height, width) {
        this._scrollable.getDomNode().style.height = `${height}px`;
        this._scrollable.getDomNode().style.width = `${width}px`;
        this._scrollable.setScrollDimensions({ width, height });
    }
    getHeightInLines() {
        return Math.min(17, this._lines);
    }
    getAriaLabel(marker) {
        let severityLabel = '';
        switch (marker.severity) {
            case MarkerSeverity.Error:
                severityLabel = nls.localize('Error', "Error");
                break;
            case MarkerSeverity.Warning:
                severityLabel = nls.localize('Warning', "Warning");
                break;
            case MarkerSeverity.Info:
                severityLabel = nls.localize('Info', "Info");
                break;
            case MarkerSeverity.Hint:
                severityLabel = nls.localize('Hint', "Hint");
                break;
        }
        let ariaLabel = nls.localize('marker aria', "{0} at {1}. ", severityLabel, marker.startLineNumber + ':' + marker.startColumn);
        const model = this._editor.getModel();
        if (model && (marker.startLineNumber <= model.getLineCount()) && (marker.startLineNumber >= 1)) {
            const lineContent = model.getLineContent(marker.startLineNumber);
            ariaLabel = `${lineContent}, ${ariaLabel}`;
        }
        return ariaLabel;
    }
}
let MarkerNavigationWidget = class MarkerNavigationWidget extends PeekViewWidget {
    constructor(editor, _themeService, _openerService, _menuService, instantiationService, _contextKeyService, _labelService) {
        super(editor, { showArrow: true, showFrame: true, isAccessible: true }, instantiationService);
        this._themeService = _themeService;
        this._openerService = _openerService;
        this._menuService = _menuService;
        this._contextKeyService = _contextKeyService;
        this._labelService = _labelService;
        this._callOnDispose = new DisposableStore();
        this._onDidSelectRelatedInformation = new Emitter();
        this.onDidSelectRelatedInformation = this._onDidSelectRelatedInformation.event;
        this._severity = MarkerSeverity.Warning;
        this._backgroundColor = Color.white;
        this._applyTheme(_themeService.getColorTheme());
        this._callOnDispose.add(_themeService.onDidColorThemeChange(this._applyTheme.bind(this)));
        this.create();
    }
    _applyTheme(theme) {
        this._backgroundColor = theme.getColor(editorMarkerNavigationBackground);
        let colorId = editorMarkerNavigationError;
        if (this._severity === MarkerSeverity.Warning) {
            colorId = editorMarkerNavigationWarning;
        }
        else if (this._severity === MarkerSeverity.Info) {
            colorId = editorMarkerNavigationInfo;
        }
        const frameColor = theme.getColor(colorId);
        this.style({
            arrowColor: frameColor,
            frameColor: frameColor,
            headerBackgroundColor: this._backgroundColor,
            primaryHeadingColor: theme.getColor(peekViewTitleForeground),
            secondaryHeadingColor: theme.getColor(peekViewTitleInfoForeground)
        }); // style() will trigger _applyStyles
    }
    _applyStyles() {
        if (this._parentContainer) {
            this._parentContainer.style.backgroundColor = this._backgroundColor ? this._backgroundColor.toString() : '';
        }
        super._applyStyles();
    }
    dispose() {
        this._callOnDispose.dispose();
        super.dispose();
    }
    _fillHead(container) {
        super._fillHead(container);
        this._disposables.add(this._actionbarWidget.actionRunner.onBeforeRun(e => this.editor.focus()));
        const actions = [];
        const menu = this._menuService.createMenu(MarkerNavigationWidget.TitleMenu, this._contextKeyService);
        createAndFillInActionBarActions(menu, undefined, actions);
        this._actionbarWidget.push(actions, { label: false, icon: true, index: 0 });
        menu.dispose();
    }
    _fillTitleIcon(container) {
        this._icon = dom.append(container, dom.$(''));
    }
    _fillBody(container) {
        this._parentContainer = container;
        container.classList.add('marker-widget');
        this._parentContainer.tabIndex = 0;
        this._parentContainer.setAttribute('role', 'tooltip');
        this._container = document.createElement('div');
        container.appendChild(this._container);
        this._message = new MessageWidget(this._container, this.editor, related => this._onDidSelectRelatedInformation.fire(related), this._openerService, this._labelService);
        this._disposables.add(this._message);
    }
    show() {
        throw new Error('call showAtMarker');
    }
    showAtMarker(marker, markerIdx, markerCount) {
        // update:
        // * title
        // * message
        this._container.classList.remove('stale');
        this._message.update(marker);
        // update frame color (only applied on 'show')
        this._severity = marker.severity;
        this._applyTheme(this._themeService.getColorTheme());
        // show
        let range = Range.lift(marker);
        const editorPosition = this.editor.getPosition();
        let position = editorPosition && range.containsPosition(editorPosition) ? editorPosition : range.getStartPosition();
        super.show(position, this.computeRequiredHeight());
        const model = this.editor.getModel();
        if (model) {
            const detail = markerCount > 1
                ? nls.localize('problems', "{0} of {1} problems", markerIdx, markerCount)
                : nls.localize('change', "{0} of {1} problem", markerIdx, markerCount);
            this.setTitle(basename(model.uri), detail);
        }
        this._icon.className = `codicon ${SeverityIcon.className(MarkerSeverity.toSeverity(this._severity))}`;
        this.editor.revealPositionNearTop(position, 0 /* Smooth */);
        this.editor.focus();
    }
    updateMarker(marker) {
        this._container.classList.remove('stale');
        this._message.update(marker);
    }
    showStale() {
        this._container.classList.add('stale');
        this._relayout();
    }
    _doLayoutBody(heightInPixel, widthInPixel) {
        super._doLayoutBody(heightInPixel, widthInPixel);
        this._heightInPixel = heightInPixel;
        this._message.layout(heightInPixel, widthInPixel);
        this._container.style.height = `${heightInPixel}px`;
    }
    _onWidth(widthInPixel) {
        this._message.layout(this._heightInPixel, widthInPixel);
    }
    _relayout() {
        super._relayout(this.computeRequiredHeight());
    }
    computeRequiredHeight() {
        return 3 + this._message.getHeightInLines();
    }
};
MarkerNavigationWidget.TitleMenu = new MenuId('gotoErrorTitleMenu');
MarkerNavigationWidget = __decorate([
    __param(1, IThemeService),
    __param(2, IOpenerService),
    __param(3, IMenuService),
    __param(4, IInstantiationService),
    __param(5, IContextKeyService),
    __param(6, ILabelService)
], MarkerNavigationWidget);
export { MarkerNavigationWidget };
// theming
let errorDefault = oneOf(editorErrorForeground, editorErrorBorder);
let warningDefault = oneOf(editorWarningForeground, editorWarningBorder);
let infoDefault = oneOf(editorInfoForeground, editorInfoBorder);
export const editorMarkerNavigationError = registerColor('editorMarkerNavigationError.background', { dark: errorDefault, light: errorDefault, hc: errorDefault }, nls.localize('editorMarkerNavigationError', 'Editor marker navigation widget error color.'));
export const editorMarkerNavigationWarning = registerColor('editorMarkerNavigationWarning.background', { dark: warningDefault, light: warningDefault, hc: warningDefault }, nls.localize('editorMarkerNavigationWarning', 'Editor marker navigation widget warning color.'));
export const editorMarkerNavigationInfo = registerColor('editorMarkerNavigationInfo.background', { dark: infoDefault, light: infoDefault, hc: infoDefault }, nls.localize('editorMarkerNavigationInfo', 'Editor marker navigation widget info color.'));
export const editorMarkerNavigationBackground = registerColor('editorMarkerNavigation.background', { dark: '#2D2D30', light: Color.white, hc: '#0C141F' }, nls.localize('editorMarkerNavigationBackground', 'Editor marker navigation widget background.'));
registerThemingParticipant((theme, collector) => {
    const linkFg = theme.getColor(textLinkForeground);
    if (linkFg) {
        collector.addRule(`.monaco-editor .marker-widget a { color: ${linkFg}; }`);
        collector.addRule(`.monaco-editor .marker-widget a.code-link span:hover { color: ${linkFg}; }`);
    }
});
