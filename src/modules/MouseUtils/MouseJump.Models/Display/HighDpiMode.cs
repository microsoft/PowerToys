// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace MouseJump.Models.Display;

/// <summary>
/// See https://github.com/dotnet/winforms/blob/main/src/System.Windows.Forms.Primitives/src/System/Windows/Forms/HighDpiMode.cs#L26
/// </summary>
public enum HighDpiMode
{
    DpiUnaware,
    SystemAware,
    PerMonitor,
    PerMonitorV2,
    DpiUnawareGdiScaled,
}
