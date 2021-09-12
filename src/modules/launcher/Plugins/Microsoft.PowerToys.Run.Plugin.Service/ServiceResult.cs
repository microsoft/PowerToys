// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ServiceProcess;

namespace Microsoft.PowerToys.Run.Plugin.Service
{
    public class ServiceResult
    {
        public string ServiceName { get; }

        public string DisplayName { get; }

        public ServiceStartMode StartMode { get; }

        public bool IsRunning { get; }

        public ServiceResult(ServiceController serviceController)
        {
            if (serviceController == null)
            {
                throw new ArgumentNullException(nameof(serviceController));
            }

            ServiceName = serviceController.ServiceName;
            DisplayName = serviceController.DisplayName;
            StartMode = serviceController.StartType;
            IsRunning = serviceController.Status != ServiceControllerStatus.Stopped && serviceController.Status != ServiceControllerStatus.StopPending;
        }
    }
}
