// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using WorkspacesEditor.Models;

namespace WorkspacesEditor.Services
{
    internal interface IWorkspaceShortcutService
    {
        bool Exists(Project project);

        void Apply(Project project);

        void Remove(Project project);
    }
}
