// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Windows.Interop;
using interop;
using PowerOCR.Helpers;
using PowerOCR.Utilities;

namespace PowerOCR.Keyboard
{
    /// <summary>
    /// This class handles the interaction model when running from PowerToys Run.
    /// Handles activation through the event sent by the runner.
    /// </summary>
    internal class EventMonitor
    {
        public EventMonitor()
        {
            NativeEventWaiter.WaitForEventLoop(Constants.ShowPowerOCRSharedEvent(), StartOCRSession);
        }

        public void StartOCRSession()
        {
            if (!WindowUtilities.IsOCROverlayCreated())
            {
                WindowUtilities.LaunchOCROverlayOnEveryScreen();
            }
        }
    }
}
