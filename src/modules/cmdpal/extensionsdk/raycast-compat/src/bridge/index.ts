// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * Bridge entry point — boots a Raycast extension inside CmdPal.
 *
 * This is the script that CmdPal spawns as a Node.js child process.
 * It reads the Raycast extension manifest, creates the bridge provider,
 * registers JSON-RPC handlers that map CmdPal protocol calls to the
 * bridge provider's methods, and starts the stdin/stdout message loop.
 *
 * Usage:
 *   node dist/bridge/index.js --extension-dir /path/to/raycast-ext [--command cmdName]
 *
 * The extension directory must contain:
 *   - package.json with Raycast manifest fields (name, title, commands[])
 *   - Built command entry points (as specified in commands[].name)
 */

import * as path from 'path';
import * as fs from 'fs';

import { RaycastBridgeProvider } from './bridge-provider';
import type { RaycastExtensionManifest, RaycastCommandModule } from './bridge-provider';
import { _configureEnvironment, LaunchType } from '../api-stubs/environment';
import { _setStoragePath } from '../api-stubs/local-storage';

// Re-export bridge types for consumers
export { RaycastBridgeProvider } from './bridge-provider';
export type {
  RaycastCommandManifest,
  RaycastExtensionManifest,
  RaycastCommandModule,
  NotifyFn,
  PageSnapshot,
} from './bridge-provider';

// ══════════════════════════════════════════════════════════════════════════
// JSON-RPC transport (inline minimal implementation for the bridge process)
//
// We use a minimal transport here instead of importing the SDK's
// JsonRpcTransport to avoid coupling the compat package to the full SDK
// build. The protocol is identical: LSP-style Content-Length framing.
// ══════════════════════════════════════════════════════════════════════════

type RequestHandler = (params: unknown) => Promise<unknown>;

interface JsonRpcMessage {
  jsonrpc: '2.0';
  id?: number;
  method?: string;
  params?: unknown;
  result?: unknown;
  error?: { code: number; message: string; data?: unknown };
}

class BridgeTransport {
  private handlers = new Map<string, RequestHandler>();
  private buffer = Buffer.alloc(0);
  private nextLen: number | null = null;

  onRequest(method: string, handler: RequestHandler): void {
    this.handlers.set(method, handler);
  }

  sendNotification(method: string, params?: unknown): void {
    this._write({ jsonrpc: '2.0', method, params } as JsonRpcMessage);
  }

  start(): void {
    process.stdin.on('data', (chunk: Buffer) => {
      this.buffer = Buffer.concat([this.buffer, chunk]);
      while (this._tryParse()) { /* drain */ }
    });
    process.stdin.on('end', () => process.exit(0));
    process.stdin.resume();
  }

  private _tryParse(): boolean {
    if (this.nextLen === null) {
      const idx = this._findHeaderEnd();
      if (idx === -1) return false;
      const header = this.buffer.toString('utf8', 0, idx);
      const match = header.match(/content-length:\s*(\d+)/i);
      if (!match) {
        this.buffer = this.buffer.subarray(idx + 4);
        return true;
      }
      this.nextLen = parseInt(match[1], 10);
      this.buffer = this.buffer.subarray(idx + 4);
    }
    if (this.buffer.length < this.nextLen) return false;

    const json = this.buffer.subarray(0, this.nextLen).toString('utf8');
    this.buffer = this.buffer.subarray(this.nextLen);
    this.nextLen = null;

    try {
      const msg: JsonRpcMessage = JSON.parse(json);
      if (msg.id !== undefined && msg.method) {
        void this._handleRequest(msg);
      }
    } catch { /* skip malformed */ }
    return true;
  }

  private async _handleRequest(msg: JsonRpcMessage): Promise<void> {
    const handler = this.handlers.get(msg.method!);
    if (!handler) {
      this._write({
        jsonrpc: '2.0',
        id: msg.id,
        error: { code: -32601, message: `Method not found: ${msg.method}` },
      } as JsonRpcMessage);
      return;
    }
    try {
      const result = await handler(msg.params);
      this._write({ jsonrpc: '2.0', id: msg.id, result } as JsonRpcMessage);
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : String(err);
      this._write({
        jsonrpc: '2.0',
        id: msg.id,
        error: { code: -32603, message },
      } as JsonRpcMessage);
    }
  }

