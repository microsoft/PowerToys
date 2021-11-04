/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { registerEditorCommand } from '../../browser/editorExtensions.js';
import { WordPartOperations } from '../../common/controller/cursorWordOperations.js';
import { Range } from '../../common/core/range.js';
import { EditorContextKeys } from '../../common/editorContextKeys.js';
import { DeleteWordCommand, MoveWordCommand } from '../wordOperations/wordOperations.js';
import { CommandsRegistry } from '../../../platform/commands/common/commands.js';
export class DeleteWordPartLeft extends DeleteWordCommand {
    constructor() {
        super({
            whitespaceHeuristics: true,
            wordNavigationType: 0 /* WordStart */,
            id: 'deleteWordPartLeft',
            precondition: EditorContextKeys.writable,
            kbOpts: {
                kbExpr: EditorContextKeys.textInputFocus,
                primary: 0,
                mac: { primary: 256 /* WinCtrl */ | 512 /* Alt */ | 1 /* Backspace */ },
                weight: 100 /* EditorContrib */
            }
        });
    }
    _delete(ctx, wordNavigationType) {
        let r = WordPartOperations.deleteWordPartLeft(ctx);
        if (r) {
            return r;
        }
        return new Range(1, 1, 1, 1);
    }
}
export class DeleteWordPartRight extends DeleteWordCommand {
    constructor() {
        super({
            whitespaceHeuristics: true,
            wordNavigationType: 2 /* WordEnd */,
            id: 'deleteWordPartRight',
            precondition: EditorContextKeys.writable,
            kbOpts: {
                kbExpr: EditorContextKeys.textInputFocus,
                primary: 0,
                mac: { primary: 256 /* WinCtrl */ | 512 /* Alt */ | 20 /* Delete */ },
                weight: 100 /* EditorContrib */
            }
        });
    }
    _delete(ctx, wordNavigationType) {
        let r = WordPartOperations.deleteWordPartRight(ctx);
        if (r) {
            return r;
        }
        const lineCount = ctx.model.getLineCount();
        const maxColumn = ctx.model.getLineMaxColumn(lineCount);
        return new Range(lineCount, maxColumn, lineCount, maxColumn);
    }
}
export class WordPartLeftCommand extends MoveWordCommand {
    _move(wordSeparators, model, position, wordNavigationType) {
        return WordPartOperations.moveWordPartLeft(wordSeparators, model, position);
    }
}
export class CursorWordPartLeft extends WordPartLeftCommand {
    constructor() {
        super({
            inSelectionMode: false,
            wordNavigationType: 0 /* WordStart */,
            id: 'cursorWordPartLeft',
            precondition: undefined,
            kbOpts: {
                kbExpr: EditorContextKeys.textInputFocus,
                primary: 0,
                mac: { primary: 256 /* WinCtrl */ | 512 /* Alt */ | 15 /* LeftArrow */ },
                weight: 100 /* EditorContrib */
            }
        });
    }
}
// Register previous id for compatibility purposes
CommandsRegistry.registerCommandAlias('cursorWordPartStartLeft', 'cursorWordPartLeft');
export class CursorWordPartLeftSelect extends WordPartLeftCommand {
    constructor() {
        super({
            inSelectionMode: true,
            wordNavigationType: 0 /* WordStart */,
            id: 'cursorWordPartLeftSelect',
            precondition: undefined,
            kbOpts: {
                kbExpr: EditorContextKeys.textInputFocus,
                primary: 0,
                mac: { primary: 256 /* WinCtrl */ | 512 /* Alt */ | 1024 /* Shift */ | 15 /* LeftArrow */ },
                weight: 100 /* EditorContrib */
            }
        });
    }
}
// Register previous id for compatibility purposes
CommandsRegistry.registerCommandAlias('cursorWordPartStartLeftSelect', 'cursorWordPartLeftSelect');
export class WordPartRightCommand extends MoveWordCommand {
    _move(wordSeparators, model, position, wordNavigationType) {
        return WordPartOperations.moveWordPartRight(wordSeparators, model, position);
    }
}
export class CursorWordPartRight extends WordPartRightCommand {
    constructor() {
        super({
            inSelectionMode: false,
            wordNavigationType: 2 /* WordEnd */,
            id: 'cursorWordPartRight',
            precondition: undefined,
            kbOpts: {
                kbExpr: EditorContextKeys.textInputFocus,
                primary: 0,
                mac: { primary: 256 /* WinCtrl */ | 512 /* Alt */ | 17 /* RightArrow */ },
                weight: 100 /* EditorContrib */
            }
        });
    }
}
export class CursorWordPartRightSelect extends WordPartRightCommand {
    constructor() {
        super({
            inSelectionMode: true,
            wordNavigationType: 2 /* WordEnd */,
            id: 'cursorWordPartRightSelect',
            precondition: undefined,
            kbOpts: {
                kbExpr: EditorContextKeys.textInputFocus,
                primary: 0,
                mac: { primary: 256 /* WinCtrl */ | 512 /* Alt */ | 1024 /* Shift */ | 17 /* RightArrow */ },
                weight: 100 /* EditorContrib */
            }
        });
    }
}
registerEditorCommand(new DeleteWordPartLeft());
registerEditorCommand(new DeleteWordPartRight());
registerEditorCommand(new CursorWordPartLeft());
registerEditorCommand(new CursorWordPartLeftSelect());
registerEditorCommand(new CursorWordPartRight());
registerEditorCommand(new CursorWordPartRightSelect());
