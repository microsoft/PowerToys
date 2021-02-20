/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import * as assert from '../../../base/common/assert.js';
import { Emitter } from '../../../base/common/event.js';
import { Disposable } from '../../../base/common/lifecycle.js';
import * as objects from '../../../base/common/objects.js';
import { Range } from '../../common/core/range.js';
const defaultOptions = {
    followsCaret: true,
    ignoreCharChanges: true,
    alwaysRevealFirst: true
};
/**
 * Create a new diff navigator for the provided diff editor.
 */
export class DiffNavigator extends Disposable {
    constructor(editor, options = {}) {
        super();
        this._onDidUpdate = this._register(new Emitter());
        this._editor = editor;
        this._options = objects.mixin(options, defaultOptions, false);
        this.disposed = false;
        this.nextIdx = -1;
        this.ranges = [];
        this.ignoreSelectionChange = false;
        this.revealFirst = Boolean(this._options.alwaysRevealFirst);
        // hook up to diff editor for diff, disposal, and caret move
        this._register(this._editor.onDidDispose(() => this.dispose()));
        this._register(this._editor.onDidUpdateDiff(() => this._onDiffUpdated()));
        if (this._options.followsCaret) {
            this._register(this._editor.getModifiedEditor().onDidChangeCursorPosition((e) => {
                if (this.ignoreSelectionChange) {
                    return;
                }
                this.nextIdx = -1;
            }));
        }
        if (this._options.alwaysRevealFirst) {
            this._register(this._editor.getModifiedEditor().onDidChangeModel((e) => {
                this.revealFirst = true;
            }));
        }
        // init things
        this._init();
    }
    _init() {
        let changes = this._editor.getLineChanges();
        if (!changes) {
            return;
        }
    }
    _onDiffUpdated() {
        this._init();
        this._compute(this._editor.getLineChanges());
        if (this.revealFirst) {
            // Only reveal first on first non-null changes
            if (this._editor.getLineChanges() !== null) {
                this.revealFirst = false;
                this.nextIdx = -1;
                this.next(1 /* Immediate */);
            }
        }
    }
    _compute(lineChanges) {
        // new ranges
        this.ranges = [];
        if (lineChanges) {
            // create ranges from changes
            lineChanges.forEach((lineChange) => {
                if (!this._options.ignoreCharChanges && lineChange.charChanges) {
                    lineChange.charChanges.forEach((charChange) => {
                        this.ranges.push({
                            rhs: true,
                            range: new Range(charChange.modifiedStartLineNumber, charChange.modifiedStartColumn, charChange.modifiedEndLineNumber, charChange.modifiedEndColumn)
                        });
                    });
                }
                else {
                    this.ranges.push({
                        rhs: true,
                        range: new Range(lineChange.modifiedStartLineNumber, 1, lineChange.modifiedStartLineNumber, 1)
                    });
                }
            });
        }
        // sort
        this.ranges.sort((left, right) => {
            if (left.range.getStartPosition().isBeforeOrEqual(right.range.getStartPosition())) {
                return -1;
            }
            else if (right.range.getStartPosition().isBeforeOrEqual(left.range.getStartPosition())) {
                return 1;
            }
            else {
                return 0;
            }
        });
        this._onDidUpdate.fire(this);
    }
    _initIdx(fwd) {
        let found = false;
        let position = this._editor.getPosition();
        if (!position) {
            this.nextIdx = 0;
            return;
        }
        for (let i = 0, len = this.ranges.length; i < len && !found; i++) {
            let range = this.ranges[i].range;
            if (position.isBeforeOrEqual(range.getStartPosition())) {
                this.nextIdx = i + (fwd ? 0 : -1);
                found = true;
            }
        }
        if (!found) {
            // after the last change
            this.nextIdx = fwd ? 0 : this.ranges.length - 1;
        }
        if (this.nextIdx < 0) {
            this.nextIdx = this.ranges.length - 1;
        }
    }
    _move(fwd, scrollType) {
        assert.ok(!this.disposed, 'Illegal State - diff navigator has been disposed');
        if (!this.canNavigate()) {
            return;
        }
        if (this.nextIdx === -1) {
            this._initIdx(fwd);
        }
        else if (fwd) {
            this.nextIdx += 1;
            if (this.nextIdx >= this.ranges.length) {
                this.nextIdx = 0;
            }
        }
        else {
            this.nextIdx -= 1;
            if (this.nextIdx < 0) {
                this.nextIdx = this.ranges.length - 1;
            }
        }
        let info = this.ranges[this.nextIdx];
        this.ignoreSelectionChange = true;
        try {
            let pos = info.range.getStartPosition();
            this._editor.setPosition(pos);
            this._editor.revealPositionInCenter(pos, scrollType);
        }
        finally {
            this.ignoreSelectionChange = false;
        }
    }
    canNavigate() {
        return this.ranges && this.ranges.length > 0;
    }
    next(scrollType = 0 /* Smooth */) {
        this._move(true, scrollType);
    }
    previous(scrollType = 0 /* Smooth */) {
        this._move(false, scrollType);
    }
    dispose() {
        super.dispose();
        this.ranges = [];
        this.disposed = true;
    }
}
