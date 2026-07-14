// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import type { IInvokableCommand, CommandResult, IconInfo } from '../types'
import { ExtensionHost } from '../runtime/ExtensionHost'

/**
 * A command that does nothing when invoked — returns KeepOpen.
 */
export class NoOpCommand implements IInvokableCommand {
  id: string
  name: string
  icon?: IconInfo | null

  constructor(id: string = 'noop', name: string = '') {
    this.id = id
    this.name = name
  }

  invoke(): CommandResult {
    return { kind: 'keepOpen' }
  }
}

/**
 * A command that opens a URL when invoked.
 */
export class OpenUrlCommand implements IInvokableCommand {
  id: string
  name: string
  icon?: IconInfo | null
  private readonly url: string

  constructor(url: string, name?: string) {
    this.id = `open-url-${url}`
    this.name = name ?? url
    this.url = url
  }

  invoke(): CommandResult {
    return { kind: 'dismiss' }
  }

  getUrl(): string {
    return this.url
  }
}

/**
 * A command that copies text to clipboard and shows a toast notification.
 */
export class CopyTextCommand implements IInvokableCommand {
  id: string
  name: string
  icon?: IconInfo | null
  private readonly text: string
  private readonly toastMessage: string

  constructor(text: string, name?: string, toastMessage?: string) {
    this.id = `copy-text-${text.substring(0, 20)}`
    this.name = name ?? 'Copy'
    this.text = text
    this.toastMessage = toastMessage ?? 'Copied to clipboard'
  }

  invoke(): CommandResult {
    ExtensionHost.copyToClipboard(this.text)
    return { kind: 'showToast', args: { message: this.toastMessage } }
  }
}

/**
 * A command that wraps another command with a confirmation dialog.
 */
export class ConfirmableCommand implements IInvokableCommand {
  id: string
  name: string
  icon?: IconInfo | null

  private readonly title: string
  private readonly description: string
  private readonly primaryCommand: IInvokableCommand
  private readonly isCritical: boolean

  constructor(options: {
    id: string
    name: string
    title: string
    description: string
    primaryCommand: IInvokableCommand
    isCritical?: boolean
    icon?: IconInfo | null
  }) {
    this.id = options.id
    this.name = options.name
    this.title = options.title
    this.description = options.description
    this.primaryCommand = options.primaryCommand
    this.isCritical = options.isCritical ?? false
    this.icon = options.icon
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
    }
  }
}
