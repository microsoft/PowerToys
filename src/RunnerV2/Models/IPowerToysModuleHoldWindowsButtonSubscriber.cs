// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace RunnerV2.Models
{
    // Todo: implement
    internal interface IPowerToysModuleHoldWindowsButtonSubscriber
    {
        public int GetWindowsKeyHoldDuration { get; }

        public bool IsWindowsKeyHoldEnabled { get; }

        public void OnWindowsKeyHold();
    }
}
