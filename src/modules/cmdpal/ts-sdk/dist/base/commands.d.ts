import type { IInvokableCommand, CommandResult, IconInfo } from '../types';
/**
 * A command that does nothing when invoked — returns KeepOpen.
 */
export declare class NoOpCommand implements IInvokableCommand {
    id: string;
    name: string;
    icon?: IconInfo | null;
    constructor(id?: string, name?: string);
    invoke(): CommandResult;
}
/**
 * A command that opens a URL when invoked.
 */
export declare class OpenUrlCommand implements IInvokableCommand {
    id: string;
    name: string;
    icon?: IconInfo | null;
    private readonly url;
    constructor(url: string, name?: string);
    invoke(): CommandResult;
    getUrl(): string;
}
/**
 * A command that copies text to clipboard and shows a toast notification.
 */
export declare class CopyTextCommand implements IInvokableCommand {
    id: string;
    name: string;
    icon?: IconInfo | null;
    private readonly text;
    private readonly toastMessage;
    constructor(text: string, name?: string, toastMessage?: string);
    invoke(): CommandResult;
    getText(): string;
}
/**
 * A command that wraps another command with a confirmation dialog.
 */
export declare class ConfirmableCommand implements IInvokableCommand {
    id: string;
    name: string;
    icon?: IconInfo | null;
    private readonly title;
    private readonly description;
    private readonly primaryCommand;
    private readonly isCritical;
    constructor(options: {
        id: string;
        name: string;
        title: string;
        description: string;
        primaryCommand: IInvokableCommand;
        isCritical?: boolean;
        icon?: IconInfo | null;
    });
    invoke(): CommandResult;
}
//# sourceMappingURL=commands.d.ts.map