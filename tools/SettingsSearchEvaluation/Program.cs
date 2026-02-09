// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

namespace SettingsSearchEvaluation;

internal static class Program
{
    private static readonly JsonSerializerOptions OutputJsonOptions = new()
    {
        WriteIndented = true,
    };

    private static int Main(string[] args)
    {
        try
        {
            return MainAsync(args).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unhandled error: {ex.Message}");
            return 99;
        }
    }

    private static async Task<int> MainAsync(string[] args)
    {
        args = ResolveArgs(args);

        if (args.Any(arg => string.Equals(arg, "--help", StringComparison.OrdinalIgnoreCase) || string.Equals(arg, "-h", StringComparison.OrdinalIgnoreCase)))
        {
            PrintUsage();
            return 0;
        }

        if (!TryParseArgs(args, out var options, out var parseError))
        {
            Console.Error.WriteLine(parseError);
            Console.Error.WriteLine();
            PrintUsage();
            return 2;
        }

        var sourcePath = options.InputDataPath;
        if (!File.Exists(sourcePath))
        {
            Console.Error.WriteLine($"Input file not found: {sourcePath}");
            return 3;
        }

        if (!string.IsNullOrWhiteSpace(options.CasesJsonPath) && !File.Exists(options.CasesJsonPath))
        {
            Console.Error.WriteLine($"Cases file not found: {options.CasesJsonPath}");
            return 3;
        }

        var (entries, dataset) = options.UseNormalizedCorpus
            ? EvaluationDataLoader.LoadEntriesFromNormalizedCorpusFile(sourcePath)
            : EvaluationDataLoader.LoadEntriesFromFile(sourcePath);

        if (!string.IsNullOrWhiteSpace(options.ExportNormalizedPath))
        {
            EvaluationDataLoader.WriteNormalizedCorpusFile(options.ExportNormalizedPath, entries);
            Console.WriteLine($"Wrote normalized corpus to '{options.ExportNormalizedPath}'.");

            var textOnlyPath = GetTextOnlyCorpusPath(options.ExportNormalizedPath);
            EvaluationDataLoader.WriteNormalizedTextCorpusFile(textOnlyPath, entries);
            Console.WriteLine($"Wrote text-only normalized corpus to '{textOnlyPath}'.");
        }

        if (options.ExportOnly)
        {
            return 0;
        }

        var cases = EvaluationDataLoader.LoadCases(options.CasesJsonPath, entries);
        if (cases.Count == 0)
        {
            Console.Error.WriteLine("No valid evaluation cases were found.");
            return 3;
        }

        Console.WriteLine($"Loaded {entries.Count} entries from '{sourcePath}'.");
        Console.WriteLine($"Cases: {cases.Count}");
        Console.WriteLine($"Duplicate id buckets: {dataset.DuplicateIdBucketCount}");
        if (dataset.DuplicateIdBucketCount > 0)
        {
            var largest = dataset.DuplicateIdCounts
                .OrderByDescending(x => x.Value)
                .Take(5)
                .Select(x => $"{x.Key} x{x.Value}");
            Console.WriteLine($"Top duplicate ids: {string.Join(", ", largest)}");
        }

        var report = await Evaluator.RunAsync(options, entries, dataset, cases);
        PrintSummary(report, options.TopK);

        if (!string.IsNullOrWhiteSpace(options.OutputJsonPath))
        {
            var outputDirectory = Path.GetDirectoryName(options.OutputJsonPath);
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            var json = JsonSerializer.Serialize(report, OutputJsonOptions);
            File.WriteAllText(options.OutputJsonPath, json);
            Console.WriteLine($"Wrote report to '{options.OutputJsonPath}'.");
        }

        return report.Engines.Any(engine => engine.IsAvailable) ? 0 : 4;
    }

