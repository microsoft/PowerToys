// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Text;
using System.Threading;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToys.Interop;
using PowerToysExtension.Properties;

namespace PowerToysExtension.Commands;

/// <summary>
/// Opens the Seamless Multi-Screen Cursor calibration overlay via the shared calibrate event.
/// </summary>
internal sealed partial class CalibrateSeamlessCursorCommand : InvokableCommand
{
    private static readonly CompositeFormat CalibrateFailedFormat = CompositeFormat.Parse(Resources.MouseUtils_SeamlessCursor_CalibrateFailed_Format);

    public CalibrateSeamlessCursorCommand()
    {
        Name = Resources.MouseUtils_SeamlessCursor_Title;
    }

    public override CommandResult Invoke()
    {
        try
        {
            using var evt = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.SeamlessCursorCalibrateEvent());
            evt.Set();
            return CommandResult.Dismiss();
        }
        catch (Exception ex)
        {
            return CommandResult.ShowToast(string.Format(CultureInfo.CurrentCulture, CalibrateFailedFormat, ex.Message));
        }
    }
}
