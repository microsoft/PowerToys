// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.FileLocksmith.UITests;

/// <summary>Which Explorer context-menu surface a probe should drive.</summary>
internal enum ContextMenuTier
{
    /// <summary>Whatever the OS shows on a plain right-click: tier-1 on Windows 11, classic on Windows 10.</summary>
    Default,

    /// <summary>The classic <c>#32768</c> menu, reached through "Show more options" on Windows 11.</summary>
    Classic,
}
