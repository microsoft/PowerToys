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
import { ExtensionRuntime } from './runtime.js';

export { sendNotification } from './notifications.js';

/** Factory that produces the extension's command provider. */
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
 */
export function startJsonRpcServer(factory: ProviderFactory): void {
  const framer = new MessageFramer();
  let shuttingDown = false;

  const writeMessage = (message: JsonRpcMessage): void => {
    process.stdout.write(encodeMessage(message));
  };

  const notify = (method: string, params?: unknown): void => {
    writeMessage({ jsonrpc: JSONRPC_VERSION, method, params });
  };

  const shutdown = (code: number): void => {
    if (shuttingDown) {
      return;
    }
    shuttingDown = true;
    runtime.dispose();
    setNotificationSink(null);
    ExtensionHost.initialize(null);
    process.exit(code);
  };

  const runtime = new ExtensionRuntime({
    send: writeMessage,
    onDispose: () => {
      shutdown(0);
    },
  });

  setNotificationSink(notify);
  ExtensionHost.initialize(createHostBridge(notify));

  const ready = (async (): Promise<void> => {
    const provider = await factory();
    provider.initializeWithHost?.(ExtensionHostBridgeProxy);
    await runtime.setProvider(provider);
  })();

  let chain: Promise<void> = ready.catch((error: unknown) => {
    process.stderr.write(`cmdpal-sdk: failed to initialize extension: ${describeError(error)}\n`);
  });

  const enqueue = (message: unknown): void => {
    chain = chain
      .then(async () => {
        if (isRequest(message)) {
          await runtime.handleRequest(message);
        } else if (isNotification(message)) {
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
    shutdown(0);
  });
}

/** Alias for {@link startJsonRpcServer}. */
export function run(factory: ProviderFactory): void {
  startJsonRpcServer(factory);
}

/**
 * Convenience activation wrapper. Returns the provider produced by `factory`;
 * the runtime handles host wiring when {@link startJsonRpcServer} runs.
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
  showStatus: (message, state, progress) => {
    ExtensionHost.showStatus(message, state, progress);
  },
  hideStatus: (messageId) => {
    ExtensionHost.hideStatus(messageId);
  },
  copyToClipboard: (text) => {
    ExtensionHost.copyToClipboard(text);
  },
};

function createHostBridge(notify: (method: string, params?: unknown) => void): IExtensionHost {
  return {
    log(message: string, state: MessageState = 'info'): void {
      notify('host/logMessage', { message, state: MESSAGE_STATE_VALUE[state] });
    },
    showStatus(message: string, state: MessageState = 'info', progress?: ProgressState): void {
      notify('host/showStatus', {
        message: { Message: message, State: MESSAGE_STATE_VALUE[state] },
        progress,
        context: 'extension',
      });
    },
    hideStatus(messageId: string): void {
      notify('host/hideStatus', {
        message: { Message: messageId, State: MESSAGE_STATE_VALUE.info },
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
