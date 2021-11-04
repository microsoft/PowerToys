import { URI } from '../../../base/common/uri.js';
export const WORKSPACE_EXTENSION = 'code-workspace';
export function isSingleFolderWorkspaceIdentifier(obj) {
    const singleFolderIdentifier = obj;
    return typeof (singleFolderIdentifier === null || singleFolderIdentifier === void 0 ? void 0 : singleFolderIdentifier.id) === 'string' && URI.isUri(singleFolderIdentifier.uri);
}
export function toWorkspaceIdentifier(workspace) {
    // Multi root
    if (workspace.configuration) {
        return {
            id: workspace.id,
            configPath: workspace.configuration
        };
    }
    // Single folder
    if (workspace.folders.length === 1) {
        return {
            id: workspace.id,
            uri: workspace.folders[0].uri
        };
    }
    // Empty workspace
    return undefined;
}
//#endregion
