// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import type { CommandResult, IInvokableCommand, IconInfo } from '../types.js';
import { ExtensionHost } from '../runtime/ExtensionHost.js';
import { stableId } from '../runtime/hash.js';
import { openUrlInDefaultBrowser, type UrlOpener } from '../runtime/openUrl.js';

/** A command that performs no action and keeps the palette open. */
export class NoOpCommand implements IInvokableCommand {
  id: string;
  name: string;
  icon?: IconInfo | null;

  /**
   * Creates a no-op command.
   *
   * @param id Identifier for the command. Defaults to `'noop'`.
   * @param name Display name. Defaults to an empty string.
   */
  constructor(id = 'noop', name = '') {
    this.id = id;
    this.name = name;
  }

  /** Keeps the palette open without doing anything. */
  invoke(): CommandResult {
    return { kind: 'keepOpen' };
  }
}

/** A command that asks the host to open a URL in the default browser. */
export class OpenUrlCommand implements IInvokableCommand {
  id: string;
  name: string;
  icon?: IconInfo | null;
  /** The URL opened when the command is invoked. */
  readonly url: string;

  private readonly opener: UrlOpener;

  /**
   * Creates a command that opens a URL.
   *
   * @param url URL to open.
   * @param name Display name. Defaults to the URL.
   * @param opener Function used to open the URL. Defaults to the system browser;
   * override in tests.
   */
  constructor(url: string, name?: string, opener: UrlOpener = openUrlInDefaultBrowser) {
    this.id = `open-url:${url}`;
    this.name = name ?? url;
    this.url = url;
    this.opener = opener;
  }

  /** Opens the URL and dismisses the palette. */
  invoke(): CommandResult {
    this.opener(this.url);
    return { kind: 'dismiss' };
  }
}

/** A command that copies text to the clipboard and shows a confirmation toast. */
export class CopyTextCommand implements IInvokableCommand {
  id: string;
  name: string;
  icon?: IconInfo | null;
  /** The text copied to the clipboard when the command is invoked. */
  readonly text: string;
  /** The toast message shown after the text is copied. */
  readonly toastMessage: string;

  /**
   * Creates a command that copies text.
   *
   * @param text Text to copy to the clipboard.
   * @param name Display name. Defaults to `'Copy'`.
   * @param toastMessage Confirmation shown after copying. Defaults to
   * `'Copied to clipboard'`.
   * @param id Explicit stable id. When omitted, a collision-resistant id is
   * derived from the full command payload, so two different strings that share
   * the same leading characters still receive distinct ids.
   */
  constructor(text: string, name?: string, toastMessage?: string, id?: string) {
    this.name = name ?? 'Copy';
    this.text = text;
    this.toastMessage = toastMessage ?? 'Copied to clipboard';
    this.id =
      id ?? stableId('copy-text', { text, name: this.name, toastMessage: this.toastMessage });
  }

  /** Copies the text to the clipboard and shows the confirmation toast. */
  invoke(): CommandResult {
    ExtensionHost.copyToClipboard(this.text);
    return { kind: 'showToast', args: { message: this.toastMessage } };
  }
}

/** Options for constructing a {@link ConfirmableCommand}. */
export interface ConfirmableCommandOptions {
  /** Identifier for the command. */
  id: string;
  /** Display name of the command. */
  name: string;
  /** Title of the confirmation dialog. */
  title: string;
  /** Body text explaining what the user is confirming. */
  description: string;
  /** Command run once the user confirms. */
  primaryCommand: IInvokableCommand;
  /** Render the confirm action with a destructive (critical) style. */
  isCritical?: boolean;
  /** Icon shown for the command. */
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

  /**
   * Creates a confirmable command.
   *
   * @param options Dialog text and the command to run on confirmation.
   */
  constructor(options: ConfirmableCommandOptions) {
    this.id = options.id;
    this.name = options.name;
    this.title = options.title;
    this.description = options.description;
    this.primaryCommand = options.primaryCommand;
    this.isCritical = options.isCritical ?? false;
    this.icon = options.icon ?? null;
  }

  /** Returns a `confirm` result that prompts the user before proceeding. */
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
