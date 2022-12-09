// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Peek.FilePreviewer.Previewers
{
    using System;

    public interface IBrowserPreviewer : IPreviewer
    {
        public Uri? Preview { get; }
    }
}
