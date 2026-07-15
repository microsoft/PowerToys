// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import type { CommandResult, IInvokableCommand, IconInfo } from '../types.js';
import { ExtensionHost } from '../runtime/ExtensionHost.js';

/** A command that performs no action and keeps the palette open. */
export class NoOpCommand implements IInvokableCommand {
  id: string;
  name: string;
  icon?: IconInfo | null;

  constructor(id = 'noop', name = '') {
    this.id = id;
    this.name = name;
  }

  invoke(): CommandResult {
    return { kind: 'keepOpen' };
  }
}

/** A command that asks the host to open a URL in the default browser. */
export class OpenUrlCommand implements IInvokableCommand {
  id: string;
  name: string;
  icon?: IconInfo | null;
  readonly url: string;

  constructor(url: string, name?: string) {
    this.id = `open-url:${url}`;
    this.name = name ?? url;
    this.url = url;
  }

  invoke(): CommandResult {
    return { kind: 'dismiss', args: { url: this.url } };
  }
}

/** A command that copies text to the clipboard and shows a confirmation toast. */
export class CopyTextCommand implements IInvokableCommand {
  id: string;
  name: string;
  icon?: IconInfo | null;
  readonly text: string;
  readonly toastMessage: string;

  constructor(text: string, name?: string, toastMessage?: string) {
    this.id = `copy-text:${text.slice(0, 24)}`;
    this.name = name ?? 'Copy';
    this.text = text;
    this.toastMessage = toastMessage ?? 'Copied to clipboard';
  }

  invoke(): CommandResult {
    ExtensionHost.copyToClipboard(this.text);
    return { kind: 'showToast', args: { message: this.toastMessage } };
  }
}

export interface ConfirmableCommandOptions {
  id: string;
  name: string;
  title: string;
  description: string;
  primaryCommand: IInvokableCommand;
  isCritical?: boolean;
  icon?: IconInfo | null;
}

/** A command that shows a confirmation dialog before running another command. */
export class ConfirmableCommand implements IInvokableCommand {
  id: string;
  name: string;
  icon?: IconInfo | null;

  private readonly title: string;
  private readonly description: string;
  private readonly primaryCommand: IInvokableCommand;
  private readonly isCritical: boolean;

  constructor(options: ConfirmableCommandOptions) {
    this.id = options.id;
    this.name = options.name;
    this.title = options.title;
    this.description = options.description;
    this.primaryCommand = options.primaryCommand;
    this.isCritical = options.isCritical ?? false;
    this.icon = options.icon ?? null;
  }

  invoke(): CommandResult {
    return {
      kind: 'confirm',
      args: {
        title: this.title,
        description: this.description,
        primaryCommand: this.primaryCommand,
        isPrimaryCommandCritical: this.isCritical,
      },
    };
  }
}
