import type { IListItem, ICommand, ContextItem, IconInfo, Tag, Details } from '../types';
/**
 * Base class for list items displayed in list pages.
 *
 * @example
 * ```typescript
 * import { ListItemBase } from '@microsoft/cmdpal-sdk';
 *
 * const item = new ListItemBase({
 *   command: { id: 'open-file', name: 'Open File' },
 *   title: 'document.txt',
 *   subtitle: 'Modified 2 hours ago',
 *   tags: [{ text: 'Recent' }],
 *   section: 'Documents',
 * });
 * ```
 */
export declare class ListItemBase implements IListItem {
    command: ICommand;
    title: string;
    subtitle?: string;
    icon?: IconInfo | null;
    moreCommands?: ContextItem[];
    tags?: Tag[];
    details?: Details;
    section?: string;
    textToSuggest?: string;
    constructor(options: {
        command: ICommand;
        title: string;
        subtitle?: string;
        icon?: IconInfo | null;
        moreCommands?: ContextItem[];
        tags?: Tag[];
        details?: Details;
        section?: string;
        textToSuggest?: string;
    });
}
//# sourceMappingURL=ListItemBase.d.ts.map