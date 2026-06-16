"use strict";
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
Object.defineProperty(exports, "__esModule", { value: true });
exports.ConfirmableCommand = exports.CopyTextCommand = exports.OpenUrlCommand = exports.NoOpCommand = void 0;
/**
 * A command that does nothing when invoked — returns KeepOpen.
 */
class NoOpCommand {
    id;
    name;
    icon;
    constructor(id = 'noop', name = '') {
        this.id = id;
        this.name = name;
    }
    invoke() {
        return { kind: 'keepOpen' };
    }
}
exports.NoOpCommand = NoOpCommand;
/**
 * A command that opens a URL when invoked.
 */
class OpenUrlCommand {
    id;
    name;
    icon;
    url;
    constructor(url, name) {
        this.id = `open-url-${url}`;
        this.name = name ?? url;
        this.url = url;
    }
    invoke() {
        return { kind: 'dismiss' };
    }
    getUrl() {
        return this.url;
    }
}
exports.OpenUrlCommand = OpenUrlCommand;
/**
 * A command that copies text to clipboard and shows a toast notification.
 */
class CopyTextCommand {
    id;
    name;
    icon;
    text;
    toastMessage;
    constructor(text, name, toastMessage) {
        this.id = `copy-text-${text.substring(0, 20)}`;
        this.name = name ?? 'Copy';
        this.text = text;
        this.toastMessage = toastMessage ?? 'Copied to clipboard';
    }
    invoke() {
        return { kind: 'showToast', args: { message: this.toastMessage } };
    }
    getText() {
        return this.text;
    }
}
exports.CopyTextCommand = CopyTextCommand;
/**
 * A command that wraps another command with a confirmation dialog.
 */
class ConfirmableCommand {
    id;
    name;
    icon;
    title;
    description;
    primaryCommand;
    isCritical;
    constructor(options) {
        this.id = options.id;
        this.name = options.name;
        this.title = options.title;
        this.description = options.description;
        this.primaryCommand = options.primaryCommand;
        this.isCritical = options.isCritical ?? false;
        this.icon = options.icon;
    }
    invoke() {
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
exports.ConfirmableCommand = ConfirmableCommand;
//# sourceMappingURL=commands.js.map