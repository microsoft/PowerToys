// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using WorkspacesEditor.Models;

namespace WorkspacesEditor.Messages
{
    /// <summary>
    /// Sent by ViewModel to request navigation to the editor page for a project.
    /// </summary>
    public sealed class NavigateToEditorMessage
    {
        public Project Project { get; }

        public NavigateToEditorMessage(Project project)
        {
            Project = project;
        }
    }
}
