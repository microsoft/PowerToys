/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { Emitter } from '../../../base/common/event.js';
import { Disposable } from '../../../base/common/lifecycle.js';
import { FrankensteinMode } from '../modes/abstractMode.js';
import { NULL_LANGUAGE_IDENTIFIER } from '../modes/nullMode.js';
import { LanguagesRegistry } from './languagesRegistry.js';
import { firstOrDefault } from '../../../base/common/arrays.js';
class LanguageSelection extends Disposable {
    constructor(onLanguagesMaybeChanged, selector) {
        super();
        this._onDidChange = this._register(new Emitter());
        this.onDidChange = this._onDidChange.event;
        this._selector = selector;
        this.languageIdentifier = this._selector();
        this._register(onLanguagesMaybeChanged(() => this._evaluate()));
    }
    _evaluate() {
        let languageIdentifier = this._selector();
        if (languageIdentifier.id === this.languageIdentifier.id) {
            // no change
            return;
        }
        this.languageIdentifier = languageIdentifier;
        this._onDidChange.fire(this.languageIdentifier);
    }
}
export class ModeServiceImpl extends Disposable {
    constructor(warnOnOverwrite = false) {
        super();
        this._onDidCreateMode = this._register(new Emitter());
        this.onDidCreateMode = this._onDidCreateMode.event;
        this._onLanguagesMaybeChanged = this._register(new Emitter());
        this.onLanguagesMaybeChanged = this._onLanguagesMaybeChanged.event;
        this._instantiatedModes = {};
        this._registry = this._register(new LanguagesRegistry(true, warnOnOverwrite));
        this._register(this._registry.onDidChange(() => this._onLanguagesMaybeChanged.fire()));
    }
    isRegisteredMode(mimetypeOrModeId) {
        return this._registry.isRegisteredMode(mimetypeOrModeId);
    }
    getModeIdForLanguageName(alias) {
        return this._registry.getModeIdForLanguageNameLowercase(alias);
    }
    getModeIdByFilepathOrFirstLine(resource, firstLine) {
        const modeIds = this._registry.getModeIdsFromFilepathOrFirstLine(resource, firstLine);
        return firstOrDefault(modeIds, null);
    }
    getModeId(commaSeparatedMimetypesOrCommaSeparatedIds) {
        const modeIds = this._registry.extractModeIds(commaSeparatedMimetypesOrCommaSeparatedIds);
        return firstOrDefault(modeIds, null);
    }
    getLanguageIdentifier(modeId) {
        return this._registry.getLanguageIdentifier(modeId);
    }
    // --- instantiation
    create(commaSeparatedMimetypesOrCommaSeparatedIds) {
        return new LanguageSelection(this.onLanguagesMaybeChanged, () => {
            const modeId = this.getModeId(commaSeparatedMimetypesOrCommaSeparatedIds);
            return this._createModeAndGetLanguageIdentifier(modeId);
        });
    }
    createByFilepathOrFirstLine(resource, firstLine) {
        return new LanguageSelection(this.onLanguagesMaybeChanged, () => {
            const modeId = this.getModeIdByFilepathOrFirstLine(resource, firstLine);
            return this._createModeAndGetLanguageIdentifier(modeId);
        });
    }
    _createModeAndGetLanguageIdentifier(modeId) {
        // Fall back to plain text if no mode was found
        const languageIdentifier = this.getLanguageIdentifier(modeId || 'plaintext') || NULL_LANGUAGE_IDENTIFIER;
        this._getOrCreateMode(languageIdentifier.language);
        return languageIdentifier;
    }
    triggerMode(commaSeparatedMimetypesOrCommaSeparatedIds) {
        const modeId = this.getModeId(commaSeparatedMimetypesOrCommaSeparatedIds);
        // Fall back to plain text if no mode was found
        this._getOrCreateMode(modeId || 'plaintext');
    }
    _getOrCreateMode(modeId) {
        if (!this._instantiatedModes.hasOwnProperty(modeId)) {
            let languageIdentifier = this.getLanguageIdentifier(modeId) || NULL_LANGUAGE_IDENTIFIER;
            this._instantiatedModes[modeId] = new FrankensteinMode(languageIdentifier);
            this._onDidCreateMode.fire(this._instantiatedModes[modeId]);
        }
        return this._instantiatedModes[modeId];
    }
}
