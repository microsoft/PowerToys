/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { createDecorator } from '../../instantiation/common/instantiation.js';
import { TernarySearchTree } from '../../../base/common/map.js';
export const IWorkspaceContextService = createDecorator('contextService');
export class Workspace {
    constructor(_id, folders, _configuration, _ignorePathCasing) {
        this._id = _id;
        this._configuration = _configuration;
        this._ignorePathCasing = _ignorePathCasing;
        this._foldersMap = TernarySearchTree.forUris(this._ignorePathCasing);
        this.folders = folders;
    }
    get folders() {
        return this._folders;
    }
    set folders(folders) {
        this._folders = folders;
        this.updateFoldersMap();
    }
    get id() {
        return this._id;
    }
    get configuration() {
        return this._configuration;
    }
    set configuration(configuration) {
        this._configuration = configuration;
    }
    getFolder(resource) {
        if (!resource) {
            return null;
        }
        return this._foldersMap.findSubstr(resource.with({
            scheme: resource.scheme,
            authority: resource.authority,
            path: resource.path
        })) || null;
    }
    updateFoldersMap() {
        this._foldersMap = TernarySearchTree.forUris(this._ignorePathCasing);
        for (const folder of this.folders) {
            this._foldersMap.set(folder.uri, folder);
        }
    }
    toJSON() {
        return { id: this.id, folders: this.folders, configuration: this.configuration };
    }
}
export class WorkspaceFolder {
    constructor(data, raw) {
        this.raw = raw;
        this.uri = data.uri;
        this.index = data.index;
        this.name = data.name;
    }
    toJSON() {
        return { uri: this.uri, name: this.name, index: this.index };
    }
}
