"use strict";
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
Object.defineProperty(exports, "__esModule", { value: true });
exports.ListItem = exports.CommandItem = exports.NoOpCommand = exports.InvokableCommand = exports.Command = void 0;
/**
 * Base class for commands.
 */
class Command {
    constructor() {
        this.id = '';
        this.name = '';
    }
    _initializeWithTransport(transport) {
        this._transport = transport;
    }
    notifyPropChanged(propertyName) {
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
    toJSON() {
        return { id: this.id, name: this.name, icon: this.icon };
    }
}
exports.Command = Command;
/**
 * Base class for invokable commands that execute an action.
 */
class InvokableCommand extends Command {
}
exports.InvokableCommand = InvokableCommand;
/**
 * A command that does nothing when invoked. Useful for gallery/grid items
 * that only need to display content without an action.
 */
class NoOpCommand extends InvokableCommand {
    constructor(id, name) {
        super();
        this.id = id ?? `noop-${++NoOpCommand._counter}`;
        this.name = name ?? '';
    }
    invoke() {
        return { kind: 0 }; // Dismiss
    }
}
exports.NoOpCommand = NoOpCommand;
NoOpCommand._counter = 0;
/**
 * Represents a command item shown in the UI.
 */
class CommandItem {
    constructor(options) {
        this.title = '';
        this.subtitle = '';
        this.moreCommands = [];
        if (options) {
            Object.assign(this, options);
        }
    }
    _initializeWithTransport(transport) {
        this._transport = transport;
    }
    notifyPropChanged(propertyName) {
        if (this.PropChanged) {
            this.PropChanged({ propertyName });
        }
        if (this._transport) {
            const commandId = this.command && 'id' in this.command ? this.command.id : '';
            this._transport.sendNotification('command/propChanged', {
                commandId,
                propertyName,
            });
        }
    }
}
exports.CommandItem = CommandItem;
/**
 * Represents a list item with additional metadata.
 */
class ListItem extends CommandItem {
    constructor(options) {
        super(options);
        this.tags = [];
        this.section = '';
        this.textToSuggest = '';
        if (options) {
            Object.assign(this, options);
        }
    }
}
exports.ListItem = ListItem;
//# sourceMappingURL=command.js.map