  private _findHeaderEnd(): number {
    for (let i = 0; i < this.buffer.length - 3; i++) {
      if (
        this.buffer[i] === 0x0d &&
        this.buffer[i + 1] === 0x0a &&
        this.buffer[i + 2] === 0x0d &&
        this.buffer[i + 3] === 0x0a
      ) return i;
    }
    return -1;
  }

  private _write(msg: JsonRpcMessage): void {
    const json = JSON.stringify(msg);
    const bytes = Buffer.from(json, 'utf8');
    const header = `Content-Length: ${bytes.length}\r\n\r\n`;
    process.stdout.write(Buffer.concat([Buffer.from(header, 'utf8'), bytes]));
  }
}

// ══════════════════════════════════════════════════════════════════════════
// Bootstrap
// ══════════════════════════════════════════════════════════════════════════

/** Parse CLI arguments. */
function parseArgs(): { extensionDir: string; command?: string } {
  const args = process.argv.slice(2);
  let extensionDir = '';
  let command: string | undefined;

  for (let i = 0; i < args.length; i++) {
    if (args[i] === '--extension-dir' && args[i + 1]) {
      extensionDir = args[++i];
    } else if (args[i] === '--command' && args[i + 1]) {
      command = args[++i];
    }
  }

  if (!extensionDir) {
    console.error('Usage: node bridge/index.js --extension-dir <path> [--command <name>]');
    process.exit(1);
  }

  return { extensionDir, command };
}

/** Load the Raycast extension manifest from package.json. */
function loadManifest(extensionDir: string): RaycastExtensionManifest {
  const pkgPath = path.join(extensionDir, 'package.json');
  if (!fs.existsSync(pkgPath)) {
    throw new Error(`No package.json found at ${pkgPath}`);
  }
  const pkg = JSON.parse(fs.readFileSync(pkgPath, 'utf8'));
  return {
    name: pkg.name ?? path.basename(extensionDir),
    title: pkg.title ?? pkg.name ?? 'Raycast Extension',
    description: pkg.description,
    icon: pkg.icon,
    commands: pkg.commands ?? [],
  };
}

/** Resolve and require a command's entry point module. */
function loadCommandModule(
  extensionDir: string,
  cmdName: string,
): RaycastCommandModule | null {
  // Raycast extensions build to a single JS file per command
  const candidates = [
    path.join(extensionDir, 'dist', `${cmdName}.js`),
    path.join(extensionDir, 'build', `${cmdName}.js`),
    path.join(extensionDir, `${cmdName}.js`),
    path.join(extensionDir, 'dist', 'index.js'),
  ];

  for (const candidate of candidates) {
    if (fs.existsSync(candidate)) {
      try {
        // eslint-disable-next-line @typescript-eslint/no-require-imports
        const mod = require(candidate);
        if (mod && (typeof mod.default === 'function' || typeof mod.default === 'object')) {
          return mod as RaycastCommandModule;
        }
      } catch (err) {
        console.error(`[Bridge] Failed to load ${candidate}:`, err);
      }
    }
  }

  return null;
}

/**
 * Register JSON-RPC handlers that bridge CmdPal protocol calls to the
 * RaycastBridgeProvider.
 */
