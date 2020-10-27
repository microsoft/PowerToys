// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Windows.Controls;

namespace FancyZonesEditor
{
    public class SplitEventArgs : EventArgs
    {
        public SplitEventArgs()
        {
        }

        public SplitEventArgs(Orientation orientation, double offset, double thickness)
        {
            Orientation = orientation;
            Offset = offset;
            Space = thickness;
        }

        public Orientation Orientation { get; }

        public double Offset { get; }

        public double Space { get; }
    }

    public delegate void SplitEventHandler(object sender, SplitEventArgs args);
}
