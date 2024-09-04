// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace WindowsCommandPalette.BuiltinCommands.AllApps;

internal sealed class AppItem
{
    public string Name { get; set; } = string.Empty;

    public string Subtitle { get; set; } = string.Empty;

    public string IcoPath { get; set; } = string.Empty;

    public string ExePath { get; set; } = string.Empty;

    public string DirPath { get; set; } = string.Empty;

    public string UserModelId { get; set; } = string.Empty;

    public AppItem()
    {
    }
}
