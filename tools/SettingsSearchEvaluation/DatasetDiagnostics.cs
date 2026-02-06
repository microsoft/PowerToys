// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;

namespace SettingsSearchEvaluation;

internal sealed class DatasetDiagnostics
{
    public required int TotalEntries { get; init; }

    public required int DistinctIds { get; init; }

    public required int DuplicateIdBucketCount { get; init; }

    public required IReadOnlyDictionary<string, int> DuplicateIdCounts { get; init; }

    public static DatasetDiagnostics Empty { get; } = new()
    {
        TotalEntries = 0,
        DistinctIds = 0,
        DuplicateIdBucketCount = 0,
        DuplicateIdCounts = new ReadOnlyDictionary<string, int>(new Dictionary<string, int>()),
    };
}
