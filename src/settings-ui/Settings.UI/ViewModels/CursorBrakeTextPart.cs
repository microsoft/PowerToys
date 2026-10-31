// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    /// <summary>
    /// A piece of an edge editor line: plain text, or a monitor shown as its numbered icon followed
    /// by <see cref="Text"/>. <see cref="Plain"/> is the same piece as text alone, with the number
    /// written out, for screen readers and anything else that reads the line as a string.
    /// </summary>
    public sealed record CursorBrakeTextPart(string Text, string Plain, int MonitorNumber = 0, bool BuiltIn = false)
    {
        public bool IsMonitor => MonitorNumber > 0;
    }
}
