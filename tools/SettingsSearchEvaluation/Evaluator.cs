// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Common.Search;
using Common.Search.FuzzSearch;
using Common.Search.SemanticSearch;

namespace SettingsSearchEvaluation;

internal static class Evaluator
{
    public static async Task<EvaluationReport> RunAsync(
        RunnerOptions options,
        IReadOnlyList<SettingEntry> entries,
        DatasetDiagnostics dataset,
        IReadOnlyList<EvaluationCase> cases,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(dataset);
        ArgumentNullException.ThrowIfNull(cases);

        var reports = new List<EngineEvaluationReport>(options.Engines.Count);
        foreach (var engine in options.Engines)
        {
            cancellationToken.ThrowIfCancellationRequested();
            reports.Add(engine switch
            {
                SearchEngineKind.Basic => await EvaluateBasicAsync(options, entries, cases, cancellationToken),
                SearchEngineKind.Semantic => await EvaluateSemanticAsync(options, entries, cases, cancellationToken),
                _ => throw new InvalidOperationException($"Unsupported engine '{engine}'."),
            });
        }

        return new EvaluationReport
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            IndexJsonPath = options.InputDataPath,
            Dataset = dataset,
            CaseCount = cases.Count,
            Engines = reports,
        };
    }

    private static async Task<EngineEvaluationReport> EvaluateBasicAsync(
        RunnerOptions options,
        IReadOnlyList<SettingEntry> entries,
        IReadOnlyList<EvaluationCase> cases,
        CancellationToken cancellationToken)
    {
        using var engine = new FuzzSearchEngine<SettingEntry>();

        var indexingStopwatch = Stopwatch.StartNew();
        await engine.InitializeAsync(cancellationToken);
        await engine.IndexBatchAsync(entries, cancellationToken);
        indexingStopwatch.Stop();

        var metrics = await EvaluateQueryLoopAsync(
            cases,
            options,
            (query, searchOptions, token) => engine.SearchAsync(query, searchOptions, token),
            cancellationToken);

        return new EngineEvaluationReport
        {
            Engine = SearchEngineKind.Basic,
            IsAvailable = true,
            AvailabilityError = null,
            CapabilitiesSummary = "Fuzzy text search engine",
            IndexedEntries = entries.Count,
            QueryCount = cases.Count,
            IndexingTimeMs = indexingStopwatch.Elapsed.TotalMilliseconds,
            RecallAtK = metrics.RecallAtK,
            Mrr = metrics.Mrr,
            SearchLatencyMs = metrics.Latency,
            CaseResults = metrics.CaseResults,
        };
    }

    private static async Task<EngineEvaluationReport> EvaluateSemanticAsync(
        RunnerOptions options,
        IReadOnlyList<SettingEntry> entries,
        IReadOnlyList<EvaluationCase> cases,
        CancellationToken cancellationToken)
    {
        var indexName = $"PowerToys.Settings.Eval.{Environment.ProcessId}.{Guid.NewGuid():N}";
        using var engine = new SemanticSearchEngine<SettingEntry>(indexName);

        var initResult = await engine.InitializeWithResultAsync(cancellationToken);
        if (initResult.IsFailure || !engine.IsReady)
        {
            return new EngineEvaluationReport
            {
                Engine = SearchEngineKind.Semantic,
                IsAvailable = false,
                AvailabilityError = FormatError(initResult.Error) ?? "Semantic engine is not ready.",
                CapabilitiesSummary = null,
                IndexedEntries = 0,
                QueryCount = 0,
                IndexingTimeMs = 0,
                RecallAtK = 0,
                Mrr = 0,
                SearchLatencyMs = LatencySummary.Empty,
                CaseResults = Array.Empty<QueryEvaluationResult>(),
            };
        }

        var indexingStopwatch = Stopwatch.StartNew();
        var indexResult = await engine.IndexBatchWithResultAsync(entries, cancellationToken);
        if (indexResult.IsFailure)
        {
            return new EngineEvaluationReport
            {
                Engine = SearchEngineKind.Semantic,
                IsAvailable = false,
                AvailabilityError = FormatError(indexResult.Error) ?? "Semantic indexing failed.",
                CapabilitiesSummary = BuildCapabilitiesSummary(engine.SemanticCapabilities),
                IndexedEntries = 0,
                QueryCount = 0,
                IndexingTimeMs = indexingStopwatch.Elapsed.TotalMilliseconds,
                RecallAtK = 0,
                Mrr = 0,
                SearchLatencyMs = LatencySummary.Empty,
                CaseResults = Array.Empty<QueryEvaluationResult>(),
            };
        }

        try
        {
            await engine.WaitForIndexingCompleteAsync(options.SemanticIndexTimeout);
        }
        catch (Exception ex)
        {
            return new EngineEvaluationReport
            {
                Engine = SearchEngineKind.Semantic,
                IsAvailable = false,
                AvailabilityError = $"Semantic indexing did not become idle: {ex.Message}",
                CapabilitiesSummary = BuildCapabilitiesSummary(engine.SemanticCapabilities),
                IndexedEntries = 0,
                QueryCount = 0,
                IndexingTimeMs = indexingStopwatch.Elapsed.TotalMilliseconds,
                RecallAtK = 0,
                Mrr = 0,
                SearchLatencyMs = LatencySummary.Empty,
                CaseResults = Array.Empty<QueryEvaluationResult>(),
            };
        }

        indexingStopwatch.Stop();
        var metrics = await EvaluateQueryLoopAsync(
            cases,
            options,
            async (query, searchOptions, token) =>
            {
                var result = await engine.SearchWithResultAsync(query, searchOptions, token);
                return result.Value ?? Array.Empty<SearchResult<SettingEntry>>();
            },
            cancellationToken);

        return new EngineEvaluationReport
        {
            Engine = SearchEngineKind.Semantic,
            IsAvailable = true,
            AvailabilityError = null,
            CapabilitiesSummary = BuildCapabilitiesSummary(engine.SemanticCapabilities),
            IndexedEntries = entries.Count,
            QueryCount = cases.Count,
            IndexingTimeMs = indexingStopwatch.Elapsed.TotalMilliseconds,
            RecallAtK = metrics.RecallAtK,
            Mrr = metrics.Mrr,
            SearchLatencyMs = metrics.Latency,
            CaseResults = metrics.CaseResults,
        };
    }

    private static async Task<QueryRunMetrics> EvaluateQueryLoopAsync(
        IReadOnlyList<EvaluationCase> cases,
        RunnerOptions options,
        Func<string, SearchOptions, CancellationToken, Task<IReadOnlyList<SearchResult<SettingEntry>>>> searchAsync,
        CancellationToken cancellationToken)
    {
        var caseResults = new List<QueryEvaluationResult>(cases.Count);
        var latencySamples = new List<double>(Math.Max(1, cases.Count * options.Iterations));

        var hits = 0;
        var reciprocalRankSum = 0.0;
        var searchOptions = new SearchOptions
        {
            MaxResults = options.MaxResults,
            IncludeMatchSpans = false,
        };

        foreach (var queryCase in cases)
        {
            cancellationToken.ThrowIfCancellationRequested();

            for (int warmup = 0; warmup < options.WarmupIterations; warmup++)
            {
                _ = await searchAsync(queryCase.Query, searchOptions, cancellationToken);
            }

            IReadOnlyList<SearchResult<SettingEntry>> firstMeasuredResult = Array.Empty<SearchResult<SettingEntry>>();
            for (int iteration = 0; iteration < options.Iterations; iteration++)
            {
                var sw = Stopwatch.StartNew();
                var queryResult = await searchAsync(queryCase.Query, searchOptions, cancellationToken);
                sw.Stop();
                latencySamples.Add(sw.Elapsed.TotalMilliseconds);

                if (iteration == 0)
                {
                    firstMeasuredResult = queryResult;
                }
            }

            var rankedIds = firstMeasuredResult.Select(result => result.Item.Id).ToArray();
            var expected = new HashSet<string>(queryCase.ExpectedIds, StringComparer.OrdinalIgnoreCase);
            var bestRank = EvaluationMath.FindBestRank(rankedIds, expected);
            var hit = bestRank > 0 && bestRank <= options.TopK;

            if (hit)
            {
                hits++;
            }

            if (bestRank > 0)
            {
                reciprocalRankSum += 1.0 / bestRank;
            }

            caseResults.Add(new QueryEvaluationResult
            {
                Query = queryCase.Query,
                ExpectedIds = queryCase.ExpectedIds,
                TopResultIds = rankedIds.Take(options.TopK).ToArray(),
                BestRank = bestRank,
                HitAtK = hit,
                Notes = queryCase.Notes,
            });
        }

        var totalCases = Math.Max(1, cases.Count);
        return new QueryRunMetrics
        {
            CaseResults = caseResults,
            RecallAtK = hits / (double)totalCases,
            Mrr = reciprocalRankSum / totalCases,
            Latency = EvaluationMath.ComputeLatencySummary(latencySamples),
        };
    }

    private static string? FormatError(SearchError? error)
    {
        if (error == null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(error.Details))
        {
            return $"{error.Message} ({error.Details})";
        }

        return error.Message;
    }

    private static string BuildCapabilitiesSummary(SemanticSearchCapabilities? capabilities)
    {
        if (capabilities == null)
        {
            return "Capabilities unavailable";
        }

        return $"TextLexical={capabilities.TextLexicalAvailable}, TextSemantic={capabilities.TextSemanticAvailable}, ImageSemantic={capabilities.ImageSemanticAvailable}, ImageOcr={capabilities.ImageOcrAvailable}";
    }

    private sealed class QueryRunMetrics
    {
        public required IReadOnlyList<QueryEvaluationResult> CaseResults { get; init; }

        public required double RecallAtK { get; init; }

        public required double Mrr { get; init; }

        public required LatencySummary Latency { get; init; }
    }
}
