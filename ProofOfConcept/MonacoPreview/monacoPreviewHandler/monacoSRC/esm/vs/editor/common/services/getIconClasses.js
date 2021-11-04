/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { Schemas } from '../../../base/common/network.js';
import { DataUri, basenameOrAuthority } from '../../../base/common/resources.js';
import { PLAINTEXT_MODE_ID } from '../modes/modesRegistry.js';
import { FileKind } from '../../../platform/files/common/files.js';
export function getIconClasses(modelService, modeService, resource, fileKind) {
    // we always set these base classes even if we do not have a path
    const classes = fileKind === FileKind.ROOT_FOLDER ? ['rootfolder-icon'] : fileKind === FileKind.FOLDER ? ['folder-icon'] : ['file-icon'];
    if (resource) {
        // Get the path and name of the resource. For data-URIs, we need to parse specially
        let name;
        if (resource.scheme === Schemas.data) {
            const metadata = DataUri.parseMetaData(resource);
            name = metadata.get(DataUri.META_DATA_LABEL);
        }
        else {
            name = cssEscape(basenameOrAuthority(resource).toLowerCase());
        }
        // Folders
        if (fileKind === FileKind.FOLDER) {
            classes.push(`${name}-name-folder-icon`);
        }
        // Files
        else {
            // Name & Extension(s)
            if (name) {
                classes.push(`${name}-name-file-icon`);
                const dotSegments = name.split('.');
                for (let i = 1; i < dotSegments.length; i++) {
                    classes.push(`${dotSegments.slice(i).join('.')}-ext-file-icon`); // add each combination of all found extensions if more than one
                }
                classes.push(`ext-file-icon`); // extra segment to increase file-ext score
            }
            // Detected Mode
            const detectedModeId = detectModeId(modelService, modeService, resource);
            if (detectedModeId) {
                classes.push(`${cssEscape(detectedModeId)}-lang-file-icon`);
            }
        }
    }
    return classes;
}
export function detectModeId(modelService, modeService, resource) {
    if (!resource) {
        return null; // we need a resource at least
    }
    let modeId = null;
    // Data URI: check for encoded metadata
    if (resource.scheme === Schemas.data) {
        const metadata = DataUri.parseMetaData(resource);
        const mime = metadata.get(DataUri.META_DATA_MIME);
        if (mime) {
            modeId = modeService.getModeId(mime);
        }
    }
    // Any other URI: check for model if existing
    else {
        const model = modelService.getModel(resource);
        if (model) {
            modeId = model.getModeId();
        }
    }
    // only take if the mode is specific (aka no just plain text)
    if (modeId && modeId !== PLAINTEXT_MODE_ID) {
        return modeId;
    }
    // otherwise fallback to path based detection
    return modeService.getModeIdByFilepathOrFirstLine(resource);
}
export function cssEscape(str) {
    return str.replace(/[\11\12\14\15\40]/g, '/'); // HTML class names can not contain certain whitespace characters, use / instead, which doesn't exist in file names.
}
