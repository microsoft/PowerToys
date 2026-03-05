import { IMarkdownContent, IFormContent, ITreeContent, IContent, ICommandResult, ContentType } from '../generated/types';
import { JsonRpcTransport } from '../transport/json-rpc';
/**
 * Markdown content for displaying rich text.
 */
export declare class MarkdownContent implements IMarkdownContent {
    type: ContentType;
    id: string;
    body: string;
    protected _transport?: JsonRpcTransport;
    PropChanged?: (args: unknown) => void;
    constructor(body?: string);
    _initializeWithTransport(transport: JsonRpcTransport): void;
    protected notifyPropChanged(propertyName: string): void;
}
/**
 * Form content for user input.
 */
export declare abstract class FormContent implements IFormContent {
    type: ContentType;
    id: string;
    protected _transport?: JsonRpcTransport;
    PropChanged?: (args: unknown) => void;
    abstract get templateJson(): string;
    abstract get dataJson(): string;
    abstract get stateJson(): string;
    abstract submitForm(inputs: string, data: string): ICommandResult;
    /** Ensure getter-based properties are included in JSON serialization. */
    toJSON(): {
        type: ContentType;
        id: string;
        templateJson: string;
        dataJson: string;
        stateJson: string;
    };
    _initializeWithTransport(transport: JsonRpcTransport): void;
    protected notifyPropChanged(propertyName: string): void;
}
/**
 * Tree content for hierarchical data.
 */
export declare abstract class TreeContent implements ITreeContent {
    type: ContentType;
    id: string;
    protected _transport?: JsonRpcTransport;
    PropChanged?: (args: unknown) => void;
    ItemsChanged?: (args: unknown) => void;
    /** Get the child content nodes. */
    abstract getChildren(): IContent[];
    /** Ensure children are included in JSON serialization. */
    toJSON(): {
        type: ContentType;
        id: string;
        children: IContent[];
    };
    _initializeWithTransport(transport: JsonRpcTransport): void;
    protected notifyPropChanged(propertyName: string): void;
    protected notifyItemsChanged(totalItems?: number): void;
}
//# sourceMappingURL=content.d.ts.map