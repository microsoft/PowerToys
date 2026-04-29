// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.Win32;

namespace RunnerV2.Helpers
{
    internal static class NotificationHelper
    {
        public enum ToastType
        {
            ElevatedDontShowAgain,
            CouldntToggleFileExplorerModules,
        }

        public static string GetToastKey(ToastType key) => key switch
        {
            ToastType.ElevatedDontShowAgain => @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\DontShowMeThisDialogAgain\{e16ea82f-6d94-4f30-bb02-d6d911588afd}",
            ToastType.CouldntToggleFileExplorerModules => @"(SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\DontShowMeThisDialogAgain\{7e29e2b2-b31c-4dcd-b7b0-79c078b02430})",
            _ => throw new ArgumentOutOfRangeException(nameof(key), key, null),
        };

        public static bool DisableToast(ToastType type)
        {
            try
            {
                RegistryKey? key = Registry.CurrentUser.CreateSubKey(GetToastKey(type));

                if (key != null)
                {
                    key.SetValue(null, BitConverter.GetBytes(DateTimeOffset.UtcNow.ToUnixTimeSeconds()), RegistryValueKind.QWord);
                    key.Close();
                    return true;
                }
            }
            catch (Exception e)
            {
                Logger.LogError("Could not disable toast notification.", e);
            }

            return false;
        }
    }
}
