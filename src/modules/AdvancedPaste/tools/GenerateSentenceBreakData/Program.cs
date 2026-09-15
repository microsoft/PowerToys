// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Security.Cryptography;
using System.Text;

const string UnicodeVersion = "17.0.0";
const string ExpectedSha256 = "871c0c985ad95125e25b302414065a10839d068970bceb383ecec138f22a0a18";
const int MaximumScalar = 0x10FFFF;
string[] propertyNames = ["Other", "ATerm", "Close", "Format", "Lower", "Numeric", "OLetter", "Sep", "Sp", "STerm", "Upper", "CR", "Extend", "LF", "SContinue"];
var knownProperties = propertyNames.ToHashSet(StringComparer.Ordinal);

if (args.Length is < 2 or > 3 || (args.Length == 3 && args[0] != "--verify"))
{
    Console.Error.WriteLine("Usage: GenerateSentenceBreakData [--verify] <SentenceBreakProperty.txt> <SentenceBreakData.g.cs>");
    return 2;
}

bool verify = args.Length == 3;
string sourcePath = Path.GetFullPath(args[verify ? 1 : 0]);
string outputPath = Path.GetFullPath(args[verify ? 2 : 1]);

try
{
    byte[] sourceBytes = File.ReadAllBytes(sourcePath);
    string actualHash = Convert.ToHexStringLower(SHA256.HashData(sourceBytes));
    if (!actualHash.Equals(ExpectedSha256, StringComparison.Ordinal))
    {
        throw new InvalidDataException($"Source SHA-256 mismatch. Expected {ExpectedSha256}, got {actualHash}.");
    }

    string source = Encoding.UTF8.GetString(sourceBytes);
    if (!source.Contains($"SentenceBreakProperty-{UnicodeVersion}.txt", StringComparison.Ordinal))
    {
        throw new InvalidDataException($"Source header does not identify Unicode {UnicodeVersion}.");
    }

    var ranges = ParseRanges(source);
    var boundaries = BuildBoundaries(ranges);
    if (boundaries.Count != 3363)
    {
        throw new InvalidDataException($"Expected the verified Unicode source to produce 3363 boundaries, but produced {boundaries.Count}.");
    }

    VerifyLookup(ranges, boundaries);
    string generated = GenerateSource(boundaries);
    byte[] generatedBytes = new UTF8Encoding(false).GetBytes(generated);

    if (verify)
    {
        byte[] existing = File.ReadAllBytes(outputPath);
        if (!existing.AsSpan().SequenceEqual(generatedBytes))
        {
            throw new InvalidDataException($"Generated output is stale: {outputPath}");
        }

        Console.WriteLine($"Verified Unicode {UnicodeVersion}: {boundaries.Count} boundaries, 1,112,064 valid scalars, 0 mismatches.");
    }
    else
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllBytes(outputPath, generatedBytes);
        Console.WriteLine($"Generated {outputPath}: {boundaries.Count} boundaries.");
    }

    return 0;
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or FormatException)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

