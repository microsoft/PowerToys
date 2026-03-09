// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.CmdPal.Ext.RaycastStore.GitHub;

internal sealed class GitHubRateLimit
{
    public int Remaining { get; set; }

    public int Limit { get; set; }

    public DateTimeOffset ResetTime { get; set; }
}
