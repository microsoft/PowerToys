// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;
using Microsoft.PowerToys.Run.Plugin.Service.Properties;
using Wox.Plugin;
using Wox.Plugin.Logger;

namespace Microsoft.PowerToys.Run.Plugin.Service.Helpers
{
    public static class ServiceHelper
    {
        public static IEnumerable<Result> Search(string search, string icoPath)
        {
            var services = ServiceController.GetServices();

            return services
                .Where(s => s.DisplayName.StartsWith(search, StringComparison.OrdinalIgnoreCase) || s.ServiceName.StartsWith(search, StringComparison.OrdinalIgnoreCase))
                .Select(s => new Result
                {
                    Title = GetResultTitle(s),
                    SubTitle = GetResultSubTitle(s),
                    IcoPath = icoPath,
                    ContextData = new ServiceResult(s),
                });
        }

        public static void ChangeStatus(ServiceResult serviceResult, Action action, IPublicAPI contextAPI)
        {
            if (serviceResult == null)
            {
                throw new ArgumentNullException(nameof(serviceResult));
            }

            if (contextAPI == null)
            {
                throw new ArgumentNullException(nameof(contextAPI));
            }

            try
            {
                var info = new ProcessStartInfo
                {
                    FileName = "net",
                    Verb = "runas",
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                };

                if (action == Action.Start)
                {
                    info.Arguments = string.Join(' ', "start", serviceResult.ServiceName);
                }
                else if (action == Action.Stop)
                {
                    info.Arguments = string.Join(' ', "stop", serviceResult.ServiceName);
                }
                else if (action == Action.Restart)
                {
                    info.FileName = "cmd";
                    info.Arguments = string.Join(' ', "/c net stop", serviceResult.ServiceName, "&&", "net start", serviceResult.ServiceName);
                }

                var process = Process.Start(info);
                process.WaitForExit();
                var exitCode = process.ExitCode;

                if (exitCode == 0)
                {
                    contextAPI.ShowNotification(GetLocalizedMessage(serviceResult, action));
                }
                else
                {
                    contextAPI.ShowNotification("An error occurred");
                    Log.Error($"The command returned {exitCode}", MethodBase.GetCurrentMethod().DeclaringType);
                }
            }
            catch (Win32Exception ex)
            {
                Log.Error(ex.Message, MethodBase.GetCurrentMethod().DeclaringType);
            }
        }

        public static void OpenServices()
        {
            try
            {
                var info = new ProcessStartInfo
                {
                    FileName = "services.msc",
                    UseShellExecute = true,
                };

                Process.Start(info);
            }
            catch (Win32Exception ex)
            {
                Log.Error(ex.Message, MethodBase.GetCurrentMethod().DeclaringType);
            }
        }

        private static string GetResultTitle(ServiceController serviceController)
        {
            if (serviceController == null)
            {
                throw new ArgumentNullException(nameof(serviceController));
            }

            var suffix = $"({serviceController.ServiceName})";
            return serviceController.DisplayName.EndsWith(suffix, StringComparison.CurrentCulture) ? serviceController.DisplayName : $"{serviceController.DisplayName} {suffix}";
        }

        private static string GetResultSubTitle(ServiceController serviceController)
        {
            if (serviceController == null)
            {
                throw new ArgumentNullException(nameof(serviceController));
            }

            return $"{Resources.wox_plugin_service_status}: {GetLocalizedStatus(serviceController.Status)} - {Resources.wox_plugin_service_startup}: {GetLocalizedStartType(serviceController.StartType)}";
        }

        private static string GetLocalizedStatus(ServiceControllerStatus status)
        {
            if (status == ServiceControllerStatus.Stopped)
            {
                return Resources.wox_plugin_service_stopped;
            }
            else if (status == ServiceControllerStatus.StartPending)
            {
                return Resources.wox_plugin_service_start_pending;
            }
            else if (status == ServiceControllerStatus.StopPending)
            {
                return Resources.wox_plugin_service_stop_pending;
            }
            else if (status == ServiceControllerStatus.Running)
            {
                return Resources.wox_plugin_service_running;
            }
            else if (status == ServiceControllerStatus.ContinuePending)
            {
                return Resources.wox_plugin_service_continue_pending;
            }
            else if (status == ServiceControllerStatus.PausePending)
            {
                return Resources.wox_plugin_service_pause_pending;
            }
            else if (status == ServiceControllerStatus.Paused)
            {
                return Resources.wox_plugin_service_paused;
            }
            else
            {
                return status.ToString();
            }
        }

        private static string GetLocalizedStartType(ServiceStartMode startMode)
        {
            if (startMode == ServiceStartMode.Boot)
            {
                return Resources.wox_plugin_service_start_mode_boot;
            }
            else if (startMode == ServiceStartMode.System)
            {
                return Resources.wox_plugin_service_start_mode_system;
            }
            else if (startMode == ServiceStartMode.Automatic)
            {
                return Resources.wox_plugin_service_start_mode_automatic;
            }
            else if (startMode == ServiceStartMode.Manual)
            {
                return Resources.wox_plugin_service_start_mode_manual;
            }
            else if (startMode == ServiceStartMode.Disabled)
            {
                return Resources.wox_plugin_service_start_mode_disabled;
            }
            else
            {
                return startMode.ToString();
            }
        }

        private static string GetLocalizedMessage(ServiceResult serviceResult, Action action)
        {
            if (action == Action.Start)
            {
                return string.Format(CultureInfo.CurrentCulture, Resources.wox_plugin_service_started_notification, serviceResult.DisplayName);
            }
            else if (action == Action.Stop)
            {
                return string.Format(CultureInfo.CurrentCulture, Resources.wox_plugin_service_stopped_notification, serviceResult.DisplayName);
            }
            else if (action == Action.Restart)
            {
                return string.Format(CultureInfo.CurrentCulture, Resources.wox_plugin_service_restarted_notification, serviceResult.DisplayName);
            }
            else
            {
                return string.Empty;
            }
        }
    }
}
