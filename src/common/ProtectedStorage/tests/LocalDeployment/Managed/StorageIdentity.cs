// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerToys.ProtectedStorage;

public static class StorageIdentity
{
    public static string ProductName => "PowerToysProtectedStorageLocalTest";

    public static bool IsLocalTest => true;

#if POWERTOYS_LOCAL_E2E_BUILD
    public static string WorkspacesDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "PowerToysLocalE2E", "Workspaces");
#else
    public static string WorkspacesDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ProductName, "Workspaces");
#endif
}
