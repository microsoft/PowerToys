/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { createDecorator } from '../../../platform/instantiation/common/instantiation.js';
import { URI } from '../../../base/common/uri.js';
import { isObject } from '../../../base/common/types.js';
export const IBulkEditService = createDecorator('IWorkspaceEditService');
function isWorkspaceFileEdit(thing) {
    return isObject(thing) && (Boolean(thing.newUri) || Boolean(thing.oldUri));
}
function isWorkspaceTextEdit(thing) {
    return isObject(thing) && URI.isUri(thing.resource) && isObject(thing.edit);
}
export class ResourceEdit {
    constructor(metadata) {
        this.metadata = metadata;
    }
    static convert(edit) {
        return edit.edits.map(edit => {
            if (isWorkspaceTextEdit(edit)) {
                return new ResourceTextEdit(edit.resource, edit.edit, edit.modelVersionId, edit.metadata);
            }
            if (isWorkspaceFileEdit(edit)) {
                return new ResourceFileEdit(edit.oldUri, edit.newUri, edit.options, edit.metadata);
            }
            throw new Error('Unsupported edit');
        });
    }
}
export class ResourceTextEdit extends ResourceEdit {
    constructor(resource, textEdit, versionId, metadata) {
        super(metadata);
        this.resource = resource;
        this.textEdit = textEdit;
        this.versionId = versionId;
        this.metadata = metadata;
    }
}
export class ResourceFileEdit extends ResourceEdit {
    constructor(oldResource, newResource, options, metadata) {
        super(metadata);
        this.oldResource = oldResource;
        this.newResource = newResource;
        this.options = options;
        this.metadata = metadata;
    }
}
