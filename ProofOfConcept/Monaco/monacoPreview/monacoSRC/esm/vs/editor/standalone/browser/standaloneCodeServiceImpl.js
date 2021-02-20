/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { windowOpenNoOpener } from '../../../base/browser/dom.js';
import { Schemas } from '../../../base/common/network.js';
import { CodeEditorServiceImpl } from '../../browser/services/codeEditorServiceImpl.js';
export class StandaloneCodeEditorServiceImpl extends CodeEditorServiceImpl {
    getActiveCodeEditor() {
        return null; // not supported in the standalone case
    }
    openCodeEditor(input, source, sideBySide) {
        if (!source) {
            return Promise.resolve(null);
        }
        return Promise.resolve(this.doOpenEditor(source, input));
    }
    doOpenEditor(editor, input) {
        const model = this.findModel(editor, input.resource);
        if (!model) {
            if (input.resource) {
                const schema = input.resource.scheme;
                if (schema === Schemas.http || schema === Schemas.https) {
                    // This is a fully qualified http or https URL
                    windowOpenNoOpener(input.resource.toString());
                    return editor;
                }
            }
            return null;
        }
        const selection = (input.options ? input.options.selection : null);
        if (selection) {
            if (typeof selection.endLineNumber === 'number' && typeof selection.endColumn === 'number') {
                editor.setSelection(selection);
                editor.revealRangeInCenter(selection, 1 /* Immediate */);
            }
            else {
                const pos = {
                    lineNumber: selection.startLineNumber,
                    column: selection.startColumn
                };
                editor.setPosition(pos);
                editor.revealPositionInCenter(pos, 1 /* Immediate */);
            }
        }
        return editor;
    }
    findModel(editor, resource) {
        const model = editor.getModel();
        if (model && model.uri.toString() !== resource.toString()) {
            return null;
        }
        return model;
    }
}
