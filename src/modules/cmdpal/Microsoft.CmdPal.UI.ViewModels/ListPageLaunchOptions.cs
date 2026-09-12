// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels;

public sealed record ListPageLaunchOptions(string? Query = null, string? FilterId = null)
{
    public bool IsEmpty => Query is null && FilterId is null;

    public bool RequiresOneTimeConsent => FilterId is not null;
}
