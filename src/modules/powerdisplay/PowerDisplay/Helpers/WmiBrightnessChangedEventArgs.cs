// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace PowerDisplay.Helpers
{
    public class WmiBrightnessChangedEventArgs : EventArgs
    {
        public string InstanceName { get; }

        public int Brightness { get; }

        public WmiBrightnessChangedEventArgs(string instanceName, int brightness)
        {
            InstanceName = instanceName;
            Brightness = brightness;
        }
    }
}
