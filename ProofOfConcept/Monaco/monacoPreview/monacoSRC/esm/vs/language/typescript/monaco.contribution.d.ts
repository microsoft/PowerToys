import { IEvent, IDisposable, Uri } from './fillers/monaco-editor-core';
export declare enum ModuleKind {
    None = 0,
    CommonJS = 1,
    AMD = 2,
    UMD = 3,
    System = 4,
    ES2015 = 5,
    ESNext = 99
}
export declare enum JsxEmit {
    None = 0,
    Preserve = 1,
    React = 2,
    ReactNative = 3,
    ReactJSX = 4,
    ReactJSXDev = 5
}
export declare enum NewLineKind {
    CarriageReturnLineFeed = 0,
    LineFeed = 1
}
export declare enum ScriptTarget {
    ES3 = 0,
    ES5 = 1,
    ES2015 = 2,
    ES2016 = 3,
    ES2017 = 4,
    ES2018 = 5,
    ES2019 = 6,
    ES2020 = 7,
    ESNext = 99,
    JSON = 100,
    Latest = 99
}
export declare enum ModuleResolutionKind {
    Classic = 1,
    NodeJs = 2
}
interface MapLike<T> {
    [index: string]: T;
}
declare type CompilerOptionsValue = string | number | boolean | (string | number)[] | string[] | MapLike<string[]> | null | undefined;
interface CompilerOptions {
    allowJs?: boolean;
    allowSyntheticDefaultImports?: boolean;
    allowUmdGlobalAccess?: boolean;
    allowUnreachableCode?: boolean;
    allowUnusedLabels?: boolean;
    alwaysStrict?: boolean;
    baseUrl?: string;
    charset?: string;
    checkJs?: boolean;
    declaration?: boolean;
    declarationMap?: boolean;
    emitDeclarationOnly?: boolean;
    declarationDir?: string;
    disableSizeLimit?: boolean;
    disableSourceOfProjectReferenceRedirect?: boolean;
    downlevelIteration?: boolean;
    emitBOM?: boolean;
    emitDecoratorMetadata?: boolean;
    experimentalDecorators?: boolean;
    forceConsistentCasingInFileNames?: boolean;
    importHelpers?: boolean;
    inlineSourceMap?: boolean;
    inlineSources?: boolean;
    isolatedModules?: boolean;
    jsx?: JsxEmit;
    keyofStringsOnly?: boolean;
    lib?: string[];
    locale?: string;
    mapRoot?: string;
    maxNodeModuleJsDepth?: number;
    module?: ModuleKind;
    moduleResolution?: ModuleResolutionKind;
    newLine?: NewLineKind;
    noEmit?: boolean;
    noEmitHelpers?: boolean;
    noEmitOnError?: boolean;
    noErrorTruncation?: boolean;
    noFallthroughCasesInSwitch?: boolean;
    noImplicitAny?: boolean;
    noImplicitReturns?: boolean;
    noImplicitThis?: boolean;
    noStrictGenericChecks?: boolean;
    noUnusedLocals?: boolean;
    noUnusedParameters?: boolean;
    noImplicitUseStrict?: boolean;
    noLib?: boolean;
    noResolve?: boolean;
    out?: string;
    outDir?: string;
    outFile?: string;
    paths?: MapLike<string[]>;
    preserveConstEnums?: boolean;
    preserveSymlinks?: boolean;
    project?: string;
    reactNamespace?: string;
    jsxFactory?: string;
    composite?: boolean;
    removeComments?: boolean;
    rootDir?: string;
    rootDirs?: string[];
    skipLibCheck?: boolean;
    skipDefaultLibCheck?: boolean;
    sourceMap?: boolean;
    sourceRoot?: string;
    strict?: boolean;
    strictFunctionTypes?: boolean;
    strictBindCallApply?: boolean;
    strictNullChecks?: boolean;
    strictPropertyInitialization?: boolean;
    stripInternal?: boolean;
    suppressExcessPropertyErrors?: boolean;
    suppressImplicitAnyIndexErrors?: boolean;
    target?: ScriptTarget;
    traceResolution?: boolean;
    resolveJsonModule?: boolean;
    types?: string[];
    /** Paths used to compute primary types search locations */
    typeRoots?: string[];
    esModuleInterop?: boolean;
    useDefineForClassFields?: boolean;
    [option: string]: CompilerOptionsValue | undefined;
}
export interface DiagnosticsOptions {
    noSemanticValidation?: boolean;
    noSyntaxValidation?: boolean;
    noSuggestionDiagnostics?: boolean;
    diagnosticCodesToIgnore?: number[];
}
export interface WorkerOptions {
    /** A full HTTP path to a JavaScript file which adds a function `customTSWorkerFactory` to the self inside a web-worker */
    customWorkerPath?: string;
}
interface IExtraLib {
    content: string;
    version: number;
}
export interface IExtraLibs {
    [path: string]: IExtraLib;
}
/**
 * A linked list of formatted diagnostic messages to be used as part of a multiline message.
 * It is built from the bottom up, leaving the head to be the "main" diagnostic.
 */
