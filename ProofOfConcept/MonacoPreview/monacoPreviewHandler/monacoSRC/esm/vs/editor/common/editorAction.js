/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
export class InternalEditorAction {
    constructor(id, label, alias, precondition, run, contextKeyService) {
        this.id = id;
        this.label = label;
        this.alias = alias;
        this._precondition = precondition;
        this._run = run;
        this._contextKeyService = contextKeyService;
    }
    isSupported() {
        return this._contextKeyService.contextMatchesRules(this._precondition);
    }
    run() {
        if (!this.isSupported()) {
            return Promise.resolve(undefined);
        }
        return this._run();
    }
}
