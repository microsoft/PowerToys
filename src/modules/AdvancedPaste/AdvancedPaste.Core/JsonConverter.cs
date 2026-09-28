// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using Newtonsoft.Json;

namespace AdvancedPaste.Core;

public static class JsonConverter
{
    private static readonly Regex IniSectionNameRegex = new(@"^\[(.+)\]");
    private static readonly Regex IniValueLineRegex = new(@"^([^=]+)\s*=\s*(.*)$");
    private static readonly char[] CsvDelimiters = [',', ';', '\t'];
    private static readonly Regex CsvSeparatorIdentifierRegex = new(@"^sep=(.)$", RegexOptions.IgnoreCase);
    private static readonly string CsvDelimiterSeparatorRegex = @"(?=(?:[^""]*""[^""]*"")*(?![^""]*""))";
    private static readonly Regex CsvRemoveSingleQuotationMarksRegex = new(@"^""(?!"")|(?<!"")""$|^""""$");
    private static readonly Regex CsvRemoveStartAndEndQuotationMarksRegex = new(@"^""(?=(""{2})+)|(?<=(""{2})+)""$");
    private static readonly Regex CsvReplaceDoubleQuotationMarksRegex = new(@"""{2}");

    public static string Convert(string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        cancellationToken.ThrowIfCancellationRequested();

        if (IsJson(text))
        {
            cancellationToken.ThrowIfCancellationRequested();
            return text;
        }

        if (TryConvertXml(text, cancellationToken, out var jsonText))
        {
            return jsonText;
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (TryConvertIni(text, cancellationToken, out jsonText))
        {
            return jsonText;
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (TryConvertCsv(text, cancellationToken, out jsonText))
        {
            return jsonText;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var serialized = JsonConvert.SerializeObject(
            SplitLines(text),
            Newtonsoft.Json.Formatting.Indented);
        cancellationToken.ThrowIfCancellationRequested();
        return serialized;
    }

    private static bool IsJson(string text)
    {
        try
        {
            _ = JsonDocument.Parse(text);
            return true;
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }

    private static bool TryConvertXml(string text, CancellationToken cancellationToken, out string jsonText)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var document = new XmlDocument();
            document.LoadXml(text);
            cancellationToken.ThrowIfCancellationRequested();
            jsonText = JsonConvert.SerializeXmlNode(document, Newtonsoft.Json.Formatting.Indented);
            cancellationToken.ThrowIfCancellationRequested();
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            cancellationToken.ThrowIfCancellationRequested();
            jsonText = string.Empty;
            return false;
        }
    }

    private static bool TryConvertIni(string text, CancellationToken cancellationToken, out string jsonText)
    {
        try
        {
            var lines = SplitLines(text)
                .Where(line => !line.StartsWith(';'))
                .ToArray();
            if (lines.Length < 2
                || !IniSectionNameRegex.IsMatch(lines[0])
                || (!IniSectionNameRegex.IsMatch(lines[1]) && !IniValueLineRegex.IsMatch(lines[1])))
            {
                jsonText = string.Empty;
                return false;
            }

            var ini = new Dictionary<string, Dictionary<string, string>>();
            var lastSectionName = string.Empty;
            foreach (var line in lines)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var section = IniSectionNameRegex.Match(line);
                var keyValue = IniValueLineRegex.Match(line);
                if (section.Success)
                {
                    lastSectionName = section.Groups[1].Value.Trim();
                    if (string.IsNullOrWhiteSpace(lastSectionName))
                    {
                        throw new FormatException();
                    }

                    ini.Add(lastSectionName, new Dictionary<string, string>());
                }
                else if (!keyValue.Success || string.IsNullOrWhiteSpace(keyValue.Groups[1].Value))
                {
                    throw new FormatException();
                }
                else
                {
                    ini[lastSectionName].Add(keyValue.Groups[1].Value.Trim(), keyValue.Groups[2].Value);
                }
            }

            jsonText = JsonConvert.SerializeObject(ini, Newtonsoft.Json.Formatting.Indented);
            cancellationToken.ThrowIfCancellationRequested();
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            cancellationToken.ThrowIfCancellationRequested();
            jsonText = string.Empty;
            return false;
        }
    }

    private static bool TryConvertCsv(string text, CancellationToken cancellationToken, out string jsonText)
    {
        try
        {
            var lines = SplitLines(text);
            GetCsvDelimiter(lines, out var delimiter, out var delimiterCount);
            var delimiterPattern = GetCsvDelimiterPattern(delimiter);
            var csv = new List<IEnumerable<string>>();
            foreach (var line in lines)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (CsvSeparatorIdentifierRegex.IsMatch(line))
                {
                    continue;
                }

                if (Regex.Count(line, delimiterPattern) != delimiterCount
                    || !int.IsEvenInteger(line.Count(character => character == '"')))
                {
                    throw new FormatException();
                }

                csv.Add(Regex.Split(line, delimiterPattern, RegexOptions.IgnoreCase)
                    .Select(ReplaceQuotationMarksInCsvData));
            }

            jsonText = JsonConvert.SerializeObject(csv, Newtonsoft.Json.Formatting.Indented);
            cancellationToken.ThrowIfCancellationRequested();
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            cancellationToken.ThrowIfCancellationRequested();
            jsonText = string.Empty;
            return false;
        }
    }

    private static void GetCsvDelimiter(string[] csvLines, out char delimiter, out int delimiterCount)
    {
        delimiter = '\0';
        delimiterCount = 0;
        if (csvLines.Length > 1)
        {
            var separator = CsvSeparatorIdentifierRegex.Match(csvLines[0]);
            if (separator.Success)
            {
                delimiter = separator.Groups[1].Value.Trim()[0];
                delimiterCount = Regex.Count(csvLines[1], GetCsvDelimiterPattern(delimiter), RegexOptions.IgnoreCase);
            }
        }

        if (csvLines.Length > 0 && delimiterCount == 0)
        {
            foreach (var candidate in CsvDelimiters)
            {
                var candidatePattern = GetCsvDelimiterPattern(candidate);
                var firstLineCount = Regex.Count(csvLines[0], candidatePattern, RegexOptions.IgnoreCase);
                var secondLineCount = csvLines.Length >= 2
                    ? Regex.Count(csvLines[1], candidatePattern, RegexOptions.IgnoreCase)
                    : 0;
                if (firstLineCount > delimiterCount && (secondLineCount == 0 || secondLineCount == firstLineCount))
                {
                    delimiter = candidate;
                    delimiterCount = firstLineCount;
                }
            }
        }

        if (delimiterCount == 0)
        {
            throw new FormatException();
        }
    }

    private static string GetCsvDelimiterPattern(char delimiter)
        => Regex.Escape(delimiter.ToString()) + CsvDelimiterSeparatorRegex;

    private static string ReplaceQuotationMarksInCsvData(string value)
    {
        value = CsvRemoveSingleQuotationMarksRegex.Replace(value, string.Empty);
        value = CsvRemoveStartAndEndQuotationMarksRegex.Replace(value, string.Empty);
        return CsvReplaceDoubleQuotationMarksRegex.Replace(value, "\"");
    }

    private static string[] SplitLines(string text)
        => Regex.Split(text, "\r\n|\n|\r")
            .Where(line => line.Length > 0)
            .ToArray();
}