List<PropertyRange> ParseRanges(string source)
{
    var ranges = new List<PropertyRange>();
    bool missingOther = false;
    int lineNumber = 0;
    foreach (string rawLine in source.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
    {
        lineNumber++;
        string trimmed = rawLine.Trim();
        if (trimmed.StartsWith("# @missing:", StringComparison.Ordinal))
        {
            string declaration = trimmed[11..].Trim();
            missingOther |= declaration.Equals("0000..10FFFF; Other", StringComparison.Ordinal);
        }

        string data = rawLine.Split('#', 2)[0].Trim();
        if (data.Length == 0)
        {
            continue;
        }

        string[] fields = data.Split(';', StringSplitOptions.TrimEntries);
        if (fields.Length != 2 || !knownProperties.Contains(fields[1]))
        {
            throw new InvalidDataException($"Line {lineNumber}: malformed entry or unknown property.");
        }

        string[] endpoints = fields[0].Split("..", StringSplitOptions.None);
        if (endpoints.Length is < 1 or > 2 || !TryScalar(endpoints[0], out int start) || !TryScalar(endpoints[^1], out int end) || start > end)
        {
            throw new InvalidDataException($"Line {lineNumber}: malformed scalar range.");
        }

        ranges.Add(new(start, end, fields[1]));
    }

    if (!missingOther)
    {
        throw new InvalidDataException("Source must declare @missing: 0000..10FFFF; Other.");
    }

    // The normative file groups ranges by property rather than by scalar value.
    ranges.Sort(static (left, right) => left.Start.CompareTo(right.Start));
    for (int index = 1; index < ranges.Count; index++)
    {
        if (ranges[index].Start <= ranges[index - 1].End)
        {
            throw new InvalidDataException($"Overlapping ranges at U+{ranges[index].Start:X4}.");
        }
    }

    return ranges;
}

bool TryScalar(string text, out int scalar) =>
    int.TryParse(text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out scalar) && scalar is >= 0 and <= MaximumScalar;

List<Boundary> BuildBoundaries(List<PropertyRange> ranges)
{
    var result = new List<Boundary>();
    AddBoundary(0, "Other");
    foreach (PropertyRange range in ranges)
    {
        AddBoundary(range.Start, range.Property);
        if (range.End < MaximumScalar)
        {
            AddBoundary(range.End + 1, "Other");
        }
    }

    return result;

    void AddBoundary(int scalar, string property)
    {
        if (result.Count != 0 && result[^1].Scalar == scalar)
        {
            result[^1] = new(scalar, property);
            if (result.Count > 1 && result[^2].Property == property)
            {
                result.RemoveAt(result.Count - 1);
            }
        }
        else if (result.Count == 0 || result[^1].Property != property)
        {
            result.Add(new(scalar, property));
        }
    }
}

void VerifyLookup(List<PropertyRange> ranges, List<Boundary> boundaries)
{
    int rangeIndex = 0;
    int boundaryIndex = 0;
    for (int scalar = 0; scalar <= MaximumScalar; scalar++)
    {
        if (scalar is >= 0xD800 and <= 0xDFFF)
        {
            continue;
        }

        while (rangeIndex < ranges.Count && scalar > ranges[rangeIndex].End)
        {
            rangeIndex++;
        }

        string expected = rangeIndex < ranges.Count && scalar >= ranges[rangeIndex].Start ? ranges[rangeIndex].Property : "Other";
        while (boundaryIndex + 1 < boundaries.Count && boundaries[boundaryIndex + 1].Scalar <= scalar)
        {
            boundaryIndex++;
        }

        if (boundaries[boundaryIndex].Property != expected)
        {
            throw new InvalidDataException($"Generated lookup mismatch at U+{scalar:X4}: expected {expected}, got {boundaries[boundaryIndex].Property}.");
        }
    }
}

string GenerateSource(List<Boundary> boundaries)
{
    var builder = new StringBuilder();
    builder.Append("// Copyright (c) Microsoft Corporation\n// The Microsoft Corporation licenses this file to you under the MIT license.\n// See the LICENSE file in the project root for more information.\n\n");
    builder.Append("// <auto-generated>\n// Generated file. Do not edit manually.\n");
    builder.Append("// Unicode version: 17.0.0\n// Source: SentenceBreakProperty.txt\n");
    builder.Append("// Source URL: https://www.unicode.org/Public/17.0.0/ucd/auxiliary/SentenceBreakProperty.txt\n");
    builder.Append("// Source SHA-256: 871c0c985ad95125e25b302414065a10839d068970bceb383ecec138f22a0a18\n");
    builder.Append("// Generator: src/modules/AdvancedPaste/tools/GenerateSentenceBreakData\n");
    builder.Append("// Unicode License: src/modules/AdvancedPaste/UnicodeData/LICENSE.txt\n// </auto-generated>\n\n");
    builder.Append("using static AdvancedPaste.Helpers.SentenceBreakType;\n\nnamespace AdvancedPaste.Helpers;\n\ninternal enum SentenceBreakType : byte\n{\n");
    foreach (string property in propertyNames)
    {
        builder.Append("    ").Append(property).Append(",\n");
    }

    builder.Append("}\n\ninternal static class SentenceBreakData\n{\n    private static System.ReadOnlySpan<int> BoundaryStarts =>\n    [\n");
    AppendValues(boundaries.Select(static boundary => $"0x{boundary.Scalar:X}"));
    builder.Append("    ];\n\n    private static System.ReadOnlySpan<SentenceBreakType> BoundaryTypes =>\n    [\n");
    AppendValues(boundaries.Select(static boundary => boundary.Property));
    builder.Append("    ];\n\n    internal static SentenceBreakType GetSentenceBreakType(int scalar)\n    {\n");
    builder.Append("        if ((uint)scalar > 0x10FFFF)\n        {\n            throw new System.ArgumentOutOfRangeException(nameof(scalar));\n        }\n\n");
    builder.Append("        int low = 0;\n        int high = BoundaryStarts.Length - 1;\n        while (low <= high)\n        {\n            int middle = low + ((high - low) / 2);\n            if (BoundaryStarts[middle] <= scalar)\n            {\n                low = middle + 1;\n            }\n            else\n            {\n                high = middle - 1;\n            }\n        }\n\n        return BoundaryTypes[high];\n    }\n}\n");
    return builder.ToString();

    void AppendValues(IEnumerable<string> values)
    {
        int column = 0;
        foreach (string value in values)
        {
            if (column == 0)
            {
                builder.Append("        ");
            }

            builder.Append(value).Append(',');
            column++;
            if (column == 8)
            {
                builder.Append('\n');
                column = 0;
            }
            else
            {
                builder.Append(' ');
            }
        }

        if (column != 0)
        {
            builder.Length--;
            builder.Append('\n');
        }
    }
}

internal readonly record struct PropertyRange(int Start, int End, string Property);

internal readonly record struct Boundary(int Scalar, string Property);
