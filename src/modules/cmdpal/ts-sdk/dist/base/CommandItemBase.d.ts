import type { ICommandItem, ICommand, ContextItem, IconInfo } from '../types';
/**
 * Base class for command items displayed in lists and menus.
 */
export declare class CommandItemBase implements ICommandItem {
    command: ICommand;
    title: string;
    subtitle?: string;
    icon?: IconInfo | null;
    moreCommands?: ContextItem[];
    constructor(options: {
        command: ICommand;
        title: string;
        subtitle?: string;
        icon?: IconInfo | null;
        moreCommands?: ContextItem[];
    });
}
//# sourceMappingURL=CommandItemBase.d.ts.map