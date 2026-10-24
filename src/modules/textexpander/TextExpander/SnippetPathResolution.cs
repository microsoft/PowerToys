// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// The resolved snippet file and whether it came from a folder setting whose two supported file
/// names must continue to be re-evaluated as another app or sync service changes files.
/// </summary>
internal readonly record struct SnippetPathResolution(string FilePath, bool ResolveFolderNames);
