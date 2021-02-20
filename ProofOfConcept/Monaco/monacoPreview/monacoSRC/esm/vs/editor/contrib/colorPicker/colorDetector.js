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
import { TimeoutTimer, createCancelablePromise } from '../../../base/common/async.js';
import { RGBA } from '../../../base/common/color.js';
import { onUnexpectedError } from '../../../base/common/errors.js';
import { hash } from '../../../base/common/hash.js';
import { Disposable, DisposableStore } from '../../../base/common/lifecycle.js';
import { registerEditorContribution } from '../../browser/editorExtensions.js';
import { ICodeEditorService } from '../../browser/services/codeEditorService.js';
import { Range } from '../../common/core/range.js';
import { ModelDecorationOptions } from '../../common/model/textModel.js';
import { ColorProviderRegistry } from '../../common/modes.js';
import { getColors } from './color.js';
import { IConfigurationService } from '../../../platform/configuration/common/configuration.js';
const MAX_DECORATORS = 500;
let ColorDetector = class ColorDetector extends Disposable {
    constructor(_editor, _codeEditorService, _configurationService) {
        super();
        this._editor = _editor;
        this._codeEditorService = _codeEditorService;
        this._configurationService = _configurationService;
        this._localToDispose = this._register(new DisposableStore());
        this._decorationsIds = [];
        this._colorDatas = new Map();
        this._colorDecoratorIds = [];
        this._decorationsTypes = new Set();
        this._register(_editor.onDidChangeModel(() => {
            this._isEnabled = this.isEnabled();
            this.onModelChanged();
        }));
        this._register(_editor.onDidChangeModelLanguage(() => this.onModelChanged()));
        this._register(ColorProviderRegistry.onDidChange(() => this.onModelChanged()));
        this._register(_editor.onDidChangeConfiguration(() => {
            let prevIsEnabled = this._isEnabled;
            this._isEnabled = this.isEnabled();
            if (prevIsEnabled !== this._isEnabled) {
                if (this._isEnabled) {
                    this.onModelChanged();
                }
                else {
                    this.removeAllDecorations();
                }
            }
        }));
        this._timeoutTimer = null;
        this._computePromise = null;
        this._isEnabled = this.isEnabled();
        this.onModelChanged();
    }
    isEnabled() {
        const model = this._editor.getModel();
        if (!model) {
            return false;
        }
        const languageId = model.getLanguageIdentifier();
        // handle deprecated settings. [languageId].colorDecorators.enable
        const deprecatedConfig = this._configurationService.getValue(languageId.language);
        if (deprecatedConfig) {
            const colorDecorators = deprecatedConfig['colorDecorators']; // deprecatedConfig.valueOf('.colorDecorators.enable');
            if (colorDecorators && colorDecorators['enable'] !== undefined && !colorDecorators['enable']) {
                return colorDecorators['enable'];
            }
        }
        return this._editor.getOption(14 /* colorDecorators */);
    }
    static get(editor) {
        return editor.getContribution(this.ID);
    }
    dispose() {
        this.stop();
        this.removeAllDecorations();
        super.dispose();
    }
    onModelChanged() {
        this.stop();
        if (!this._isEnabled) {
            return;
        }
        const model = this._editor.getModel();
        if (!model || !ColorProviderRegistry.has(model)) {
            return;
        }
        this._localToDispose.add(this._editor.onDidChangeModelContent(() => {
            if (!this._timeoutTimer) {
                this._timeoutTimer = new TimeoutTimer();
                this._timeoutTimer.cancelAndSet(() => {
                    this._timeoutTimer = null;
                    this.beginCompute();
                }, ColorDetector.RECOMPUTE_TIME);
            }
        }));
        this.beginCompute();
    }
    beginCompute() {
        this._computePromise = createCancelablePromise(token => {
            const model = this._editor.getModel();
            if (!model) {
                return Promise.resolve([]);
            }
            return getColors(model, token);
        });
        this._computePromise.then((colorInfos) => {
            this.updateDecorations(colorInfos);
            this.updateColorDecorators(colorInfos);
            this._computePromise = null;
        }, onUnexpectedError);
    }
    stop() {
        if (this._timeoutTimer) {
            this._timeoutTimer.cancel();
            this._timeoutTimer = null;
        }
        if (this._computePromise) {
            this._computePromise.cancel();
            this._computePromise = null;
        }
        this._localToDispose.clear();
    }
    updateDecorations(colorDatas) {
        const decorations = colorDatas.map(c => ({
            range: {
                startLineNumber: c.colorInfo.range.startLineNumber,
                startColumn: c.colorInfo.range.startColumn,
                endLineNumber: c.colorInfo.range.endLineNumber,
                endColumn: c.colorInfo.range.endColumn
            },
            options: ModelDecorationOptions.EMPTY
        }));
        this._decorationsIds = this._editor.deltaDecorations(this._decorationsIds, decorations);
        this._colorDatas = new Map();
        this._decorationsIds.forEach((id, i) => this._colorDatas.set(id, colorDatas[i]));
    }
    updateColorDecorators(colorData) {
        let decorations = [];
        let newDecorationsTypes = {};
        for (let i = 0; i < colorData.length && decorations.length < MAX_DECORATORS; i++) {
            const { red, green, blue, alpha } = colorData[i].colorInfo.color;
            const rgba = new RGBA(Math.round(red * 255), Math.round(green * 255), Math.round(blue * 255), alpha);
            let subKey = hash(`rgba(${rgba.r},${rgba.g},${rgba.b},${rgba.a})`).toString(16);
            let color = `rgba(${rgba.r}, ${rgba.g}, ${rgba.b}, ${rgba.a})`;
            let key = 'colorBox-' + subKey;
            if (!this._decorationsTypes.has(key) && !newDecorationsTypes[key]) {
                this._codeEditorService.registerDecorationType(key, {
                    before: {
                        contentText: ' ',
                        border: 'solid 0.1em #000',
                        margin: '0.1em 0.2em 0 0.2em',
                        width: '0.8em',
                        height: '0.8em',
                        backgroundColor: color
                    },
                    dark: {
                        before: {
                            border: 'solid 0.1em #eee'
                        }
                    }
                }, undefined, this._editor);
            }
            newDecorationsTypes[key] = true;
            decorations.push({
                range: {
                    startLineNumber: colorData[i].colorInfo.range.startLineNumber,
                    startColumn: colorData[i].colorInfo.range.startColumn,
                    endLineNumber: colorData[i].colorInfo.range.endLineNumber,
                    endColumn: colorData[i].colorInfo.range.endColumn
                },
                options: this._codeEditorService.resolveDecorationOptions(key, true)
            });
        }
        this._decorationsTypes.forEach(subType => {
            if (!newDecorationsTypes[subType]) {
                this._codeEditorService.removeDecorationType(subType);
            }
        });
        this._colorDecoratorIds = this._editor.deltaDecorations(this._colorDecoratorIds, decorations);
    }
    removeAllDecorations() {
        this._decorationsIds = this._editor.deltaDecorations(this._decorationsIds, []);
        this._colorDecoratorIds = this._editor.deltaDecorations(this._colorDecoratorIds, []);
        this._decorationsTypes.forEach(subType => {
            this._codeEditorService.removeDecorationType(subType);
        });
    }
    getColorData(position) {
        const model = this._editor.getModel();
        if (!model) {
            return null;
        }
        const decorations = model
            .getDecorationsInRange(Range.fromPositions(position, position))
            .filter(d => this._colorDatas.has(d.id));
        if (decorations.length === 0) {
            return null;
        }
        return this._colorDatas.get(decorations[0].id);
    }
};
ColorDetector.ID = 'editor.contrib.colorDetector';
ColorDetector.RECOMPUTE_TIME = 1000; // ms
ColorDetector = __decorate([
    __param(1, ICodeEditorService),
    __param(2, IConfigurationService)
], ColorDetector);
export { ColorDetector };
registerEditorContribution(ColorDetector.ID, ColorDetector);
