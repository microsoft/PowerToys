// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using Microsoft.Windows.ApplicationModel.Resources;

namespace FileExplorerScripts;

internal static class Strings
{
    private static readonly ResourceLoader Loader = new("PowerToys.FileExplorerScripts.pri");

    public static CompositeFormat SelectionSummary { get; } = CompositeFormat.Parse(Get("SelectionSummary"));

    public static CompositeFormat CreatedFiles { get; } = CompositeFormat.Parse(Get("CreatedFiles"));

    public static string Get(string key) => Loader.GetString(key);
}
