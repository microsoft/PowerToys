// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;
using System.Threading.Tasks;
using Microsoft.PowerToys.Run.Plugin.Service.Properties;
using Microsoft.Win32;
using Wox.Infrastructure;
using Wox.Plugin;
using Wox.Plugin.Logger;

namespace Microsoft.PowerToys.Run.Plugin.Service.Helpers
{
    public static class ServiceHelper
    {
        public static IEnumerable<Result> Search(string search, string icoPath, PluginInitContext context)
        {
            var services = ServiceController.GetServices().OrderBy(s => s.DisplayName);
            IEnumerable<ServiceController> serviceList = new List<ServiceController>();

            if (search.StartsWith(Resources.wox_plugin_service_status + ":", StringComparison.CurrentCultureIgnoreCase))
            {
                // allows queries like 'status:running'
                serviceList = services.Where(s => GetLocalizedStatus(s.Status).Contains(search.Split(':')[1], StringComparison.CurrentCultureIgnoreCase));
            }
            else if (search.StartsWith(Resources.wox_plugin_service_startup + ":", StringComparison.CurrentCultureIgnoreCase))
            {
                // allows queries like 'startup:automatic'
                serviceList = services.Where(s => GetLocalizedStartType(s.StartType, s.ServiceName).Contains(search.Split(':')[1], StringComparison.CurrentCultureIgnoreCase));
            }
            else
            {
                // To show 'starts with' results first, we split the search into two steps and then concatenating the lists.
                var servicesStartsWith = services
                    .Where(s => s.DisplayName.StartsWith(search, StringComparison.OrdinalIgnoreCase) || s.ServiceName.StartsWith(search, StringComparison.OrdinalIgnoreCase));
                var servicesContains = services.Except(servicesStartsWith)
                    .Where(s => s.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase) || s.ServiceName.Contains(search, StringComparison.OrdinalIgnoreCase));
                serviceList = servicesStartsWith.Concat(servicesContains);
            }

            return serviceList.Select(s =>
            {
                ServiceResult serviceResult = new ServiceResult(s);
                Func<ActionContext, bool> serviceAction;
                if (serviceResult.IsRunning)
                {
                    serviceAction = _ =>
                    {
                        Task.Run(() => ServiceHelper.ChangeStatus(serviceResult, Action.Stop, context.API));
                        return true;
                    };
                }
                else
                {
                    serviceAction = _ =>
                    {
                        Task.Run(() => ServiceHelper.ChangeStatus(serviceResult, Action.Start, context.API));
                        return true;
                    };
                }

                return new Result
                {
                    Title = s.DisplayName,
                    SubTitle = ServiceHelper.GetResultSubTitle(s),
                    ToolTipData = new ToolTipData(serviceResult.DisplayName, serviceResult.ServiceName),
                    IcoPath = icoPath,
                    ContextData = serviceResult,
                    Action = serviceAction,
                };
            });
        }

        public static void ChangeStatus(ServiceResult serviceResult, Action action, IPublicAPI contextAPI)
        {
            ArgumentNullException.ThrowIfNull(serviceResult);

            ArgumentNullException.ThrowIfNull(contextAPI);

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
                    info.Arguments = $"start \"{serviceResult.ServiceName}\"";
                }
                else if (action == Action.Stop)
                {
                    info.Arguments = $"stop \"{serviceResult.ServiceName}\"";
                }
                else if (action == Action.Restart)
                {
                    info.FileName = "cmd";
                    info.Arguments = $"/c net stop \"{serviceResult.ServiceName}\" && net start \"{serviceResult.ServiceName}\"";
                }

                var process = Process.Start(info);
                process.WaitForExit();
                var exitCode = process.ExitCode;

                if (exitCode == 0)
                {
                    contextAPI.ShowNotification(GetLocalizedMessage(action), serviceResult.DisplayName);
                }
                else
                {
                    contextAPI.ShowNotification(GetLocalizedErrorMessage(action), serviceResult.DisplayName);
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
            Helper.OpenInShell("services.msc");
        }

        private static string GetResultSubTitle(ServiceController serviceController)
        {
            ArgumentNullException.ThrowIfNull(serviceController);

            return $"{Resources.wox_plugin_service_status}: {GetLocalizedStatus(serviceController.Status)} - {Resources.wox_plugin_service_startup}: {GetLocalizedStartType(serviceController.StartType, serviceController.ServiceName)} - {Resources.wox_plugin_service_name}: {serviceController.ServiceName}";
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

        private static string GetLocalizedStartType(ServiceStartMode startMode, string serviceName)
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
                return !IsDelayedStart(serviceName) ? Resources.wox_plugin_service_start_mode_automatic : Resources.wox_plugin_service_start_mode_automaticDelayed;
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

        private static string GetLocalizedMessage(Action action)
        {
            if (action == Action.Start)
            {
                return Resources.wox_plugin_service_started_notification;
            }
            else if (action == Action.Stop)
            {
                return Resources.wox_plugin_service_stopped_notification;
            }
            else if (action == Action.Restart)
            {
                return Resources.wox_plugin_service_restarted_notification;
            }
            else
            {
                return string.Empty;
            }
        }

        private static string GetLocalizedErrorMessage(Action action)
        {
            if (action == Action.Start)
            {
                return Resources.wox_plugin_service_start_error_notification;
            }
            else if (action == Action.Stop)
            {
                return Resources.wox_plugin_service_stop_error_notification;
            }
            else if (action == Action.Restart)
            {
                return Resources.wox_plugin_service_restart_error_notification;
            }
            else
            {
                return string.Empty;
            }
        }

        private static bool IsDelayedStart(string serviceName)
        {
            return (int?)Registry.LocalMachine.OpenSubKey(@"System\CurrentControlSet\Services\" + serviceName, false)?.GetValue("DelayedAutostart", 0, RegistryValueOptions.None) == 1;
        }
    }
}