    private static bool TryParseArgs(string[] args, out RunnerOptions options, out string error)
    {
        string defaultIndex = GetDefaultIndexPath();
        string? indexPath = null;
        string? normalizedCorpusPath = null;
        string? exportNormalizedPath = null;
        string? casesPath = null;
        string? outputPath = null;
        var exportOnly = false;
        var indexPathExplicitlySet = false;
        var maxResults = 10;
        var topK = 5;
        var iterations = 5;
        var warmup = 1;
        var semanticTimeoutMs = 15000;
        IReadOnlyList<SearchEngineKind> engines = new[] { SearchEngineKind.Basic, SearchEngineKind.Semantic };

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg.ToLowerInvariant())
            {
                case "--index-json":
                    if (!TryReadValue(args, ref i, out indexPath))
                    {
                        options = null!;
                        error = "Missing value for --index-json";
                        return false;
                    }

                    indexPathExplicitlySet = true;
                    break;
                case "--normalized-corpus":
                    if (!TryReadValue(args, ref i, out normalizedCorpusPath))
                    {
                        options = null!;
                        error = "Missing value for --normalized-corpus";
                        return false;
                    }

                    break;
                case "--export-normalized":
                    if (!TryReadValue(args, ref i, out exportNormalizedPath))
                    {
                        options = null!;
                        error = "Missing value for --export-normalized";
                        return false;
                    }

                    break;
                case "--export-only":
                    exportOnly = true;
                    break;
                case "--cases-json":
                    if (!TryReadValue(args, ref i, out casesPath))
                    {
                        options = null!;
                        error = "Missing value for --cases-json";
                        return false;
                    }

                    break;
                case "--output-json":
                    if (!TryReadValue(args, ref i, out outputPath))
                    {
                        options = null!;
                        error = "Missing value for --output-json";
                        return false;
                    }

                    break;
                case "--engine":
                    if (!TryReadValue(args, ref i, out var engineText))
                    {
                        options = null!;
                        error = "Missing value for --engine";
                        return false;
                    }

                    if (!TryParseEngines(engineText!, out engines))
                    {
                        options = null!;
                        error = "Invalid --engine value. Allowed values: basic, semantic, both.";
                        return false;
                    }

                    break;
                case "--max-results":
                    if (!TryReadInt(args, ref i, out maxResults) || maxResults <= 0)
                    {
                        options = null!;
                        error = "Invalid --max-results value. Must be a positive integer.";
                        return false;
                    }

                    break;
                case "--top-k":
                    if (!TryReadInt(args, ref i, out topK) || topK <= 0)
                    {
                        options = null!;
                        error = "Invalid --top-k value. Must be a positive integer.";
                        return false;
                    }

                    break;
                case "--iterations":
                    if (!TryReadInt(args, ref i, out iterations) || iterations <= 0)
                    {
                        options = null!;
                        error = "Invalid --iterations value. Must be a positive integer.";
                        return false;
                    }

                    break;
                case "--warmup":
                    if (!TryReadInt(args, ref i, out warmup) || warmup < 0)
                    {
                        options = null!;
                        error = "Invalid --warmup value. Must be a non-negative integer.";
                        return false;
                    }

                    break;
                case "--semantic-timeout-ms":
                    if (!TryReadInt(args, ref i, out semanticTimeoutMs) || semanticTimeoutMs <= 0)
                    {
                        options = null!;
                        error = "Invalid --semantic-timeout-ms value. Must be a positive integer.";
                        return false;
                    }

                    break;
                default:
                    options = null!;
                    error = $"Unknown argument: {arg}";
                    return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(normalizedCorpusPath) && indexPathExplicitlySet)
        {
            options = null!;
            error = "Use either --index-json or --normalized-corpus, not both.";
            return false;
        }

        if (exportOnly && string.IsNullOrWhiteSpace(exportNormalizedPath))
        {
            options = null!;
            error = "--export-only requires --export-normalized <path>.";
            return false;
        }

