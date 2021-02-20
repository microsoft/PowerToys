/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { EditorCommand, registerEditorCommand } from '../editorExtensions.js';
import { IContextKeyService, RawContextKey } from '../../../platform/contextkey/common/contextkey.js';
import { CancellationTokenSource } from '../../../base/common/cancellation.js';
import { LinkedList } from '../../../base/common/linkedList.js';
import { createDecorator } from '../../../platform/instantiation/common/instantiation.js';
import { registerSingleton } from '../../../platform/instantiation/common/extensions.js';
const IEditorCancellationTokens = createDecorator('IEditorCancelService');
const ctxCancellableOperation = new RawContextKey('cancellableOperation', false);
registerSingleton(IEditorCancellationTokens, class {
    constructor() {
        this._tokens = new WeakMap();
    }
    add(editor, cts) {
        let data = this._tokens.get(editor);
        if (!data) {
            data = editor.invokeWithinContext(accessor => {
                const key = ctxCancellableOperation.bindTo(accessor.get(IContextKeyService));
                const tokens = new LinkedList();
                return { key, tokens };
            });
            this._tokens.set(editor, data);
        }
        let removeFn;
        data.key.set(true);
        removeFn = data.tokens.push(cts);
        return () => {
            // remove w/o cancellation
            if (removeFn) {
                removeFn();
                data.key.set(!data.tokens.isEmpty());
                removeFn = undefined;
            }
        };
    }
    cancel(editor) {
        const data = this._tokens.get(editor);
        if (!data) {
            return;
        }
        // remove with cancellation
        const cts = data.tokens.pop();
        if (cts) {
            cts.cancel();
            data.key.set(!data.tokens.isEmpty());
        }
    }
}, true);
export class EditorKeybindingCancellationTokenSource extends CancellationTokenSource {
    constructor(editor, parent) {
        super(parent);
        this.editor = editor;
        this._unregister = editor.invokeWithinContext(accessor => accessor.get(IEditorCancellationTokens).add(editor, this));
    }
    dispose() {
        this._unregister();
        super.dispose();
    }
}
registerEditorCommand(new class extends EditorCommand {
    constructor() {
        super({
            id: 'editor.cancelOperation',
            kbOpts: {
                weight: 100 /* EditorContrib */,
                primary: 9 /* Escape */
            },
            precondition: ctxCancellableOperation
        });
    }
    runEditorCommand(accessor, editor) {
        accessor.get(IEditorCancellationTokens).cancel(editor);
    }
});
