// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace SoundManager
{
    // One application that is playing or recording audio.
    public sealed class AudioAppInfo
    {
        public string SessionId { get; set; } = string.Empty;

        public string ProcessPath { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public uint ProcessId { get; set; }

        public double Volume { get; set; }

        public bool Muted { get; set; }

        public bool IsActive { get; set; }

        public bool IsCapture { get; set; }

        public string OutputDeviceId { get; set; } = string.Empty;

        public double BalanceLeft { get; set; }

        public double BalanceRight { get; set; }
    }
}
