import type { IInvokableCommand, CommandResult, IconInfo } from '../types';
/**
 * Base class for commands that can be invoked (executed) by the user.
 *
 * @example
 * ```typescript
 * import { InvokableCommandBase } from '@microsoft/cmdpal-sdk';
 *
 * class CopyCommand extends InvokableCommandBase {
 *   id = 'copy-text';
 *   name = 'Copy to Clipboard';
 *
 *   async invoke() {
 *     // Perform the action
 *     return { kind: 'showToast', args: { message: 'Copied!' } };
 *   }
 * }
 * ```
 */
export declare abstract class InvokableCommandBase implements IInvokableCommand {
    abstract id: string;
    abstract name: string;
    icon?: IconInfo | null;
    abstract invoke(): Promise<CommandResult> | CommandResult;
}
//# sourceMappingURL=InvokableCommandBase.d.ts.map