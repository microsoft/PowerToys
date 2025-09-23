// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.CmdPal.Ext.Bookmarks.Helpers;

namespace Microsoft.CmdPal.Ext.Bookmarks.Services;

internal sealed partial class BookmarkResolver : IBookmarkResolver
{
    private readonly IPlaceholderParser _placeholderParser;

    private const string UriSchemeShell = "shell";

    public BookmarkResolver(IPlaceholderParser placeholderParser)
    {
        ArgumentNullException.ThrowIfNull(placeholderParser);
        _placeholderParser = placeholderParser;
    }

    public async Task<(bool Success, Classification Result)> TryClassifyAsync(
        string? input,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await Task.Run(
                () => TryClassify(input, out var classification)
                    ? classification
                    : Classification.Unknown(input ?? string.Empty),
                cancellationToken);
            return (true, result);
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to classify", ex);
            var result = Classification.Unknown(input ?? string.Empty);
            return (false, result);
        }
    }

    public Classification ClassifyOrUnknown(string input)
    {
        return TryClassify(input, out var c) ? c : Classification.Unknown(input);
    }

    private bool TryClassify(string? input, [NotNullWhen(true)] out Classification result)
    {
        try
        {
            var success = TryClassifyCore(input, out result);
            return success;
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to classify", ex);
            result = Classification.Unknown(input ?? string.Empty);
            return false;
        }
    }

    private bool TryClassifyCore(string? input, out Classification result)
    {
        result = Classification.Unknown(input ?? string.Empty);

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        input = input.Trim();

        // is placeholder?
        var isPlaceholder = _placeholderParser.ParsePlaceholders(input, out var inputUntilFirstPlaceholder, out _);
        return ClassifyWithoutPlaceholder(input, out result, isPlaceholder, inputUntilFirstPlaceholder);
    }

    private static bool ClassifyWithoutPlaceholder(string input, out Classification result, bool isPlaceholder, string inputUntilFirstPlaceholder)
    {
        // 1) UWP/AppX via AppsFolder/AUMID or pkgfamily!app
        if (IsUwpAumidLike(input))
        {
            result = new Classification(
                CommandKind.UwpAumid,
                input,
                NormalizeAumid(input),
                string.Empty,
                LaunchMethod.UwpActivate,
                null,
                isPlaceholder);

            return true;
        }

        // 2) Try URI parsing first (accepts custom schemes, e.g., shell:, ms-settings:)
        //    File URIs must start with "file:" to avoid confusion with local paths - which are handled below, in more sophisticated ways -
        //    as TryCreate would automatically add "file://" to bare paths like "C:\path\to\file.txt" which we don't want.
        if (Uri.TryCreate(input, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Scheme)
                                                                && (uri.Scheme != Uri.UriSchemeFile || input.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
                                                                && uri.Scheme != UriSchemeShell)
        {
            // http/https → Url; any other scheme → Protocol (mailto:, ms-settings:, slack://, etc.)
            var isWeb = uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;

            result = new Classification(
                isWeb ? CommandKind.WebUrl : CommandKind.Protocol,
                input,
                input,
                string.Empty,
                LaunchMethod.ShellExecute, // Shell picks the right handler
                null,
                isPlaceholder);

            return true;
        }

        // 2a) We're a placeholder and start look like a protocol scheme (e.g. "myapp:{{placeholder}}")
        if (isPlaceholder && !string.IsNullOrWhiteSpace(inputUntilFirstPlaceholder) && inputUntilFirstPlaceholder.Contains(':'))
        {
            var indexOfColon = inputUntilFirstPlaceholder.IndexOf(':');
            var scheme = inputUntilFirstPlaceholder[..indexOfColon];

            if (scheme.Length > 1 && scheme != Uri.UriSchemeFile && scheme != UriSchemeShell)
            {
                var isWeb = scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) || scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);

                result = new Classification(
                    isWeb ? CommandKind.WebUrl : CommandKind.Protocol,
                    input,
                    input,
                    string.Empty,
                    LaunchMethod.ShellExecute, // Shell picks the right handler
                    null,
                    isPlaceholder);
                return true;
            }
        }

        // 5) Shortcuts (.lnk/.url)
        if (LooksLikeExisting(input, out var existingFull))
        {
            var ext = Path.GetExtension(existingFull);
            if (ext.Equals(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                result = new Classification(
                    CommandKind.Shortcut,
                    input,
                    existingFull,
                    string.Empty, // arguments come from the .lnk at launch time
                    LaunchMethod.ShellExecute,
                    Path.GetDirectoryName(existingFull),
                    isPlaceholder);

                return true;
            }

            if (ext.Equals(".url", StringComparison.OrdinalIgnoreCase))
            {
                // ShellExecute on the .url works; if you want, you can read the INI to extract URL.
                result = new Classification(
                    CommandKind.InternetShortcut,
                    input,
                    existingFull,
                    string.Empty,
                    LaunchMethod.ShellExecute,
                    Path.GetDirectoryName(existingFull),
                    isPlaceholder);

                return true;
            }
        }

        // 6) Existing file/dir or "longest plausible prefix"
        // Try to grow head (only for unquoted original) to include spaces until a path exists.
        var (headPath, tailArgs) = ExpandToBestExistingPath(input, isPlaceholder);
        if (headPath is not null)
        {
            var args = tailArgs ?? string.Empty;

            if (Directory.Exists(headPath))
            {
                result = new Classification(
                    CommandKind.Directory,
                    input,
                    headPath,
                    string.Empty,
                    LaunchMethod.ExplorerOpen,
                    headPath,
                    isPlaceholder);

                return true;
            }

            if (File.Exists(headPath) || isPlaceholder)
            {
                if (ShellHelpers.IsExecutableFile(headPath))
                {
                    result = new Classification(
                        CommandKind.FileExecutable,
                        input,
                        headPath,
                        args,
                        LaunchMethod.ShellExecute, // direct exec; or ShellExecute if you want verb support
                        Path.GetDirectoryName(headPath),
                        isPlaceholder);
                }
                else
                {
                    result = new Classification(
                        CommandKind.FileDocument,
                        input,
                        headPath,
                        args, // docs seldom take args, but keep user intent
                        LaunchMethod.ShellExecute,
                        Path.GetDirectoryName(headPath),
                        isPlaceholder);
                }

                return true;
            }
        }

        // 7a) Bare command resolution via PATH + executable ext
        // At this point 'head' is our best intended command token.
        var (head, tail) = SplitHeadAndArgs(input);

        if (TryResolveViaPath(head, out var resolvedExe))
        {
            result = new Classification(
                CommandKind.PathCommand,
                input,
                resolvedExe,
                tail,
                LaunchMethod.ShellExecute,
                null,
                isPlaceholder);

            return true;
        }

        // 7b) If it looks like a path with ext but missing file, treat as document (Shell will handle assoc / error)
        if (LooksPathy(head) && Path.HasExtension(head))
        {
            // if the path extension contains placeholders, we can't assume what it is so, skip it and treat it as unknown
            var hasSpecificExtension = !isPlaceholder || !Path.GetExtension(head).Contains('{');
            if (hasSpecificExtension)
            {
                result = new Classification(
                    ShellHelpers.IsExecutableFile(head) ? CommandKind.FileExecutable : CommandKind.FileDocument,
                    input,
                    head,
                    tail,
                    LaunchMethod.ShellExecute,
                    HasDir(head) ? Path.GetDirectoryName(head) : null,
                    isPlaceholder);

                return true;
            }
        }

        // 8) looks like a web URL without scheme, but not like a file with extension
        if (head.Contains('.', StringComparison.OrdinalIgnoreCase) && head.StartsWith("www", StringComparison.OrdinalIgnoreCase))
        {
            // treat as URL, add https://
            var url = "https://" + input;
            result = new Classification(
                CommandKind.WebUrl,
                input,
                url,
                string.Empty,
                LaunchMethod.ShellExecute,
                null,
                isPlaceholder);
            return true;
        }

        // 9) Fallback: let ShellExecute try the whole input
        result = new Classification(
            CommandKind.Unknown,
            input,
            input,
            string.Empty,
            LaunchMethod.ShellExecute,
            null,
            isPlaceholder);

        return true;
    }

    private static (string Head, string Tail) SplitHeadAndArgs(string input)
    {
        if (input.StartsWith('"'))
        {
            var token = ReadQuoted(input, out var consumed);
            if (consumed > 0)
            {
                var head = token;
                var tail = consumed < input.Length ? input[consumed..].TrimStart() : string.Empty;
                return (head, tail);
            }

            return (input, string.Empty); // no closing quote
        }

        // take until first whitespace
        for (var i = 0; i < input.Length; i++)
        {
            if (char.IsWhiteSpace(input[i]))
            {
                return (input[..i], input[(i + 1)..].TrimStart());
            }
        }

        return (input, string.Empty);
    }

    // Finds the best existing path prefix in an *unquoted* input by scanning
    // whitespace boundaries. Prefers files over directories; for same kind,
    // prefers the longer path. Returns (head, tail) or (null, null) if nothing found.
    private static (string? Head, string? Tail) ExpandToBestExistingPath(string input, bool containsPlaceholders = false)
    {
        try
        {
            // Quick exit if input starts with a quote – we don't handle quoted here
            if (input.StartsWith('"'))
            {
                return (null, null);
            }

            var head = input;

            // Find the next quote and ignore anything after it
            var quoteIndex = head.IndexOf('"');
            if (quoteIndex >= 0)
            {
                head = head[..quoteIndex].TrimEnd();
            }

            // Be greedy: try to find the longest existing path prefix
            for (var i = head.Length; i >= 0; i--)
            {
                if (i < head.Length && !char.IsWhiteSpace(head[i]))
                {
                    continue;
                }

                var candidate = head.AsSpan(0, i).TrimEnd().ToString();
                if (candidate.Length == 0)
                {
                    continue;
                }

                // If we have placeholders, check if this candidate would contain a non-path placeholder
                if (containsPlaceholders && ContainsNonPathPlaceholder(candidate))
                {
                    continue; // Skip this candidate, try a shorter one
                }

                try
                {
                    // Expand environment variables to help discovery
                    var expanded = Environment.ExpandEnvironmentVariables(candidate);

                    if (input.StartsWith("shell:", StringComparison.OrdinalIgnoreCase) ||
                        input.StartsWith("::", StringComparison.OrdinalIgnoreCase))
                    {
                        // shell:Downloads, shell:::{GUID}, ::{GUID}
                        var firstSeparator = expanded.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]);
                        var firstSegment = firstSeparator >= 0 ? expanded[..firstSeparator] : expanded;
                        var rest = firstSeparator >= 0 ? expanded[firstSeparator..] : string.Empty;
                        if (ShellNames.TryGetFileSystemPath(firstSegment, out var fsPath))
                        {
                            expanded = fsPath + rest;
                        }
                    }

                    // Check if this candidate exists as file or dir
                    var isFile = File.Exists(expanded);
                    var isDir = !isFile && Directory.Exists(expanded);
                    if (isFile || isDir)
                    {
                        var full = Path.GetFullPath(expanded);
                        var tail = i < input.Length ? input[i..].TrimStart() : string.Empty;
                        return (full, tail);
                    }
                }
                catch
                {
                    // Ignore malformed paths; keep scanning
                }
            }

            return (null, null);
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to find best path", ex);
            throw;
        }
    }

    private static bool ContainsNonPathPlaceholder(string candidate)
    {
        // Look for placeholders that are likely command arguments rather than path components
        var regex = new Regex(@"\{[a-zA-Z0-9_-]+\}");
        var matches = regex.Matches(candidate);

        foreach (Match match in matches)
        {
            var placeholderContext = GetPlaceholderContextInFileSystemPath(candidate, match.Index);

            // If placeholder appears after what looks like a command-line flag/option
            if (placeholderContext.IsAfterFlag)
            {
                return true;
            }

            // If placeholder doesn't look like a typical path component
            if (!placeholderContext.LooksLikePathComponent)
            {
                return true;
            }
        }

        return false;
    }

    private static PlaceholderContext GetPlaceholderContextInFileSystemPath(string input, int placeholderIndex)
    {
        var beforePlaceholder = input[..placeholderIndex].TrimEnd();

        var isAfterFlag = beforePlaceholder.EndsWith(" -", StringComparison.OrdinalIgnoreCase) ||
                          beforePlaceholder.EndsWith(" /", StringComparison.OrdinalIgnoreCase) ||
                          beforePlaceholder.EndsWith(" --", StringComparison.OrdinalIgnoreCase);

        var looksLikePathComponent = !isAfterFlag;

        var nearbyText = input.Substring(Math.Max(0, placeholderIndex - 20), Math.Min(40, input.Length - Math.Max(0, placeholderIndex - 20)));
        var hasPathSeparators = nearbyText.Contains('\\') || nearbyText.Contains('/');

        if (!hasPathSeparators && isAfterFlag)
        {
            looksLikePathComponent = false;
        }

        return new PlaceholderContext(isAfterFlag, looksLikePathComponent);
    }

    private static bool IsUwpAumidLike(string s)
    {
        // Examples:
        //   shell:AppsFolder\Microsoft.WindowsTerminal_8wekyb3d8bbwe!App
        //   Microsoft.WindowsTerminal_8wekyb3d8bbwe!App
        if (s.StartsWith("shell:AppsFolder\\", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return s.Contains('!') && s.Contains('_'); // cheap heuristic for pkgfamily!app
    }

    private static string NormalizeAumid(string s)
    {
        if (s.StartsWith("shell:AppsFolder\\", StringComparison.OrdinalIgnoreCase))
        {
            return s; // already fully qualified
        }

        return "shell:AppsFolder\\" + s;
    }

    private static bool LooksLikeExisting(string path, out string fullPath)
    {
        try
        {
            // Expand environment variables for convenience
            path = Environment.ExpandEnvironmentVariables(path);
            if (File.Exists(path) || Directory.Exists(path))
            {
                fullPath = Path.GetFullPath(path);
                return true;
            }
        }
        catch
        {
            /* ignore */
        }

        fullPath = path;
        return false;
    }

    private static bool LooksPathy(string input)
    {
        // Basic: drive:\, UNC, relative with . or .., or has dir separator
        if (input.Contains('\\') || input.Contains('/'))
        {
            return true;
        }

        if (input is [_, ':', ..])
        {
            return true;
        }

        if (input.StartsWith(@"\\", StringComparison.InvariantCulture) || input.StartsWith("./", StringComparison.InvariantCulture) || input.StartsWith(".\\", StringComparison.InvariantCulture) || input.StartsWith("..\\", StringComparison.InvariantCulture))
        {
            return true;
        }

        return false;
    }

    private static bool HasDir(string path) => !string.IsNullOrEmpty(Path.GetDirectoryName(path));

    private static bool TryResolveViaPath(string head, out string resolvedExe)
    {
        resolvedExe = string.Empty;

        if (string.IsNullOrWhiteSpace(head))
        {
            return false;
        }

        if (Path.HasExtension(head) && ShellHelpers.FileExistInPath(head, out resolvedExe))
        {
            return true;
        }

        // If head has dir, treat as path probe
        if (HasDir(head))
        {
            if (Path.HasExtension(head))
            {
                var p = TryProbe(Environment.CurrentDirectory, head);
                if (p is not null)
                {
                    resolvedExe = p;
                    return true;
                }

                return false;
            }

            foreach (var ext in ShellHelpers.ExecutableExtensions)
            {
                var p = TryProbe(null, head + ext);
                if (p is not null)
                {
                    resolvedExe = p;
                    return true;
                }
            }

            return false;
        }

        return ShellHelpers.TryResolveExecutableAsShell(head, out resolvedExe);
    }

    private static string? TryProbe(string? dir, string name)
    {
        try
        {
            var path = dir is null ? name : Path.Combine(dir, name);
            if (File.Exists(path))
            {
                return Path.GetFullPath(path);
            }
        }
        catch
        {
            /* ignore */
        }

        return null;
    }

    private static string ReadQuoted(string input, out int consumed)
    {
        var sb = new StringBuilder(input.Length);
        var i = 1;
        while (i < input.Length)
        {
            var c = input[i];
            if (c == '"')
            {
                // double quote -> escaped
                if (i + 1 < input.Length && input[i + 1] == '"')
                {
                    sb.Append('"');
                    i += 2;
                    continue;
                }

                i++; // consume closing quote
                while (i < input.Length && char.IsWhiteSpace(input[i]))
                {
                    i++;
                }

                consumed = i;
                return sb.ToString();
            }

            sb.Append(c);
            i++;
        }

        consumed = 0; // no closing quote
        return sb.ToString();
    }

    private record PlaceholderContext(bool IsAfterFlag, bool LooksLikePathComponent);
}
