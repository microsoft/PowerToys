/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import * as nls from '../../../nls.js';
import { Emitter } from '../../../base/common/event.js';
import { Disposable } from '../../../base/common/lifecycle.js';
import * as objects from '../../../base/common/objects.js';
import * as arrays from '../../../base/common/arrays.js';
import { editorOptionsRegistry, ValidatedEditorOptions, ConfigurationChangedEvent, EDITOR_MODEL_DEFAULTS, ComputeOptionsMemory } from './editorOptions.js';
import { EditorZoom } from './editorZoom.js';
import { BareFontInfo } from './fontInfo.js';
import { Extensions } from '../../../platform/configuration/common/configurationRegistry.js';
import { Registry } from '../../../platform/registry/common/platform.js';
import { forEach } from '../../../base/common/collections.js';
export const TabFocus = new class {
    constructor() {
        this._tabFocus = false;
        this._onDidChangeTabFocus = new Emitter();
        this.onDidChangeTabFocus = this._onDidChangeTabFocus.event;
    }
    getTabFocusMode() {
        return this._tabFocus;
    }
    setTabFocusMode(tabFocusMode) {
        if (this._tabFocus === tabFocusMode) {
            return;
        }
        this._tabFocus = tabFocusMode;
        this._onDidChangeTabFocus.fire(this._tabFocus);
    }
};
const hasOwnProperty = Object.hasOwnProperty;
export class ComputedEditorOptions {
    constructor() {
        this._values = [];
    }
    _read(id) {
        return this._values[id];
    }
    get(id) {
        return this._values[id];
    }
    _write(id, value) {
        this._values[id] = value;
    }
}
class RawEditorOptions {
    constructor() {
        this._values = [];
    }
    _read(id) {
        return this._values[id];
    }
    _write(id, value) {
        this._values[id] = value;
    }
}
class EditorConfiguration2 {
    static readOptions(_options) {
        const options = _options;
        const result = new RawEditorOptions();
        for (const editorOption of editorOptionsRegistry) {
            const value = (editorOption.name === '_never_' ? undefined : options[editorOption.name]);
            result._write(editorOption.id, value);
        }
        return result;
    }
    static validateOptions(options) {
        const result = new ValidatedEditorOptions();
        for (const editorOption of editorOptionsRegistry) {
            result._write(editorOption.id, editorOption.validate(options._read(editorOption.id)));
        }
        return result;
    }
    static computeOptions(options, env) {
        const result = new ComputedEditorOptions();
        for (const editorOption of editorOptionsRegistry) {
            result._write(editorOption.id, editorOption.compute(env, result, options._read(editorOption.id)));
        }
        return result;
    }
    static _deepEquals(a, b) {
        if (typeof a !== 'object' || typeof b !== 'object') {
            return (a === b);
        }
        if (Array.isArray(a) || Array.isArray(b)) {
            return (Array.isArray(a) && Array.isArray(b) ? arrays.equals(a, b) : false);
        }
        for (let key in a) {
            if (!EditorConfiguration2._deepEquals(a[key], b[key])) {
                return false;
            }
        }
        return true;
    }
    static checkEquals(a, b) {
        const result = [];
        let somethingChanged = false;
        for (const editorOption of editorOptionsRegistry) {
            const changed = !EditorConfiguration2._deepEquals(a._read(editorOption.id), b._read(editorOption.id));
            result[editorOption.id] = changed;
            if (changed) {
                somethingChanged = true;
            }
        }
        return (somethingChanged ? new ConfigurationChangedEvent(result) : null);
    }
}
/**
 * Compatibility with old options
 */