function registerHandlers(
  transport: BridgeTransport,
  provider: RaycastBridgeProvider,
): void {
  transport.onRequest('initialize', async () => ({
    capabilities: {
      providesTopLevelCommands: true,
      providesFallbackCommands: false,
      providesCommandDetails: true,
      supportsDynamicPages: true,
      supportsContentPages: true,
      supportsForms: false,
      supportsSettings: false,
    },
    version: '1.0.0',
  }));

  transport.onRequest('dispose', async () => {
    provider.dispose();
    // Allow a brief delay for cleanup before exiting
    setTimeout(() => process.exit(0), 100);
    return {};
  });

  transport.onRequest('provider/getProperties', async () =>
    provider.getProperties(),
  );

  transport.onRequest('provider/getTopLevelCommands', async () =>
    provider.topLevelCommands(),
  );

  transport.onRequest('provider/getFallbackCommands', async () => []);

  transport.onRequest('provider/getCommand', async (params: unknown) => {
    const { commandId } = params as { commandId: string };
    if (!commandId) throw new Error('Missing commandId parameter');
    const cmd = provider.getCommand(commandId);
    if (!cmd) throw new Error(`Command not found: ${commandId}`);
    return cmd;
  });

  transport.onRequest('listPage/getItems', async (params: unknown) => {
    const { pageId } = params as { pageId: string };
    if (!pageId) throw new Error('Missing pageId parameter');
    const items = provider.getItems();
    return { items, totalItems: items.length };
  });

  transport.onRequest('listPage/setSearchText', async (params: unknown) => {
    const { pageId, searchText } = params as { pageId: string; searchText: string };
    if (!pageId) throw new Error('Missing pageId parameter');
    provider.setSearchText(searchText ?? '');
    const items = provider.getItems();
    return { updatedItemCount: items.length };
  });

  transport.onRequest('listPage/loadMore', async (params: unknown) => {
    const { pageId } = params as { pageId: string };
    if (!pageId) throw new Error('Missing pageId parameter');
    // Raycast extensions don't have explicit loadMore — they load everything
    const items = provider.getItems();
    return { newItemCount: items.length };
  });

  transport.onRequest('contentPage/getContent', async (params: unknown) => {
    const { pageId } = params as { pageId: string };
    if (!pageId) throw new Error('Missing pageId parameter');
    return { content: provider.getContent() };
  });

  transport.onRequest('command/invoke', async (params: unknown) => {
    const { commandId } = params as { commandId: string };
    if (!commandId) throw new Error('Missing commandId parameter');
    return provider.invokeCommand(commandId);
  });
}

// ══════════════════════════════════════════════════════════════════════════
// Main
// ══════════════════════════════════════════════════════════════════════════

/**
 * Boot the bridge. Can be called programmatically (for testing) or
 * runs automatically when this file is the entry point.
 */
export function boot(options?: {
  extensionDir: string;
  command?: string;
  /** Override transport for testing (skip stdin/stdout). */
  transport?: BridgeTransport;
}): { provider: RaycastBridgeProvider; transport: BridgeTransport } {
  const opts = options ?? { ...parseArgs(), transport: undefined };
  const extensionDir = path.resolve(opts.extensionDir);

  // Load manifest
  const manifest = loadManifest(extensionDir);

  // Configure environment stubs
  const basePath = path.join(
    process.env.LOCALAPPDATA ?? process.env.TEMP ?? '.',
    'Microsoft', 'PowerToys', 'CommandPalette', 'JSExtensions',
    manifest.name,
  );
  _configureEnvironment({
    extensionName: manifest.name,
    commandName: opts.command ?? manifest.commands[0]?.name ?? 'default',
    assetsPath: path.join(extensionDir, 'assets'),
    supportPath: path.join(basePath, 'data'),
    extensionDir,
    launchType: LaunchType.UserInitiated,
  });
  _setStoragePath(path.join(basePath, 'storage'));

  // Create provider
  const provider = new RaycastBridgeProvider(manifest);

  // Load command modules
  const commandsToLoad = opts.command
    ? [manifest.commands.find((c) => c.name === opts.command) ?? manifest.commands[0]]
    : manifest.commands;

  for (const cmd of commandsToLoad) {
    if (!cmd) continue;
    const mod = loadCommandModule(extensionDir, cmd.name);
    if (mod) {
      provider.registerCommand(cmd.name, mod);
    } else {
      console.warn(`[Bridge] Could not load command module: ${cmd.name}`);
    }
  }

  // Create transport and wire notifications
  const transport = opts.transport ?? new BridgeTransport();
  provider.setNotifyFn((method, params) => {
    transport.sendNotification(method, params);
  });

  // Register protocol handlers
  registerHandlers(transport, provider);

  return { provider, transport };
}

// Auto-start when run as the entry point
const isEntryPoint =
  typeof require !== 'undefined' && require.main === module;

if (isEntryPoint) {
  const { transport } = boot();
  transport.start();
}
