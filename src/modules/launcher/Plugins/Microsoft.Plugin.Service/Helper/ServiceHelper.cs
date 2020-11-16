// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using Wox.Plugin;

namespace Microsoft.Plugin.Service.Helper
{
    public static class ServiceHelper
    {
        public static IEnumerable<Result> Search(string search)
        {
            var services = ServiceController.GetServices();

            return services
                .Where(s => s.DisplayName.StartsWith(search, StringComparison.OrdinalIgnoreCase) || s.ServiceName.StartsWith(search, StringComparison.OrdinalIgnoreCase))
                .Select(s => new Result
                {
                    Title = s.DisplayName,
                    SubTitle = s.Status.ToString(),
                    ContextData = new ServiceResult(s.ServiceName, s.DisplayName, s.Status != ServiceControllerStatus.Stopped && s.Status != ServiceControllerStatus.StopPending),
                });
        }

        public static void Stop(ServiceResult serviceResult, IPublicAPI contextAPI)
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
                if (ChangeStatus(serviceResult.ServiceName, Action.Stop))
                {
                    contextAPI.ShowMsg(string.Empty, $"{serviceResult.DisplayName} has been stopped");
                }
                else
                {
                    contextAPI.ShowMsg(string.Empty, $"An error occured");
                }
            }
            catch (Win32Exception ex)
            {
                contextAPI.ShowMsg(string.Empty, ex.Message);
            }
        }

        public static void Start(ServiceResult serviceResult, IPublicAPI contextAPI)
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
                if (ChangeStatus(serviceResult.ServiceName, Action.Start))
                {
                    contextAPI.ShowMsg(string.Empty, $"{serviceResult.DisplayName} has been started");
                }
                else
                {
                    contextAPI.ShowMsg(string.Empty, $"An error occured");
                }
            }
            catch (Win32Exception ex)
            {
                contextAPI.ShowMsg(string.Empty, ex.Message);
            }
        }

        public static void Restart(ServiceResult serviceResult, IPublicAPI contextAPI)
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
                if (ChangeStatus(serviceResult.ServiceName, Action.Restart))
                {
                    contextAPI.ShowMsg(string.Empty, $"{serviceResult.DisplayName} has been restarted");
                }
                else
                {
                    contextAPI.ShowMsg(string.Empty, $"An error occured");
                }
            }
            catch (Win32Exception ex)
            {
                contextAPI.ShowMsg(string.Empty, ex.Message);
            }
}

        private static bool ChangeStatus(string serviceName, Action action)
        {
            var info = new ProcessStartInfo
            {
                FileName = "cmd",
                Verb = "runas",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            };

            if (action == Action.Start)
            {
                info.Arguments = string.Join(' ', "/c net start", serviceName);
            }
            else if (action == Action.Stop)
            {
                info.Arguments = string.Join(' ', "/c net stop", serviceName);
            }
            else if (action == Action.Restart)
            {
                info.Arguments = string.Join(' ', "/c net stop", serviceName, "&&", "net start", serviceName);
            }

            var process = Process.Start(info);
            process.WaitForExit();
            return process.ExitCode == 0;
        }
    }
}
