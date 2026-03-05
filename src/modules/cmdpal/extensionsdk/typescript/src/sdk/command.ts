// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import {
  ICommand,
  IInvokableCommand,
  ICommandItem,
  IListItem,
  IIconInfo,
  IContextItem,
  ITag,
  IDetails,
  ICommandResult,
} from '../generated/types';
import { JsonRpcTransport } from '../transport/json-rpc';

/**
 * Base class for commands.
 */
export class Command implements ICommand {
  id: string = '';
  name: string = '';
  icon?: IIconInfo;

  protected _transport?: JsonRpcTransport;

  PropChanged?: (args: unknown) => void;

  _initializeWithTransport(transport: JsonRpcTransport): void {
    this._transport = transport;
  }

  protected notifyPropChanged(propertyName: string): void {
    if (this.PropChanged) {
      this.PropChanged({ propertyName });
    }
    if (this._transport) {
      this._transport.sendNotification('command/propChanged', {
        commandId: this.id,
        propertyName,
      });
    }
  }

  // Only serialize public interface fields — exclude _transport, provider refs, etc.
  toJSON(): Record<string, unknown> {
    return { id: this.id, name: this.name, icon: this.icon };
  }
}

/**
 * Base class for invokable commands that execute an action.
 */
export abstract class InvokableCommand extends Command implements IInvokableCommand {
  abstract invoke(sender?: unknown): ICommandResult;
}

/**
 * A command that does nothing when invoked. Useful for gallery/grid items
 * that only need to display content without an action.
 */
export class NoOpCommand extends InvokableCommand {
  private static _counter = 0;

  constructor(id?: string, name?: string) {
    super();
    this.id = id ?? `noop-${++NoOpCommand._counter}`;
    this.name = name ?? '';
  }

  invoke(): ICommandResult {
    return { kind: 0 } as ICommandResult; // Dismiss
  }
}

/**
 * Represents a command item shown in the UI.
 */
export class CommandItem implements ICommandItem {
  title: string = '';
  subtitle: string = '';
  icon?: IIconInfo;
  command?: ICommand;
  moreCommands: IContextItem[] = [];

  protected _transport?: JsonRpcTransport;

  PropChanged?: (args: unknown) => void;

  constructor(options?: Partial<CommandItem>) {
    if (options) {
      Object.assign(this, options);
    }
  }

  _initializeWithTransport(transport: JsonRpcTransport): void {
    this._transport = transport;
  }

  protected notifyPropChanged(propertyName: string): void {
    if (this.PropChanged) {
      this.PropChanged({ propertyName });
    }
    if (this._transport) {
      const commandId = this.command && 'id' in this.command ? (this.command as Command).id : '';
      this._transport.sendNotification('command/propChanged', {
        commandId,
        propertyName,
      });
    }
  }
}

/**
 * Represents a list item with additional metadata.
 */
export class ListItem extends CommandItem implements IListItem {
  tags: ITag[] = [];
  details?: IDetails;
  section: string = '';
  textToSuggest: string = '';

  constructor(options?: Partial<ListItem>) {
    super(options);
    if (options) {
      Object.assign(this, options);
    }
  }
}
