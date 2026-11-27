// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace SoundManager
{
    // The loudest moment of every audio session.
    public sealed class MixerPeaks
    {
        public List<AppPeak> Peaks { get; set; } = new();
    }
}
