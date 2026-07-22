// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * JSON-RPC 2.0 server over stdio for Command Palette extensions. Wires the
 * process streams to an {@link ExtensionRuntime}, installs the
 * {@link ExtensionHost} bridge, and exposes the activation entry points.
 */

import type {
  ActivationContext,
  ICommandProvider,
  IExtensionHost,
  MessageState,
  ProgressState,
} from '../types.js';
import { encodeMessage, MessageFramer } from './framing.js';
import { JSONRPC_VERSION, isNotification, isRequest, type JsonRpcMessage } from './jsonrpc.js';
import { setNotificationSink } from './notifications.js';
import { ExtensionHost } from './ExtensionHost.js';
import { ExtensionRuntime, DEFAULT_DISPOSE_TIMEOUT_MS } from './runtime.js';
import { claimProtocolStdout } from './stdio.js';

export { sendNotification } from './notifications.js';

/**
 * Factory that produces the extension's command provider. Called once when the
 * JSON-RPC server starts.
 *
 * @returns The extension's {@link ICommandProvider}, synchronously or as a
 * promise.
 */
export type ProviderFactory = () => ICommandProvider | Promise<ICommandProvider>;

const MESSAGE_STATE_VALUE: Record<MessageState, number> = {
  info: 0,
  success: 1,
  warning: 2,
  error: 3,
};

/**
 * Starts the JSON-RPC stdio server. This reads framed messages from stdin,
 * dispatches them to the provider produced by `factory`, and writes framed
 * responses and notifications to stdout. It runs until the host closes the
 * connection or sends a `dispose` notification.
 *
 * @param factory Produces the extension's command provider.
 */
export function startJsonRpcServer(factory: ProviderFactory): void {
  // Claim stdout for the protocol before any extension author code runs, so
  // early logging is redirected to stderr and cannot corrupt a protocol frame.
  const stdout = claimProtocolStdout();
  const framer = new MessageFramer();
  let finalized = false;
  let disposeTimeoutMs = DEFAULT_DISPOSE_TIMEOUT_MS;

  const writeMessage = (message: JsonRpcMessage): void => {
    stdout.writeRaw(encodeMessage(message));
  };

  const notify = (method: string, params?: unknown): void => {
    writeMessage({ jsonrpc: JSONRPC_VERSION, method, params });
  };

  // Idempotent shutdown: dispose the provider within the host-supplied bound,
  // release the host bridge, restore stdout, and prefer setting process.exitCode
  // over a hard process.exit so buffered protocol writes can flush.
  const finalize = async (code: number): Promise<void> => {
    if (finalized) {
      return;
    }
    finalized = true;
    if (code !== 0) {
      process.exitCode = code;
    }
    try {
      await runtime.dispose(disposeTimeoutMs);
    } catch (error) {
      process.stderr.write(`cmdpal-sdk: shutdown disposal failed: ${describeError(error)}\n`);
    }
    setNotificationSink(null);
    ExtensionHost.initialize(null);
    stdout.restore();
    process.stdin.pause();
  };

  const runtime = new ExtensionRuntime({
    send: writeMessage,
    onDispose: () => {
      void finalize(0);
    },
    reportFatal: (code: number) => {
      process.exitCode = code;
    },
  });

  setNotificationSink(notify);
  ExtensionHost.initialize(createHostBridge(notify));

  runtime.beginInitialization(
    (async (): Promise<ICommandProvider> => {
      const provider = await factory();
      provider.initializeWithHost?.(ExtensionHostBridgeProxy);
      return provider;
    })(),
  );

  let chain: Promise<void> = Promise.resolve();

  const enqueue = (message: unknown): void => {
    chain = chain
      .then(async () => {
        if (isRequest(message)) {
          await runtime.handleRequest(message);
        } else if (isNotification(message)) {
          if (message.method === 'dispose') {
            const params = message.params;
            if (params && typeof params === 'object' && 'timeoutMs' in params) {
              const value = (params as { timeoutMs?: unknown }).timeoutMs;
              if (typeof value === 'number' && Number.isFinite(value) && value >= 0) {
                disposeTimeoutMs = value;
              }
            }
          }
          await runtime.handleNotification(message);
        }
      })
      .catch((error: unknown) => {
        process.stderr.write(`cmdpal-sdk: message handling failed: ${describeError(error)}\n`);
      });
  };

  process.stdin.on('data', (chunk: Buffer) => {
    for (const body of framer.push(chunk)) {
      let parsed: unknown;
      try {
        parsed = JSON.parse(body);
      } catch {
        continue;
      }
      enqueue(parsed);
    }
  });

  process.stdin.on('end', () => {
    void finalize(0);
  });
}

