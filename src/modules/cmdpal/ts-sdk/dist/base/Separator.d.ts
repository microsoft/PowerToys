import type { IListItem, ICommand, ContextItem, IconInfo, Tag, Details } from '../types';
/**
 * A separator item that can be used in list pages as a visual divider.
 * When placed in a list, it appears as a section header or divider line.
 */
export declare class Separator implements IListItem {
    command: ICommand;
    title: string;
    subtitle?: string;
    icon?: IconInfo | null;
    moreCommands?: ContextItem[];
    tags?: Tag[];
    details?: Details;
    section?: string;
    textToSuggest?: string;
    /** Marker for serialization to identify this as a separator */
    readonly _isSeparator = true;
    constructor(title?: string);
}
//# sourceMappingURL=Separator.d.ts.map