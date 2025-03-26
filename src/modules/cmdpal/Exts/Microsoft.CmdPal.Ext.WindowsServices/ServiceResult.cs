// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ServiceProcess;

namespace Microsoft.CmdPal.Ext.WindowsServices;

public class ServiceResult
{
    public string ServiceName { get; private set; }

    public string DisplayName { get; private set; }

    public ServiceStartMode StartMode { get; private set; }

    public bool IsRunning { get; private set; }

    public ServiceResult(ServiceController serviceController)
    {
        ArgumentNullException.ThrowIfNull(serviceController);

        ServiceName = serviceController.ServiceName;
        DisplayName = serviceController.DisplayName;
        StartMode = serviceController.StartType;
        IsRunning = serviceController.Status != ServiceControllerStatus.Stopped && serviceController.Status != ServiceControllerStatus.StopPending;
    }

    private ServiceResult()
    {
    }

    public static ServiceResult FromServiceController(ServiceController serviceController)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(serviceController);
            var result = new ServiceResult();
            result.ServiceName = serviceController.ServiceName;
            result.DisplayName = serviceController.DisplayName;
            result.StartMode = serviceController.StartType;
            result.IsRunning = serviceController.Status != ServiceControllerStatus.Stopped && serviceController.Status != ServiceControllerStatus.StopPending;

            return result;
        }
        catch (Exception)
        {
            // try to log the exception in the future
            // retrive properties from serviceController will thorw exception. Such as PlatformNotSupportedException.
        }

        return null;
    }
}
