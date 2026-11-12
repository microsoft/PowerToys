// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace SoundManager
{
    // Everything the window needs to draw itself, as sent by the module interface.
    public sealed class MixerSnapshot
    {
        public List<AudioDeviceInfo> Outputs { get; set; } = new();

        public List<AudioDeviceInfo> Inputs { get; set; } = new();

        public List<AudioAppInfo> Apps { get; set; } = new();

        // The device the test sound is playing through at this moment. It is empty when no test sound is playing.
        public string TestingDeviceId { get; set; } = string.Empty;
    }
}
