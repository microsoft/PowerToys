// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using WorkspacesEditor.Models;

namespace WorkspacesEditor.Services
{
    internal interface IWorkspaceShortcutService
    {
        bool Exists(Project project);

        void Apply(Project project, string previousName = null);

        void Remove(Project project);

        // An unreadable link cannot establish either its ownership or its absence.
        bool? GetPresence(Project project) => Exists(project);

        bool TryPrepareChange(Project original, bool deleting) => true;

        WorkspaceShortcutResult ApplyWithResult(Project project, string previousName = null)
        {
            bool existed = project.IsShortcutNeeded && Exists(project);
            Apply(project, previousName);
            return new(WorkspaceShortcutStatus.Success, project.IsShortcutNeeded && !existed && Exists(project));
        }

        WorkspaceShortcutResult RemoveWithResult(Project project)
        {
            Remove(project);
            return new(WorkspaceShortcutStatus.Success);
        }

        bool RetryPendingRemovals(IEnumerable<string> activeWorkspaceIds) => false;
    }
}