/**
 * Alias for {@link startJsonRpcServer}.
 *
 * @param factory Produces the extension's command provider.
 */
export function run(factory: ProviderFactory): void {
  startJsonRpcServer(factory);
}

/**
 * Convenience activation wrapper. Returns the provider produced by `factory`;
 * the runtime handles host wiring when {@link startJsonRpcServer} runs.
 *
 * @param _context Activation details supplied by the host. Currently unused.
 * @param factory Produces the extension's command provider.
 * @returns The extension's command provider, synchronously or as a promise.
 */
export function activate(
  _context: ActivationContext,
  factory: ProviderFactory,
): ICommandProvider | Promise<ICommandProvider> {
  return factory();
}

/**
 * A stable {@link IExtensionHost} that delegates to whatever implementation is
 * currently installed on {@link ExtensionHost}. Passing this to a provider's
 * `initializeWithHost` keeps the provider working across reinitialization.
 */
const ExtensionHostBridgeProxy: IExtensionHost = {
  log: (message, state) => {
    ExtensionHost.log(message, state);
  },
  showStatus: (message, state, progress) => ExtensionHost.showStatus(message, state, progress),
  updateStatus: (statusId, message, state, progress) => {
    ExtensionHost.updateStatus(statusId, message, state, progress);
  },
  hideStatus: (statusId) => {
    ExtensionHost.hideStatus(statusId);
  },
  copyToClipboard: (text) => {
    ExtensionHost.copyToClipboard(text);
  },
};

interface TrackedStatus {
  message: string;
  state: MessageState;
  progress?: ProgressState;
}

/**
 * Builds the host bridge that forwards {@link IExtensionHost} calls to the host
 * as JSON-RPC notifications. Exported for tests. The bridge mints a stable
 * statusId for every status shown so a status can be updated or hidden by id,
 * and keeps the message text so a host that still hides by message text keeps
 * working until it is upgraded.
 */
export function createHostBridge(
  notify: (method: string, params?: unknown) => void,
): IExtensionHost {
  let statusCounter = 0;
  const statuses = new Map<string, TrackedStatus>();

  const sendStatus = (statusId: string, tracked: TrackedStatus): void => {
    // Carry the SDK-minted statusId so an up-to-date host can update or hide the
    // exact status by id, and keep the Message text so today's host (which hides
    // by message text) keeps working until it is upgraded.
    notify('host/showStatus', {
      statusId,
      message: { Message: tracked.message, State: MESSAGE_STATE_VALUE[tracked.state] },
      progress: tracked.progress,
      context: 'extension',
    });
  };

  return {
    log(message: string, state: MessageState = 'info'): void {
      notify('host/logMessage', { message, state: MESSAGE_STATE_VALUE[state] });
    },
    showStatus(message: string, state: MessageState = 'info', progress?: ProgressState): string {
      statusCounter += 1;
      const statusId = `status-${String(statusCounter)}`;
      const tracked: TrackedStatus = { message, state, progress };
      statuses.set(statusId, tracked);
      sendStatus(statusId, tracked);
      return statusId;
    },
    updateStatus(
      statusId: string,
      message: string,
      state: MessageState = 'info',
      progress?: ProgressState,
    ): void {
      const tracked: TrackedStatus = { message, state, progress };
      statuses.set(statusId, tracked);
      sendStatus(statusId, tracked);
    },
    hideStatus(statusId: string): void {
      const tracked = statuses.get(statusId);
      statuses.delete(statusId);
      notify('host/hideStatus', {
        statusId,
        message: {
          Message: tracked?.message ?? statusId,
          State: MESSAGE_STATE_VALUE[tracked?.state ?? 'info'],
        },
      });
    },
    copyToClipboard(text: string): void {
      notify('host/copyText', { text });
    },
  };
}

function describeError(error: unknown): string {
  return error instanceof Error ? error.message : String(error);
}
