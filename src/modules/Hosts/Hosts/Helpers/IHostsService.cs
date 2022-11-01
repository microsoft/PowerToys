// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Hosts.Models;

namespace Hosts.Helpers
{
    public interface IHostsService : IDisposable
    {
        string HostsFilePath { get; }

        event EventHandler FileChanged;

        Task<(string Unparsed, List<Entry> Entries)> ReadAsync();

        Task<bool> WriteAsync(string additionalLines, IEnumerable<Entry> entries);

        Task<bool> PingAsync(string address);

        void CleanupBackup();

        void OpenHostsFile();
    }
}
