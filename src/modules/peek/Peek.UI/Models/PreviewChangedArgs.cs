// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Peek.UI.Models
{
    using System.Drawing;

    public class PreviewChangedArgs
    {
        public PreviewChangedArgs(Size requestedWindowSize)
        {
            RequestedWindowSize = requestedWindowSize;
        }

        public Size RequestedWindowSize { get; init; }
    }
}
