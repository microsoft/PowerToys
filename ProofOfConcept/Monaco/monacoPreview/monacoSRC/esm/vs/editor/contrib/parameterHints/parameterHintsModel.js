/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
var __awaiter = (this && this.__awaiter) || function (thisArg, _arguments, P, generator) {
    function adopt(value) { return value instanceof P ? value : new P(function (resolve) { resolve(value); }); }
    return new (P || (P = Promise))(function (resolve, reject) {
        function fulfilled(value) { try { step(generator.next(value)); } catch (e) { reject(e); } }
        function rejected(value) { try { step(generator["throw"](value)); } catch (e) { reject(e); } }
        function step(result) { result.done ? resolve(result.value) : adopt(result.value).then(fulfilled, rejected); }
        step((generator = generator.apply(thisArg, _arguments || [])).next());
    });
};
import { createCancelablePromise, Delayer } from '../../../base/common/async.js';
import { onUnexpectedError } from '../../../base/common/errors.js';
import { Emitter } from '../../../base/common/event.js';
import { Disposable, MutableDisposable } from '../../../base/common/lifecycle.js';
import { CharacterSet } from '../../common/core/characterClassifier.js';
import * as modes from '../../common/modes.js';
import { provideSignatureHelp } from './provideSignatureHelp.js';
var ParameterHintState;
(function (ParameterHintState) {
    ParameterHintState.Default = { type: 0 /* Default */ };
    class Pending {
        constructor(request, previouslyActiveHints) {
            this.request = request;
            this.previouslyActiveHints = previouslyActiveHints;
            this.type = 2 /* Pending */;
        }
    }
    ParameterHintState.Pending = Pending;
    class Active {
        constructor(hints) {
            this.hints = hints;
            this.type = 1 /* Active */;
        }
    }
    ParameterHintState.Active = Active;
})(ParameterHintState || (ParameterHintState = {}));
export class ParameterHintsModel extends Disposable {
    constructor(editor, delay = ParameterHintsModel.DEFAULT_DELAY) {
        super();
        this._onChangedHints = this._register(new Emitter());
        this.onChangedHints = this._onChangedHints.event;
        this.triggerOnType = false;
        this._state = ParameterHintState.Default;
        this._pendingTriggers = [];
        this._lastSignatureHelpResult = this._register(new MutableDisposable());
        this.triggerChars = new CharacterSet();
        this.retriggerChars = new CharacterSet();
        this.triggerId = 0;
        this.editor = editor;
        this.throttledDelayer = new Delayer(delay);
        this._register(this.editor.onDidChangeConfiguration(() => this.onEditorConfigurationChange()));
        this._register(this.editor.onDidChangeModel(e => this.onModelChanged()));
        this._register(this.editor.onDidChangeModelLanguage(_ => this.onModelChanged()));
        this._register(this.editor.onDidChangeCursorSelection(e => this.onCursorChange(e)));
        this._register(this.editor.onDidChangeModelContent(e => this.onModelContentChange()));
        this._register(modes.SignatureHelpProviderRegistry.onDidChange(this.onModelChanged, this));
        this._register(this.editor.onDidType(text => this.onDidType(text)));
        this.onEditorConfigurationChange();
        this.onModelChanged();
    }
    get state() { return this._state; }
    set state(value) {
        if (this._state.type === 2 /* Pending */) {
            this._state.request.cancel();
        }
        this._state = value;
    }
    cancel(silent = false) {
        this.state = ParameterHintState.Default;
        this.throttledDelayer.cancel();
        if (!silent) {
            this._onChangedHints.fire(undefined);
        }
    }
    trigger(context, delay) {
        const model = this.editor.getModel();
        if (!model || !modes.SignatureHelpProviderRegistry.has(model)) {
            return;
        }
        const triggerId = ++this.triggerId;
        this._pendingTriggers.push(context);
        this.throttledDelayer.trigger(() => {
            return this.doTrigger(triggerId);
        }, delay)
            .catch(onUnexpectedError);
    }
    next() {
        if (this.state.type !== 1 /* Active */) {
            return;
        }
        const length = this.state.hints.signatures.length;
        const activeSignature = this.state.hints.activeSignature;
        const last = (activeSignature % length) === (length - 1);
        const cycle = this.editor.getOption(70 /* parameterHints */).cycle;
        // If there is only one signature, or we're on last signature of list
        if ((length < 2 || last) && !cycle) {
            this.cancel();
            return;
        }
        this.updateActiveSignature(last && cycle ? 0 : activeSignature + 1);
    }
    previous() {
        if (this.state.type !== 1 /* Active */) {
            return;
        }
        const length = this.state.hints.signatures.length;
        const activeSignature = this.state.hints.activeSignature;
        const first = activeSignature === 0;
        const cycle = this.editor.getOption(70 /* parameterHints */).cycle;
        // If there is only one signature, or we're on first signature of list
        if ((length < 2 || first) && !cycle) {
            this.cancel();
            return;
        }
        this.updateActiveSignature(first && cycle ? length - 1 : activeSignature - 1);
    }
    updateActiveSignature(activeSignature) {
        if (this.state.type !== 1 /* Active */) {
            return;
        }
        this.state = new ParameterHintState.Active(Object.assign(Object.assign({}, this.state.hints), { activeSignature }));
        this._onChangedHints.fire(this.state.hints);
    }
    doTrigger(triggerId) {
        return __awaiter(this, void 0, void 0, function* () {
            const isRetrigger = this.state.type === 1 /* Active */ || this.state.type === 2 /* Pending */;
            const activeSignatureHelp = this.getLastActiveHints();
            this.cancel(true);
            if (this._pendingTriggers.length === 0) {
                return false;
            }
            const context = this._pendingTriggers.reduce(mergeTriggerContexts);
            this._pendingTriggers = [];
            const triggerContext = {
                triggerKind: context.triggerKind,
                triggerCharacter: context.triggerCharacter,
                isRetrigger: isRetrigger,
                activeSignatureHelp: activeSignatureHelp
            };
            if (!this.editor.hasModel()) {
                return false;
            }
            const model = this.editor.getModel();
            const position = this.editor.getPosition();
            this.state = new ParameterHintState.Pending(createCancelablePromise(token => provideSignatureHelp(model, position, triggerContext, token)), activeSignatureHelp);
            try {
                const result = yield this.state.request;
                // Check that we are still resolving the correct signature help
                if (triggerId !== this.triggerId) {
                    result === null || result === void 0 ? void 0 : result.dispose();
                    return false;
                }
                if (!result || !result.value.signatures || result.value.signatures.length === 0) {
                    result === null || result === void 0 ? void 0 : result.dispose();
                    this._lastSignatureHelpResult.clear();
                    this.cancel();
                    return false;
                }
                else {
                    this.state = new ParameterHintState.Active(result.value);
                    this._lastSignatureHelpResult.value = result;
                    this._onChangedHints.fire(this.state.hints);
                    return true;
                }
            }
            catch (error) {
                if (triggerId === this.triggerId) {
                    this.state = ParameterHintState.Default;
                }
                onUnexpectedError(error);
                return false;
            }
        });
    }
    getLastActiveHints() {
        switch (this.state.type) {
            case 1 /* Active */: return this.state.hints;
            case 2 /* Pending */: return this.state.previouslyActiveHints;
            default: return undefined;
        }
    }
    get isTriggered() {
        return this.state.type === 1 /* Active */
            || this.state.type === 2 /* Pending */
            || this.throttledDelayer.isTriggered();
    }
    onModelChanged() {
        this.cancel();
        // Update trigger characters
        this.triggerChars = new CharacterSet();
        this.retriggerChars = new CharacterSet();
        const model = this.editor.getModel();
        if (!model) {
            return;
        }
        for (const support of modes.SignatureHelpProviderRegistry.ordered(model)) {
            for (const ch of support.signatureHelpTriggerCharacters || []) {
                this.triggerChars.add(ch.charCodeAt(0));
                // All trigger characters are also considered retrigger characters
                this.retriggerChars.add(ch.charCodeAt(0));
            }
            for (const ch of support.signatureHelpRetriggerCharacters || []) {
                this.retriggerChars.add(ch.charCodeAt(0));
            }
        }
    }
    onDidType(text) {
        if (!this.triggerOnType) {
            return;
        }
        const lastCharIndex = text.length - 1;
        const triggerCharCode = text.charCodeAt(lastCharIndex);
        if (this.triggerChars.has(triggerCharCode) || this.isTriggered && this.retriggerChars.has(triggerCharCode)) {
            this.trigger({
                triggerKind: modes.SignatureHelpTriggerKind.TriggerCharacter,
                triggerCharacter: text.charAt(lastCharIndex),
            });
        }
    }
    onCursorChange(e) {
        if (e.source === 'mouse') {
            this.cancel();
        }
        else if (this.isTriggered) {
            this.trigger({ triggerKind: modes.SignatureHelpTriggerKind.ContentChange });
        }
    }
    onModelContentChange() {
        if (this.isTriggered) {
            this.trigger({ triggerKind: modes.SignatureHelpTriggerKind.ContentChange });
        }
    }
    onEditorConfigurationChange() {
        this.triggerOnType = this.editor.getOption(70 /* parameterHints */).enabled;
        if (!this.triggerOnType) {
            this.cancel();
        }
    }
    dispose() {
        this.cancel(true);
        super.dispose();
    }
}
ParameterHintsModel.DEFAULT_DELAY = 120; // ms
function mergeTriggerContexts(previous, current) {
    switch (current.triggerKind) {
        case modes.SignatureHelpTriggerKind.Invoke:
            // Invoke overrides previous triggers.
            return current;
        case modes.SignatureHelpTriggerKind.ContentChange:
            // Ignore content changes triggers
            return previous;
        case modes.SignatureHelpTriggerKind.TriggerCharacter:
        default:
            return current;
    }
}
