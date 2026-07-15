// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import { ExtensionHost, InvokableCommandBase } from '@microsoft/cmdpal-sdk';
import type { CommandResult, MessageState } from '@microsoft/cmdpal-sdk';

/**
 * Shows a toast in the transparent toast window by returning a `showToast`
 * command result. Mirrors the C# `ShowToastCommand`.
 */
export class ShowToastCommand extends InvokableCommandBase {
  readonly id: string;
  readonly name = 'Show toast';

  private readonly message: string;

  constructor(message: string, id = 'show-toast') {
    super();
    this.id = id;
    this.message = message;
  }

  override invoke(): CommandResult {
    return { kind: 'showToast', args: { message: this.message } };
  }
}

/**
 * Shows an in-page status message through the host bridge and keeps the palette
 * open. Mirrors the C# `ToastCommand`, which uses `ToastStatusMessage` (an
 * inline banner) rather than the toast window.
 */
export class StatusMessageCommand extends InvokableCommandBase {
  readonly id: string;
  name: string;

  private readonly message: string;
  private readonly state: MessageState;

  constructor(message: string, state: MessageState = 'info', id = `status:${message}`) {
    super();
    this.id = id;
    this.name = 'Show status';
    this.message = message;
    this.state = state;
  }

  override invoke(): CommandResult {
    ExtensionHost.showStatus(this.message, this.state);
    return { kind: 'keepOpen' };
  }
}

/**
 * Cycles through the four message states each time it is invoked. Mirrors the
 * C# `SendMessageCommand`.
 */
export class SendMessageCommand extends InvokableCommandBase {
  readonly id = 'send-message';
  readonly name = 'Send message';

  private sentMessages = 0;

  override invoke(): CommandResult {
    const states: MessageState[] = ['info', 'success', 'warning', 'error'];
    const state = states[this.sentMessages % states.length] ?? 'info';
    ExtensionHost.showStatus(`I am status message no.${this.sentMessages}`, state);
    this.sentMessages += 1;
    return { kind: 'keepOpen' };
  }
}

/**
 * Shows a single status message and hides it again on the next invoke. Mirrors
 * the C# `SingleMessageCommand`.
 */
export class SingleMessageCommand extends InvokableCommandBase {
  readonly id = 'single-message';
  name = 'Show';

  private shown = false;

  get isShown(): boolean {
    return this.shown;
  }

  override invoke(): CommandResult {
    if (this.shown) {
      ExtensionHost.hideStatus('I am a status message');
    } else {
      ExtensionHost.showStatus('I am a status message', 'info');
    }
    this.shown = !this.shown;
    this.name = this.shown ? 'Hide' : 'Show';
    return { kind: 'keepOpen' };
  }
}

/**
 * Shows an indeterminate progress status, then completes it after a short
 * delay. Mirrors the C# `IndeterminateProgressMessageCommand`.
 */
export class IndeterminateProgressMessageCommand extends InvokableCommandBase {
  readonly id = 'indeterminate-progress';
  readonly name = 'Do the thing';

  private running = false;

  override invoke(): CommandResult {
    if (!this.running) {
      this.running = true;
      ExtensionHost.showStatus('Doing the thing...', 'info', { isIndeterminate: true });
      setTimeout(() => {
        ExtensionHost.showStatus('Did the thing!', 'success');
        setTimeout(() => {
          ExtensionHost.hideStatus('Did the thing!');
          this.running = false;
        }, 3000);
      }, 3000);
    }
    return { kind: 'keepOpen' };
  }
}

/**
 * Shows an indeterminate progress status and then resolves it to a success
 * message after a short delay. Used by the details command buttons to make the
 * status banner and its spinner obviously visible when the button is invoked.
 */
export class ProgressStatusCommand extends InvokableCommandBase {
  readonly id: string;
  name: string;

  private readonly workingMessage: string;
  private readonly doneMessage: string;
  private running = false;

  constructor(name: string, workingMessage: string, doneMessage: string, id: string) {
    super();
    this.id = id;
    this.name = name;
    this.workingMessage = workingMessage;
    this.doneMessage = doneMessage;
  }

  override invoke(): CommandResult {
    if (!this.running) {
      this.running = true;
      ExtensionHost.showStatus(this.workingMessage, 'info', { isIndeterminate: true });
      setTimeout(() => {
        ExtensionHost.showStatus(this.doneMessage, 'success');
        setTimeout(() => {
          ExtensionHost.hideStatus(this.doneMessage);
          this.running = false;
        }, 3000);
      }, 2000);
    }
    return { kind: 'keepOpen' };
  }
}