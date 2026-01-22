// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

namespace Microsoft.CmdPal.Ext.WebSearch.Helpers.Browser.Providers;

/// <summary>
/// Provides a fallback implementation of the default browser provider that returns information for Microsoft Edge.
/// </summary>
/// <remarks>This class is used when no other default browser provider is available. It supplies the path,
/// arguments pattern, and name for Microsoft Edge as the default browser information.</remarks>
internal sealed class FallbackMsEdgeBrowserProvider : IDefaultBrowserProvider
{
    private const string MsEdgeArgumentsPattern = "--single-argument %1";

    private const string MsEdgeName = "Microsoft Edge";

    private static string MsEdgePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        @"Microsoft\Edge\Application\msedge.exe");

    public BrowserInfo GetDefaultBrowserInfo() => new()
    {
        Path = MsEdgePath,
        ArgumentsPattern = MsEdgeArgumentsPattern,
        Name = MsEdgeName,
    };
}
