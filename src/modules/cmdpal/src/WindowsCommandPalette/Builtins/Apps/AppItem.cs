// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace WindowsCommandPalette.BuiltinCommands.AllApps;

internal sealed class AppItem
{
    public string Name { get; set; } = "";
    public string Subtitle { get; set; } = "";
    public string IcoPath { get; set; } = "";
    public string ExePath { get; set; } = "";
    public string DirPath { get; set; } = "";
    public string UserModelId { get; set; } = "";

    public AppItem()
    {
    }
}
