// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using Microsoft.Toolkit.Uwp.Notifications;

namespace PowerLauncher.Helper
{
    [ClassInterface(ClassInterfaceType.None)]
#pragma warning disable CS0618 // Type or member is obsolete
    [ComSourceInterfaces(typeof(INotificationActivationCallback))]
#pragma warning restore CS0618 // Type or member is obsolete
    [Guid("DD5CACDA-7C2E-4997-A62A-04A597B58F76")]
    [ComVisible(true)]
    public class LauncherNotificationActivator : NotificationActivator
    {
        public override void OnActivated(string invokedArgs, NotificationUserInput userInput, string appUserModelId)
        {
        }
    }
}
