// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace AllApps;

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
