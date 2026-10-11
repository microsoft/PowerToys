// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerToys.ProtectedStorage;

public static class StorageIdentity
{
    public static string ProductName => "PowerToysProtectedStorage";

    public static bool IsLocalTest => false;

    public static string WorkspacesDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "PowerToys", "Workspaces");
}
