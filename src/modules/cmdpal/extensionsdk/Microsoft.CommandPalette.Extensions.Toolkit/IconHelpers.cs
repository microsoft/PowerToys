// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public sealed class IconHelpers
{
    public static IconInfo FromRelativePath(string path) => new(Path.Combine(AppDomain.CurrentDomain.BaseDirectory.ToString(), path));

    public static IconInfo FromRelativePaths(string lightIcon, string darkIcon) =>
        new(
            new(Path.Combine(AppDomain.CurrentDomain.BaseDirectory.ToString(), lightIcon)),
            new(Path.Combine(AppDomain.CurrentDomain.BaseDirectory.ToString(), darkIcon)));
}
