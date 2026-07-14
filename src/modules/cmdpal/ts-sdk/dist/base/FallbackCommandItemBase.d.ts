import type { IFallbackCommandItem, IFallbackHandler, ICommand, ContextItem, IconInfo } from '../types';
/**
 * Base class for fallback command items that appear when the user types
 * from the home page. These provide search/filter results as the user types.
 *
 * @example
 * ```typescript
 * class WebSearchFallback extends FallbackCommandItemBase {
 *   command = new WebSearchCommand()
 *   title = 'Search the web'
 *
 *   updateQuery(query: string) {
 *     this.displayTitle = `Search for "${query}"`
 *   }
 * }
 * ```
 */
export declare abstract class FallbackCommandItemBase implements IFallbackCommandItem, IFallbackHandler {
    abstract command: ICommand;
    abstract title: string;
    subtitle?: string;
    icon?: IconInfo | null;
    moreCommands?: ContextItem[];
    displayTitle?: string;
    get fallbackHandler(): IFallbackHandler;
    abstract updateQuery(query: string): void;
}
//# sourceMappingURL=FallbackCommandItemBase.d.ts.map