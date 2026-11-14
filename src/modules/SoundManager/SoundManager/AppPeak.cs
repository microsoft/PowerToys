// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace SoundManager
{
    // The peak level of one audio session.
    public sealed class AppPeak
    {
        public string SessionId { get; set; } = string.Empty;

        public double Peak { get; set; }
    }
}
