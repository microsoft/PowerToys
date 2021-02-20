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
var _a;
import * as nls from '../../../nls.js';
import { createMatches } from '../../../base/common/filters.js';
import { DisposableStore } from '../../../base/common/lifecycle.js';
import { append, $, hide, show } from '../../../base/browser/dom.js';
import { IThemeService, ThemeIcon } from '../../../platform/theme/common/themeService.js';
import { IModeService } from '../../common/services/modeService.js';
import { completionKindToCssClass } from '../../common/modes.js';
import { IconLabel } from '../../../base/browser/ui/iconLabel/iconLabel.js';
import { getIconClasses } from '../../common/services/getIconClasses.js';
import { IModelService } from '../../common/services/modelService.js';
import { URI } from '../../../base/common/uri.js';
import { FileKind } from '../../../platform/files/common/files.js';
import { flatten } from '../../../base/common/arrays.js';
import { canExpandCompletionItem } from './suggestWidgetDetails.js';
import { Codicon } from '../../../base/common/codicons.js';
import { Emitter } from '../../../base/common/event.js';
import { registerIcon } from '../../../platform/theme/common/iconRegistry.js';
export function getAriaId(index) {
    return `suggest-aria-id:${index}`;
}
export const suggestMoreInfoIcon = registerIcon('suggest-more-info', Codicon.chevronRight, nls.localize('suggestMoreInfoIcon', 'Icon for more information in the suggest widget.'));
const _completionItemColor = new (_a = class ColorExtractor {
        extract(item, out) {
            if (item.textLabel.match(ColorExtractor._regexStrict)) {
                out[0] = item.textLabel;
                return true;
            }
            if (item.completion.detail && item.completion.detail.match(ColorExtractor._regexStrict)) {
                out[0] = item.completion.detail;
                return true;
            }
            if (typeof item.completion.documentation === 'string') {
                const match = ColorExtractor._regexRelaxed.exec(item.completion.documentation);
                if (match && (match.index === 0 || match.index + match[0].length === item.completion.documentation.length)) {
                    out[0] = match[0];
                    return true;
                }
            }
            return false;
        }
    },
    _a._regexRelaxed = /(#([\da-fA-F]{3}){1,2}|(rgb|hsl)a\(\s*(\d{1,3}%?\s*,\s*){3}(1|0?\.\d+)\)|(rgb|hsl)\(\s*\d{1,3}%?(\s*,\s*\d{1,3}%?){2}\s*\))/,
    _a._regexStrict = new RegExp(`^${_a._regexRelaxed.source}$`, 'i'),
    _a);
let ItemRenderer = class ItemRenderer {
    constructor(_editor, _modelService, _modeService, _themeService) {
        this._editor = _editor;
        this._modelService = _modelService;
        this._modeService = _modeService;
        this._themeService = _themeService;
        this._onDidToggleDetails = new Emitter();
        this.onDidToggleDetails = this._onDidToggleDetails.event;
        this.templateId = 'suggestion';
    }
    dispose() {
        this._onDidToggleDetails.dispose();
    }
    renderTemplate(container) {
        const data = Object.create(null);
        data.disposables = new DisposableStore();
        data.root = container;
        data.root.classList.add('show-file-icons');
        data.icon = append(container, $('.icon'));
        data.colorspan = append(data.icon, $('span.colorspan'));
        const text = append(container, $('.contents'));
        const main = append(text, $('.main'));
        data.iconContainer = append(main, $('.icon-label.codicon'));
        data.left = append(main, $('span.left'));
        data.right = append(main, $('span.right'));
        data.iconLabel = new IconLabel(data.left, { supportHighlights: true, supportIcons: true });
        data.disposables.add(data.iconLabel);
        data.parametersLabel = append(data.left, $('span.signature-label'));
        data.qualifierLabel = append(data.left, $('span.qualifier-label'));
        data.detailsLabel = append(data.right, $('span.details-label'));
        data.readMore = append(data.right, $('span.readMore' + ThemeIcon.asCSSSelector(suggestMoreInfoIcon)));
        data.readMore.title = nls.localize('readMore', "Read More");
        const configureFont = () => {
            const options = this._editor.getOptions();
            const fontInfo = options.get(38 /* fontInfo */);
            const fontFamily = fontInfo.fontFamily;
            const fontFeatureSettings = fontInfo.fontFeatureSettings;
            const fontSize = options.get(102 /* suggestFontSize */) || fontInfo.fontSize;
            const lineHeight = options.get(103 /* suggestLineHeight */) || fontInfo.lineHeight;
            const fontWeight = fontInfo.fontWeight;
            const fontSizePx = `${fontSize}px`;
            const lineHeightPx = `${lineHeight}px`;
            data.root.style.fontSize = fontSizePx;
            data.root.style.fontWeight = fontWeight;
            main.style.fontFamily = fontFamily;
            main.style.fontFeatureSettings = fontFeatureSettings;
            main.style.lineHeight = lineHeightPx;
            data.icon.style.height = lineHeightPx;
            data.icon.style.width = lineHeightPx;
            data.readMore.style.height = lineHeightPx;
            data.readMore.style.width = lineHeightPx;
        };
        configureFont();
        data.disposables.add(this._editor.onDidChangeConfiguration(e => {
            if (e.hasChanged(38 /* fontInfo */) || e.hasChanged(102 /* suggestFontSize */) || e.hasChanged(103 /* suggestLineHeight */)) {
                configureFont();
            }
        }));
        return data;
    }
    renderElement(element, index, data) {
        var _b, _c, _d;
        const { completion } = element;
        const textLabel = typeof completion.label === 'string' ? completion.label : completion.label.name;
        data.root.id = getAriaId(index);
        data.colorspan.style.backgroundColor = '';
        const labelOptions = {
            labelEscapeNewLines: true,
            matches: createMatches(element.score)
        };
        let color = [];
        if (completion.kind === 19 /* Color */ && _completionItemColor.extract(element, color)) {
            // special logic for 'color' completion items
            data.icon.className = 'icon customcolor';
            data.iconContainer.className = 'icon hide';
            data.colorspan.style.backgroundColor = color[0];
        }
        else if (completion.kind === 20 /* File */ && this._themeService.getFileIconTheme().hasFileIcons) {
            // special logic for 'file' completion items
            data.icon.className = 'icon hide';
            data.iconContainer.className = 'icon hide';
            const labelClasses = getIconClasses(this._modelService, this._modeService, URI.from({ scheme: 'fake', path: textLabel }), FileKind.FILE);
            const detailClasses = getIconClasses(this._modelService, this._modeService, URI.from({ scheme: 'fake', path: completion.detail }), FileKind.FILE);
            labelOptions.extraClasses = labelClasses.length > detailClasses.length ? labelClasses : detailClasses;
        }
        else if (completion.kind === 23 /* Folder */ && this._themeService.getFileIconTheme().hasFolderIcons) {
            // special logic for 'folder' completion items
            data.icon.className = 'icon hide';
            data.iconContainer.className = 'icon hide';
            labelOptions.extraClasses = flatten([
                getIconClasses(this._modelService, this._modeService, URI.from({ scheme: 'fake', path: textLabel }), FileKind.FOLDER),
                getIconClasses(this._modelService, this._modeService, URI.from({ scheme: 'fake', path: completion.detail }), FileKind.FOLDER)
            ]);
        }
        else {
            // normal icon
            data.icon.className = 'icon hide';
            data.iconContainer.className = '';
            data.iconContainer.classList.add('suggest-icon', ...completionKindToCssClass(completion.kind).split(' '));
        }
        if (completion.tags && completion.tags.indexOf(1 /* Deprecated */) >= 0) {
            labelOptions.extraClasses = (labelOptions.extraClasses || []).concat(['deprecated']);
            labelOptions.matches = [];
        }
        data.iconLabel.setLabel(textLabel, undefined, labelOptions);
        if (typeof completion.label === 'string') {
            data.parametersLabel.textContent = '';
            data.qualifierLabel.textContent = '';
            data.detailsLabel.textContent = (completion.detail || '').replace(/\n.*$/m, '');
            data.root.classList.add('string-label');
            data.root.title = '';
        }
        else {
            data.parametersLabel.textContent = (completion.label.parameters || '').replace(/\n.*$/m, '');
            data.qualifierLabel.textContent = (completion.label.qualifier || '').replace(/\n.*$/m, '');
            data.detailsLabel.textContent = (completion.label.type || '').replace(/\n.*$/m, '');
            data.root.classList.remove('string-label');
            data.root.title = `${textLabel}${(_b = completion.label.parameters) !== null && _b !== void 0 ? _b : ''}  ${(_c = completion.label.qualifier) !== null && _c !== void 0 ? _c : ''}  ${(_d = completion.label.type) !== null && _d !== void 0 ? _d : ''}`;
        }
        if (this._editor.getOption(101 /* suggest */).showInlineDetails) {
            show(data.detailsLabel);
        }
        else {
            hide(data.detailsLabel);
        }
        if (canExpandCompletionItem(element)) {
            data.right.classList.add('can-expand-details');
            show(data.readMore);
            data.readMore.onmousedown = e => {
                e.stopPropagation();
                e.preventDefault();
            };
            data.readMore.onclick = e => {
                e.stopPropagation();
                e.preventDefault();
                this._onDidToggleDetails.fire();
            };
        }
        else {
            data.right.classList.remove('can-expand-details');
            hide(data.readMore);
            data.readMore.onmousedown = null;
            data.readMore.onclick = null;
        }
    }
    disposeTemplate(templateData) {
        templateData.disposables.dispose();
    }
};
ItemRenderer = __decorate([
    __param(1, IModelService),
    __param(2, IModeService),
    __param(3, IThemeService)
], ItemRenderer);
export { ItemRenderer };
