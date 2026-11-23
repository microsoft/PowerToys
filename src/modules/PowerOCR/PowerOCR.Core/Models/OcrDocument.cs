// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerOCR.Core.Models;

public sealed record OcrDocument(IReadOnlyList<OcrLineData> Lines)
{
    public IEnumerable<OcrWordData> Words => Lines.SelectMany(line => line.Words);
}
