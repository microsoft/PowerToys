// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace WorkspacesEditor.Services
{
    internal readonly record struct WorkspaceShortcutResult(WorkspaceShortcutStatus Status, bool Created = false);

    internal enum WorkspaceShortcutStatus
    {
        Success,
        NameConflict,
        Failed,
    }
}
