// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

using EnvironmentVariablesUILib.Models;

namespace EnvironmentVariablesUILib.Helpers;

/// <summary>
/// Centralises comparison logic so that Windows' case-insensitive name semantics are
/// applied consistently throughout the application, rather than each call site
/// independently selecting a StringComparison value.
/// </summary>
internal static class EnvironmentVariableComparisonHelper
{
    /// <summary>
    /// Compare variable name strings. Windows treats environment variable names case-
    /// insensitively: "PATH", "Path" and "path" all refer to the same variable.
    /// </summary>
    internal static bool NamesEqual(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Compare environment variable entries. Names are compared case-insensitively,
    /// values are compared case-sensitively.
    /// </summary>
    internal static bool EntriesEqual(Variable left, Variable right) =>
        left is not null
        && right is not null
        && NamesEqual(left.Name, right.Name)
        && string.Equals(left.Values, right.Values, StringComparison.Ordinal);

    /// <summary>
    /// Groups environment variables by name, ignoring case, and returns only those groups
    /// that contain logical duplicates, e.g. "Path" and "PATH". This may occur due to
    /// legacy tools or direct registry edits.
    /// </summary>
    internal static IEnumerable<IGrouping<string, Variable>> GetDuplicateNameGroups(IEnumerable<Variable> variables) =>
        variables.GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1);
}
