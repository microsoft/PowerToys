/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import * as nls from '../../../nls.js';
import { IntervalTimer } from '../../../base/common/async.js';
import { Emitter, Event } from '../../../base/common/event.js';
import { Disposable } from '../../../base/common/lifecycle.js';
export class AbstractKeybindingService extends Disposable {
    constructor(_contextKeyService, _commandService, _telemetryService, _notificationService, _logService) {
        super();
        this._contextKeyService = _contextKeyService;
        this._commandService = _commandService;
        this._telemetryService = _telemetryService;
        this._notificationService = _notificationService;
        this._logService = _logService;
        this._onDidUpdateKeybindings = this._register(new Emitter());
        this._currentChord = null;
        this._currentChordChecker = new IntervalTimer();
        this._currentChordStatusMessage = null;
        this._logging = false;
    }
    get onDidUpdateKeybindings() {
        return this._onDidUpdateKeybindings ? this._onDidUpdateKeybindings.event : Event.None; // Sinon stubbing walks properties on prototype
    }
    dispose() {
        super.dispose();
    }
    _log(str) {
        if (this._logging) {
            this._logService.info(`[KeybindingService]: ${str}`);
        }
    }
    getKeybindings() {
        return this._getResolver().getKeybindings();
    }
    lookupKeybinding(commandId) {
        const result = this._getResolver().lookupPrimaryKeybinding(commandId);
        if (!result) {
            return undefined;
        }
        return result.resolvedKeybinding;
    }
    dispatchEvent(e, target) {
        return this._dispatch(e, target);
    }
    softDispatch(e, target) {
        const keybinding = this.resolveKeyboardEvent(e);
        if (keybinding.isChord()) {
            console.warn('Unexpected keyboard event mapped to a chord');
            return null;
        }
        const [firstPart,] = keybinding.getDispatchParts();
        if (firstPart === null) {
            // cannot be dispatched, probably only modifier keys
            return null;
        }
        const contextValue = this._contextKeyService.getContext(target);
        const currentChord = this._currentChord ? this._currentChord.keypress : null;
        return this._getResolver().resolve(contextValue, currentChord, firstPart);
    }
    _enterChordMode(firstPart, keypressLabel) {
        this._currentChord = {
            keypress: firstPart,
            label: keypressLabel
        };
        this._currentChordStatusMessage = this._notificationService.status(nls.localize('first.chord', "({0}) was pressed. Waiting for second key of chord...", keypressLabel));
        const chordEnterTime = Date.now();
        this._currentChordChecker.cancelAndSet(() => {
            if (!this._documentHasFocus()) {
                // Focus has been lost => leave chord mode
                this._leaveChordMode();
                return;
            }
            if (Date.now() - chordEnterTime > 5000) {
                // 5 seconds elapsed => leave chord mode
                this._leaveChordMode();
            }
        }, 500);
    }
    _leaveChordMode() {
        if (this._currentChordStatusMessage) {
            this._currentChordStatusMessage.dispose();
            this._currentChordStatusMessage = null;
        }
        this._currentChordChecker.cancel();
        this._currentChord = null;
    }
    _dispatch(e, target) {
        return this._doDispatch(this.resolveKeyboardEvent(e), target);
    }
    _doDispatch(keybinding, target) {
        let shouldPreventDefault = false;
        if (keybinding.isChord()) {
            console.warn('Unexpected keyboard event mapped to a chord');
            return false;
        }
        const [firstPart,] = keybinding.getDispatchParts();
        if (firstPart === null) {
            this._log(`\\ Keyboard event cannot be dispatched.`);
            // cannot be dispatched, probably only modifier keys
            return shouldPreventDefault;
        }
        const contextValue = this._contextKeyService.getContext(target);
        const currentChord = this._currentChord ? this._currentChord.keypress : null;
        const keypressLabel = keybinding.getLabel();
        const resolveResult = this._getResolver().resolve(contextValue, currentChord, firstPart);
        this._logService.trace('KeybindingService#dispatch', keypressLabel, resolveResult === null || resolveResult === void 0 ? void 0 : resolveResult.commandId);
        if (resolveResult && resolveResult.enterChord) {
            shouldPreventDefault = true;
            this._enterChordMode(firstPart, keypressLabel);
            return shouldPreventDefault;
        }
        if (this._currentChord) {
            if (!resolveResult || !resolveResult.commandId) {
                this._notificationService.status(nls.localize('missing.chord', "The key combination ({0}, {1}) is not a command.", this._currentChord.label, keypressLabel), { hideAfter: 10 * 1000 /* 10s */ });
                shouldPreventDefault = true;
            }
        }
        this._leaveChordMode();
        if (resolveResult && resolveResult.commandId) {
            if (!resolveResult.bubble) {
                shouldPreventDefault = true;
            }
            if (typeof resolveResult.commandArgs === 'undefined') {
                this._commandService.executeCommand(resolveResult.commandId).then(undefined, err => this._notificationService.warn(err));
            }
            else {
                this._commandService.executeCommand(resolveResult.commandId, resolveResult.commandArgs).then(undefined, err => this._notificationService.warn(err));
            }
            this._telemetryService.publicLog2('workbenchActionExecuted', { id: resolveResult.commandId, from: 'keybinding' });
        }
        return shouldPreventDefault;
    }
    mightProducePrintableCharacter(event) {
        if (event.ctrlKey || event.metaKey) {
            // ignore ctrl/cmd-combination but not shift/alt-combinatios
            return false;
        }
        // weak check for certain ranges. this is properly implemented in a subclass
        // with access to the KeyboardMapperFactory.
        if ((event.keyCode >= 31 /* KEY_A */ && event.keyCode <= 56 /* KEY_Z */)
            || (event.keyCode >= 21 /* KEY_0 */ && event.keyCode <= 30 /* KEY_9 */)) {
            return true;
        }
        return false;
    }
}
