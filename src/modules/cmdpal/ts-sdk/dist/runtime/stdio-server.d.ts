/**
 * JSONRPC over stdio server for Command Palette extensions.
 * This module handles LSP-style Content-Length framed JSON-RPC 2.0
 * communication directly with the C# host over stdin/stdout.
 *
 * Usage:
 * ```typescript
 * import { startJsonRpcServer } from '@microsoft/cmdpal-sdk/runtime/stdio-server';
 * import { MyProvider } from './provider';
 *
 * startJsonRpcServer(() => new MyProvider());
 * ```
 */
import type { ICommandProvider } from '../types';
export declare function sendNotification(method: string, params: unknown): void;
/**
 * Starts the JSONRPC stdio server with the given provider factory.
 * This function never returns — it runs the read loop until the process is terminated.
 */
export declare function startJsonRpcServer(providerFactory: () => ICommandProvider | Promise<ICommandProvider>): void;
//# sourceMappingURL=stdio-server.d.ts.map