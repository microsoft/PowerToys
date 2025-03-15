// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Microsoft.Plugin.Program.UnitTests")]

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class MatchOption
{
    /// <summary>
    /// Gets or sets prefix of match char, use for highlight
    /// </summary>
    [Obsolete("this is never used")]
    public string Prefix { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets suffix of match char, use for highlight
    /// </summary>
    [Obsolete("this is never used")]
    public string Suffix { get; set; } = string.Empty;

    public bool IgnoreCase { get; set; } = true;
}
