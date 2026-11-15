// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.Helpers;

[Flags]
internal enum IconRequestReason
{
    None = 0,
    SourceChanged = 1 << 0,
    HandlerAttached = 1 << 1,
    Loaded = 1 << 2,
    ThemeChanged = 1 << 3,
    ScaleChanged = 1 << 4,
    Retry = 1 << 5,
}
