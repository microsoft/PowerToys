import { ICommand, IInvokableCommand, ICommandItem, IListItem, IIconInfo, IContextItem, ITag, IDetails, ICommandResult } from '../generated/types';
import { JsonRpcTransport } from '../transport/json-rpc';
/**
 * Base class for commands.
 */
export declare class Command implements ICommand {
    id: string;
    name: string;
    icon?: IIconInfo;
    protected _transport?: JsonRpcTransport;
    PropChanged?: (args: unknown) => void;
    _initializeWithTransport(transport: JsonRpcTransport): void;
    protected notifyPropChanged(propertyName: string): void;
    toJSON(): Record<string, unknown>;
}
/**
 * Base class for invokable commands that execute an action.
 */
export declare abstract class InvokableCommand extends Command implements IInvokableCommand {
    abstract invoke(sender?: unknown): ICommandResult;
}
/**
 * A command that does nothing when invoked. Useful for gallery/grid items
 * that only need to display content without an action.
 */
export declare class NoOpCommand extends InvokableCommand {
    private static _counter;
    constructor(id?: string, name?: string);
    invoke(): ICommandResult;
}
/**
 * Represents a command item shown in the UI.
 */
export declare class CommandItem implements ICommandItem {
    title: string;
    subtitle: string;
    icon?: IIconInfo;
    command?: ICommand;
    moreCommands: IContextItem[];
    protected _transport?: JsonRpcTransport;
    PropChanged?: (args: unknown) => void;
    constructor(options?: Partial<CommandItem>);
    _initializeWithTransport(transport: JsonRpcTransport): void;
    protected notifyPropChanged(propertyName: string): void;
}
/**
 * Represents a list item with additional metadata.
 */
export declare class ListItem extends CommandItem implements IListItem {
    tags: ITag[];
    details?: IDetails;
    section: string;
    textToSuggest: string;
    constructor(options?: Partial<ListItem>);
}
//# sourceMappingURL=command.d.ts.map