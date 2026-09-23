// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;

namespace WorkspacesCsharpLibrary.Data;

public static class WorkspacesStorage
{
    public static async Task<IReadOnlyList<ProjectWrapper>> LoadAsync(CancellationToken cancellationToken = default)
    {
        return (await new WorkspacesRepository().LoadAsync(cancellationToken).ConfigureAwait(false)).Projects;
    }

    public static string GetLegacyFilePath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "Microsoft", "PowerToys", "Workspaces", "workspaces.json");
    }
}