function migrateOptions(options) {
    const wordWrap = options.wordWrap;
    if (wordWrap === true) {
        options.wordWrap = 'on';
    }
    else if (wordWrap === false) {
        options.wordWrap = 'off';
    }
    const lineNumbers = options.lineNumbers;
    if (lineNumbers === true) {
        options.lineNumbers = 'on';
    }
    else if (lineNumbers === false) {
        options.lineNumbers = 'off';
    }
    const autoClosingBrackets = options.autoClosingBrackets;
    if (autoClosingBrackets === false) {
        options.autoClosingBrackets = 'never';
        options.autoClosingQuotes = 'never';
        options.autoSurround = 'never';
    }
    const cursorBlinking = options.cursorBlinking;
    if (cursorBlinking === 'visible') {
        options.cursorBlinking = 'solid';
    }
    const renderWhitespace = options.renderWhitespace;
    if (renderWhitespace === true) {
        options.renderWhitespace = 'boundary';
    }
    else if (renderWhitespace === false) {
        options.renderWhitespace = 'none';
    }
    const renderLineHighlight = options.renderLineHighlight;
    if (renderLineHighlight === true) {
        options.renderLineHighlight = 'line';
    }
    else if (renderLineHighlight === false) {
        options.renderLineHighlight = 'none';
    }
    const acceptSuggestionOnEnter = options.acceptSuggestionOnEnter;
    if (acceptSuggestionOnEnter === true) {
        options.acceptSuggestionOnEnter = 'on';
    }
    else if (acceptSuggestionOnEnter === false) {
        options.acceptSuggestionOnEnter = 'off';
    }
    const tabCompletion = options.tabCompletion;
    if (tabCompletion === false) {
        options.tabCompletion = 'off';
    }
    else if (tabCompletion === true) {
        options.tabCompletion = 'onlySnippets';
    }
    const suggest = options.suggest;
    if (suggest && typeof suggest.filteredTypes === 'object' && suggest.filteredTypes) {
        const mapping = {};
        mapping['method'] = 'showMethods';
        mapping['function'] = 'showFunctions';
        mapping['constructor'] = 'showConstructors';
        mapping['field'] = 'showFields';
        mapping['variable'] = 'showVariables';
        mapping['class'] = 'showClasses';
        mapping['struct'] = 'showStructs';
        mapping['interface'] = 'showInterfaces';
        mapping['module'] = 'showModules';
        mapping['property'] = 'showProperties';
        mapping['event'] = 'showEvents';
        mapping['operator'] = 'showOperators';
        mapping['unit'] = 'showUnits';
        mapping['value'] = 'showValues';
        mapping['constant'] = 'showConstants';
        mapping['enum'] = 'showEnums';
        mapping['enumMember'] = 'showEnumMembers';
        mapping['keyword'] = 'showKeywords';
        mapping['text'] = 'showWords';
        mapping['color'] = 'showColors';
        mapping['file'] = 'showFiles';
        mapping['reference'] = 'showReferences';
        mapping['folder'] = 'showFolders';
        mapping['typeParameter'] = 'showTypeParameters';
        mapping['snippet'] = 'showSnippets';
        forEach(mapping, entry => {
            const value = suggest.filteredTypes[entry.key];
            if (value === false) {
                suggest[entry.value] = value;
            }
        });
        // delete (<any>suggest).filteredTypes;
    }
    const hover = options.hover;
    if (hover === true) {
        options.hover = {
            enabled: true
        };
    }
    else if (hover === false) {
        options.hover = {
            enabled: false
        };
    }
    const parameterHints = options.parameterHints;
    if (parameterHints === true) {
        options.parameterHints = {
            enabled: true
        };
    }
    else if (parameterHints === false) {
        options.parameterHints = {
            enabled: false
        };
    }
    const autoIndent = options.autoIndent;
    if (autoIndent === true) {
        options.autoIndent = 'full';
    }
    else if (autoIndent === false) {
        options.autoIndent = 'advanced';
    }
    const matchBrackets = options.matchBrackets;
    if (matchBrackets === true) {
        options.matchBrackets = 'always';
    }
    else if (matchBrackets === false) {
        options.matchBrackets = 'never';
    }
}
function deepCloneAndMigrateOptions(_options) {
    const options = objects.deepClone(_options);
    migrateOptions(options);
    return options;
}
export class CommonEditorConfiguration extends Disposable {
    constructor(isSimpleWidget, _options) {
        super();
        this._onDidChange = this._register(new Emitter());
        this.onDidChange = this._onDidChange.event;
        this._onDidChangeFast = this._register(new Emitter());
        this.onDidChangeFast = this._onDidChangeFast.event;
        this.isSimpleWidget = isSimpleWidget;
        this._isDominatedByLongLines = false;
        this._computeOptionsMemory = new ComputeOptionsMemory();
        this._viewLineCount = 1;
        this._lineNumbersDigitCount = 1;
        this._rawOptions = deepCloneAndMigrateOptions(_options);
        this._readOptions = EditorConfiguration2.readOptions(this._rawOptions);
        this._validatedOptions = EditorConfiguration2.validateOptions(this._readOptions);
        this._register(EditorZoom.onDidChangeZoomLevel(_ => this._recomputeOptions()));
        this._register(TabFocus.onDidChangeTabFocus(_ => this._recomputeOptions()));
    }
    observeReferenceElement(dimension) {
    }
    updatePixelRatio() {
    }
    _recomputeOptions() {
        const oldOptions = this.options;
        const newOptions = this._computeInternalOptions();
        if (!oldOptions) {
            this.options = newOptions;
        }
        else {
            const changeEvent = EditorConfiguration2.checkEquals(oldOptions, newOptions);
            if (changeEvent === null) {
                // nothing changed!
                return;
            }
            this.options = newOptions;
            this._onDidChangeFast.fire(changeEvent);
            this._onDidChange.fire(changeEvent);
        }
    }
    getRawOptions() {
        return this._rawOptions;
    }
    _computeInternalOptions() {
        const partialEnv = this._getEnvConfiguration();
        const bareFontInfo = BareFontInfo.createFromValidatedSettings(this._validatedOptions, partialEnv.zoomLevel, partialEnv.pixelRatio, this.isSimpleWidget);
        const env = {
            memory: this._computeOptionsMemory,
            outerWidth: partialEnv.outerWidth,
            outerHeight: partialEnv.outerHeight,
            fontInfo: this.readConfiguration(bareFontInfo),
            extraEditorClassName: partialEnv.extraEditorClassName,
            isDominatedByLongLines: this._isDominatedByLongLines,
            viewLineCount: this._viewLineCount,
            lineNumbersDigitCount: this._lineNumbersDigitCount,
            emptySelectionClipboard: partialEnv.emptySelectionClipboard,
            pixelRatio: partialEnv.pixelRatio,
            tabFocusMode: TabFocus.getTabFocusMode(),
            accessibilitySupport: partialEnv.accessibilitySupport
        };
        return EditorConfiguration2.computeOptions(this._validatedOptions, env);
    }
    static _subsetEquals(base, subset) {
        for (const key in subset) {
            if (hasOwnProperty.call(subset, key)) {
                const subsetValue = subset[key];
                const baseValue = base[key];
                if (baseValue === subsetValue) {
                    continue;
                }
                if (Array.isArray(baseValue) && Array.isArray(subsetValue)) {
                    if (!arrays.equals(baseValue, subsetValue)) {
                        return false;
                    }
                    continue;
                }
                if (baseValue && typeof baseValue === 'object' && subsetValue && typeof subsetValue === 'object') {
                    if (!this._subsetEquals(baseValue, subsetValue)) {
                        return false;
                    }
                    continue;
                }
                return false;
            }
        }
        return true;
    }
    updateOptions(_newOptions) {
        if (typeof _newOptions === 'undefined') {
            return;
        }
        const newOptions = deepCloneAndMigrateOptions(_newOptions);
        if (CommonEditorConfiguration._subsetEquals(this._rawOptions, newOptions)) {
            return;
        }
        this._rawOptions = objects.mixin(this._rawOptions, newOptions || {});
        this._readOptions = EditorConfiguration2.readOptions(this._rawOptions);
        this._validatedOptions = EditorConfiguration2.validateOptions(this._readOptions);
        this._recomputeOptions();
    }
    setIsDominatedByLongLines(isDominatedByLongLines) {
        this._isDominatedByLongLines = isDominatedByLongLines;
        this._recomputeOptions();
    }
    setMaxLineNumber(maxLineNumber) {
        const lineNumbersDigitCount = CommonEditorConfiguration._digitCount(maxLineNumber);
        if (this._lineNumbersDigitCount === lineNumbersDigitCount) {
            return;
        }
        this._lineNumbersDigitCount = lineNumbersDigitCount;
        this._recomputeOptions();
    }
    setViewLineCount(viewLineCount) {
        if (this._viewLineCount === viewLineCount) {
            return;
        }
        this._viewLineCount = viewLineCount;
        this._recomputeOptions();
    }
    static _digitCount(n) {
        let r = 0;
        while (n) {
            n = Math.floor(n / 10);
            r++;
        }
        return r ? r : 1;
    }
}
export const editorConfigurationBaseNode = Object.freeze({
    id: 'editor',
    order: 5,
    type: 'object',
    title: nls.localize('editorConfigurationTitle', "Editor"),
    scope: 5 /* LANGUAGE_OVERRIDABLE */,
});
const configurationRegistry = Registry.as(Extensions.Configuration);
const editorConfiguration = Object.assign(Object.assign({}, editorConfigurationBaseNode), { properties: {
        'editor.tabSize': {
            type: 'number',
            default: EDITOR_MODEL_DEFAULTS.tabSize,
            minimum: 1,
            markdownDescription: nls.localize('tabSize', "The number of spaces a tab is equal to. This setting is overridden based on the file contents when `#editor.detectIndentation#` is on.")
        },
        // 'editor.indentSize': {
        // 	'anyOf': [
        // 		{
        // 			type: 'string',
        // 			enum: ['tabSize']
        // 		},
        // 		{
        // 			type: 'number',
        // 			minimum: 1
        // 		}
        // 	],
        // 	default: 'tabSize',
        // 	markdownDescription: nls.localize('indentSize', "The number of spaces used for indentation or 'tabSize' to use the value from `#editor.tabSize#`. This setting is overridden based on the file contents when `#editor.detectIndentation#` is on.")
        // },
        'editor.insertSpaces': {
            type: 'boolean',
            default: EDITOR_MODEL_DEFAULTS.insertSpaces,
            markdownDescription: nls.localize('insertSpaces', "Insert spaces when pressing `Tab`. This setting is overridden based on the file contents when `#editor.detectIndentation#` is on.")
        },
        'editor.detectIndentation': {
            type: 'boolean',
            default: EDITOR_MODEL_DEFAULTS.detectIndentation,
            markdownDescription: nls.localize('detectIndentation', "Controls whether `#editor.tabSize#` and `#editor.insertSpaces#` will be automatically detected when a file is opened based on the file contents.")
        },
        'editor.trimAutoWhitespace': {
            type: 'boolean',
            default: EDITOR_MODEL_DEFAULTS.trimAutoWhitespace,
            description: nls.localize('trimAutoWhitespace', "Remove trailing auto inserted whitespace.")
        },
        'editor.largeFileOptimizations': {
            type: 'boolean',
            default: EDITOR_MODEL_DEFAULTS.largeFileOptimizations,
            description: nls.localize('largeFileOptimizations', "Special handling for large files to disable certain memory intensive features.")
        },
        'editor.wordBasedSuggestions': {
            type: 'boolean',
            default: true,
            description: nls.localize('wordBasedSuggestions', "Controls whether completions should be computed based on words in the document.")
        },
        'editor.wordBasedSuggestionsMode': {
            enum: ['currentDocument', 'matchingDocuments', 'allDocuments'],
            default: 'matchingDocuments',
            enumDescriptions: [
                nls.localize('wordBasedSuggestionsMode.currentDocument', 'Only suggest words from the active document.'),
                nls.localize('wordBasedSuggestionsMode.matchingDocuments', 'Suggest words from all open documents of the same language.'),
                nls.localize('wordBasedSuggestionsMode.allDocuments', 'Suggest words from all open documents.')
            ],
            description: nls.localize('wordBasedSuggestionsMode', "Controls form what documents word based completions are computed.")
        },
        'editor.semanticHighlighting.enabled': {
            enum: [true, false, 'configuredByTheme'],
            enumDescriptions: [
                nls.localize('semanticHighlighting.true', 'Semantic highlighting enabled for all color themes.'),
                nls.localize('semanticHighlighting.false', 'Semantic highlighting disabled for all color themes.'),
                nls.localize('semanticHighlighting.configuredByTheme', 'Semantic highlighting is configured by the current color theme\'s `semanticHighlighting` setting.')
            ],
            default: 'configuredByTheme',
            description: nls.localize('semanticHighlighting.enabled', "Controls whether the semanticHighlighting is shown for the languages that support it.")
        },
        'editor.stablePeek': {
            type: 'boolean',
            default: false,
            markdownDescription: nls.localize('stablePeek', "Keep peek editors open even when double clicking their content or when hitting `Escape`.")
        },
        'editor.maxTokenizationLineLength': {
            type: 'integer',
            default: 20000,
            description: nls.localize('maxTokenizationLineLength', "Lines above this length will not be tokenized for performance reasons")
        },
        'diffEditor.maxComputationTime': {
            type: 'number',
            default: 5000,
            description: nls.localize('maxComputationTime', "Timeout in milliseconds after which diff computation is cancelled. Use 0 for no timeout.")
        },
        'diffEditor.renderSideBySide': {
            type: 'boolean',
            default: true,
            description: nls.localize('sideBySide', "Controls whether the diff editor shows the diff side by side or inline.")
        },
        'diffEditor.ignoreTrimWhitespace': {
            type: 'boolean',
            default: true,
            description: nls.localize('ignoreTrimWhitespace', "When enabled, the diff editor ignores changes in leading or trailing whitespace.")
        },
        'diffEditor.renderIndicators': {
            type: 'boolean',
            default: true,
            description: nls.localize('renderIndicators', "Controls whether the diff editor shows +/- indicators for added/removed changes.")
        },
        'diffEditor.codeLens': {
            type: 'boolean',
            default: false,
            description: nls.localize('codeLens', "Controls whether the editor shows CodeLens.")
        },
        'diffEditor.wordWrap': {
            type: 'string',
            enum: ['off', 'on', 'inherit'],
            default: 'inherit',
            markdownEnumDescriptions: [
                nls.localize('wordWrap.off', "Lines will never wrap."),
                nls.localize('wordWrap.on', "Lines will wrap at the viewport width."),
                nls.localize('wordWrap.inherit', "Lines will wrap according to the `#editor.wordWrap#` setting."),
            ]
        }
    } });
