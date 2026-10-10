// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.CmdPal.Ext.Apps.Catalog.Sources;

/// <summary>
/// Represents one user-configured directory with an explicit discovery profile and depth limit.
/// </summary>
internal sealed class CustomDirectoryAppSource : DirectoryWin32ProgramSource
{
    public override string Id { get; }

    public override int Priority => 0;

    public override bool IsEnabled => true;

    public override Win32ProgramSourceProfile Profile { get; }

    public override int MaximumDepth { get; }

    /// <summary>Initializes a new instance of the <see cref="CustomDirectoryAppSource"/> class. Creates a custom-folder discovery source with explicit suffix, depth, and interpretation rules.</summary>
    public CustomDirectoryAppSource(
        string idPrefix,
        string directory,
        IReadOnlyList<string> suffixes,
        Win32ProgramSourceProfile profile,
        int maximumDepth)
        : base([directory ?? throw new ArgumentNullException(nameof(directory))], suffixes)
    {
        if (string.IsNullOrWhiteSpace(idPrefix))
        {
            throw new ArgumentException("A custom source ID prefix is required.", nameof(idPrefix));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(maximumDepth);

        Id = $"{idPrefix}:{directory}";
        Profile = profile;
        MaximumDepth = maximumDepth;
    }
}
