// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using Microsoft.CmdPal.Ext.Registry.Constants;

namespace Microsoft.CmdPal.Ext.Registry.Helpers;

/// <summary>
/// Helper class to easier work with queries
/// </summary>
internal static partial class QueryHelper
{
    /// <summary>
    /// The character to distinguish if the search query contain multiple parts (typically "\\")
    /// </summary>
    internal const string QuerySplitCharacter = "\\\\";

    /// <summary>
    /// A list that contain short names of all registry base keys
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> _shortBaseKeys = new Dictionary<string, string>(6)
    {
        { Win32.Registry.ClassesRoot.Name, KeyName.ClassRootShort },
        { Win32.Registry.CurrentConfig.Name, KeyName.CurrentConfigShort },
        { Win32.Registry.CurrentUser.Name, KeyName.CurrentUserShort },
        { Win32.Registry.LocalMachine.Name, KeyName.LocalMachineShort },
        { Win32.Registry.PerformanceData.Name, KeyName.PerformanceDataShort },
        { Win32.Registry.Users.Name, KeyName.UsersShort },
    };

    [GeneratedRegex(@"/(?<=^(?:[^""]*""[^""]*"")*[^""]*)(?<!//.+)", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex FrontToBackSlashRegex();

    /// <summary>
    /// Sanitize the query to avoid issues with the regex
    /// </summary>
    /// <param name="query">Query containing front-slash</param>
    /// <returns>A string replacing all the front-slashes with back-slashes</returns>
    private static string SanitizeQuery(in string query)
    {
        var sanitizedQuery = FrontToBackSlashRegex().Replace(query, "\\");

        return sanitizedQuery.Replace("\"", string.Empty);
    }

    /// <summary>
    /// Return the parts of a given query
    /// </summary>
    /// <param name="query">The query that could contain parts</param>
    /// <param name="queryKey">The key part of the query</param>
    /// <param name="queryValueName">The value name part of the query</param>
    /// <returns><see langword="true"/> when the query search for a key and a value name, otherwise <see langword="false"/></returns>
    internal static bool GetQueryParts(in string query, out string queryKey, out string queryValueName)
    {
        var sanitizedQuery = SanitizeQuery(query);

        if (!sanitizedQuery.Contains(QuerySplitCharacter, StringComparison.InvariantCultureIgnoreCase))
        {
            queryKey = sanitizedQuery;
            queryValueName = string.Empty;
            return false;
        }

        var querySplit = sanitizedQuery.Split(QuerySplitCharacter);

        queryKey = querySplit.First();
        queryValueName = querySplit.Last();
        return true;
    }

    /// <summary>
    /// Return a registry key with a long base key
    /// </summary>
    /// <param name="registryKey">A registry key with a short base key</param>
    /// <returns>A registry key with a long base key</returns>
    internal static string GetKeyWithLongBaseKey(in string registryKey)
    {
        foreach (var shortName in _shortBaseKeys)
        {
            if (!registryKey.StartsWith(shortName.Value, StringComparison.InvariantCultureIgnoreCase))
            {
                continue;
            }

            return registryKey.Replace(shortName.Value, shortName.Key, StringComparison.InvariantCultureIgnoreCase);
        }

        return registryKey;
    }

    /// <summary>
    /// Return a registry key with a short base key (useful to reduce the text length of a registry key)
    /// </summary>
    /// <param name="registryKey">A registry key with a full base key</param>
    /// <returns>A registry key with a short base key</returns>
    internal static string GetKeyWithShortBaseKey(in string registryKey)
    {
        foreach (var shortName in _shortBaseKeys)
        {
            if (!registryKey.StartsWith(shortName.Key, StringComparison.InvariantCultureIgnoreCase))
            {
                continue;
            }

            return registryKey.Replace(shortName.Key, shortName.Value, StringComparison.InvariantCultureIgnoreCase);
        }

        return registryKey;
    }
}
