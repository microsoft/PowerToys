﻿// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ServiceProcess;

namespace Microsoft.CmdPal.Ext.WindowsServices;

public class ServiceResult
{
    public string ServiceName { get; }

    public string DisplayName { get; }

    public ServiceStartMode StartMode { get; }

    public bool IsRunning { get; }

    public ServiceResult(ServiceController serviceController)
    {
        ArgumentNullException.ThrowIfNull(serviceController);

        ServiceName = serviceController.ServiceName;
        DisplayName = serviceController.DisplayName;
        StartMode = serviceController.StartType;
        IsRunning = serviceController.Status != ServiceControllerStatus.Stopped && serviceController.Status != ServiceControllerStatus.StopPending;
    }
}
