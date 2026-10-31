// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor.UnitTests;

internal sealed class SettingsTestFile : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("PerformanceMonitorTests-");

    public string FilePath => Path.Combine(_directory.FullName, "performanceMonitor.settings.json");

    public void Dispose() => _directory.Delete(true);
}