interface DiagnosticMessageChain {
    messageText: string;
    /** Diagnostic category: warning = 0, error = 1, suggestion = 2, message = 3 */
    category: 0 | 1 | 2 | 3;
    code: number;
    next?: DiagnosticMessageChain[];
}
export interface Diagnostic extends DiagnosticRelatedInformation {
    /** May store more in future. For now, this will simply be `true` to indicate when a diagnostic is an unused-identifier diagnostic. */
    reportsUnnecessary?: {};
    source?: string;
    relatedInformation?: DiagnosticRelatedInformation[];
}
interface DiagnosticRelatedInformation {
    /** Diagnostic category: warning = 0, error = 1, suggestion = 2, message = 3 */
    category: 0 | 1 | 2 | 3;
    code: number;
    /** TypeScriptWorker removes this to avoid serializing circular JSON structures. */
    file: undefined;
    start: number | undefined;
    length: number | undefined;
    messageText: string | DiagnosticMessageChain;
}
interface EmitOutput {
    outputFiles: OutputFile[];
    emitSkipped: boolean;
}
interface OutputFile {
    name: string;
    writeByteOrderMark: boolean;
    text: string;
}
export interface LanguageServiceDefaults {
    /**
     * Event fired when compiler options or diagnostics options are changed.
     */
    readonly onDidChange: IEvent<void>;
    /**
     * Event fired when extra libraries registered with the language service change.
     */
    readonly onDidExtraLibsChange: IEvent<void>;
    readonly workerOptions: WorkerOptions;
    /**
     * Get the current extra libs registered with the language service.
     */
    getExtraLibs(): IExtraLibs;
    /**
     * Add an additional source file to the language service. Use this
     * for typescript (definition) files that won't be loaded as editor
     * documents, like `jquery.d.ts`.
     *
     * @param content The file content
     * @param filePath An optional file path
     * @returns A disposable which will remove the file from the
     * language service upon disposal.
     */
    addExtraLib(content: string, filePath?: string): IDisposable;
    /**
     * Remove all existing extra libs and set the additional source
     * files to the language service. Use this for typescript definition
     * files that won't be loaded as editor documents, like `jquery.d.ts`.
     * @param libs An array of entries to register.
     */
    setExtraLibs(libs: {
        content: string;
        filePath?: string;
    }[]): void;
    /**
     * Get current TypeScript compiler options for the language service.
     */
    getCompilerOptions(): CompilerOptions;
    /**
     * Set TypeScript compiler options.
     */
    setCompilerOptions(options: CompilerOptions): void;
    /**
     * Get the current diagnostics options for the language service.
     */
    getDiagnosticsOptions(): DiagnosticsOptions;
    /**
     * Configure whether syntactic and/or semantic validation should
     * be performed
     */
    setDiagnosticsOptions(options: DiagnosticsOptions): void;
    /**
     * Configure webworker options
     */
    setWorkerOptions(options: WorkerOptions): void;
    /**
     * No-op.
     */
    setMaximumWorkerIdleTime(value: number): void;
    /**
     * Configure if all existing models should be eagerly sync'd
     * to the worker on start or restart.
     */
    setEagerModelSync(value: boolean): void;
    /**
     * Get the current setting for whether all existing models should be eagerly sync'd
     * to the worker on start or restart.
     */
    getEagerModelSync(): boolean;
}
export interface TypeScriptWorker {
    /**
     * Get diagnostic messages for any syntax issues in the given file.
     */
    getSyntacticDiagnostics(fileName: string): Promise<Diagnostic[]>;
    /**
     * Get diagnostic messages for any semantic issues in the given file.
     */
    getSemanticDiagnostics(fileName: string): Promise<Diagnostic[]>;
    /**
     * Get diagnostic messages for any suggestions related to the given file.
     */
    getSuggestionDiagnostics(fileName: string): Promise<Diagnostic[]>;
    /**
     * Get the content of a given file.
     */
    getScriptText(fileName: string): Promise<string | undefined>;
    /**
     * Get diagnostic messages related to the current compiler options.
     * @param fileName Not used
     */
    getCompilerOptionsDiagnostics(fileName: string): Promise<Diagnostic[]>;
    /**
     * Get code completions for the given file and position.
     * @returns `Promise<typescript.CompletionInfo | undefined>`
     */
    getCompletionsAtPosition(fileName: string, position: number): Promise<any | undefined>;
    /**
     * Get code completion details for the given file, position, and entry.
     * @returns `Promise<typescript.CompletionEntryDetails | undefined>`
     */
    getCompletionEntryDetails(fileName: string, position: number, entry: string): Promise<any | undefined>;
    /**
     * Get signature help items for the item at the given file and position.
     * @returns `Promise<typescript.SignatureHelpItems | undefined>`
     */
    getSignatureHelpItems(fileName: string, position: number, options: any): Promise<any | undefined>;
    /**
     * Get quick info for the item at the given position in the file.
     * @returns `Promise<typescript.QuickInfo | undefined>`
     */
    getQuickInfoAtPosition(fileName: string, position: number): Promise<any | undefined>;
    /**
     * Get other ranges which are related to the item at the given position in the file (often used for highlighting).
     * @returns `Promise<ReadonlyArray<typescript.ReferenceEntry> | undefined>`
     */
    getOccurrencesAtPosition(fileName: string, position: number): Promise<ReadonlyArray<any> | undefined>;
    /**
     * Get the definition of the item at the given position in the file.
     * @returns `Promise<ReadonlyArray<typescript.DefinitionInfo> | undefined>`
     */
    getDefinitionAtPosition(fileName: string, position: number): Promise<ReadonlyArray<any> | undefined>;
    /**
     * Get references to the item at the given position in the file.
     * @returns `Promise<typescript.ReferenceEntry[] | undefined>`
     */
    getReferencesAtPosition(fileName: string, position: number): Promise<any[] | undefined>;
    /**
     * Get outline entries for the item at the given position in the file.
     * @returns `Promise<typescript.NavigationBarItem[]>`
     */
    getNavigationBarItems(fileName: string): Promise<any[]>;
    /**
     * Get changes which should be applied to format the given file.
     * @param options `typescript.FormatCodeOptions`
     * @returns `Promise<typescript.TextChange[]>`
     */
    getFormattingEditsForDocument(fileName: string, options: any): Promise<any[]>;
    /**
     * Get changes which should be applied to format the given range in the file.
     * @param options `typescript.FormatCodeOptions`
     * @returns `Promise<typescript.TextChange[]>`
     */
    getFormattingEditsForRange(fileName: string, start: number, end: number, options: any): Promise<any[]>;
    /**
     * Get formatting changes which should be applied after the given keystroke.
     * @param options `typescript.FormatCodeOptions`
     * @returns `Promise<typescript.TextChange[]>`
     */
    getFormattingEditsAfterKeystroke(fileName: string, postion: number, ch: string, options: any): Promise<any[]>;
    /**
     * Get other occurrences which should be updated when renaming the item at the given file and position.
     * @returns `Promise<readonly typescript.RenameLocation[] | undefined>`
     */
    findRenameLocations(fileName: string, positon: number, findInStrings: boolean, findInComments: boolean, providePrefixAndSuffixTextForRename: boolean): Promise<readonly any[] | undefined>;
    /**
     * Get edits which should be applied to rename the item at the given file and position (or a failure reason).
     * @param options `typescript.RenameInfoOptions`
     * @returns `Promise<typescript.RenameInfo>`
     */
    getRenameInfo(fileName: string, positon: number, options: any): Promise<any>;
    /**
     * Get transpiled output for the given file.
     * @returns `typescript.EmitOutput`
     */
    getEmitOutput(fileName: string): Promise<EmitOutput>;
    /**
     * Get possible code fixes at the given position in the file.
     * @param formatOptions `typescript.FormatCodeOptions`
     * @returns `Promise<ReadonlyArray<typescript.CodeFixAction>>`
     */
    getCodeFixesAtPosition(fileName: string, start: number, end: number, errorCodes: number[], formatOptions: any): Promise<ReadonlyArray<any>>;
}
export declare const typescriptVersion: string;
export declare const typescriptDefaults: LanguageServiceDefaults;
export declare const javascriptDefaults: LanguageServiceDefaults;
export declare const getTypeScriptWorker: () => Promise<(...uris: Uri[]) => Promise<TypeScriptWorker>>;
export declare const getJavaScriptWorker: () => Promise<(...uris: Uri[]) => Promise<TypeScriptWorker>>;
export {};
