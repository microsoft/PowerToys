// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import {
  ICommandResult,
  ICommandResultArgs,
  IGoToPageArgs,
  IToastArgs,
  IConfirmationArgs,
  CommandResultKind,
  NavigationMode,
  ICommand,
} from '../generated/types';

/**
 * Helper class for creating command results.
 */
export class CommandResult implements ICommandResult {
  kind: CommandResultKind;
  args?: ICommandResultArgs;

  constructor(kind: CommandResultKind, args?: ICommandResultArgs) {
    this.kind = kind;
    this.args = args;
  }

  /** Dismiss the command palette. */
  static dismiss(): CommandResult {
    return new CommandResult(CommandResultKind.Dismiss);
  }

  /** Navigate to the home page. */
  static goHome(): CommandResult {
    return new CommandResult(CommandResultKind.GoHome);
  }

  /** Navigate back to the previous page. */
  static goBack(): CommandResult {
    return new CommandResult(CommandResultKind.GoBack);
  }

  /** Hide the command palette without dismissing. */
  static hide(): CommandResult {
    return new CommandResult(CommandResultKind.Hide);
  }

  /** Keep the command palette open. */
  static keepOpen(): CommandResult {
    return new CommandResult(CommandResultKind.KeepOpen);
  }

  /**
   * Navigate to a specific page.
   * @param pageId The ID of the page to navigate to
   * @param mode The navigation mode (default: Push)
   */
  static goToPage(pageId: string, mode: NavigationMode = NavigationMode.Push): CommandResult {
    const args: IGoToPageArgs = { pageId, mode };
    return new CommandResult(CommandResultKind.GoToPage, args);
  }

  /**
   * Show a toast notification.
   * @param message The message to display
   * @param dismissAfterMs Optional auto-dismiss time in milliseconds
   */
  static showToast(message: string, dismissAfterMs?: number): CommandResult {
    const args: IToastArgs = { message, dismissAfterMs };
    return new CommandResult(CommandResultKind.ShowToast, args);
  }

  /**
   * Show a confirmation dialog.
   * @param title The dialog title
   * @param description The dialog description
   * @param primaryCommand The command to execute on confirmation
   */
  static confirm(title: string, description: string, primaryCommand: ICommand): CommandResult {
    const args: IConfirmationArgs = { title, description, primaryCommand };
    return new CommandResult(CommandResultKind.Confirm, args);
  }
}
