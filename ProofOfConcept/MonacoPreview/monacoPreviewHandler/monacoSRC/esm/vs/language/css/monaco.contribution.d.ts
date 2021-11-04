import { IEvent } from './fillers/monaco-editor-core';
export interface DiagnosticsOptions {
    readonly validate?: boolean;
    readonly lint?: {
        readonly compatibleVendorPrefixes?: 'ignore' | 'warning' | 'error';
        readonly vendorPrefix?: 'ignore' | 'warning' | 'error';
        readonly duplicateProperties?: 'ignore' | 'warning' | 'error';
        readonly emptyRules?: 'ignore' | 'warning' | 'error';
        readonly importStatement?: 'ignore' | 'warning' | 'error';
        readonly boxModel?: 'ignore' | 'warning' | 'error';
        readonly universalSelector?: 'ignore' | 'warning' | 'error';
        readonly zeroUnits?: 'ignore' | 'warning' | 'error';
        readonly fontFaceProperties?: 'ignore' | 'warning' | 'error';
        readonly hexColorLength?: 'ignore' | 'warning' | 'error';
        readonly argumentsInColorFunction?: 'ignore' | 'warning' | 'error';
        readonly unknownProperties?: 'ignore' | 'warning' | 'error';
        readonly ieHack?: 'ignore' | 'warning' | 'error';
        readonly unknownVendorSpecificProperties?: 'ignore' | 'warning' | 'error';
        readonly propertyIgnoredDueToDisplay?: 'ignore' | 'warning' | 'error';
        readonly important?: 'ignore' | 'warning' | 'error';
        readonly float?: 'ignore' | 'warning' | 'error';
        readonly idSelector?: 'ignore' | 'warning' | 'error';
    };
}
export interface ModeConfiguration {
    /**
     * Defines whether the built-in completionItemProvider is enabled.
     */
    readonly completionItems?: boolean;
    /**
     * Defines whether the built-in hoverProvider is enabled.
     */
    readonly hovers?: boolean;
    /**
     * Defines whether the built-in documentSymbolProvider is enabled.
     */
    readonly documentSymbols?: boolean;
    /**
     * Defines whether the built-in definitions provider is enabled.
     */
    readonly definitions?: boolean;
    /**
     * Defines whether the built-in references provider is enabled.
     */
    readonly references?: boolean;
    /**
     * Defines whether the built-in references provider is enabled.
     */
    readonly documentHighlights?: boolean;
    /**
     * Defines whether the built-in rename provider is enabled.
     */
    readonly rename?: boolean;
    /**
     * Defines whether the built-in color provider is enabled.
     */
    readonly colors?: boolean;
    /**
     * Defines whether the built-in foldingRange provider is enabled.
     */
    readonly foldingRanges?: boolean;
    /**
     * Defines whether the built-in diagnostic provider is enabled.
     */
    readonly diagnostics?: boolean;
    /**
     * Defines whether the built-in selection range provider is enabled.
     */
    readonly selectionRanges?: boolean;
}
export interface LanguageServiceDefaults {
    readonly languageId: string;
    readonly onDidChange: IEvent<LanguageServiceDefaults>;
    readonly diagnosticsOptions: DiagnosticsOptions;
    readonly modeConfiguration: ModeConfiguration;
    setDiagnosticsOptions(options: DiagnosticsOptions): void;
    setModeConfiguration(modeConfiguration: ModeConfiguration): void;
}
export declare const cssDefaults: LanguageServiceDefaults;
export declare const scssDefaults: LanguageServiceDefaults;
export declare const lessDefaults: LanguageServiceDefaults;
