// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace HostsUILib.Helpers
{
    public interface IBackupManager
    {
        void CreateBackup(string hostsFilePath);

        void DeleteBackups();
    }
}
