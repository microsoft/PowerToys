// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Peek.FilePreviewer.Previewers.EmailPreviewer
{
    internal sealed record EmlMimePart(Dictionary<string, string> Headers, string ContentType, Dictionary<string, string> Parameters, byte[] Body)
    {
        public List<EmlMimePart> Children { get; } = [];
    }
}
