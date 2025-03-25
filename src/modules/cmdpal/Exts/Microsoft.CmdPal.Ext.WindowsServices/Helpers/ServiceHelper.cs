// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using Microsoft.CmdPal.Ext.WindowsServices.Commands;
using Microsoft.CmdPal.Ext.WindowsServices.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Win32;
using Windows.System;

namespace Microsoft.CmdPal.Ext.WindowsServices.Helpers;

public static class ServiceHelper
{
    public static IEnumerable<ListItem> Search(string search)
    {
        var services = ServiceController.GetServices().OrderBy(s => s.DisplayName);
        IEnumerable<ServiceController> serviceList = [];

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
            var serviceResult = new ServiceResult(s);
            ServiceCommand serviceCommand;
            CommandContextItem[] moreCommands;
            if (serviceResult.IsRunning)
            {
                serviceCommand = new ServiceCommand(serviceResult, Action.Stop);
                moreCommands = [
                    new CommandContextItem(new RestartServiceCommand(serviceResult)),
                    new CommandContextItem(new OpenServicesCommand(serviceResult))
                    {
                        RequestedShortcut = KeyChordHelpers.FromModifiers(true, false, false, false, (int)VirtualKey.O, 0),
                    },
                ];
            }
            else
            {
                serviceCommand = new ServiceCommand(serviceResult, Action.Start);
                moreCommands = [
                    new CommandContextItem(new OpenServicesCommand(serviceResult)),
                ];
            }

            IconInfo icon = new("\U0001f7e2"); // unicode LARGE GREEN CIRCLE
            switch (s.Status)
            {
                case ServiceControllerStatus.Stopped:
                    icon = new("\U0001F534"); // unicode LARGE RED CIRCLE
                    break;
                case ServiceControllerStatus.Running:
                    break;
                case ServiceControllerStatus.Paused:
                    icon = new("\u23F8"); // unicode DOUBLE VERTICAL BAR, aka, "Pause"
                    break;
            }

            return new ListItem(serviceCommand)
            {
                Title = s.DisplayName,
                Subtitle = ServiceHelper.GetResultSubTitle(s),
                MoreCommands = moreCommands,
                Icon = icon,

                // TODO GH #78 we need to improve the icon story
                // TODO GH #126 investigate tooltip story
                // ToolTipData = new ToolTipData(serviceResult.DisplayName, serviceResult.ServiceName),
                // IcoPath = icoPath,
            };
        });
    }

    // TODO GH #118 IPublicAPI contextAPI isn't used anymore, but we need equivalent ways to show notifications and status
    public static void ChangeStatus(ServiceResult serviceResult, Action action)
    {
        ArgumentNullException.ThrowIfNull(serviceResult);

        // ArgumentNullException.ThrowIfNull(contextAPI);
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

#pragma warning disable IDE0059, CS0168, SA1005
            if (exitCode == 0)
            {
                // TODO GH #118 feedback to users
                // contextAPI.ShowNotification(GetLocalizedMessage(action), serviceResult.DisplayName);
            }
            else
            {
                // TODO GH #108 We need to figure out some logging
                // contextAPI.ShowNotification(GetLocalizedErrorMessage(action), serviceResult.DisplayName);
                // Log.Error($"The command returned {exitCode}", MethodBase.GetCurrentMethod().DeclaringType);
            }
        }
        catch (Win32Exception ex)
        {
            // TODO GH #108 We need to figure out some logging
            // Log.Error(ex.Message, MethodBase.GetCurrentMethod().DeclaringType);
        }
    }
#pragma warning restore IDE0059, CS0168, SA1005

    public static void OpenServices()
    {
        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "services.msc",
                UseShellExecute = true,
            };

            System.Diagnostics.Process.Start(startInfo);
        }
#pragma warning disable IDE0059, CS0168, SA1005
        catch (Exception ex)
        {
            // TODO GH #108 We need to figure out some logging
        }
    }
#pragma warning restore IDE0059, CS0168, SA1005

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
        else
        {
            return status == ServiceControllerStatus.ContinuePending
                ? Resources.wox_plugin_service_continue_pending
                : status == ServiceControllerStatus.PausePending
                            ? Resources.wox_plugin_service_pause_pending
                            : status == ServiceControllerStatus.Paused ? Resources.wox_plugin_service_paused : status.ToString();
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
        else
        {
            return startMode == ServiceStartMode.Automatic
                ? !IsDelayedStart(serviceName) ? Resources.wox_plugin_service_start_mode_automatic : Resources.wox_plugin_service_start_mode_automaticDelayed
                : startMode == ServiceStartMode.Manual
                            ? Resources.wox_plugin_service_start_mode_manual
                            : startMode == ServiceStartMode.Disabled ? Resources.wox_plugin_service_start_mode_disabled : startMode.ToString();
        }
    }

    private static string GetLocalizedMessage(Action action)
    {
        return action == Action.Start
            ? Resources.wox_plugin_service_started_notification
            : action == Action.Stop
                ? Resources.wox_plugin_service_stopped_notification
                : action == Action.Restart ? Resources.wox_plugin_service_restarted_notification : string.Empty;
    }

    private static string GetLocalizedErrorMessage(Action action)
    {
        return action == Action.Start
            ? Resources.wox_plugin_service_start_error_notification
            : action == Action.Stop
                ? Resources.wox_plugin_service_stop_error_notification
                : action == Action.Restart ? Resources.wox_plugin_service_restart_error_notification : string.Empty;
    }

    private static bool IsDelayedStart(string serviceName) => (int?)Registry.LocalMachine.OpenSubKey(@"System\CurrentControlSet\Services\" + serviceName, false)?.GetValue("DelayedAutostart", 0, RegistryValueOptions.None) == 1;
}
