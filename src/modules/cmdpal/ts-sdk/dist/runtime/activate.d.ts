import type { ICommandProvider, ActivationContext } from '../types';
/**
 * Helper function for extension activation.
 * Wraps a provider factory with automatic ExtensionHost initialization.
 *
 * @example
 * ```typescript
 * // In your extension's index.ts:
 * import { activate as sdkActivate, CommandProviderBase } from '@microsoft/cmdpal-sdk';
 *
 * class MyProvider extends CommandProviderBase {
 *   id = 'my-ext';
 *   displayName = 'My Extension';
 *   topLevelCommands() { return []; }
 * }
 *
 * export function activate(context: ActivationContext) {
 *   return sdkActivate(context, () => new MyProvider());
 * }
 * ```
 */
export declare function activate(context: ActivationContext, providerFactory: () => ICommandProvider | Promise<ICommandProvider>): ICommandProvider | Promise<ICommandProvider>;
//# sourceMappingURL=activate.d.ts.map