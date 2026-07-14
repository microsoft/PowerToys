import type { IContentPage, Content, Details, ContextItem, IconInfo, OptionalColor } from '../types';
/**
 * Base class for content pages that display rich content (markdown, forms, images, etc.).
 *
 * @example
 * ```typescript
 * import { ContentPageBase } from '@microsoft/cmdpal-sdk';
 *
 * class ReadmePage extends ContentPageBase {
 *   id = 'readme';
 *   name = 'README';
 *   title = 'About This Extension';
 *
 *   getContent() {
 *     return [
 *       { type: 'markdown', body: '# Hello World\n\nThis is my extension.' }
 *     ];
 *   }
 * }
 * ```
 */
export declare abstract class ContentPageBase implements IContentPage {
    abstract id: string;
    abstract name: string;
    abstract title: string;
    icon?: IconInfo | null;
    isLoading?: boolean;
    accentColor?: OptionalColor | null;
    details?: Details | null;
    commands?: ContextItem[];
    abstract getContent(): Promise<Content[]> | Content[];
}
//# sourceMappingURL=ContentPageBase.d.ts.map