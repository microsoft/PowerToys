// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace WorkspacesLauncherUI.Data
{
    // sync with WorkspacesLib : LaunchingStateEnum.h
    public enum LaunchingState
    {
        Waiting = 0,
        Launched,
        LaunchedAndMoved,
        Failed,
        Canceled,
    }
}