function isConfigurationPropertySchema(x) {
    return (typeof x.type !== 'undefined' || typeof x.anyOf !== 'undefined');
}
// Add properties from the Editor Option Registry
for (const editorOption of editorOptionsRegistry) {
    const schema = editorOption.schema;
    if (typeof schema !== 'undefined') {
        if (isConfigurationPropertySchema(schema)) {
            // This is a single schema contribution
            editorConfiguration.properties[`editor.${editorOption.name}`] = schema;
        }
        else {
            for (let key in schema) {
                if (hasOwnProperty.call(schema, key)) {
                    editorConfiguration.properties[key] = schema[key];
                }
            }
        }
    }
}
let cachedEditorConfigurationKeys = null;
function getEditorConfigurationKeys() {
    if (cachedEditorConfigurationKeys === null) {
        cachedEditorConfigurationKeys = Object.create(null);
        Object.keys(editorConfiguration.properties).forEach((prop) => {
            cachedEditorConfigurationKeys[prop] = true;
        });
    }
    return cachedEditorConfigurationKeys;
}
export function isEditorConfigurationKey(key) {
    const editorConfigurationKeys = getEditorConfigurationKeys();
    return (editorConfigurationKeys[`editor.${key}`] || false);
}
export function isDiffEditorConfigurationKey(key) {
    const editorConfigurationKeys = getEditorConfigurationKeys();
    return (editorConfigurationKeys[`diffEditor.${key}`] || false);
}
configurationRegistry.registerConfiguration(editorConfiguration);
