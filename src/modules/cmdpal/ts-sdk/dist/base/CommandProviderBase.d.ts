import type { ICommandProvider, ICommandItem, IFallbackCommandItem, ICommand, ICommandSettings, IconInfo, IExtensionHost } from '../types';
/**
 * Base class for Command Palette extension providers.
 * Extend this class to create an extension that provides commands to the palette.
 *
 * @example
 * ```typescript
 * import { CommandProviderBase, ListPageBase } from '@microsoft/cmdpal-sdk';
 *
 * class MyProvider extends CommandProviderBase {
 *   id = 'my-extension';
 *   displayName = 'My Extension';
 *
 *   topLevelCommands() {
 *     return [{ command: new MyPage(), title: 'My Command', icon: null }];
 *   }
 * }
 * ```
 */
export declare abstract class CommandProviderBase implements ICommandProvider {
    abstract id: string;
    abstract displayName: string;
    icon?: IconInfo | null;
    frozen?: boolean;
    settings?: ICommandSettings | null;
    protected host?: IExtensionHost;
    abstract topLevelCommands(): Promise<ICommandItem[]> | ICommandItem[];
    fallbackCommands?(): Promise<IFallbackCommandItem[]> | IFallbackCommandItem[];
    getCommand?(id: string): Promise<ICommand | null> | ICommand | null;
    initializeWithHost(host: IExtensionHost): void;
    dispose(): void;
}
//# sourceMappingURL=CommandProviderBase.d.ts.map