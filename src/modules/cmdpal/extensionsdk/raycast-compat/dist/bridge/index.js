"use strict";
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
var __createBinding = (this && this.__createBinding) || (Object.create ? (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    var desc = Object.getOwnPropertyDescriptor(m, k);
    if (!desc || ("get" in desc ? !m.__esModule : desc.writable || desc.configurable)) {
      desc = { enumerable: true, get: function() { return m[k]; } };
    }
    Object.defineProperty(o, k2, desc);
}) : (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    o[k2] = m[k];
}));
var __setModuleDefault = (this && this.__setModuleDefault) || (Object.create ? (function(o, v) {
    Object.defineProperty(o, "default", { enumerable: true, value: v });
}) : function(o, v) {
    o["default"] = v;
});
var __importStar = (this && this.__importStar) || (function () {
    var ownKeys = function(o) {
        ownKeys = Object.getOwnPropertyNames || function (o) {
            var ar = [];
            for (var k in o) if (Object.prototype.hasOwnProperty.call(o, k)) ar[ar.length] = k;
            return ar;
        };
        return ownKeys(o);
    };
    return function (mod) {
        if (mod && mod.__esModule) return mod;
        var result = {};
        if (mod != null) for (var k = ownKeys(mod), i = 0; i < k.length; i++) if (k[i] !== "default") __createBinding(result, mod, k[i]);
        __setModuleDefault(result, mod);
        return result;
    };
})();
Object.defineProperty(exports, "__esModule", { value: true });
exports.RaycastBridgeProvider = void 0;
exports.boot = boot;
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
const path = __importStar(require("path"));
const fs = __importStar(require("fs"));
const bridge_provider_1 = require("./bridge-provider");
const environment_1 = require("../api-stubs/environment");
const local_storage_1 = require("../api-stubs/local-storage");
// Re-export bridge types for consumers
var bridge_provider_2 = require("./bridge-provider");
Object.defineProperty(exports, "RaycastBridgeProvider", { enumerable: true, get: function () { return bridge_provider_2.RaycastBridgeProvider; } });
class BridgeTransport {
    handlers = new Map();
    buffer = Buffer.alloc(0);
    nextLen = null;
    onRequest(method, handler) {
        this.handlers.set(method, handler);
    }
    sendNotification(method, params) {
        this._write({ jsonrpc: '2.0', method, params });
    }
    start() {
        process.stdin.on('data', (chunk) => {
            this.buffer = Buffer.concat([this.buffer, chunk]);
            while (this._tryParse()) { /* drain */ }
        });
        process.stdin.on('end', () => process.exit(0));
        process.stdin.resume();
    }
    _tryParse() {
        if (this.nextLen === null) {
            const idx = this._findHeaderEnd();
            if (idx === -1)
                return false;
            const header = this.buffer.toString('utf8', 0, idx);
            const match = header.match(/content-length:\s*(\d+)/i);
            if (!match) {
                this.buffer = this.buffer.subarray(idx + 4);
                return true;
            }
            this.nextLen = parseInt(match[1], 10);
            this.buffer = this.buffer.subarray(idx + 4);
        }
        if (this.buffer.length < this.nextLen)
            return false;
        const json = this.buffer.subarray(0, this.nextLen).toString('utf8');
        this.buffer = this.buffer.subarray(this.nextLen);
        this.nextLen = null;
        try {
            const msg = JSON.parse(json);
            if (msg.id !== undefined && msg.method) {
                void this._handleRequest(msg);
            }
        }
        catch { /* skip malformed */ }
        return true;
    }
    async _handleRequest(msg) {
        const handler = this.handlers.get(msg.method);
        if (!handler) {
            this._write({
                jsonrpc: '2.0',
                id: msg.id,
                error: { code: -32601, message: `Method not found: ${msg.method}` },
            });
            return;
        }
        try {
            const result = await handler(msg.params);
            this._write({ jsonrpc: '2.0', id: msg.id, result });
        }
        catch (err) {
            const message = err instanceof Error ? err.message : String(err);
            this._write({
                jsonrpc: '2.0',
                id: msg.id,
                error: { code: -32603, message },
            });
        }
    }
    _findHeaderEnd() {
        for (let i = 0; i < this.buffer.length - 3; i++) {
            if (this.buffer[i] === 0x0d &&
                this.buffer[i + 1] === 0x0a &&
                this.buffer[i + 2] === 0x0d &&
                this.buffer[i + 3] === 0x0a)
                return i;
        }
        return -1;
    }
    _write(msg) {
        const json = JSON.stringify(msg);
        const bytes = Buffer.from(json, 'utf8');
        const header = `Content-Length: ${bytes.length}\r\n\r\n`;
        process.stdout.write(Buffer.concat([Buffer.from(header, 'utf8'), bytes]));
    }
}
// ══════════════════════════════════════════════════════════════════════════
// Bootstrap
// ══════════════════════════════════════════════════════════════════════════
/** Parse CLI arguments. Falls back to cwd when no args are provided. */
function parseArgs() {
    const args = process.argv.slice(2);
    let extensionDir = '';
    let command;
    for (let i = 0; i < args.length; i++) {
        if (args[i] === '--extension-dir' && args[i + 1]) {
            extensionDir = args[++i];
        }
        else if (args[i] === '--command' && args[i + 1]) {
            command = args[++i];
        }
    }
    // When no --extension-dir is provided, use the directory containing
    // this script's parent (i.e. the extension root). This supports the
    // installed layout where index.js lives at the extension root and
    // the bridge is in a bridge/ subdirectory.
    if (!extensionDir) {
        extensionDir = path.resolve(__dirname, '..');
    }
    return { extensionDir, command };
}
/**
 * Load the extension manifest.
 *
 * Supports two layouts:
 * 1. Development: reads package.json (Raycast format)
 * 2. Installed: reads cmdpal.json + raycast-compat.json (pipeline output)
 */
