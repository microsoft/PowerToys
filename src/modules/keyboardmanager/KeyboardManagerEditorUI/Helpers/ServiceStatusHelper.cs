// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;

namespace KeyboardManagerEditorUI.Helpers
{
    public static class ServiceStatusHelper
    {
        private const string KeyboardManagerEngineProcessName = "PowerToys.KeyboardManagerEngine";

        public static bool IsKeyboardManagerServiceRunning()
        {
            try
            {
                var processes = Process.GetProcessesByName(KeyboardManagerEngineProcessName);
                return processes.Length > 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool IsPowerToysRunning()
        {
            try
            {
                var processes = Process.GetProcessesByName("PowerToys");
                return processes.Length > 0;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
