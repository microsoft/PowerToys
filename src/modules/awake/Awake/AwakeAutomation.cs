// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using ManagedCommon;

namespace Awake
{
    /// <summary>
    /// Automation object exposed via the Running Object Table. Intentionally minimal; methods may expand in future.
    /// </summary>
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    [Guid("4F1C3769-8D28-4A2D-8A6A-AB2F4C0F5F11")]
    public sealed class AwakeAutomation : IAwakeAutomation
    {
        public string Ping() => "pong";

        public void SetIndefinite() => Logger.LogInfo("Automation: SetIndefinite");

        public void SetTimed(int seconds) => Logger.LogInfo($"Automation: SetTimed {seconds}s");

        public void SetExpirable(int minutes) => Logger.LogInfo($"Automation: SetExpirable {minutes}m");

        public void SetPassive() => Logger.LogInfo("Automation: SetPassive");

        public void Cancel() => Logger.LogInfo("Automation: Cancel");

        public string GetStatusJson() => "{\"ok\":true}";
    }
}
