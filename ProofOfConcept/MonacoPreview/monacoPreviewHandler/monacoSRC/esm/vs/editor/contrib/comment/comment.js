/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import * as nls from '../../../nls.js';
import { KeyChord } from '../../../base/common/keyCodes.js';
import { EditorAction, registerEditorAction } from '../../browser/editorExtensions.js';
import { Range } from '../../common/core/range.js';
import { EditorContextKeys } from '../../common/editorContextKeys.js';
import { BlockCommentCommand } from './blockCommentCommand.js';
import { LineCommentCommand } from './lineCommentCommand.js';
import { MenuId } from '../../../platform/actions/common/actions.js';
class CommentLineAction extends EditorAction {
    constructor(type, opts) {
        super(opts);
        this._type = type;
    }
    run(accessor, editor) {
        if (!editor.hasModel()) {
            return;
        }
        const model = editor.getModel();
        const commands = [];
        const modelOptions = model.getOptions();
        const commentsOptions = editor.getOption(16 /* comments */);
        const selections = editor.getSelections().map((selection, index) => ({ selection, index, ignoreFirstLine: false }));
        selections.sort((a, b) => Range.compareRangesUsingStarts(a.selection, b.selection));
        // Remove selections that would result in copying the same line
        let prev = selections[0];
        for (let i = 1; i < selections.length; i++) {
            const curr = selections[i];
            if (prev.selection.endLineNumber === curr.selection.startLineNumber) {
                // these two selections would copy the same line
                if (prev.index < curr.index) {
                    // prev wins
                    curr.ignoreFirstLine = true;
                }
                else {
                    // curr wins
                    prev.ignoreFirstLine = true;
                    prev = curr;
                }
            }
        }
        for (const selection of selections) {
            commands.push(new LineCommentCommand(selection.selection, modelOptions.tabSize, this._type, commentsOptions.insertSpace, commentsOptions.ignoreEmptyLines, selection.ignoreFirstLine));
        }
        editor.pushUndoStop();
        editor.executeCommands(this.id, commands);
        editor.pushUndoStop();
    }
}
class ToggleCommentLineAction extends CommentLineAction {
    constructor() {
        super(0 /* Toggle */, {
            id: 'editor.action.commentLine',
            label: nls.localize('comment.line', "Toggle Line Comment"),
            alias: 'Toggle Line Comment',
            precondition: EditorContextKeys.writable,
            kbOpts: {
                kbExpr: EditorContextKeys.editorTextFocus,
                primary: 2048 /* CtrlCmd */ | 85 /* US_SLASH */,
                weight: 100 /* EditorContrib */
            },
            menuOpts: {
                menuId: MenuId.MenubarEditMenu,
                group: '5_insert',
                title: nls.localize({ key: 'miToggleLineComment', comment: ['&& denotes a mnemonic'] }, "&&Toggle Line Comment"),
                order: 1
            }
        });
    }
}
class AddLineCommentAction extends CommentLineAction {
    constructor() {
        super(1 /* ForceAdd */, {
            id: 'editor.action.addCommentLine',
            label: nls.localize('comment.line.add', "Add Line Comment"),
            alias: 'Add Line Comment',
            precondition: EditorContextKeys.writable,
            kbOpts: {
                kbExpr: EditorContextKeys.editorTextFocus,
                primary: KeyChord(2048 /* CtrlCmd */ | 41 /* KEY_K */, 2048 /* CtrlCmd */ | 33 /* KEY_C */),
                weight: 100 /* EditorContrib */
            }
        });
    }
}
class RemoveLineCommentAction extends CommentLineAction {
    constructor() {
        super(2 /* ForceRemove */, {
            id: 'editor.action.removeCommentLine',
            label: nls.localize('comment.line.remove', "Remove Line Comment"),
            alias: 'Remove Line Comment',
            precondition: EditorContextKeys.writable,
            kbOpts: {
                kbExpr: EditorContextKeys.editorTextFocus,
                primary: KeyChord(2048 /* CtrlCmd */ | 41 /* KEY_K */, 2048 /* CtrlCmd */ | 51 /* KEY_U */),
                weight: 100 /* EditorContrib */
            }
        });
    }
}
class BlockCommentAction extends EditorAction {
    constructor() {
        super({
            id: 'editor.action.blockComment',
            label: nls.localize('comment.block', "Toggle Block Comment"),
            alias: 'Toggle Block Comment',
            precondition: EditorContextKeys.writable,
            kbOpts: {
                kbExpr: EditorContextKeys.editorTextFocus,
                primary: 1024 /* Shift */ | 512 /* Alt */ | 31 /* KEY_A */,
                linux: { primary: 2048 /* CtrlCmd */ | 1024 /* Shift */ | 31 /* KEY_A */ },
                weight: 100 /* EditorContrib */
            },
            menuOpts: {
                menuId: MenuId.MenubarEditMenu,
                group: '5_insert',
                title: nls.localize({ key: 'miToggleBlockComment', comment: ['&& denotes a mnemonic'] }, "Toggle &&Block Comment"),
                order: 2
            }
        });
    }
    run(accessor, editor) {
        if (!editor.hasModel()) {
            return;
        }
        const commentsOptions = editor.getOption(16 /* comments */);
        const commands = [];
        const selections = editor.getSelections();
        for (const selection of selections) {
            commands.push(new BlockCommentCommand(selection, commentsOptions.insertSpace));
        }
        editor.pushUndoStop();
        editor.executeCommands(this.id, commands);
        editor.pushUndoStop();
    }
}
registerEditorAction(ToggleCommentLineAction);
registerEditorAction(AddLineCommentAction);
registerEditorAction(RemoveLineCommentAction);
registerEditorAction(BlockCommentAction);
