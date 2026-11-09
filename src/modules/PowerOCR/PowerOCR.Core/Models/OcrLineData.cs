// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerOCR.Core.Models;

public sealed record OcrLineData(string Text, OcrRect Bounds, IReadOnlyList<OcrWordData> Words);
