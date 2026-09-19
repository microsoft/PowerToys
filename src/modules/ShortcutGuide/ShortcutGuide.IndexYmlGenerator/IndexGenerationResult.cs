// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace ShortcutGuide.IndexYmlGenerator
{
    public sealed record IndexGenerationResult(
        int TotalFiles,
        int IndexedFiles,
        IReadOnlyList<(string FileName, Exception Exception)> Errors,
        IReadOnlyList<(string FileName, string Warning)> Warnings);
}
