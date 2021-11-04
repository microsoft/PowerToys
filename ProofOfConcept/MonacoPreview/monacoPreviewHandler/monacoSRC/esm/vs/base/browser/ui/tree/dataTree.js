/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { AbstractTree } from './abstractTree.js';
import { ObjectTreeModel } from './objectTreeModel.js';
export class DataTree extends AbstractTree {
    constructor(user, container, delegate, renderers, dataSource, options = {}) {
        super(user, container, delegate, renderers, options);
        this.user = user;
        this.dataSource = dataSource;
        this.identityProvider = options.identityProvider;
    }
    createModel(user, view, options) {
        return new ObjectTreeModel(user, view, options);
    }
}