        options = new RunnerOptions
        {
            IndexJsonPath = Path.GetFullPath(indexPath ?? defaultIndex),
            NormalizedCorpusPath = string.IsNullOrWhiteSpace(normalizedCorpusPath) ? null : Path.GetFullPath(normalizedCorpusPath),
            ExportNormalizedPath = string.IsNullOrWhiteSpace(exportNormalizedPath) ? null : Path.GetFullPath(exportNormalizedPath),
            ExportOnly = exportOnly,
            CasesJsonPath = string.IsNullOrWhiteSpace(casesPath) ? null : Path.GetFullPath(casesPath),
            Engines = engines,
            MaxResults = maxResults,
            TopK = topK,
            Iterations = iterations,
            WarmupIterations = warmup,
            SemanticIndexTimeout = TimeSpan.FromMilliseconds(semanticTimeoutMs),
            OutputJsonPath = string.IsNullOrWhiteSpace(outputPath) ? null : Path.GetFullPath(outputPath),
        };
        error = string.Empty;
        return true;
    }

    private static string GetDefaultIndexPath()
    {
        var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
        if (!string.IsNullOrWhiteSpace(repoRoot))
        {
            return Path.GetFullPath(Path.Combine(repoRoot, "src", "settings-ui", "Settings.UI", "Assets", "Settings", "search.index.json"));
        }

        // Shared packaged app fallback: expect input in current working directory.
        return Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "search.index.json"));
    }

    private static string? FindRepoRoot(string startingDirectory)
    {
        var current = new DirectoryInfo(startingDirectory);
        while (current != null)
        {
            var markerPath = Path.Combine(current.FullName, "PowerToys.slnx");
            if (File.Exists(markerPath))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return null;
    }

    private static string[] ResolveArgs(string[] args)
    {
        if (args.Length > 0)
        {
            var hasEvaluatorOptions = args.Any(arg => arg.StartsWith("--", StringComparison.Ordinal));
            if (hasEvaluatorOptions)
            {
                return args;
            }

            var startupArgs = LoadStartupArgs();
            if (startupArgs.Length > 0)
            {
                Console.WriteLine("Activation args were ignored because they are not evaluator options.");
                return startupArgs;
            }

            return args;
        }

        var startupArgsFromFile = LoadStartupArgs();
        return startupArgsFromFile.Length > 0 ? startupArgsFromFile : args;
    }

    private static string[] LoadStartupArgs()
    {
        var envArgsPath = Environment.GetEnvironmentVariable("SETTINGS_SEARCH_EVAL_ARGS_FILE");
        if (!string.IsNullOrWhiteSpace(envArgsPath))
        {
            var fromEnv = Path.GetFullPath(Environment.ExpandEnvironmentVariables(envArgsPath));
            var loadedFromEnv = LoadArgsFile(fromEnv);
            if (loadedFromEnv.Length > 0)
            {
                Console.WriteLine($"Loaded {loadedFromEnv.Length} startup args from '{fromEnv}'.");
                return loadedFromEnv;
            }
        }

        foreach (var candidate in GetStartupArgsCandidates())
        {
            var loadedArgs = LoadArgsFile(candidate);
            if (loadedArgs.Length > 0)
            {
                Console.WriteLine($"Loaded {loadedArgs.Length} startup args from '{candidate}'.");
                return loadedArgs;
            }
        }

        return Array.Empty<string>();
    }

    private static IEnumerable<string> GetStartupArgsCandidates()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            yield return Path.Combine(localAppData, "PowerToys.SettingsSearchEvaluation", "launch.args.txt");
        }

        var rawLocalAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        if (!string.IsNullOrWhiteSpace(rawLocalAppData))
        {
            yield return Path.Combine(rawLocalAppData, "PowerToys.SettingsSearchEvaluation", "launch.args.txt");
        }

        yield return Path.Combine(AppContext.BaseDirectory, "launch.args.txt");

        var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
        if (!string.IsNullOrWhiteSpace(repoRoot))
        {
            yield return Path.Combine(repoRoot, "tools", "SettingsSearchEvaluation", "artifacts", "launch.args.txt");
        }
    }

    private static string[] LoadArgsFile(string path)
    {
        if (!File.Exists(path))
        {
            return Array.Empty<string>();
        }

        return File.ReadAllLines(path)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith('#'))
            .ToArray();
    }

    private static bool TryParseEngines(string value, out IReadOnlyList<SearchEngineKind> engines)
    {
        if (string.Equals(value, "both", StringComparison.OrdinalIgnoreCase))
        {
            engines = new[] { SearchEngineKind.Basic, SearchEngineKind.Semantic };
            return true;
        }

        if (string.Equals(value, "basic", StringComparison.OrdinalIgnoreCase))
        {
            engines = new[] { SearchEngineKind.Basic };
            return true;
        }

        if (string.Equals(value, "semantic", StringComparison.OrdinalIgnoreCase))
        {
            engines = new[] { SearchEngineKind.Semantic };
            return true;
        }

        engines = Array.Empty<SearchEngineKind>();
        return false;
    }

    private static bool TryReadValue(string[] args, ref int index, out string? value)
    {
        if (index + 1 >= args.Length)
        {
            value = null;
            return false;
        }

        index++;
        value = args[index];
        return true;
    }

    private static bool TryReadInt(string[] args, ref int index, out int value)
    {
        value = 0;
        if (!TryReadValue(args, ref index, out var text))
        {
            return false;
        }

        return int.TryParse(text, out value);
    }

    private static string GetTextOnlyCorpusPath(string exportPath)
    {
        var directory = Path.GetDirectoryName(exportPath) ?? string.Empty;
        var fileName = Path.GetFileNameWithoutExtension(exportPath);
        var extension = Path.GetExtension(exportPath);
        var suffix = string.IsNullOrWhiteSpace(extension) ? ".text" : $".text{extension}";
        return Path.Combine(directory, $"{fileName}{suffix}");
    }

    private static void PrintSummary(EvaluationReport report, int topK)
    {
        Console.WriteLine();
        Console.WriteLine("=== Evaluation Summary ===");
        Console.WriteLine($"Generated: {report.GeneratedAtUtc:O}");
        Console.WriteLine($"Dataset entries: {report.Dataset.TotalEntries} ({report.Dataset.DistinctIds} distinct ids)");
        Console.WriteLine($"Cases: {report.CaseCount}");
        Console.WriteLine();

        foreach (var engine in report.Engines)
        {
            Console.WriteLine($"[{engine.Engine}]");
            if (!engine.IsAvailable)
            {
                Console.WriteLine($"  Unavailable: {engine.AvailabilityError}");
                Console.WriteLine();
                continue;
            }

            Console.WriteLine($"  Capabilities: {engine.CapabilitiesSummary}");
            Console.WriteLine($"  Indexed entries: {engine.IndexedEntries}");
            Console.WriteLine($"  Indexing time (ms): {engine.IndexingTimeMs:F2}");
            Console.WriteLine($"  Recall@{topK}: {engine.RecallAtK:F4}");
            Console.WriteLine($"  MRR: {engine.Mrr:F4}");
            Console.WriteLine($"  Search latency ms (avg/p50/p95/max): {engine.SearchLatencyMs.AverageMs:F2}/{engine.SearchLatencyMs.P50Ms:F2}/{engine.SearchLatencyMs.P95Ms:F2}/{engine.SearchLatencyMs.MaxMs:F2}");

            var misses = engine.CaseResults
                .Where(result => !result.HitAtK)
                .Take(3)
                .ToList();

            if (misses.Count > 0)
            {
                Console.WriteLine("  Sample misses:");
                foreach (var miss in misses)
                {
                    var top = miss.TopResultIds.Count == 0 ? "(none)" : string.Join(", ", miss.TopResultIds);
                    Console.WriteLine($"    Query='{miss.Query}', expected='{string.Join("|", miss.ExpectedIds)}', top='{top}'");
                }
            }

            Console.WriteLine();
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine("SettingsSearchEvaluation");
        Console.WriteLine("Evaluates basic and semantic settings search for recall and performance.");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  SettingsSearchEvaluation [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --index-json <path>           Path to settings search index JSON.");
        Console.WriteLine("  --normalized-corpus <path>    Path to normalized corpus file (id<TAB>normalized text).");
        Console.WriteLine("  --export-normalized <path>    Export normalized corpus from loaded entries.");
        Console.WriteLine("                                Also writes a text-only companion file '<path>.text<ext>'.");
        Console.WriteLine("  --export-only                 Export normalized corpus and exit.");
        Console.WriteLine("  --cases-json <path>           Optional path to evaluation cases JSON.");
        Console.WriteLine("  --engine <basic|semantic|both> Engine selection. Default: both.");
        Console.WriteLine("  --max-results <n>             Maximum returned results per query. Default: 10.");
        Console.WriteLine("  --top-k <n>                   Recall cut-off K. Default: 5.");
        Console.WriteLine("  --iterations <n>              Measured runs per query. Default: 5.");
        Console.WriteLine("  --warmup <n>                  Warmup runs per query. Default: 1.");
        Console.WriteLine("  --semantic-timeout-ms <n>     Semantic index idle wait timeout in ms. Default: 15000.");
        Console.WriteLine("  --output-json <path>          Optional output report file.");
        Console.WriteLine("  --help                        Show this help.");
        Console.WriteLine();
        Console.WriteLine("Startup args file fallback (when no CLI args are passed):");
        Console.WriteLine("  %LOCALAPPDATA%\\PowerToys.SettingsSearchEvaluation\\launch.args.txt");
        Console.WriteLine("  (Override with env var SETTINGS_SEARCH_EVAL_ARGS_FILE)");
    }
}
