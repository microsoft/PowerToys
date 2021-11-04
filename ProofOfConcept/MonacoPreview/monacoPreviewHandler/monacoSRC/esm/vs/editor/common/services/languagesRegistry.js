/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { onUnexpectedError } from '../../../base/common/errors.js';
import { Emitter } from '../../../base/common/event.js';
import { Disposable } from '../../../base/common/lifecycle.js';
import * as mime from '../../../base/common/mime.js';
import * as strings from '../../../base/common/strings.js';
import { LanguageIdentifier } from '../modes.js';
import { ModesRegistry } from '../modes/modesRegistry.js';
import { NULL_LANGUAGE_IDENTIFIER, NULL_MODE_ID } from '../modes/nullMode.js';
import { Extensions } from '../../../platform/configuration/common/configurationRegistry.js';
import { Registry } from '../../../platform/registry/common/platform.js';
const hasOwnProperty = Object.prototype.hasOwnProperty;
export class LanguagesRegistry extends Disposable {
    constructor(useModesRegistry = true, warnOnOverwrite = false) {
        super();
        this._onDidChange = this._register(new Emitter());
        this.onDidChange = this._onDidChange.event;
        this._warnOnOverwrite = warnOnOverwrite;
        this._nextLanguageId2 = 1;
        this._languageIdToLanguage = [];
        this._languageToLanguageId = Object.create(null);
        this._languages = {};
        this._mimeTypesMap = {};
        this._nameMap = {};
        this._lowercaseNameMap = {};
        if (useModesRegistry) {
            this._initializeFromRegistry();
            this._register(ModesRegistry.onDidChangeLanguages((m) => this._initializeFromRegistry()));
        }
    }
    _initializeFromRegistry() {
        this._languages = {};
        this._mimeTypesMap = {};
        this._nameMap = {};
        this._lowercaseNameMap = {};
        const desc = ModesRegistry.getLanguages();
        this._registerLanguages(desc);
    }
    _registerLanguages(desc) {
        for (const d of desc) {
            this._registerLanguage(d);
        }
        // Rebuild fast path maps
        this._mimeTypesMap = {};
        this._nameMap = {};
        this._lowercaseNameMap = {};
        Object.keys(this._languages).forEach((langId) => {
            let language = this._languages[langId];
            if (language.name) {
                this._nameMap[language.name] = language.identifier;
            }
            language.aliases.forEach((alias) => {
                this._lowercaseNameMap[alias.toLowerCase()] = language.identifier;
            });
            language.mimetypes.forEach((mimetype) => {
                this._mimeTypesMap[mimetype] = language.identifier;
            });
        });
        Registry.as(Extensions.Configuration).registerOverrideIdentifiers(ModesRegistry.getLanguages().map(language => language.id));
        this._onDidChange.fire();
    }
    _getLanguageId(language) {
        if (this._languageToLanguageId[language]) {
            return this._languageToLanguageId[language];
        }
        const languageId = this._nextLanguageId2++;
        this._languageIdToLanguage[languageId] = language;
        this._languageToLanguageId[language] = languageId;
        return languageId;
    }
    _registerLanguage(lang) {
        const langId = lang.id;
        let resolvedLanguage;
        if (hasOwnProperty.call(this._languages, langId)) {
            resolvedLanguage = this._languages[langId];
        }
        else {
            const languageId = this._getLanguageId(langId);
            resolvedLanguage = {
                identifier: new LanguageIdentifier(langId, languageId),
                name: null,
                mimetypes: [],
                aliases: [],
                extensions: [],
                filenames: [],
                configurationFiles: []
            };
            this._languages[langId] = resolvedLanguage;
        }
        this._mergeLanguage(resolvedLanguage, lang);
    }
    _mergeLanguage(resolvedLanguage, lang) {
        const langId = lang.id;
        let primaryMime = null;
        if (Array.isArray(lang.mimetypes) && lang.mimetypes.length > 0) {
            resolvedLanguage.mimetypes.push(...lang.mimetypes);
            primaryMime = lang.mimetypes[0];
        }
        if (!primaryMime) {
            primaryMime = `text/x-${langId}`;
            resolvedLanguage.mimetypes.push(primaryMime);
        }
        if (Array.isArray(lang.extensions)) {
            if (lang.configuration) {
                // insert first as this appears to be the 'primary' language definition
                resolvedLanguage.extensions = lang.extensions.concat(resolvedLanguage.extensions);
            }
            else {
                resolvedLanguage.extensions = resolvedLanguage.extensions.concat(lang.extensions);
            }
            for (let extension of lang.extensions) {
                mime.registerTextMime({ id: langId, mime: primaryMime, extension: extension }, this._warnOnOverwrite);
            }
        }
        if (Array.isArray(lang.filenames)) {
            for (let filename of lang.filenames) {
                mime.registerTextMime({ id: langId, mime: primaryMime, filename: filename }, this._warnOnOverwrite);
                resolvedLanguage.filenames.push(filename);
            }
        }
        if (Array.isArray(lang.filenamePatterns)) {
            for (let filenamePattern of lang.filenamePatterns) {
                mime.registerTextMime({ id: langId, mime: primaryMime, filepattern: filenamePattern }, this._warnOnOverwrite);
            }
        }
        if (typeof lang.firstLine === 'string' && lang.firstLine.length > 0) {
            let firstLineRegexStr = lang.firstLine;
            if (firstLineRegexStr.charAt(0) !== '^') {
                firstLineRegexStr = '^' + firstLineRegexStr;
            }
            try {
                let firstLineRegex = new RegExp(firstLineRegexStr);
                if (!strings.regExpLeadsToEndlessLoop(firstLineRegex)) {
                    mime.registerTextMime({ id: langId, mime: primaryMime, firstline: firstLineRegex }, this._warnOnOverwrite);
                }
            }
            catch (err) {
                // Most likely, the regex was bad
                onUnexpectedError(err);
            }
        }
        resolvedLanguage.aliases.push(langId);
        let langAliases = null;
        if (typeof lang.aliases !== 'undefined' && Array.isArray(lang.aliases)) {
            if (lang.aliases.length === 0) {
                // signal that this language should not get a name
                langAliases = [null];
            }
            else {
                langAliases = lang.aliases;
            }
        }
        if (langAliases !== null) {
            for (const langAlias of langAliases) {
                if (!langAlias || langAlias.length === 0) {
                    continue;
                }
                resolvedLanguage.aliases.push(langAlias);
            }
        }
        let containsAliases = (langAliases !== null && langAliases.length > 0);
        if (containsAliases && langAliases[0] === null) {
            // signal that this language should not get a name
        }
        else {
            let bestName = (containsAliases ? langAliases[0] : null) || langId;
            if (containsAliases || !resolvedLanguage.name) {
                resolvedLanguage.name = bestName;
            }
        }
        if (lang.configuration) {
            resolvedLanguage.configurationFiles.push(lang.configuration);
        }
    }
    isRegisteredMode(mimetypeOrModeId) {
        // Is this a known mime type ?
        if (hasOwnProperty.call(this._mimeTypesMap, mimetypeOrModeId)) {
            return true;
        }
        // Is this a known mode id ?
        return hasOwnProperty.call(this._languages, mimetypeOrModeId);
    }
    getModeIdForLanguageNameLowercase(languageNameLower) {
        if (!hasOwnProperty.call(this._lowercaseNameMap, languageNameLower)) {
            return null;
        }
        return this._lowercaseNameMap[languageNameLower].language;
    }
    extractModeIds(commaSeparatedMimetypesOrCommaSeparatedIds) {
        if (!commaSeparatedMimetypesOrCommaSeparatedIds) {
            return [];
        }
        return (commaSeparatedMimetypesOrCommaSeparatedIds.
            split(',').
            map((mimeTypeOrId) => mimeTypeOrId.trim()).
            map((mimeTypeOrId) => {
            if (hasOwnProperty.call(this._mimeTypesMap, mimeTypeOrId)) {
                return this._mimeTypesMap[mimeTypeOrId].language;
            }
            return mimeTypeOrId;
        }).
            filter((modeId) => {
            return hasOwnProperty.call(this._languages, modeId);
        }));
    }
    getLanguageIdentifier(_modeId) {
        if (_modeId === NULL_MODE_ID || _modeId === 0 /* Null */) {
            return NULL_LANGUAGE_IDENTIFIER;
        }
        let modeId;
        if (typeof _modeId === 'string') {
            modeId = _modeId;
        }
        else {
            modeId = this._languageIdToLanguage[_modeId];
            if (!modeId) {
                return null;
            }
        }
        if (!hasOwnProperty.call(this._languages, modeId)) {
            return null;
        }
        return this._languages[modeId].identifier;
    }
    getModeIdsFromFilepathOrFirstLine(resource, firstLine) {
        if (!resource && !firstLine) {
            return [];
        }
        let mimeTypes = mime.guessMimeTypes(resource, firstLine);
        return this.extractModeIds(mimeTypes.join(','));
    }
}
