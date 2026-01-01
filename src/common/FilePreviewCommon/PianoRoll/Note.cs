// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.FilePreviewCommon.PianoRoll
{
    internal sealed class Note
    {
        public int StartPos { get; set; }

        public int Length { get; set; }

        public int KeyNumber { get; set; }
    }
}
