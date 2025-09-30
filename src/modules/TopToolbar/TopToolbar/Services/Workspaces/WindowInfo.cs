// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace TopToolbar.Services.Workspaces
{
    internal sealed class WindowInfo
    {
        public WindowInfo(
            IntPtr handle,
            uint processId,
            string processPath,
            string processFileName,
            string processName,
            string title,
            string appUserModelId,
            bool isVisible,
            WindowBounds bounds)
        {
            Handle = handle;
            ProcessId = processId;
            ProcessPath = processPath ?? string.Empty;
            ProcessFileName = processFileName ?? string.Empty;
            ProcessName = processName ?? string.Empty;
            Title = title ?? string.Empty;
            AppUserModelId = appUserModelId ?? string.Empty;
            IsVisible = isVisible;
            Bounds = bounds;
        }

        public IntPtr Handle { get; }

        public uint ProcessId { get; }

        public string ProcessPath { get; }

        public string ProcessFileName { get; }

        public string ProcessName { get; }

        public string Title { get; }

        public string AppUserModelId { get; }

        public bool IsVisible { get; }

        public WindowBounds Bounds { get; }
    }
}
