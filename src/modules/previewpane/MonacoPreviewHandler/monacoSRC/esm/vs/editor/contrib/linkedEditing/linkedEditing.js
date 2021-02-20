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
var __awaiter = (this && this.__awaiter) || function (thisArg, _arguments, P, generator) {
    function adopt(value) { return value instanceof P ? value : new P(function (resolve) { resolve(value); }); }
    return new (P || (P = Promise))(function (resolve, reject) {
        function fulfilled(value) { try { step(generator.next(value)); } catch (e) { reject(e); } }
        function rejected(value) { try { step(generator["throw"](value)); } catch (e) { reject(e); } }
        function step(result) { result.done ? resolve(result.value) : adopt(result.value).then(fulfilled, rejected); }
        step((generator = generator.apply(thisArg, _arguments || [])).next());
    });
};
import * as nls from '../../../nls.js';
import { registerEditorContribution, registerModelAndPositionCommand, EditorAction, EditorCommand, registerEditorAction, registerEditorCommand } from '../../browser/editorExtensions.js';
import * as arrays from '../../../base/common/arrays.js';
import { Disposable, DisposableStore } from '../../../base/common/lifecycle.js';
import { Position } from '../../common/core/position.js';
import { CancellationToken } from '../../../base/common/cancellation.js';
import { Range } from '../../common/core/range.js';
import { LinkedEditingRangeProviderRegistry } from '../../common/modes.js';
import { first, createCancelablePromise, Delayer } from '../../../base/common/async.js';
import { ModelDecorationOptions } from '../../common/model/textModel.js';
import { ContextKeyExpr, RawContextKey, IContextKeyService } from '../../../platform/contextkey/common/contextkey.js';
import { EditorContextKeys } from '../../common/editorContextKeys.js';
import { URI } from '../../../base/common/uri.js';
import { ICodeEditorService } from '../../browser/services/codeEditorService.js';
import { isPromiseCanceledError, onUnexpectedError, onUnexpectedExternalError } from '../../../base/common/errors.js';
import * as strings from '../../../base/common/strings.js';
import { registerColor } from '../../../platform/theme/common/colorRegistry.js';
import { registerThemingParticipant } from '../../../platform/theme/common/themeService.js';
import { Color } from '../../../base/common/color.js';
import { LanguageConfigurationRegistry } from '../../common/modes/languageConfigurationRegistry.js';
export const CONTEXT_ONTYPE_RENAME_INPUT_VISIBLE = new RawContextKey('LinkedEditingInputVisible', false);
const DECORATION_CLASS_NAME = 'linked-editing-decoration';
let LinkedEditingContribution = class LinkedEditingContribution extends Disposable {
    constructor(editor, contextKeyService) {
        super();
        this._debounceDuration = 200;
        this._localToDispose = this._register(new DisposableStore());
        this._editor = editor;
        this._enabled = false;
        this._visibleContextKey = CONTEXT_ONTYPE_RENAME_INPUT_VISIBLE.bindTo(contextKeyService);
        this._currentDecorations = [];
        this._languageWordPattern = null;
        this._currentWordPattern = null;
        this._ignoreChangeEvent = false;
        this._localToDispose = this._register(new DisposableStore());
        this._rangeUpdateTriggerPromise = null;
        this._rangeSyncTriggerPromise = null;
        this._currentRequest = null;
        this._currentRequestPosition = null;
        this._currentRequestModelVersion = null;
        this._register(this._editor.onDidChangeModel(() => this.reinitialize()));
        this._register(this._editor.onDidChangeConfiguration(e => {
            if (e.hasChanged(56 /* linkedEditing */) || e.hasChanged(76 /* renameOnType */)) {
                this.reinitialize();
            }
        }));
        this._register(LinkedEditingRangeProviderRegistry.onDidChange(() => this.reinitialize()));
        this._register(this._editor.onDidChangeModelLanguage(() => this.reinitialize()));
        this.reinitialize();
    }
    static get(editor) {
        return editor.getContribution(LinkedEditingContribution.ID);
    }
    reinitialize() {
        const model = this._editor.getModel();
        const isEnabled = model !== null && (this._editor.getOption(56 /* linkedEditing */) || this._editor.getOption(76 /* renameOnType */)) && LinkedEditingRangeProviderRegistry.has(model);
        if (isEnabled === this._enabled) {
            return;
        }
        this._enabled = isEnabled;
        this.clearRanges();
        this._localToDispose.clear();
        if (!isEnabled || model === null) {
            return;
        }
        this._languageWordPattern = LanguageConfigurationRegistry.getWordDefinition(model.getLanguageIdentifier().id);
        this._localToDispose.add(model.onDidChangeLanguageConfiguration(() => {
            this._languageWordPattern = LanguageConfigurationRegistry.getWordDefinition(model.getLanguageIdentifier().id);
        }));
        const rangeUpdateScheduler = new Delayer(this._debounceDuration);
        const triggerRangeUpdate = () => {
            this._rangeUpdateTriggerPromise = rangeUpdateScheduler.trigger(() => this.updateRanges(), this._debounceDuration);
        };
        const rangeSyncScheduler = new Delayer(0);
        const triggerRangeSync = (decorations) => {
            this._rangeSyncTriggerPromise = rangeSyncScheduler.trigger(() => this._syncRanges(decorations));
        };
        this._localToDispose.add(this._editor.onDidChangeCursorPosition(() => {
            triggerRangeUpdate();
        }));
        this._localToDispose.add(this._editor.onDidChangeModelContent((e) => {
            if (!this._ignoreChangeEvent) {
                if (this._currentDecorations.length > 0) {
                    const referenceRange = model.getDecorationRange(this._currentDecorations[0]);
                    if (referenceRange && e.changes.every(c => referenceRange.intersectRanges(c.range))) {
                        triggerRangeSync(this._currentDecorations);
                        return;
                    }
                }
            }
            triggerRangeUpdate();
        }));
        this._localToDispose.add({
            dispose: () => {
                rangeUpdateScheduler.cancel();
                rangeSyncScheduler.cancel();
            }
        });
        this.updateRanges();
    }
    _syncRanges(decorations) {
        // dalayed invocation, make sure we're still on
        if (!this._editor.hasModel() || decorations !== this._currentDecorations || decorations.length === 0) {
            // nothing to do
            return;
        }
        const model = this._editor.getModel();
        const referenceRange = model.getDecorationRange(decorations[0]);
        if (!referenceRange || referenceRange.startLineNumber !== referenceRange.endLineNumber) {
            return this.clearRanges();
        }
        const referenceValue = model.getValueInRange(referenceRange);
        if (this._currentWordPattern) {
            const match = referenceValue.match(this._currentWordPattern);
            const matchLength = match ? match[0].length : 0;
            if (matchLength !== referenceValue.length) {
                return this.clearRanges();
            }
        }
        let edits = [];
        for (let i = 1, len = decorations.length; i < len; i++) {
            const mirrorRange = model.getDecorationRange(decorations[i]);
            if (!mirrorRange) {
                continue;
            }
            if (mirrorRange.startLineNumber !== mirrorRange.endLineNumber) {
                edits.push({
                    range: mirrorRange,
                    text: referenceValue
                });
            }
            else {
                let oldValue = model.getValueInRange(mirrorRange);
                let newValue = referenceValue;
                let rangeStartColumn = mirrorRange.startColumn;
                let rangeEndColumn = mirrorRange.endColumn;
                const commonPrefixLength = strings.commonPrefixLength(oldValue, newValue);
                rangeStartColumn += commonPrefixLength;
                oldValue = oldValue.substr(commonPrefixLength);
                newValue = newValue.substr(commonPrefixLength);
                const commonSuffixLength = strings.commonSuffixLength(oldValue, newValue);
                rangeEndColumn -= commonSuffixLength;
                oldValue = oldValue.substr(0, oldValue.length - commonSuffixLength);
                newValue = newValue.substr(0, newValue.length - commonSuffixLength);
                if (rangeStartColumn !== rangeEndColumn || newValue.length !== 0) {
                    edits.push({
                        range: new Range(mirrorRange.startLineNumber, rangeStartColumn, mirrorRange.endLineNumber, rangeEndColumn),
                        text: newValue
                    });
                }
            }
        }
        if (edits.length === 0) {
            return;
        }
        try {
            this._editor.popUndoStop();
            this._ignoreChangeEvent = true;
            const prevEditOperationType = this._editor._getViewModel().getPrevEditOperationType();
            this._editor.executeEdits('linkedEditing', edits);
            this._editor._getViewModel().setPrevEditOperationType(prevEditOperationType);
        }
        finally {
            this._ignoreChangeEvent = false;
        }
    }
    dispose() {
        this.clearRanges();
        super.dispose();
    }
    clearRanges() {
        this._visibleContextKey.set(false);
        this._currentDecorations = this._editor.deltaDecorations(this._currentDecorations, []);
        if (this._currentRequest) {
            this._currentRequest.cancel();
            this._currentRequest = null;
            this._currentRequestPosition = null;
        }
    }
    updateRanges(force = false) {
        return __awaiter(this, void 0, void 0, function* () {
            if (!this._editor.hasModel()) {
                this.clearRanges();
                return;
            }
            const position = this._editor.getPosition();
            if (!this._enabled && !force || this._editor.getSelections().length > 1) {
                // disabled or multicursor
                this.clearRanges();
                return;
            }
            const model = this._editor.getModel();
            const modelVersionId = model.getVersionId();
            if (this._currentRequestPosition && this._currentRequestModelVersion === modelVersionId) {
                if (position.equals(this._currentRequestPosition)) {
                    return; // same position
                }
                if (this._currentDecorations && this._currentDecorations.length > 0) {
                    const range = model.getDecorationRange(this._currentDecorations[0]);
                    if (range && range.containsPosition(position)) {
                        return; // just moving inside the existing primary range
                    }
                }
            }
            this._currentRequestPosition = position;
            this._currentRequestModelVersion = modelVersionId;
            const request = createCancelablePromise((token) => __awaiter(this, void 0, void 0, function* () {
                try {
                    const response = yield getLinkedEditingRanges(model, position, token);
                    if (request !== this._currentRequest) {
                        return;
                    }
                    this._currentRequest = null;
                    if (modelVersionId !== model.getVersionId()) {
                        return;
                    }
                    let ranges = [];
                    if (response === null || response === void 0 ? void 0 : response.ranges) {
                        ranges = response.ranges;
                    }
                    this._currentWordPattern = (response === null || response === void 0 ? void 0 : response.wordPattern) || this._languageWordPattern;
                    let foundReferenceRange = false;
                    for (let i = 0, len = ranges.length; i < len; i++) {
                        if (Range.containsPosition(ranges[i], position)) {
                            foundReferenceRange = true;
                            if (i !== 0) {
                                const referenceRange = ranges[i];
                                ranges.splice(i, 1);
                                ranges.unshift(referenceRange);
                            }
                            break;
                        }
                    }
                    if (!foundReferenceRange) {
                        // Cannot do linked editing if the ranges are not where the cursor is...
                        this.clearRanges();
                        return;
                    }
                    const decorations = ranges.map(range => ({ range: range, options: LinkedEditingContribution.DECORATION }));
                    this._visibleContextKey.set(true);
                    this._currentDecorations = this._editor.deltaDecorations(this._currentDecorations, decorations);
                }
                catch (err) {
                    if (!isPromiseCanceledError(err)) {
                        onUnexpectedError(err);
                    }
                    if (this._currentRequest === request || !this._currentRequest) {
                        // stop if we are still the latest request
                        this.clearRanges();
                    }
                }
            }));
            this._currentRequest = request;
            return request;
        });
    }
};
LinkedEditingContribution.ID = 'editor.contrib.linkedEditing';
LinkedEditingContribution.DECORATION = ModelDecorationOptions.register({
    stickiness: 0 /* AlwaysGrowsWhenTypingAtEdges */,
    className: DECORATION_CLASS_NAME
});
LinkedEditingContribution = __decorate([
    __param(1, IContextKeyService)
], LinkedEditingContribution);
export { LinkedEditingContribution };
export class LinkedEditingAction extends EditorAction {
    constructor() {
        super({
            id: 'editor.action.linkedEditing',
            label: nls.localize('linkedEditing.label', "Start Linked Editing"),
            alias: 'Start Linked Editing',
            precondition: ContextKeyExpr.and(EditorContextKeys.writable, EditorContextKeys.hasRenameProvider),
            kbOpts: {
                kbExpr: EditorContextKeys.editorTextFocus,
                primary: 2048 /* CtrlCmd */ | 1024 /* Shift */ | 60 /* F2 */,
                weight: 100 /* EditorContrib */
            }
        });
    }
    runCommand(accessor, args) {
        const editorService = accessor.get(ICodeEditorService);
        const [uri, pos] = Array.isArray(args) && args || [undefined, undefined];
        if (URI.isUri(uri) && Position.isIPosition(pos)) {
            return editorService.openCodeEditor({ resource: uri }, editorService.getActiveCodeEditor()).then(editor => {
                if (!editor) {
                    return;
                }
                editor.setPosition(pos);
                editor.invokeWithinContext(accessor => {
                    this.reportTelemetry(accessor, editor);
                    return this.run(accessor, editor);
                });
            }, onUnexpectedError);
        }
        return super.runCommand(accessor, args);
    }
    run(_accessor, editor) {
        const controller = LinkedEditingContribution.get(editor);
        if (controller) {
            return Promise.resolve(controller.updateRanges(true));
        }
        return Promise.resolve();
    }
}
const LinkedEditingCommand = EditorCommand.bindToContribution(LinkedEditingContribution.get);
registerEditorCommand(new LinkedEditingCommand({
    id: 'cancelLinkedEditingInput',
    precondition: CONTEXT_ONTYPE_RENAME_INPUT_VISIBLE,
    handler: x => x.clearRanges(),
    kbOpts: {
        kbExpr: EditorContextKeys.editorTextFocus,
        weight: 100 /* EditorContrib */ + 99,
        primary: 9 /* Escape */,
        secondary: [1024 /* Shift */ | 9 /* Escape */]
    }
}));
function getLinkedEditingRanges(model, position, token) {
    const orderedByScore = LinkedEditingRangeProviderRegistry.ordered(model);
    // in order of score ask the linked editing range provider
    // until someone response with a good result
    // (good = not null)
    return first(orderedByScore.map(provider => () => __awaiter(this, void 0, void 0, function* () {
        try {
            return yield provider.provideLinkedEditingRanges(model, position, token);
        }
        catch (e) {
            onUnexpectedExternalError(e);
            return undefined;
        }
    })), result => !!result && arrays.isNonEmptyArray(result === null || result === void 0 ? void 0 : result.ranges));
}
export const editorLinkedEditingBackground = registerColor('editor.linkedEditingBackground', { dark: Color.fromHex('#f00').transparent(0.3), light: Color.fromHex('#f00').transparent(0.3), hc: Color.fromHex('#f00').transparent(0.3) }, nls.localize('editorLinkedEditingBackground', 'Background color when the editor auto renames on type.'));
registerThemingParticipant((theme, collector) => {
    const editorLinkedEditingBackgroundColor = theme.getColor(editorLinkedEditingBackground);
    if (editorLinkedEditingBackgroundColor) {
        collector.addRule(`.monaco-editor .${DECORATION_CLASS_NAME} { background: ${editorLinkedEditingBackgroundColor}; border-left-color: ${editorLinkedEditingBackgroundColor}; }`);
    }
});
registerModelAndPositionCommand('_executeLinkedEditingProvider', (model, position) => getLinkedEditingRanges(model, position, CancellationToken.None));
registerEditorContribution(LinkedEditingContribution.ID, LinkedEditingContribution);
registerEditorAction(LinkedEditingAction);
