// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.Helpers;

internal enum IconLoadInputKind
{
    Empty,
    String,
    ShellBinary,
    Stream,
    SpecializedAppIcon,
    GeneratedSwatch,
    GeneratedInitials,
    SvgFile,
    SvgInline,
    ThemedSvgFile,
    ThemedSvgInline,
    ShellItemIcon,
}
