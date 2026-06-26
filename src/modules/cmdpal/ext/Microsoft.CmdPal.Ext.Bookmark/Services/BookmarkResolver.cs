// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
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

    private bool TryClassify(string? input, out Classification result)
    {
        try
        {
            bool success;

            if (string.IsNullOrWhiteSpace(input))
            {
                result = Classification.Unknown(input ?? string.Empty);
                success = false;
            }
            else
            {
                input = input.Trim();

                // is placeholder?
                var isPlaceholder = _placeholderParser.ParsePlaceholders(input, out var inputUntilFirstPlaceholder, out _);
                success = ClassifyCore(input, out result, isPlaceholder, inputUntilFirstPlaceholder, _placeholderParser);
            }

            return success;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to classify bookmark \"{input}\"", ex);
            result = Classification.Unknown(input ?? string.Empty);
            return false;
        }
    }

    private static bool ClassifyCore(string input, out Classification result, bool isPlaceholder, string inputUntilFirstPlaceholder, IPlaceholderParser placeholderParser)
    {
        // 1) Try URI parsing first (accepts custom schemes, e.g., shell:, ms-settings:)
        //    File URIs must start with "file:" to avoid confusion with local paths - which are handled below, in more sophisticated ways -
        //    as TryCreate would automatically add "file://" to bare paths like "C:\path\to\file.txt" which we don't want.
        if (Uri.TryCreate(input, UriKind.Absolute, out var uri)
            && !string.IsNullOrWhiteSpace(uri.Scheme)
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

        // 1a) We're a placeholder and start look like a protocol scheme (e.g. "myapp:{{placeholder}}")
        if (isPlaceholder && UriHelper.TryGetScheme(inputUntilFirstPlaceholder, out var scheme, out _))
        {
            // single letter schemes are probably drive letters, ignore, file and shell protocols are handled elsewhere
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

        // 2) Existing file/dir or "longest plausible prefix"
        // Try to grow head (only for unquoted original) to include spaces until a path exists.

        // Find longest unquoted argument string
        var (longestUnquotedHead, tailAfterLongestUnquotedHead) = CommandLineHelper.SplitLongestHeadBeforeQuotedArg(input);
        if (longestUnquotedHead == string.Empty)
        {
            (longestUnquotedHead, tailAfterLongestUnquotedHead) = CommandLineHelper.SplitHeadAndArgs(input);
        }

        var (headPath, tailArgs) = ExpandToBestExistingPath(longestUnquotedHead, tailAfterLongestUnquotedHead, isPlaceholder, placeholderParser);
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

            var ext = Path.GetExtension(headPath);
            if (ShellHelpers.IsExecutableExtension(ext))
            {
                result = new Classification(
                    CommandKind.FileExecutable,
                    input,
                    headPath,
                    args,
                    LaunchMethod.ShellExecute, // direct exec; or ShellExecute if you want verb support
                    Path.GetDirectoryName(headPath),
                    isPlaceholder);

                return true;
            }

            var isShellLink = ext.Equals(".lnk", StringComparison.OrdinalIgnoreCase);
            var isUrlLink = ext.Equals(".url", StringComparison.OrdinalIgnoreCase);
            if (isShellLink || isUrlLink)
            {
                // In the future we can fetch data out of the link
                result = new Classification(
                    isUrlLink ? CommandKind.InternetShortcut : CommandKind.Shortcut,
                    input,
                    headPath,
                    string.Empty,
                    LaunchMethod.ShellExecute,
                    Path.GetDirectoryName(headPath),
                    isPlaceholder);

                return true;
            }

            result = new Classification(
                CommandKind.FileDocument,
                input,
                headPath,
                args,
                LaunchMethod.ShellExecute,
                Path.GetDirectoryName(headPath),
                isPlaceholder);

            return true;
        }

        if (TryGetAumid(longestUnquotedHead, out var aumid))
        {
            result = new Classification(
                CommandKind.Aumid,
                longestUnquotedHead,
                aumid,
                tailAfterLongestUnquotedHead,
                LaunchMethod.ActivateAppId,
                null,
                isPlaceholder);

            return true;
        }

        // 3) Bare command resolution via PATH + executable ext
        // At this point 'head' is our best intended command token.
        var (firstHead, tail) = SplitHeadAndArgs(input);
        CommandLineHelper.ExpandPathToPhysicalFile(firstHead, true, out var head);

        // Normalize a copy for path-detection/probing only. We must not overwrite 'head'
        // itself, because normalization trims trailing whitespace and would turn a meaningful
        // token (e.g. a quoted single space) into an empty string for the fallback target below.
        var normalizedHead = NormalizePathForWindows(head);

        // 3.1) UWP/AppX via AppsFolder/AUMID or pkgfamily!app
        //      Since the AUMID can be actually anything, we either take a full shell:AppsFolder\AUMID
        //      as entered and we try to detect packaged app ids (pkgfamily!app).
        if (TryGetAumid(normalizedHead, out var aumid2))
        {
            result = new Classification(
                CommandKind.Aumid,
                normalizedHead,
                aumid2,
                tail,
                LaunchMethod.ActivateAppId,
                null,
                isPlaceholder);

            return true;
        }

        // 3.2) It's a virtual shell item (e.g. Control Panel, Recycle Bin, This PC)
        //    Shell items that are backed by filesystem paths (e.g. Downloads) should be already handled above.
        if (CommandLineHelper.HasShellPrefix(normalizedHead))
        {
            ShellNames.TryGetFriendlyName(input, out var displayName);
            ShellNames.TryGetFileSystemPath(input, out var fsPath);
            result = new Classification(
                CommandKind.VirtualShellItem,
                input,
                input,
                string.Empty,
                LaunchMethod.ShellExecute,
                fsPath is not null && Directory.Exists(fsPath) ? fsPath : null,
                isPlaceholder,
                fsPath,
                displayName);
            return true;
        }

        // 3.3) Search paths for the file name (with or without ext)
        //     If head is a file name with extension, we look only for that. If there's no extension
        //     we go and follow Windows Shell resolution rules.
        if (TryResolveViaPath(normalizedHead, out var resolvedFilePath))
        {
            result = new Classification(
                CommandKind.PathCommand,
                input,
                resolvedFilePath,
                tail,
                LaunchMethod.ShellExecute,
                null,
                isPlaceholder);

            return true;
        }

        // 3.4) If it looks like a path with ext but missing file, treat as document (Shell will handle assoc / error)
        if (LooksPathy(normalizedHead) && Path.HasExtension(normalizedHead))
        {
            var extension = Path.GetExtension(normalizedHead);

            // if the path extension contains placeholders, we can't assume what it is so, skip it and treat it as unknown
            var hasSpecificExtension = !isPlaceholder || !extension.Contains('{');
            if (hasSpecificExtension)
            {
                result = new Classification(
                    ShellHelpers.IsExecutableExtension(extension) ? CommandKind.FileExecutable : CommandKind.FileDocument,
                    input,
                    normalizedHead,
                    tail,
                    LaunchMethod.ShellExecute,
                    HasDir(normalizedHead) ? Path.GetDirectoryName(normalizedHead) : null,
                    isPlaceholder);

                return true;
            }
        }

        // 4) looks like a web URL without scheme, but not like a file with extension
        if (normalizedHead.Contains('.', StringComparison.OrdinalIgnoreCase) && normalizedHead.StartsWith("www", StringComparison.OrdinalIgnoreCase))
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

        // 5) Fallback: let ShellExecute try the whole input
        result = new Classification(
            CommandKind.Unknown,
            input,
            head,
            tail,
            LaunchMethod.ShellExecute,
            null,
            isPlaceholder);

        return true;
    }

    private static (string Head, string Tail) SplitHeadAndArgs(string input) => CommandLineHelper.SplitHeadAndArgs(input);

    // Finds the best existing path prefix in an *unquoted* input by scanning
    // whitespace boundaries. Prefers files to directories; for same kind,
    // prefers the longer path.
    // Returns (head, tail) or (null, null) if nothing found.
    private static (string? Head, string? Tail) ExpandToBestExistingPath(string head, string tail, bool containsPlaceholders, IPlaceholderParser placeholderParser)
    {
        try
        {
            var (greedyHead, greedyTail) = GreedyFind(head, containsPlaceholders, placeholderParser);

            // put tails back together:
            return (Head: greedyHead, string.Join(" ", greedyTail, tail).Trim());
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to find best path", ex);
            throw;
        }
    }

    private static (string? Head, string? Tail) GreedyFind(string input, bool containsPlaceholders, IPlaceholderParser placeholderParser)
    {
        // Be greedy: try to find the longest existing path prefix.
        // Only probe at whitespace boundaries to find where the command ends and arguments begin.
        // We intentionally do NOT split at path separators — that would act as parent-directory
        // fallback, misclassifying non-existing files (e.g. classifying C:\Users\x\file.txt as
        // Directory because C:\Users exists).
        for (var i = input.Length; i >= 0; i--)
        {
            if (i < input.Length && !char.IsWhiteSpace(input[i]))
            {
                continue;
            }

            var candidate = input.AsSpan(0, i).TrimEnd();
            if (candidate.Length == 0)
            {
                continue;
            }

            var candidateStr = candidate.ToString();

            // If we have placeholders, check if this candidate would contain a non-path placeholder
            if (containsPlaceholders && ContainsNonPathPlaceholder(candidateStr, placeholderParser))
            {
                continue; // Skip this candidate, try a shorter one
            }

            if (TryResolvePathCandidate(candidateStr, out var resolvedCandidate))
            {
                var tail = i < input.Length ? input[i..].TrimStart() : string.Empty;
                return (resolvedCandidate, tail);
            }
        }

        return (null, null);
    }

    // Attempts to guess if any placeholders in the candidate string are likely not part of a filesystem path.
    private static bool ContainsNonPathPlaceholder(string candidate, IPlaceholderParser placeholderParser)
    {
        placeholderParser.ParsePlaceholders(candidate, out _, out var placeholders);
        foreach (var match in placeholders)
        {
            var placeholderContext = GuessPlaceholderContextInFileSystemPath(candidate, match.Index);

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

    // Heuristically determines the context of a placeholder inside a filesystem-like input string.
    // Sets:
    //  - IsAfterFlag: true if immediately preceded by a token that looks like a command-line flag prefix (" -", " /", " --").
    //  - LooksLikePathComponent: true if (a) not after a flag or (b) nearby text shows path separators.
    private static PlaceholderContext GuessPlaceholderContextInFileSystemPath(string input, int placeholderIndex)
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

    private static bool TryGetAumid(string input, out string aumid)
    {
        // App ids are a lot of fun, since they can look like anything.
        // And yes, they can contain spaces too, like Zoom:
        //     shell:AppsFolder\zoom.us.Zoom Video Meetings
        // so unless that thing is quoted, we can't just assume the first token is the AUMID.
        const string appsFolder = "shell:AppsFolder\\";

        // Guard against null or empty input
        if (string.IsNullOrEmpty(input))
        {
            aumid = string.Empty;
            return false;
        }

        // Already a fully qualified AUMID path
        if (input.StartsWith(appsFolder, StringComparison.OrdinalIgnoreCase))
        {
            aumid = input;
            return true;
        }

        aumid = string.Empty;
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

    private static bool TryResolveViaPath(string head, out string resolvedFile)
    {
        resolvedFile = string.Empty;

        if (string.IsNullOrWhiteSpace(head))
        {
            return false;
        }

        if (Path.HasExtension(head) && ShellHelpers.FileExistInPath(head, out resolvedFile))
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
                    resolvedFile = p;
                    return true;
                }

                return false;
            }

            foreach (var ext in ShellHelpers.ExecutableExtensions)
            {
                var p = TryProbe(null, head + ext);
                if (p is not null)
                {
                    resolvedFile = p;
                    return true;
                }
            }

            return false;
        }

        return ShellHelpers.TryResolveExecutableAsShell(head, out resolvedFile);
    }

    private static bool TryResolvePathCandidate(string candidate, out string resolvedPath)
    {
        resolvedPath = string.Empty;

        var normalizedCandidate = NormalizePathForWindows(candidate);

        if (TryResolvePathCandidateCore(normalizedCandidate, out resolvedPath))
        {
            return true;
        }

        if (TryPercentDecodePathCandidate(normalizedCandidate, out var decodedCandidate)
            && !decodedCandidate.Equals(normalizedCandidate, StringComparison.Ordinal)
            && TryResolvePathCandidateCore(decodedCandidate, out resolvedPath))
        {
            return true;
        }

        return false;
    }

    private static bool TryResolvePathCandidateCore(string candidate, out string resolvedPath)
    {
        resolvedPath = string.Empty;

        try
        {
            var normalizedCandidate = NormalizePathForWindows(candidate);
            if (CommandLineHelper.ExpandPathToPhysicalFile(normalizedCandidate, true, out var expanded))
            {
                var normalizedExpanded = NormalizePathForWindows(expanded);
                if (File.Exists(normalizedExpanded) || Directory.Exists(normalizedExpanded))
                {
                    resolvedPath = normalizedExpanded;
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to resolve path candidate '{candidate}'", ex);
        }

        return false;
    }

    private static bool TryPercentDecodePathCandidate(string input, out string decoded)
    {
        decoded = input;
        if (!ContainsPercentEncodingSequence(input))
        {
            return false;
        }

        try
        {
            decoded = NormalizePathForWindows(Uri.UnescapeDataString(input));
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to percent-decode path candidate '{input}'", ex);
            return false;
        }
    }

    private static bool ContainsPercentEncodingSequence(string input)
    {
        for (var i = 0; i + 2 < input.Length; i++)
        {
            if (input[i] == '%'
                && IsHexDigit(input[i + 1])
                && IsHexDigit(input[i + 2]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsHexDigit(char c) =>
        (c >= '0' && c <= '9')
        || (c >= 'a' && c <= 'f')
        || (c >= 'A' && c <= 'F');

    private static bool TryGetNearestExistingParentDirectory(string path, out string parentDirectory)
    {
        // Delegate to shared helper to avoid duplicated logic across the extension
        return PathHelpers.TryGetNearestExistingParentDirectory(path, out parentDirectory);
    }

    private static string NormalizePathForWindows(string path) =>
        PathNormalization.NormalizePathForWindows(path);

    private static string? TryProbe(string? dir, string name)
    {
        try
        {
            var path = dir is null ? name : Path.Combine(dir, name);

            // Normalize before probing filesystem
            path = NormalizePathForWindows(path);
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

    private record PlaceholderContext(bool IsAfterFlag, bool LooksLikePathComponent);
}
