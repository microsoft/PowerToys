// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerToys.TryRun.Core;

public sealed record RunEnvironment(string Backend, string EntryPoint, string WorkingFolder, IReadOnlyList<string> ReadOnlyFolders, IReadOnlyList<string> WritableFolders, string Network, string UserInterface, string Resources, int TimeoutSeconds, string NativeVersion)
{
    public string Describe() => $"Backend: {Backend}\nEntry: {EntryPoint}\nWorking folder: {WorkingFolder}\nRead-only grants: {string.Join("; ", ReadOnlyFolders)}\nWritable grants: {string.Join("; ", WritableFolders)}\nNetwork: {Network}\nUI: {UserInterface}\nResources: {Resources}\nTime limit: {TimeoutSeconds} seconds\nMXC: {NativeVersion}\nThese are configured restrictions; individual observations are listed below.";
}
