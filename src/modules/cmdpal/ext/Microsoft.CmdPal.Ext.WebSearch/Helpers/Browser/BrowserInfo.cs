// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Ext.WebSearch.Helpers.Browser;

public record BrowserInfo
{
    public required string Path { get; init; }

    public required string Name { get; init; }

    public string? ArgumentsPattern { get; init; }
}
