// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Common.UI;
using interop;
using PowerOCR.Utilities;

namespace PowerOCR.Keyboard
{
    /// <summary>
    /// This class handles the interaction model when running from PowerToys Run.
    /// Handles activation through the event sent by the runner.
    /// </summary>
    internal sealed class EventMonitor
    {
        public EventMonitor(System.Windows.Threading.Dispatcher dispatcher, System.Threading.CancellationToken exitToken)
        {
            NativeEventWaiter.WaitForEventLoop(Constants.ShowPowerOCRSharedEvent(), StartOCRSession, dispatcher, exitToken);
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
