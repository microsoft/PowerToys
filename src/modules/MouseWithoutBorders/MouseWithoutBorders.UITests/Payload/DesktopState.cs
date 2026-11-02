// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Also compiled by Windows PowerShell 5.1 in the disposable guest. Keep this file C# 5 compatible.
namespace Microsoft.MouseWithoutBorders.UITests
{
    public sealed class DesktopState
    {
        public DesktopState()
        {
            InputDesktop = string.Empty;
        }

        public int SessionId { get; set; }

        public uint ActiveConsoleSessionId { get; set; }

        public bool IsConsoleSession
        {
            get { return SessionId >= 0 && (uint)SessionId == ActiveConsoleSessionId; }
        }

        public int WtsState { get; set; }

        public string InputDesktop { get; set; }

        public bool InputAvailable { get; set; }

        public bool Elevated { get; set; }

        public bool IsSystem { get; set; }

        public long ForegroundHwnd { get; set; }

        public bool Ready
        {
            get { return SessionId > 0 && WtsState == 0 && InputDesktop == "Default" && InputAvailable && !IsSystem; }
        }
    }
}