function loadManifest(extensionDir) {
    // Try package.json first (development / source layout)
    const pkgPath = path.join(extensionDir, 'package.json');
    if (fs.existsSync(pkgPath)) {
        const pkg = JSON.parse(fs.readFileSync(pkgPath, 'utf8'));
        return {
            name: pkg.name ?? path.basename(extensionDir),
            title: pkg.title ?? pkg.name ?? 'Raycast Extension',
            description: pkg.description,
            icon: pkg.icon,
            commands: pkg.commands ?? [],
        };
    }
    // Fall back to installed layout: cmdpal.json + raycast-compat.json
    const cmdpalPath = path.join(extensionDir, 'cmdpal.json');
    const compatPath = path.join(extensionDir, 'raycast-compat.json');
    if (!fs.existsSync(cmdpalPath)) {
        throw new Error(`No package.json or cmdpal.json found in ${extensionDir}`);
    }
    const cmdpal = JSON.parse(fs.readFileSync(cmdpalPath, 'utf8'));
    const compat = fs.existsSync(compatPath)
        ? JSON.parse(fs.readFileSync(compatPath, 'utf8'))
        : { commands: [], raycastOriginalName: '' };
    return {
        name: compat.raycastOriginalName || cmdpal.name || path.basename(extensionDir),
        title: cmdpal.displayName || cmdpal.name || 'Raycast Extension',
        description: cmdpal.description,
        icon: cmdpal.icon,
        commands: compat.commands ?? [],
    };
}
/** Resolve and require a command's entry point module. */
function loadCommandModule(extensionDir, cmdName) {
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
                    return mod;
                }
            }
            catch (err) {
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
function registerHandlers(transport, provider) {
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
    transport.onRequest('provider/getProperties', async () => provider.getProperties());
    transport.onRequest('provider/getTopLevelCommands', async () => provider.topLevelCommands());
    transport.onRequest('provider/getFallbackCommands', async () => []);
    transport.onRequest('provider/getCommand', async (params) => {
        const { commandId } = params;
        if (!commandId)
            throw new Error('Missing commandId parameter');
        const cmd = provider.getCommand(commandId);
        if (!cmd)
            throw new Error(`Command not found: ${commandId}`);
        return cmd;
    });
    transport.onRequest('listPage/getItems', async (params) => {
        const { pageId } = params;
        if (!pageId)
            throw new Error('Missing pageId parameter');
        provider.ensureMounted(pageId);
        const items = provider.getItems();
        return { items, totalItems: items.length };
    });
    transport.onRequest('listPage/setSearchText', async (params) => {
        const { pageId, searchText } = params;
        if (!pageId)
            throw new Error('Missing pageId parameter');
        provider.ensureMounted(pageId);
        provider.setSearchText(searchText ?? '');
        const items = provider.getItems();
        return { updatedItemCount: items.length };
    });
    transport.onRequest('listPage/loadMore', async (params) => {
        const { pageId } = params;
        if (!pageId)
            throw new Error('Missing pageId parameter');
        provider.ensureMounted(pageId);
        // Raycast extensions don't have explicit loadMore — they load everything
        const items = provider.getItems();
        return { newItemCount: items.length };
    });
    transport.onRequest('contentPage/getContent', async (params) => {
        const { pageId } = params;
        if (!pageId)
            throw new Error('Missing pageId parameter');
        provider.ensureMounted(pageId);
        return { content: provider.getContent() };
    });
    transport.onRequest('command/invoke', async (params) => {
        const { commandId } = params;
        if (!commandId)
            throw new Error('Missing commandId parameter');
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
function boot(options) {
    const opts = options ?? { ...parseArgs(), transport: undefined };
    const extensionDir = path.resolve(opts.extensionDir);
    // Load manifest
    const manifest = loadManifest(extensionDir);
    // Configure environment stubs
    const basePath = path.join(process.env.LOCALAPPDATA ?? process.env.TEMP ?? '.', 'Microsoft', 'PowerToys', 'CommandPalette', 'JSExtensions', manifest.name);
    (0, environment_1._configureEnvironment)({
        extensionName: manifest.name,
        commandName: opts.command ?? manifest.commands[0]?.name ?? 'default',
        assetsPath: path.join(extensionDir, 'assets'),
        supportPath: path.join(basePath, 'data'),
        extensionDir,
        launchType: environment_1.LaunchType.UserInitiated,
    });
    (0, local_storage_1._setStoragePath)(path.join(basePath, 'storage'));
    // Create provider
    const provider = new bridge_provider_1.RaycastBridgeProvider(manifest);
    // Load command modules
    const commandsToLoad = opts.command
        ? [manifest.commands.find((c) => c.name === opts.command) ?? manifest.commands[0]]
        : manifest.commands;
    for (const cmd of commandsToLoad) {
        if (!cmd)
            continue;
        const mod = loadCommandModule(extensionDir, cmd.name);
        if (mod) {
            provider.registerCommand(cmd.name, mod);
        }
        else {
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
const isEntryPoint = typeof require !== 'undefined' && require.main === module;
if (isEntryPoint) {
    const { transport } = boot();
    transport.start();
}
//# sourceMappingURL=index.js.map