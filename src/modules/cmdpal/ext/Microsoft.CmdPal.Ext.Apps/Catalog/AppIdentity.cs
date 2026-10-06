// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CmdPal.Ext.Apps.Utils;

namespace Microsoft.CmdPal.Ext.Apps.Catalog;

/// <summary>
/// Creates canonical application identities whose format is shared across discovery sources.
/// </summary>
internal static class AppIdentity
{
    private const string PackagedPrefix = "packaged:";
    private const string Win32Prefix = "win32:";
    private const string Win32CommandPrefix = "app-v1-win32-";
    private const string PackagedCommandPrefix = "app-v1-packaged-";

    private const int HashLength = 64;
    private const int MaxCommandIdLength = 512;

    private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

    /// <summary>Creates a typed command ID, including the actual target filename when available.</summary>
    internal static string ForCommand(string catalogIdentity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogIdentity);
        if (catalogIdentity.StartsWith(PackagedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return $"{PackagedCommandPrefix}{catalogIdentity[PackagedPrefix.Length..]}";
        }

        var hash = GetHash(catalogIdentity);
        var filename = GetTargetFilename(catalogIdentity);
        return string.IsNullOrEmpty(filename)
            ? $"{Win32CommandPrefix}{hash}"
            : $"{Win32CommandPrefix}{filename}-{hash}";
    }

    /// <summary>Determines whether a value parses as a supported typed app command ID.</summary>
    internal static bool IsCommandId(string id)
    {
        return TryNormalizeCommandId(id, out _);
    }

    /// <summary>Provides the canonical ID and its supported nameless Win32 form.</summary>
    internal static IEnumerable<string> GetCommandIds(string catalogIdentity)
    {
        var commandId = ForCommand(catalogIdentity);
        yield return commandId;
        if (commandId.StartsWith(Win32CommandPrefix, StringComparison.Ordinal) && commandId.Length > Win32CommandPrefix.Length + HashLength)
        {
            var hash = commandId[^HashLength..];
            yield return $"{Win32CommandPrefix}{hash}";
        }
    }

    /// <summary>Normalizes only the syntax whose casing cannot change the resolved target.</summary>
    internal static bool TryNormalizeCommandId(string? id, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrEmpty(id) || id.Length > MaxCommandIdLength)
        {
            return false;
        }

        if (id.StartsWith(PackagedCommandPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var aumid = id[PackagedCommandPrefix.Length..];
            if (string.IsNullOrWhiteSpace(aumid) || char.IsWhiteSpace(aumid[0]) || char.IsWhiteSpace(aumid[^1])
                || aumid.Contains('/') || aumid.Contains('\\') || aumid.Any(char.IsControl))
            {
                return false;
            }

            normalized = $"{PackagedCommandPrefix}{aumid}";
            return true;
        }

        if (!id.StartsWith(Win32CommandPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var payload = id[Win32CommandPrefix.Length..];
        if (payload.Length != HashLength)
        {
            if (payload.Length < HashLength + 2 || payload[^(HashLength + 1)] != '-'
                || !IsValidFilename(payload[..^(HashLength + 1)]))
            {
                return false;
            }
        }

        var hash = payload[^HashLength..];
        if (!hash.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F'))
        {
            return false;
        }

        normalized = $"{Win32CommandPrefix}{payload[..^HashLength]}{hash.ToUpperInvariant()}";
        return true;
    }

    private static string? GetTargetFilename(string catalogIdentity)
    {
        if (!catalogIdentity.StartsWith(Win32Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var argumentsIndex = catalogIdentity.IndexOf("|args:", Win32Prefix.Length, StringComparison.OrdinalIgnoreCase);
        if (argumentsIndex < 0)
        {
            return null;
        }

        var target = catalogIdentity[Win32Prefix.Length..argumentsIndex];
        if (!Path.IsPathFullyQualified(target))
        {
            return null;
        }

        var filename = Path.GetFileName(target);
        return IsValidFilename(filename) && Win32CommandPrefix.Length + filename.Length + 1 + HashLength <= MaxCommandIdLength ? filename.ToLowerInvariant() : null;
    }

    private static bool IsValidFilename(string filename)
    {
        return !string.IsNullOrWhiteSpace(filename)
            && filename is not "." and not ".." && filename.IndexOfAny(InvalidFileNameChars) < 0;
    }

    private static string GetHash(string catalogIdentity)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(catalogIdentity.ToUpperInvariant())));
    }

    /// <summary>Creates the stable catalog identity for a packaged application.</summary>
    internal static string ForPackaged(string aumid)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(aumid);
        return $"{PackagedPrefix}{aumid}";
    }

    /// <summary>Normalizes launch profiles without distinguishing default executable or installer directories.</summary>
    internal static string GetDistinctWorkingDirectory(string targetPath, string workingDirectory, string? explicitAppUserModelId = null)
    {
        if (string.IsNullOrEmpty(workingDirectory))
        {
            return string.Empty;
        }

        var expandedDirectory = Environment.ExpandEnvironmentVariables(workingDirectory);
        if (!Path.IsPathFullyQualified(expandedDirectory))
        {
            return expandedDirectory;
        }

        var normalizedDirectory = PathHelpers.NormalizePath(expandedDirectory);
        var expandedTarget = Environment.ExpandEnvironmentVariables(targetPath);
        if (Path.IsPathFullyQualified(expandedDirectory) && Path.IsPathFullyQualified(expandedTarget))
        {
            try
            {
                var targetDirectory = PathHelpers.NormalizePath(Path.GetDirectoryName(expandedTarget)!);
                if (PathHelpers.IsExecutablePath(expandedTarget)
                    && (string.Equals(normalizedDirectory, targetDirectory, StringComparison.OrdinalIgnoreCase)
                        || IsSquirrelVersionDirectory(expandedTarget, targetDirectory, normalizedDirectory, explicitAppUserModelId)))
                {
                    return string.Empty;
                }
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                // Keep malformed launch profiles distinct.
            }
        }

        return normalizedDirectory;
    }

    private static bool IsSquirrelVersionDirectory(string targetPath, string targetDirectory, string workingDirectory, string? explicitAppUserModelId)
    {
        if (string.IsNullOrEmpty(explicitAppUserModelId)
            || !string.Equals(Path.GetExtension(targetPath), ".exe", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(Path.GetDirectoryName(workingDirectory), targetDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // ponytail: numeric release folders only; extend parsing when prerelease shortcuts need deduplication.
        var directoryName = Path.GetFileName(workingDirectory);
        if (!directoryName.StartsWith("app-", StringComparison.OrdinalIgnoreCase)
            || !Version.TryParse(directoryName.AsSpan(4), out _))
        {
            return false;
        }

        // Squirrel's root launcher chooses the installed version and its working directory itself.
        var expectedId = $"com.squirrel.{Path.GetFileName(targetDirectory).Replace(" ", string.Empty)}.{Path.GetFileNameWithoutExtension(targetPath).Replace(" ", string.Empty)}";
        return string.Equals(explicitAppUserModelId, expectedId, StringComparison.OrdinalIgnoreCase);
    }
}
